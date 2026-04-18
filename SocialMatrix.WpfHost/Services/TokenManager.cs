using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace SocialMatrix.WpfHost.Services
{
    /// <summary>
    /// Token 管理器 - 使用 Windows DPAPI 加密存储
    /// </summary>
    public static class TokenManager
    {
        private static readonly string TokenFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SocialMatrix",
            "token.enc");

        /// <summary>
        /// 保存 Token（加密）
        /// </summary>
        public static void Save(string token)
        {
            try
            {
                var folder = Path.GetDirectoryName(TokenFilePath);
                if (folder != null && !Directory.Exists(folder))
                {
                    Directory.CreateDirectory(folder);
                }

                // 使用 DPAPI 加密
                var encrypted = ProtectedData.Protect(
                    Encoding.UTF8.GetBytes(token),
                    null,
                    DataProtectionScope.CurrentUser);

                File.WriteAllBytes(TokenFilePath, encrypted);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"⚠️ Token 保存失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取 Token（解密）
        /// </summary>
        public static string? Get()
        {
            try
            {
                if (!File.Exists(TokenFilePath))
                {
                    return null;
                }

                var encrypted = File.ReadAllBytes(TokenFilePath);
                var decrypted = ProtectedData.Unprotect(
                    encrypted,
                    null,
                    DataProtectionScope.CurrentUser);

                return Encoding.UTF8.GetString(decrypted);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"⚠️ Token 读取失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 清除 Token
        /// </summary>
        public static void Clear()
        {
            if (File.Exists(TokenFilePath))
            {
                File.Delete(TokenFilePath);
            }
        }
    }
}
