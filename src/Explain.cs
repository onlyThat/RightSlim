using System;
using System.IO;

namespace RightSlim
{
    /// <summary>把注册表里的冷冰冰条目翻译成“这玩意儿是干什么的”。</summary>
    internal static class Explain
    {
        private struct Rule
        {
            public readonly string Key;
            public readonly string Text;
            public Rule(string key, string text) { Key = key; Text = text; }
        }

        // 只回答“这项是干什么的”，按从具体到宽泛排列
        private static readonly Rule[] NameRules =
        {
            new Rule("copy as path",      "把文件的完整路径复制到剪贴板，粘贴到命令行/脚本里很方便。"),
            new Rule("复制文件地址",      "把文件的完整路径复制到剪贴板，粘贴到命令行/脚本里很方便。"),
            new Rule("open folder as",    "用某个开发工具把当前文件夹当成项目打开。"),
            new Rule("使用 visual studio","用 Visual Studio 打开当前文件或项目。"),
            new Rule("git bash here",     "在当前文件夹打开 Git Bash 命令行。"),
            new Rule("git gui here",      "在当前文件夹打开 Git 的图形界面。"),
            new Rule("powershell",        "在当前文件夹打开 PowerShell 命令行窗口。"),
            new Rule("打开 linux",        "在 WSL（Windows 内置的 Linux 子系统）里打开当前文件夹。"),
            new Rule("命令行",            "在当前文件夹打开 cmd 命令提示符，省得手动 cd。"),
            new Rule("7-zip",             "7-Zip 压缩工具：压缩、解压、添加到压缩包。"),
            new Rule("解压",              "把选中的压缩包解压到当前位置或指定文件夹。"),
            new Rule("压缩",              "把选中的文件打包成压缩包。"),
            new Rule("上传到百度网盘",    "把文件上传到百度网盘。"),
            new Rule("同步至其它设备",    "通过网盘把文件同步到你的其他设备。"),
            new Rule("用手机打开",        "把文件推送到手机端用对应 App 打开。"),
            new Rule("小米互传",          "通过小米互传把文件发送到附近的手机/电脑。"),
            new Rule("发送到",            "把文件发送到指定位置（如压缩文件夹、蓝牙设备、邮件收件人）。"),
            new Rule("添加到收藏夹",      "把当前位置加入资源管理器左侧的收藏栏。"),
            new Rule("固定到开始屏幕",    "把该文件的快捷方式固定到开始菜单。"),
            new Rule("固定到任务栏",      "把该文件的快捷方式固定到任务栏。"),
            new Rule("共享",              "通过局域网/系统共享把文件分享给别人。"),
            new Rule("还原以前的版本",    "查看并恢复该文件的历史备份版本（需开启系统保护或卷影副本）。"),
            new Rule("以前的版本",        "查看并恢复该文件的历史备份版本。"),
            new Rule("加密",              "用 Windows 的 EFS 加密功能加密文件内容。"),
            new Rule("扫描",              "调用安全软件扫描该文件。"),
            new Rule("问问天禧",          "调用联想天禧 AI 助手对文件提问/处理。"),
            new Rule("询问 copilot",      "调用 Windows Copilot 对该文件提问。"),
            new Rule("quark",             "调用夸克 AI 对文件进行处理。"),
            new Rule("加入知识库",        "把该文件加入某个 AI 工具的知识库。"),
            new Rule("通过 trae",         "用 Trae 编辑器打开该文件。"),
            new Rule("通过 windsurf",     "用 Windsurf 编辑器打开该文件。"),
            new Rule("工作文件夹",        "把该文件/文件夹同步到企业工作文件夹（Work Folders）。"),
            new Rule("打印",              "把文件发送到打印机打印。"),
            new Rule("转换为pdf",         "把文档转换为 PDF 格式。"),
            new Rule("编辑",              "用关联的编辑器打开该文件修改内容。"),
            new Rule("打开方式",          "选择用哪个程序来打开这个文件。"),
            new Rule("创建快捷方式",      "在当前位置为该项创建一个快捷方式。"),
            new Rule("重命名",            "修改该项的名字。"),
            new Rule("属性",              "打开该项的属性窗口（大小、权限、兼容性等）。"),
            new Rule("删除",              "把该项删除到回收站。"),
            new Rule("剪切",              "把该项剪切到剪贴板，可移动到别处。"),
            new Rule("复制",              "把该项复制到剪贴板，可粘贴到别处。"),
            new Rule("打开",              "用默认程序打开该项。"),
        };

        /// <summary>系统核心操作：隐藏后会明显影响日常使用，预览时给出警告。</summary>
        private static readonly string[] CoreNames =
        {
            "打开", "open", "编辑", "edit", "打印", "print", "剪切", "cut",
            "复制", "copy", "删除", "delete", "重命名", "rename",
            "属性", "properties", "创建快捷方式", "create shortcut",
            "发送到", "send to", "打开方式", "open with", "新建", "new"
        };

        public static bool IsCore(MenuItem mi)
        {
            // Normalize 已去掉加速键括号，所以“删除(&D)”会得到“删除”、
            // “Copy (&C)”得到“copy”，用精确匹配即可。
            // 绝不能用“前缀匹配”：那会把“Open Folder as…”“Copy as Path Menu”
            // 这类第三方项误判成系统核心操作。
            string name = Normalize(mi.DisplayName);
            foreach (var c in CoreNames)
                if (name == Normalize(c)) return true;
            return false;
        }

        private static string Normalize(string s)
        {
            if (s == null) return "";
            s = s.Replace("&", "").Trim().ToLowerInvariant();
            // 去掉尾部加速键括号，如 “删除 (&D)” -> “删除”
            int i = s.IndexOf('(');
            if (i > 0) s = s.Substring(0, i).Trim();
            i = s.IndexOf('（');
            if (i > 0) s = s.Substring(0, i).Trim();
            return s.Trim();
        }

        public static string Describe(MenuItem mi)
        {
            string name = mi.DisplayName ?? "";
            string detail = mi.Detail ?? "";

            foreach (var r in NameRules)
            {
                if (name.IndexOf(r.Key, StringComparison.OrdinalIgnoreCase) >= 0)
                    return r.Text;
            }

            string bySource = MatchSource(name, detail);
            if (bySource != null) return bySource;

            if (mi.Kind == ItemKind.Handler)
                return "由某个软件注册的右键菜单扩展，通常随该软件安装。隐藏它一般不影响文件本身。";
            return "第三方软件添加的右键菜单项。";
        }

        private static string MatchSource(string name, string detail)
        {
            string d = detail.ToLowerInvariant();
            string file = "";
            try { file = Path.GetFileName(detail).ToLowerInvariant(); } catch { }

            if (file == "shell32.dll")
                return "Windows 系统自带的功能。";
            if (file == "ntshrui.dll")
                return "Windows 的文件共享功能。";
            if (file == "twext.dll")
                return "Windows 的文件历史版本功能。";
            if (file.Contains("7-zip"))
                return "7-Zip 压缩工具的功能。";
            if (d.Contains(".exe"))
                return "某个程序提供的快捷入口，点击会启动该程序。";

            // 按 dll 所在软件名兜底
            if (file.EndsWith(".dll") && file.Length > 4)
                return "某个已安装软件添加的右键菜单扩展（来自 " + file + "）。";
            return null;
        }
    }
}