using System;
using System.IO;

namespace SocialMatrix.WpfHost.Helpers
{
    internal static class BrowserCachePaths
    {
        public static string Root => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SocialMatrix",
            "BrowserCache");

        public static string ForAccount(string accountId)
        {
            return Path.Combine(Root, $"account_{accountId}");
        }

        public static void MigrateLegacyCache()
        {
            var legacyRoot = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "BrowserCache");
            if (Directory.Exists(Root) || !Directory.Exists(legacyRoot) ||
                string.Equals(Root, legacyRoot, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Root)!);
                Directory.Move(legacyRoot, Root);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"旧浏览器缓存迁移失败，将使用新目录: {ex.Message}");
            }
        }
    }
}
