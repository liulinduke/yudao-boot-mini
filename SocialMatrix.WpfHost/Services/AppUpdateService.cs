using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Velopack;

namespace SocialMatrix.WpfHost.Services;

/// <summary>
/// WPF 启动后的静默更新检查。更新失败不能影响主程序启动。
/// </summary>
public sealed class AppUpdateService
{
    // 当前服务器先使用 HTTP；配置 SSL 后改为 https 并发布新版本。
    private const string UpdateUrl = "http://1.14.181.156/downloads/wpf/";

    public async Task CheckAndApplyAsync(Func<bool> canRestart)
    {
        try
        {
            var manager = new UpdateManager(UpdateUrl);
            var update = await manager.CheckForUpdatesAsync();
            if (update == null || !canRestart())
            {
                return;
            }

            await manager.DownloadUpdatesAsync(update);
            if (canRestart())
            {
                manager.ApplyUpdatesAndRestart();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"WPF 更新检查失败: {ex.Message}");
        }
    }
}
