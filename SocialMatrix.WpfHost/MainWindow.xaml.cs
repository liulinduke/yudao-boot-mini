
using Microsoft.Web.WebView2.Core;
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
            string? searchUrl = null, int expectedCount = 100, int taskType = 1)
        {
            // 如果窗口不存在，创建新窗口
            if (_browserMatrixWindow == null || !_browserMatrixWindow.IsVisible)
            {
                _browserMatrixWindow = new BrowserMatrixWindow();
                
                // 监听采集完成事件
                _browserMatrixWindow.OnCollectionComplete += (dId, accId, jsonData) =>
                {
                    System.Diagnostics.Debug.WriteLine($"📨 MainWindow 收到采集完成事件: 明细ID={dId}, 账号={accId}, 数据长度={jsonData.Length}");
                    
                    // 将数据回传给 Vue
                    Dispatcher.Invoke(() =>
                    {
                        ReturnCollectionDataToVue(dId, accId, jsonData);
                    });
                };
                
                System.Diagnostics.Debug.WriteLine($"✅ 已注册 OnCollectionComplete 事件监听");
                _browserMatrixWindow.Show();
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"⚠️ BrowserMatrixWindow 已存在，复用现有窗口");
            }

            // 在矩阵窗口中创建浏览器并启动自动化采集
            _browserMatrixWindow.CreateBrowser(accountId, "https://www.facebook.com", 
                cookie, searchUrl, expectedCount, taskType: taskType);
            
            // 保存 detailId 用于回传
            _browserMatrixWindow.CurrentDetailId = detailId;
            
            // 激活窗口（置顶）
            _browserMatrixWindow.Activate();
            
            UpdateStatus($"已为账号 {accountId} 启动自动化采集 (明细ID: {detailId}, 类型: {taskType})");
        }

        /// <summary>
        /// 将采集数据回传给 Vue
        /// </summary>
        private void ReturnCollectionDataToVue(string detailId, string accountId, string jsonData)
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
                                timestamp: new Date().toISOString()
                            }}
                        }}));
                        console.log('✅ CustomEvent 已触发');
                    }}, 100);
                ";
                VueWebView.CoreWebView2.ExecuteScriptAsync(script);
                System.Diagnostics.Debug.WriteLine($"✅ 已将采集数据回传给 Vue (明细ID: {detailId}, 账号: {accountId})");
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
            if (_browserMatrixWindow != null)
            {
                _browserMatrixWindow.CloseBrowser(accountId);
                UpdateStatus($"已关闭账号 {accountId} 的浏览器");
                
                // 如果没有活跃浏览器，关闭窗口
                if (_browserMatrixWindow.GetActiveBrowserCount() == 0)
                {
                    _browserMatrixWindow.Close();
                    _browserMatrixWindow = null;
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
