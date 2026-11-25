using System;
using System.IO;
using System.Linq;
using Microsoft.Win32;
using RightSlim;

namespace RightSlimTest
{
    // 端到端验证：调用 Shell.Application.Verbs() 读取真实右键菜单，
    // 确认启停操作真的让菜单项出现/消失，而不仅仅是注册表值变化。
    internal static class EndToEnd
    {
        private const string VerbPath = @"Software\Classes\*\shell\RightSlimVerify";
        private const string MenuText = "RightSlim验证项";
        private static int _fail;

        private static void Check(bool cond, string name)
        {
            Console.WriteLine((cond ? "[PASS] " : "[FAIL] ") + name);
            if (!cond) _fail++;
        }

        private static int Main()
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Cleanup();

            string sampleDir = Path.Combine(Path.GetTempPath(), "RightSlimVerify");
            Directory.CreateDirectory(sampleDir);
            string sampleFile = Path.Combine(sampleDir, "sample.txt");
            File.WriteAllText(sampleFile, "test");

            try
            {
                using (var v = Registry.CurrentUser.CreateSubKey(VerbPath))
                {
                    v.SetValue("MUIVerb", MenuText, RegistryValueKind.String);
                    using (var cmd = v.CreateSubKey("command"))
                        cmd.SetValue("", "notepad.exe \"%1\"", RegistryValueKind.String);
                }

                var before = ShellVerbs(sampleDir, "sample.txt");
                Check(before.Any(x => x.Contains(MenuText)), "启用时：真实右键菜单包含验证项");
                Console.WriteLine("    当前菜单项数：" + before.Count);

                var item = RegistryHelper.Scan().FirstOrDefault(x => x.RelativePath == @"*\shell\RightSlimVerify");
                Check(item != null && item.Enabled, "扫描识别为启用");

                RegistryHelper.SetEnabled(item, false);
                var after = ShellVerbs(sampleDir, "sample.txt");
                Check(!after.Any(x => x.Contains(MenuText)), "禁用后：真实右键菜单不再包含验证项");
                Console.WriteLine("    禁用后菜单项数：" + after.Count);

                var item2 = RegistryHelper.Scan().FirstOrDefault(x => x.RelativePath == @"*\shell\RightSlimVerify");
                RegistryHelper.SetEnabled(item2, true);
                var restored = ShellVerbs(sampleDir, "sample.txt");
                Check(restored.Any(x => x.Contains(MenuText)), "重新启用后：真实右键菜单又包含验证项");
            }
            catch (Exception ex)
            {
                Console.WriteLine("[FAIL] 异常：" + ex);
                _fail++;
            }
            finally
            {
                Cleanup();
                try { File.Delete(sampleFile); Directory.Delete(sampleDir, true); } catch { }
            }

            Console.WriteLine();
            Console.WriteLine(_fail == 0 ? "端到端全部通过" : (_fail + " 项失败"));
            return _fail == 0 ? 0 : 1;
        }

        private static System.Collections.Generic.List<string> ShellVerbs(string dir, string file)
        {
            var list = new System.Collections.Generic.List<string>();
            Type t = Type.GetTypeFromProgID("Shell.Application");
            dynamic shell = Activator.CreateInstance(t);
            dynamic folder = shell.NameSpace(dir);
            dynamic item = folder.ParseName(file);
            dynamic verbs = item.Verbs();
            int n = verbs.Count;
            for (int i = 0; i < n; i++)
            {
                string name = verbs.Item(i).Name;
                if (!string.IsNullOrEmpty(name)) list.Add(name.Replace("&", ""));
            }
            System.Runtime.InteropServices.Marshal.ReleaseComObject(verbs);
            System.Runtime.InteropServices.Marshal.ReleaseComObject(item);
            System.Runtime.InteropServices.Marshal.ReleaseComObject(folder);
            System.Runtime.InteropServices.Marshal.ReleaseComObject(shell);
            return list;
        }

        private static void Cleanup()
        {
            try { Registry.CurrentUser.DeleteSubKeyTree(VerbPath, false); } catch { }
            try { Registry.CurrentUser.DeleteSubKeyTree(@"Software\RightSlim\Backup", false); } catch { }
        }
    }
}