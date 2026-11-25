using System;
using System.Runtime.InteropServices;
using System.Text;

namespace RightSlim
{
    internal static class NativeMethods
    {
        [DllImport("shlwapi.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        private static extern int SHLoadIndirectString(
            string pszSource,
            StringBuilder pszOutBuf,
            int cchOutBuf,
            IntPtr ppvReserved);

        /// <summary>把 "@C:\Windows\system32\shell32.dll,-8506" 解析成可读文本。</summary>
        public static string ResolveIndirect(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "";
            raw = raw.Trim();
            if (!raw.StartsWith("@")) return Clean(raw);

            var sb = new StringBuilder(1024);
            try
            {
                int hr = SHLoadIndirectString(raw, sb, sb.Capacity, IntPtr.Zero);
                if (hr == 0 && sb.Length > 0)
                {
                    string s = Clean(sb.ToString());
                    if (!string.IsNullOrWhiteSpace(s)) return s;
                }
            }
            catch { /* 解析失败则回退 */ }

            // 回退：至少把资源 ID 去掉，露出 dll 名
            int comma = raw.LastIndexOf(',');
            string head = comma > 0 ? raw.Substring(0, comma) : raw;
            head = head.TrimStart('@');
            try
            {
                string name = System.IO.Path.GetFileName(head);
                if (!string.IsNullOrWhiteSpace(name) && name != head) return name;
            }
            catch { }
            return Clean(raw);
        }

        private static string Clean(string s)
        {
            if (s == null) return "";
            return s.Replace("&", "").Replace("\r", " ").Replace("\n", " ").Trim();
        }
    }
}