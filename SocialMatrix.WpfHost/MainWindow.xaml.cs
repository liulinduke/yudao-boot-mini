
using Microsoft.Web.WebView2.Core;
using System.Collections.Generic;
using SocialMatrix.WpfHost.Services;
using SocialMatrix.WpfHost.Windows;
using System;
using System.Windows;

namespace SocialMatrix.WpfHost
{
    /// <summary>
    /// MainWindow 主窗口逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        private JsBridgeService? _jsBridge;
        private BrowserMatrixWindow? _browserMatrixWindow;
        private readonly Dictionary<string, BrowserMatrixWindow> _browserMatrixWindows = new();

        public MainWindow()
        {
            InitializeComponent();
            InitializeVueWebView();
        }

        /// <summary>
        /// 初始化 WebView2 加载 Vue 前端
        /// </summary>
        private async void InitializeVueWebView()
        {
            try
            {
                // 确保 WebView2 运行时已安装
                await VueWebView.EnsureCoreWebView2Async();

                // 创建 JS 桥接服务
                _jsBridge = new JsBridgeService(this);
                VueWebView.CoreWebView2.AddHostObjectToScript("wpfBridge", _jsBridge);

                // 开发环境加载本地 dev server，生产环境加载本地文件
#if DEBUG
                VueWebView.Source = new Uri("http://localhost:80");
#else
                var indexPath = System.IO.Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "wwwroot", "index.html");
                if (System.IO.File.Exists(indexPath))
                {
                    VueWebView.Source = new Uri(indexPath);
                }
#endif

                System.Diagnostics.Debug.WriteLine("✅ WebView2 初始化成功");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"WebView2 初始化失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 为指定账号创建浏览器实例（供 Vue 调用）
        /// </summary>
        public void CreateBrowserForAccount(string detailId, string accountId, string? cookie = null,
            string? searchUrl = null, int expectedCount = 100, int taskType = 1, string? config = null, bool isOperation = false)
        {
            // 记录配置信息
            if (!string.IsNullOrEmpty(config))
            {
                System.Diagnostics.Debug.WriteLine($"📋 MainWindow 收到配置: {config}");
            }

            if (!_browserMatrixWindows.ContainsKey(accountId) &&
                _browserMatrixWindows.Count >= BrowserMatrixWindow.MaxConcurrentBrowsers)
            {
                UpdateStatus($"已达到最大并发窗口数 ({BrowserMatrixWindow.MaxConcurrentBrowsers})，无法为账号 {accountId} 创建窗口");
                return;
            }

            var browserMatrixWindow = GetOrCreateBrowserMatrixWindow(accountId);

            // 在矩阵窗口中创建浏览器并启动自动化采集（每个 WPF 窗口只承载一个账号）
            browserMatrixWindow.CreateBrowser(accountId, "https://www.facebook.com",
                cookie, searchUrl, expectedCount, taskType: taskType, config: config, detailId: detailId, isOperation: isOperation);
            
            UpdateStatus($"已为账号 {accountId} 启动自动化采集 (明细ID: {detailId}, 类型: {taskType})");
        }

        public BrowserMatrixWindow? GetBrowserMatrixWindowForAccount(string accountId)
        {
            return _browserMatrixWindows.TryGetValue(accountId, out var window) && window.IsWindowAvailable
                ? window
                : null;
        }

        private BrowserMatrixWindow GetOrCreateBrowserMatrixWindow(string accountId)
        {
            if (_browserMatrixWindows.TryGetValue(accountId, out var existingWindow) && existingWindow.IsWindowAvailable)
            {
                _browserMatrixWindow = existingWindow;
                System.Diagnostics.Debug.WriteLine($"⚠️ 账号 {accountId} 的 BrowserMatrixWindow 已存在，复用该账号窗口");
                return existingWindow;
            }

            var browserMatrixWindow = new BrowserMatrixWindow();
            _browserMatrixWindows[accountId] = browserMatrixWindow;
            _browserMatrixWindow = browserMatrixWindow;

            browserMatrixWindow.Closed += (_, _) =>
            {
                if (_browserMatrixWindows.TryGetValue(accountId, out var current) && ReferenceEquals(current, browserMatrixWindow))
                {
                    _browserMatrixWindows.Remove(accountId);
                }
                if (ReferenceEquals(_browserMatrixWindow, browserMatrixWindow))
                {
                    _browserMatrixWindow = null;
                }
            };

            // 监听采集完成事件
            browserMatrixWindow.OnCollectionComplete += (dId, accId, jsonData, taskType) =>
            {
                System.Diagnostics.Debug.WriteLine($"📨 MainWindow 收到采集完成事件: 明细ID={dId}, 账号={accId}, 数据长度={jsonData.Length}, 类型={taskType}");
                
                // 将数据回传给 Vue
                Dispatcher.Invoke(() =>
                {
                    ReturnCollectionDataToVue(dId, accId, jsonData, taskType);
                });
            };

            RegisterAccountLoginWindowEvents(browserMatrixWindow);
            browserMatrixWindow.Show();
            browserMatrixWindow.Activate();
            System.Diagnostics.Debug.WriteLine($"✅ 已为账号 {accountId} 创建独立 BrowserMatrixWindow");

            return browserMatrixWindow;
        }

        /// <summary>
        /// 将采集数据回传给 Vue
        /// </summary>
        private void ReturnCollectionDataToVue(string detailId, string accountId, string jsonData, int taskType = 1)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"🔄 开始回传数据到 Vue...");
                
                // 检查 WebView2 是否就绪
                if (VueWebView.CoreWebView2 == null)
                {
                    System.Diagnostics.Debug.WriteLine($"❌ WebView2 CoreWebView2 未初始化");
                    return;
                }
                
                // 使用 ExecuteScriptAsync 触发 CustomEvent（与旧项目保持一致）
                System.Diagnostics.Debug.WriteLine($"📤 使用 CustomEvent 发送消息...");
                var script = $@"
                    setTimeout(() => {{
                        window.dispatchEvent(new CustomEvent('fb:collection:complete', {{
                            detail: {{
                                detailId: '{detailId}',
                                accountId: '{accountId}',
                                data: {jsonData},
                                taskType: {taskType},
                                timestamp: new Date().toISOString()
                            }}
                        }}));
                        console.log('✅ CustomEvent 已触发');
                    }}, 100);
                ";
                VueWebView.CoreWebView2.ExecuteScriptAsync(script);
                System.Diagnostics.Debug.WriteLine($"✅ 已将采集数据回传给 Vue (明细ID: {detailId}, 账号: {accountId}, 类型: {taskType})");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ 数据回传失败: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"堆栈跟踪: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// 关闭指定账号的浏览器
        /// </summary>
        public void CloseBrowserForAccount(string accountId)
        {
            if (_browserMatrixWindows.TryGetValue(accountId, out var browserMatrixWindow))
            {
                browserMatrixWindow.CloseBrowser(accountId);
                UpdateStatus($"已关闭账号 {accountId} 的浏览器");
                
                // 如果没有活跃浏览器，关闭窗口
                if (browserMatrixWindow.GetActiveBrowserCount() == 0)
                {
                    browserMatrixWindow.Close();
                    _browserMatrixWindows.Remove(accountId);
                    if (ReferenceEquals(_browserMatrixWindow, browserMatrixWindow))
                    {
                        _browserMatrixWindow = null;
                    }
                }
            }
        }

        /// <summary>
        /// 更新底部状态栏
        /// </summary>
        public void UpdateStatus(string message)
        {
            System.Diagnostics.Debug.WriteLine($"[Status] {message}");
        }
    }
}
