using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Win32;
using RightSlim;

namespace RightSlimTest
{
    internal static class TestRunner
    {
        private const string VerbPath = @"Software\Classes\*\shell\RightSlimTestVerb";
        private const string HandlerPath = @"Software\Classes\*\shellex\ContextMenuHandlers\RightSlimTestHandler";
        private const string TestGuid = "{11111111-2222-3333-4444-555555555555}";
        private static int _fail = 0;

        private static void Check(bool cond, string name)
        {
            Console.WriteLine((cond ? "[PASS] " : "[FAIL] ") + name);
            if (!cond) _fail++;
        }

        private static int Main()
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Cleanup();

            try
            {
                // ---- 准备测试项 ----
                using (var v = Registry.CurrentUser.CreateSubKey(VerbPath))
                {
                    v.SetValue("MUIVerb", "RightSlim自检项", RegistryValueKind.String);
                    using (var cmd = v.CreateSubKey("command"))
                        cmd.SetValue("", "\"C:\\Windows\\notepad.exe\" \"%1\"", RegistryValueKind.String);
                }
                using (var h = Registry.CurrentUser.CreateSubKey(HandlerPath))
                    h.SetValue("", TestGuid, RegistryValueKind.String);

                // ---- 1. 扫描 ----
                var all = RegistryHelper.Scan();
                var verb = all.FirstOrDefault(x => x.RelativePath == @"*\shell\RightSlimTestVerb");
                var handler = all.FirstOrDefault(x => x.RelativePath == @"*\shellex\ContextMenuHandlers\RightSlimTestHandler");

                Check(all.Count > 0, "扫描返回非空 (共 " + all.Count + " 项)");
                Check(verb != null, "找到测试 Verb 项");
                Check(handler != null, "找到测试 Handler 项");
                if (verb == null || handler == null) { Cleanup(); return 1; }

                Check(verb.Enabled, "Verb 初始为启用");
                Check(verb.DisplayName == "RightSlim自检项", "Verb 名称解析正确 => " + verb.DisplayName);
                Check(handler.Enabled, "Handler 初始为启用");
                Check(verb.Hive == RootHive.HKCU, "Verb 归属 HKCU");
                Check(verb.Kind == ItemKind.Verb, "Verb 类型正确");
                Check(handler.Kind == ItemKind.Handler, "Handler 类型正确");

                // ---- 2. 禁用 Verb ----
                RegistryHelper.SetEnabled(verb, false);
                using (var k = Registry.CurrentUser.OpenSubKey(VerbPath))
                    Check(k.GetValue("LegacyDisable") != null, "禁用后 LegacyDisable 已写入");
                var v2 = RegistryHelper.Scan().FirstOrDefault(x => x.RelativePath == @"*\shell\RightSlimTestVerb");
                Check(v2 != null && !v2.Enabled, "重新扫描后 Verb 显示为禁用");

                // ---- 3. 启用 Verb ----
                RegistryHelper.SetEnabled(verb, true);
                using (var k = Registry.CurrentUser.OpenSubKey(VerbPath))
                    Check(k.GetValue("LegacyDisable") == null, "启用后 LegacyDisable 已移除");
                var v3 = RegistryHelper.Scan().FirstOrDefault(x => x.RelativePath == @"*\shell\RightSlimTestVerb");
                Check(v3 != null && v3.Enabled, "重新扫描后 Verb 显示为启用");

                // ---- 4. 禁用 Handler ----
                RegistryHelper.SetEnabled(handler, false);
                using (var k = Registry.CurrentUser.OpenSubKey(HandlerPath))
                {
                    string dv = k.GetValue("") as string;
                    Check(dv != null && dv == "", "禁用后 Handler 默认值为空串（真正的禁用语义）");
                }
                var h2 = RegistryHelper.Scan().FirstOrDefault(x => x.RelativePath == @"*\shellex\ContextMenuHandlers\RightSlimTestHandler");
                Check(h2 != null && !h2.Enabled, "重新扫描后 Handler 显示为禁用");

                // ---- 5. 启用 Handler（恢复 CLSID）----
                RegistryHelper.SetEnabled(handler, true);
                using (var k = Registry.CurrentUser.OpenSubKey(HandlerPath))
                {
                    string dv = k.GetValue("") as string;
                    Check(dv == TestGuid, "启用后 Handler CLSID 已恢复 => " + (dv ?? "<null>"));
                }
                var h3 = RegistryHelper.Scan().FirstOrDefault(x => x.RelativePath == @"*\shellex\ContextMenuHandlers\RightSlimTestHandler");
                Check(h3 != null && h3.Enabled, "重新扫描后 Handler 显示为启用");

                // ---- 6. 幂等：重复禁用/启用 ----
                RegistryHelper.SetEnabled(verb, false);
                RegistryHelper.SetEnabled(verb, false);
                RegistryHelper.SetEnabled(verb, true);
                var v4 = RegistryHelper.Scan().FirstOrDefault(x => x.RelativePath == @"*\shell\RightSlimTestVerb");
                Check(v4 != null && v4.Enabled, "重复操作后状态正确");

                // ---- 7. 关键场景：无默认值、靠键名当 CLSID 的 GUID 处理器 ----
                const string guidName = @"Software\Classes\*\shellex\ContextMenuHandlers\{AAAAAAAA-1111-2222-3333-444444444444}";
                Registry.CurrentUser.CreateSubKey(guidName).Close();
                var g1 = RegistryHelper.Scan().FirstOrDefault(x =>
                    x.RelativePath == @"*\shellex\ContextMenuHandlers\{AAAAAAAA-1111-2222-3333-444444444444}");
                Check(g1 != null && g1.Enabled, "无默认值 GUID 项应识别为启用（键名即 CLSID）");
                Check(g1 != null && g1.OriginalClsid == "{AAAAAAAA-1111-2222-3333-444444444444}", "GUID 项 OriginalClsid 为键名");

                RegistryHelper.SetEnabled(g1, false);
                using (var k = Registry.CurrentUser.OpenSubKey(guidName))
                    Check(k.GetValue("") is string && (string)k.GetValue("") == "", "禁用 GUID 项后默认值为空串");
                var g2 = RegistryHelper.Scan().FirstOrDefault(x =>
                    x.RelativePath == @"*\shellex\ContextMenuHandlers\{AAAAAAAA-1111-2222-3333-444444444444}");
                Check(g2 != null && !g2.Enabled, "重新扫描后 GUID 项显示为禁用");

                RegistryHelper.SetEnabled(g2, true);
                using (var k = Registry.CurrentUser.OpenSubKey(guidName))
                    Check(k.GetValue("") == null, "启用 GUID 项后恢复为“无默认值”");
                var g3 = RegistryHelper.Scan().FirstOrDefault(x =>
                    x.RelativePath == @"*\shellex\ContextMenuHandlers\{AAAAAAAA-1111-2222-3333-444444444444}");
                Check(g3 != null && g3.Enabled, "重新扫描后 GUID 项恢复为启用");
            }
            catch (Exception ex)
            {
                Console.WriteLine("[FAIL] 异常: " + ex);
                _fail++;
            }
            finally
            {
                Cleanup();
            }

            Console.WriteLine();
            Console.WriteLine(_fail == 0 ? "全部通过" : (_fail + " 项失败"));
            return _fail == 0 ? 0 : 1;
        }

        private static void Cleanup()
        {
            try
            {
                var cu = Registry.CurrentUser;
                DeleteTree(cu, @"Software\Classes\*\shell\RightSlimTestVerb");
                DeleteTree(cu, @"Software\Classes\*\shellex\ContextMenuHandlers\RightSlimTestHandler");
                DeleteTree(cu, @"Software\Classes\*\shellex\ContextMenuHandlers\{AAAAAAAA-1111-2222-3333-444444444444}");
                try { cu.DeleteSubKeyTree(@"Software\RightSlim\Backup", false); } catch { }
            }
            catch { }
        }

        private static void DeleteTree(RegistryKey root, string path)
        {
            try { root.DeleteSubKeyTree(path, false); } catch { }
        }
    }
}