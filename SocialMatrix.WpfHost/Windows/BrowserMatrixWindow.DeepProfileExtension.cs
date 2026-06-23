using System;
using SocialMatrix.WpfHost.Services;

namespace SocialMatrix.WpfHost.Windows
{
    public partial class BrowserMatrixWindow
    {
        private string GenerateDeepProfileCollectScript(string accountId, string? config)
        {
            try
            {
                var builder = new DeepProfileCollectScriptBuilder(accountId, config);
                return builder.Build();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"⚠️ 生成深度采集脚本失败: {ex.Message}");
                return "";
            }
        }
    }
}
