using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ﾆﾗ
{
    public static class 葱
    {
        static byte[] CalcHash(FileInfo path)
        {
            using (var sha = new System.Security.Cryptography.SHA512CryptoServiceProvider())
            using (var fs = new FileStream(path.FullName, FileMode.Open, FileAccess.Read)) {
                return sha.ComputeHash(fs);
            }
        }

        public static bool 比較(FileInfo path1, FileInfo path2)
        {
            if (path1.Length != path2.Length) {
                return false;
            }
            var h1 = CalcHash(path1);
            var h2 = CalcHash(path2);
            if (h1.Length != h2.Length) {
                return false;
            }

            for (var i = 0; i < h1.Length; ++i) {
                if (h1[i] != h2[i]) {
                    return false;
                }
            }
            return true;
        }

        public static DirectoryInfo ｸﾞﾛーﾊﾞﾙ設定保存先 
            =>  new DirectoryInfo(
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".ﾆﾗ"));

        public static string ｸﾞﾛーﾊﾞﾙ設定ﾌｧｲﾙ作成(string name)
        {
            var d = ｸﾞﾛーﾊﾞﾙ設定保存先;
            if (!d.Exists) {
                d.Create();
            }
            return Path.Combine(d.FullName, name);
        }

    }
}
