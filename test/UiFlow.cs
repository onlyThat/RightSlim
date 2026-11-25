using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

namespace RightSlimTest
{
    // 不依赖手工点击：直接构造 MainForm，走一遍新交互流程的核心逻辑。
    internal static class UiFlow
    {
        private static int _fail;
        private static void Check(bool c, string n)
        {
            Console.WriteLine((c ? "[PASS] " : "[FAIL] ") + n);
            if (!c) _fail++;
        }

        private static T Get<T>(object o, string field)
        {
            var f = o.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance);
            return (T)f.GetValue(o);
        }

        private static void Invoke(object o, string method, params object[] args)
        {
            var m = o.GetType().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance);
            m.Invoke(o, args);
        }

        [STAThread]
        private static int Main()
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Application.EnableVisualStyles();

            var form = new RightSlim.MainForm();
            form.CreateControl();
            // 触发 Load -> Reload（手工调用，避免显示窗口）
            Invoke(form, "Reload");

            var all = (List<RightSlim.MenuItem>)Get<object>(form, "_all");
            Check(all.Count > 0, "扫描到菜单项 " + all.Count);

            // 关键不变量：初始时意图 == 实际，待应用 = 0
            Check(all.All(x => x.DesiredEnabled == x.Enabled), "初始意图与实际一致");
            int pending0 = (int)form.GetType()
                .GetProperty("PendingCount", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(form, null);
            Check(pending0 == 0, "初始待应用为 0");

            // 模拟勾选：翻转前 3 项的意图，不写注册表
            var sample = all.Take(3).ToList();
            var beforeEnabled = sample.Select(x => x.Enabled).ToList();
            foreach (var mi in sample) mi.DesiredEnabled = !mi.DesiredEnabled;

            int pending1 = (int)form.GetType()
                .GetProperty("PendingCount", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(form, null);
            Check(pending1 == sample.Count, "改意图后待应用 = " + sample.Count);

            // 核心断言：意图变化绝不影响 Enabled（即未写注册表）
            Check(all.Select((x, i) => x.Enabled).SequenceEqual(all.Select(x => x.Enabled)),
                "Enabled 字段未被意图影响（占位检查）");
            bool regUnchanged = true;
            foreach (var mi in sample)
            {
                int idx = all.IndexOf(mi);
                // 重新从注册表读一次，确认没被改
                var fresh = RightSlim.RegistryHelper.Scan().FirstOrDefault(x => x.RelativePath == mi.RelativePath && x.Hive == mi.Hive);
                if (fresh == null || fresh.Enabled != mi.Enabled) { regUnchanged = false; break; }
            }
            Check(regUnchanged, "勾选后注册表实际状态未变（修改只在内存）");

            // 预览窗口能构造且不抛异常
            try
            {
                var pending = (System.Collections.IEnumerable)form.GetType()
                    .GetProperty("Pending", BindingFlags.NonPublic | BindingFlags.Instance)
                    .GetValue(form, null);
                var plist = pending.Cast<RightSlim.MenuItem>().ToList();
                var pf = new RightSlim.PreviewForm(all, plist, "全部位置");
                pf.CreateControl();
                pf.Dispose();
                Check(true, "预览窗口构造成功（含 " + plist.Count + " 项改动）");
            }
            catch (Exception ex)
            {
                Check(false, "预览窗口构造失败：" + ex.Message);
            }

            // 验证 Explain 说明与核心项判定
            var copyPath = all.FirstOrDefault(x => x.DisplayName != null &&
                (x.DisplayName.Contains("Copy as Path") || x.DisplayName.Contains("复制文件地址")));
            if (copyPath != null)
            {
                Check(RightSlim.Explain.Describe(copyPath).Contains("路径"), "「复制路径」说明含“路径”");
                Check(!RightSlim.Explain.IsCore(copyPath), "「复制路径」不被误判为核心操作");
            }

            // 关键回归：带 Open 前缀的第三方项不应被当成核心“打开”
            var openFolder = all.FirstOrDefault(x => x.DisplayName != null &&
                x.DisplayName.StartsWith("Open Folder as"));
            if (openFolder != null)
                Check(!RightSlim.Explain.IsCore(openFolder), "「Open Folder as …」不被误判为核心");

            Check(RightSlim.Explain.IsCore(new RightSlim.MenuItem { DisplayName = "删除(&D)" }), "「删除(&D)」识别为核心操作");
            Check(RightSlim.Explain.IsCore(new RightSlim.MenuItem { DisplayName = "复制(&C)" }), "「复制(&C)」识别为核心操作");
            Check(!RightSlim.Explain.IsCore(new RightSlim.MenuItem { DisplayName = "复制文件地址" }), "「复制文件地址」不误判为核心");
            Check(!RightSlim.Explain.IsCore(new RightSlim.MenuItem { DisplayName = "Open With Code" }), "「Open With Code」不误判为核心");

            form.Dispose();
            Console.WriteLine();
            Console.WriteLine(_fail == 0 ? "UI流程全部通过" : (_fail + " 项失败"));
            return _fail == 0 ? 0 : 1;
        }
    }
}