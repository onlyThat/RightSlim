using System;
using System.Windows.Forms;

namespace RightSlim
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            Application.ThreadException += (s, e) =>
                ShowFatal(e.Exception);
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
                ShowFatal(e.ExceptionObject as Exception);

            Application.Run(new MainForm());
        }

        private static void ShowFatal(Exception ex)
        {
            try
            {
                MessageBox.Show("程序遇到错误：\n" + (ex == null ? "未知错误" : ex.Message),
                    "右键菜单瘦身大师", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch { }
        }
    }
}