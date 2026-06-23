using System;
using SocialMatrix.WpfHost.Services;

namespace SocialMatrix.WpfHost.Windows
{
    public partial class BrowserMatrixWindow
    {
        private string GenerateFollowScriptFromConfig(string accountId, string? config)
        {
            try
            {
                var builder = new FollowScriptBuilder(accountId, config);
                return builder.Build();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"⚠️ 生成刷粉脚本失败: {ex.Message}");
                return "";
            }
        }
    }
}
