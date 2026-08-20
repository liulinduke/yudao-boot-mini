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

    /// <summary>
    /// 在主界面启动后下载可用更新，但不在当前使用过程中自动重启。
    /// 已下载的更新会在下一次应用启动时统一安装。
    /// </summary>
    public async Task CheckAndDownloadAsync()
    {
        try
        {
            var manager = new UpdateManager(UpdateUrl);
            var update = await manager.CheckForUpdatesAsync();
            if (update == null)
            {
                return;
            }

            await manager.DownloadUpdatesAsync(update);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"WPF 更新检查失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 仅在新一轮应用启动时应用已经下载完成的版本。
    /// ApplyUpdatesAndRestart 会立即结束当前进程并拉起更新后的程序。
    /// </summary>
    public void ApplyPendingUpdateOnStartup()
    {
        try
        {
            var manager = new UpdateManager(UpdateUrl);
            if (manager.IsUpdatePendingRestart)
            {
                manager.ApplyUpdatesAndRestart();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"WPF 待更新版本安装失败: {ex.Message}");
        }
    }
}
