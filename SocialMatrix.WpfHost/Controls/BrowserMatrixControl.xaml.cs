using CefSharp.Wpf;
using System.Collections.Generic;
using System.Windows.Controls;

namespace SocialMatrix.WpfHost.Controls
{
    /// <summary>
    /// 浏览器矩阵控件 - 12 宫格布局
    /// </summary>
    public partial class BrowserMatrixControl : UserControl
    {
        private readonly Dictionary<string, ChromiumWebBrowser> _browsers = new();

        public BrowserMatrixControl()
        {
            InitializeComponent();
        }

        /// <summary>
        /// 创建浏览器实例
        /// </summary>
        public void CreateBrowser(string accountId, string initialUrl = "https://www.facebook.com")
        {
            if (_browsers.ContainsKey(accountId))
            {
                System.Diagnostics.Debug.WriteLine($"⚠️ 账号 {accountId} 的浏览器已存在");
                return;
            }

            var browser = new ChromiumWebBrowser(initialUrl)
            {
                Name = accountId
            };

            _browsers[accountId] = browser;
            BrowserGrid.Children.Add(browser);

            System.Diagnostics.Debug.WriteLine($"✅ 已为账号 {accountId} 创建浏览器");
        }

        /// <summary>
        /// 关闭浏览器实例
        /// </summary>
        public void CloseBrowser(string accountId)
        {
            if (_browsers.TryGetValue(accountId, out var browser))
            {
                browser.Dispose();
                _browsers.Remove(accountId);
                BrowserGrid.Children.Remove(browser);

                System.Diagnostics.Debug.WriteLine($"✅ 已关闭账号 {accountId} 的浏览器");
            }
        }
    }
}
