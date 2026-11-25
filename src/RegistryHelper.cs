using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.Win32;

namespace RightSlim
{
    public enum ItemKind
    {
        Verb,        // <context>\shell\<name>  命令型菜单项
        Handler      // <context>\shellex\ContextMenuHandlers\<name>  COM 处理器
    }

    public enum RootHive
    {
        HKCU,
        HKLM
    }

    public sealed class MenuItem
    {
        public string Id;            // 唯一标识：hive|relativePath|kind
        public string DisplayName;   // 展示名（已解析本地化字符串）
        public string ContextName;   // 出现位置：文件 / 文件夹 / 桌面背景 ...
        public string ContextKey;    // 用于分组的键
        public ItemKind Kind;
        public RootHive Hive;
        public string RelativePath;  // 相对 Software\Classes 的完整子路径
        public bool Enabled;
        public bool DesiredEnabled;  // 用户勾选意图，点“保存并应用”时才写入注册表
        public string Detail;        // 来源信息（dll / 命令）
        public string Hint;          // 无法状态判断时提示
        public string OriginalClsid; // Handler 的原始 CLSID（用于恢复）

        public string HiveText { get { return Hive == RootHive.HKLM ? "HKLM" : "HKCU"; } }
        public string KindText
        {
            get
            {
                switch (Kind)
                {
                    case ItemKind.Verb: return "操作";
                    case ItemKind.Handler: return "扩展";
                    default: return "?";
                }
            }
        }
        public string FullRegistryPath { get { return HiveText + @"\Software\Classes\" + RelativePath; } }
    }

    public static class RegistryHelper
    {
        private const string HkcuClasses = @"Software\Classes";
        private const string HklmClasses = @"SOFTWARE\Classes";
        private const string BackupRoot = @"Software\RightSlim\Backup";

        // 需要扫描的“出现位置”
        public static readonly string[][] Contexts =
        {
            new[] { "文件",      "*" },
            new[] { "所有文件系统对象", "AllFilesystemObjects" },
            new[] { "文件夹",    "Directory" },
            new[] { "文件夹",    "Folder" },
            new[] { "桌面/文件夹背景", "Directory\\Background" },
            new[] { "驱动器",    "Drive" }
        };

        // ---------- 扫描 ----------

        public static List<MenuItem> Scan()
        {
            var result = new List<MenuItem>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var ctx in Contexts)
            {
                string ctxLabel = ctx[0];
                string ctxRel = ctx[1];

                ScanVerbs(ctxLabel, ctxRel, result, seen);
                ScanHandlers(ctxLabel, ctxRel, result, seen);
            }

            result.Sort((a, b) =>
            {
                int c = string.Compare(a.ContextName, b.ContextName, StringComparison.OrdinalIgnoreCase);
                if (c != 0) return c;
                c = string.Compare(a.KindText, b.KindText, StringComparison.OrdinalIgnoreCase);
                if (c != 0) return c;
                return string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase);
            });
            return result;
        }

        private static void ScanVerbs(string ctxLabel, string ctxRel, List<MenuItem> outList, HashSet<string> seen)
        {
            string rel = ctxRel + @"\shell";
            foreach (var hive in new[] { RootHive.HKCU, RootHive.HKLM })
            {
                using (var root = OpenClasses(hive, false))
                {
                    if (root == null) continue;
                    using (var shellKey = root.OpenSubKey(rel, false))
                    {
                        if (shellKey == null) continue;
                        foreach (string name in shellKey.GetSubKeyNames())
                        {
                            string itemRel = rel + "\\" + name;
                            string id = "V|" + itemRel;
                            if (!seen.Add(id)) continue; // HKCU 优先
                            try
                            {
                                using (var verb = shellKey.OpenSubKey(name, false))
                                {
                                    if (verb == null) continue;
                                    var mi = new MenuItem
                                    {
                                        Id = id,
                                        Kind = ItemKind.Verb,
                                        Hive = hive,
                                        RelativePath = itemRel,
                                        ContextName = ctxLabel,
                                        ContextKey = ctxLabel,
                                        Enabled = verb.GetValue("LegacyDisable") == null,
                                    };
                                    mi.DisplayName = FriendlyVerb(verb, name);
                                    mi.Detail = BuildVerbDetail(verb);
                                    if (mi.DisplayName.Length == 0) mi.DisplayName = name;
                                    outList.Add(mi);
                                }
                            }
                            catch { }
                        }
                    }
                }
            }
        }

        private static void ScanHandlers(string ctxLabel, string ctxRel, List<MenuItem> outList, HashSet<string> seen)
        {
            string rel = ctxRel + @"\shellex\ContextMenuHandlers";
            foreach (var hive in new[] { RootHive.HKCU, RootHive.HKLM })
            {
                using (var root = OpenClasses(hive, false))
                {
                    if (root == null) continue;
                    using (var handlers = root.OpenSubKey(rel, false))
                    {
                        if (handlers == null) continue;
                        foreach (string name in handlers.GetSubKeyNames())
                        {
                            string itemRel = rel + "\\" + name;
                            string id = "H|" + itemRel;
                            if (!seen.Add(id)) continue;
                            try
                            {
                                using (var handler = handlers.OpenSubKey(name, false))
                                {
                                    if (handler == null) continue;

                                    // Windows 规则：无默认值则用键名作 CLSID；
                                    // 默认值为空串则明确表示该处理器被禁用。
                                    string defVal = handler.GetValue("") as string;
                                    bool hasDefault = handler.GetValue("") != null;
                                    string clsid;
                                    bool enabled;
                                    if (!hasDefault)
                                    {
                                        clsid = name;
                                        enabled = true;
                                    }
                                    else if (string.IsNullOrWhiteSpace(defVal))
                                    {
                                        clsid = name; // 保存原始键名，供恢复用
                                        enabled = false;
                                    }
                                    else
                                    {
                                        clsid = defVal;
                                        enabled = true;
                                    }

                                    string friendly = HandlerFriendlyNameClsid(clsid, name);
                                    string detail = HandlerDllPath(clsid) ?? clsid;
                                    var mi = new MenuItem
                                    {
                                        Id = id,
                                        Kind = ItemKind.Handler,
                                        Hive = hive,
                                        RelativePath = itemRel,
                                        ContextName = ctxLabel,
                                        ContextKey = ctxLabel,
                                        Enabled = enabled,
                                        DisplayName = friendly,
                                        Detail = enabled ? detail : "已隐藏（无默认值）",
                                        OriginalClsid = clsid,
                                    };
                                    outList.Add(mi);
                                }
                            }
                            catch { }
                        }
                    }
                }
            }
        }

        private static string FriendlyVerb(RegistryKey verb, string keyName)
        {
            string mui = verb.GetValue("MUIVerb") as string;
            if (mui != null && mui.Length > 0)
            {
                string t = NativeMethods.ResolveIndirect(mui);
                if (!string.IsNullOrWhiteSpace(t)) return t;
            }
            string def = verb.GetValue("") as string;
            if (def != null && def.Length > 0)
            {
                string t = NativeMethods.ResolveIndirect(def);
                if (!string.IsNullOrWhiteSpace(t)) return t;
            }
            return keyName;
        }

        private static string BuildVerbDetail(RegistryKey verb)
        {
            try
            {
                using (var cmd = verb.OpenSubKey("command", false))
                {
                    if (cmd != null)
                    {
                        string v = cmd.GetValue("") as string;
                        if (v != null) return Truncate(v, 160);
                    }
                }
                object c = verb.GetValue("CommandFlags");
                if (c != null) return "flags=" + c;
            }
            catch { }
            return "";
        }

        /// <summary>取 CLSID 对应的 InprocServer32 完整路径，取不到返回 null。</summary>
        private static string HandlerDllPath(string clsid)
        {
            if (string.IsNullOrWhiteSpace(clsid)) return null;
            string g = clsid.Trim();
            if (!g.StartsWith("{")) return null;
            try
            {
                using (var k = Registry.ClassesRoot.OpenSubKey("CLSID\\" + g + "\\InprocServer32", false))
                {
                    if (k == null) return null;
                    string dll = k.GetValue("") as string;
                    if (string.IsNullOrWhiteSpace(dll)) return null;
                    return dll.Trim('"');
                }
            }
            catch { return null; }
        }

        private static string HandlerFriendlyNameClsid(string clsid, string fallback)
        {
            if (string.IsNullOrWhiteSpace(clsid)) return fallback;
            string g = clsid.Trim();
            if (!g.StartsWith("{")) return clsid;
            try
            {
                using (var clsidKey = Registry.ClassesRoot.OpenSubKey("CLSID\\" + g, false))
                {
                    if (clsidKey != null)
                    {
                        object def = clsidKey.GetValue("");
                        string name = def as string;
                        if (!string.IsNullOrWhiteSpace(name) &&
                            !name.StartsWith("{") &&
                            !name.Contains(".dll"))
                        {
                            return name;
                        }
                        using (var inproc = clsidKey.OpenSubKey("InprocServer32", false))
                        {
                            if (inproc != null)
                            {
                                string dll = inproc.GetValue("") as string;
                                if (!string.IsNullOrWhiteSpace(dll))
                                {
                                    try { return Path.GetFileName(dll.Trim('"')); } catch { }
                                }
                            }
                        }
                        if (!string.IsNullOrWhiteSpace(name)) return name;
                    }
                }
            }
            catch { }
            // 解析不出：若是裸 GUID，别把 GUID 糊到用户脸上
            return g.Equals(fallback, StringComparison.OrdinalIgnoreCase) ? "（系统扩展 " + ShortGuid(g) + "）" : g;
        }

        private static string ShortGuid(string g)
        {
            string s = g.Trim('{', '}');
            return s.Length >= 8 ? s.Substring(0, 8) : s;
        }

        private static string Truncate(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return s;
            return s.Length <= max ? s : s.Substring(0, max) + "…";
        }

        // ---------- 启停 ----------

        public static void SetEnabled(MenuItem item, bool enabled)
        {
            using (var classes = OpenClasses(item.Hive, true))
            {
                if (classes == null) throw new Exception("无法打开注册表写权限，请以管理员身份运行。");
                using (var key = classes.OpenSubKey(item.RelativePath, true))
                {
                    if (key == null) throw new Exception("注册表项不存在（可能已被其他程序删除）：\n" + item.FullRegistryPath);

                    if (item.Kind == ItemKind.Verb)
                    {
                        if (enabled)
                            key.DeleteValue("LegacyDisable", false);
                        else
                            key.SetValue("LegacyDisable", "", RegistryValueKind.String);
                    }
                    else
                    {
                        if (enabled) RestoreHandlerDefault(key, item);
                        else DisableHandler(key, item);
                    }
                }
            }
        }

        private static void DisableHandler(RegistryKey key, MenuItem item)
        {
            // 先记录原始状态：是否有默认值 + 其内容。
            // 注意：删除默认值会让系统回退到“用键名当 CLSID”，并非禁用；
            // 真正禁用的方式是写一个空字符串默认值。
            bool hadDefault = key.GetValue("") != null;
            string def = key.GetValue("") as string;

            if (hadDefault && !string.IsNullOrWhiteSpace(def))
            {
                // 常规情况：备份原 CLSID，然后写空串禁用
                SaveBackup(item, "clsid", def);
            }
            else if (!hadDefault)
            {
                // 原本靠键名当 CLSID（如裸 GUID 键），备份标记为“键名模式”，禁用时写空串
                SaveBackup(item, "clsid", "__KEYNAME__");
            }
            else
            {
                // 原本已是空串，已经是禁用状态，改回去也是启用——仅在缺备份时补记
                if (!HasBackup(item, "clsid")) SaveBackup(item, "clsid", item.OriginalClsid ?? "__KEYNAME__");
            }

            key.SetValue("", "", RegistryValueKind.String);
        }

        private static void RestoreHandlerDefault(RegistryKey key, MenuItem item)
        {
            string backup;
            if (TryGetBackup(item, "clsid", out backup))
            {
                if (backup == "__KEYNAME__")
                {
                    // 还原为“无默认值，靠键名”模式
                    key.DeleteValue("", false);
                }
                else
                {
                    key.SetValue("", backup, RegistryValueKind.String);
                }
                DeleteBackup(item, "clsid");
                return;
            }

            // 无备份：用扫描时记录的原始 CLSID
            string clsid = item.OriginalClsid;
            if (string.IsNullOrWhiteSpace(clsid))
                clsid = item.Detail;
            if (string.IsNullOrWhiteSpace(clsid))
                throw new Exception("此项缺少可恢复的 CLSID，请手动在注册表中修复。");

            key.SetValue("", clsid, RegistryValueKind.String);
        }

        // ---------- 备份（HKCU） ----------

        private static string BackupValueName(MenuItem item, string valueName)
        {
            string raw = item.Hive + "|" + item.RelativePath + "|" + valueName;
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(raw));
        }

        private static void SaveBackup(MenuItem item, string valueName, string data)
        {
            using (var k = Registry.CurrentUser.CreateSubKey(BackupRoot))
            {
                if (k != null) k.SetValue(BackupValueName(item, valueName), data ?? "", RegistryValueKind.String);
            }
        }

        private static bool TryGetBackup(MenuItem item, string valueName, out string data)
        {
            data = null;
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(BackupRoot, false))
                {
                    if (k == null) return false;
                    string v = k.GetValue(BackupValueName(item, valueName)) as string;
                    if (v != null) { data = v; return true; }
                }
            }
            catch { }
            return false;
        }

        private static bool HasBackup(MenuItem item, string valueName)
        {
            string s;
            return TryGetBackup(item, valueName, out s);
        }

        private static void DeleteBackup(MenuItem item, string valueName)
        {
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(BackupRoot, true))
                {
                    if (k != null) k.DeleteValue(BackupValueName(item, valueName), false);
                }
            }
            catch { }
        }

        // ---------- 基础 ----------

        private static RegistryKey OpenClasses(RootHive hive, bool writable)
        {
            try
            {
                if (hive == RootHive.HKCU)
                    return Registry.CurrentUser.OpenSubKey(HkcuClasses, writable);
                return Registry.LocalMachine.OpenSubKey(HklmClasses, writable);
            }
            catch { return null; }
        }
    }
}