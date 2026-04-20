using System.Runtime.InteropServices;
using System.Windows;

namespace SocialMatrix.WpfHost.Services
{
    /// <summary>
    /// JS 桥接服务 - 供 Vue 前端调用 WPF 功能
    /// </summary>
    [ClassInterface(ClassInterfaceType.AutoDual)]
    [ComVisible(true)]
    public class JsBridgeService
    {
        private readonly MainWindow _mainWindow;

        public JsBridgeService(MainWindow mainWindow)
        {
            _mainWindow = mainWindow;
        }

        /// <summary>
        /// Vue 调用此方法启动浏览器并开始自动化采集
        /// </summary>
        /// <param name="detailId">明细ID(不是taskId)</param>
        /// <param name="accountId">账号ID(fbAccount)</param>
        /// <param name="cookie">Cookie</param>
        /// <param name="searchUrl">搜索URL</param>
        /// <param name="expectedCount">期望采集数量</param>
        /// <param name="taskType">任务类型(1主页/2帖子/3用户/4群组/5活动/6评论)</param>
        public void StartBrowser(string detailId, string accountId, string cookie, string searchUrl, int expectedCount, int taskType = 1)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                _mainWindow.CreateBrowserForAccount(detailId, accountId, 
                    string.IsNullOrEmpty(cookie) ? null : cookie, 
                    string.IsNullOrEmpty(searchUrl) ? null : searchUrl, 
                    expectedCount,
                    taskType: taskType);
            });
        }

        /// <summary>
        /// Vue 调用此方法关闭浏览器
        /// </summary>
        public void StopBrowser(string accountId)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                _mainWindow.CloseBrowserForAccount(accountId);
            });
        }

        /// <summary>
        /// 保存 Vue 登录后的 Token
        /// </summary>
        public void SaveToken(string token)
        {
            TokenManager.Save(token);
            System.Diagnostics.Debug.WriteLine($"✅ Token 已保存: {token.Substring(0, 20)}...");
        }

        /// <summary>
        /// 获取当前 Token
        /// </summary>
        public string GetToken()
        {
            return TokenManager.Get() ?? string.Empty;
        }

        /// <summary>
        /// 显示消息提示
        /// </summary>
        public void ShowMessage(string message)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                MessageBox.Show(message, "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            });
        }
    }
}
