using CefSharp;
using CefSharp.Wpf;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SocialMatrix.WpfHost.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;

namespace SocialMatrix.WpfHost.Windows
{
    /// <summary>
    /// 浏览器矩阵窗口 - 独立弹窗显示
    /// </summary>
    public partial class BrowserMatrixWindow : Window
    {
        private readonly Dictionary<string, ChromiumWebBrowser> _browsers = new();
        private readonly Dictionary<string, bool> _browserInitialized = new(); // 跟踪指纹注入状态
        private readonly Dictionary<string, int> _accountTaskTypes = new(); // 账号 -> 任务类型映射
        private readonly Dictionary<string, string> _accountDetailIds = new(); // 账号 -> 任务明细ID
        private readonly Dictionary<string, IRequestContext> _requestContexts = new(); // 账号 -> 独立请求上下文
        private readonly Dictionary<string, (string fbUserId, string messageText)> _dmOperationParams = new(); // 账号 -> 私信参数
        private readonly Dictionary<string, string> _dmTaskIds = new(); // 账号 -> 私信主任务ID
        private readonly Dictionary<string, bool> _accountIsOperation = new(); // 账号 -> 是否为运营任务

        // 当前明细ID(用于回传,单任务场景，兼容旧逻辑)
        public string? CurrentDetailId { get; set; }

        // 采集结果回调
        public event Action<string, string, string, int>? OnCollectionComplete; // (detailId, accountId, jsonData, taskType)
        public event Action<string, string>? OnCollectionError;    // (accountId, errorMessage)

        // 最大并发数配置（从后端读取，默认19 - 8GB内存推荐值）
        private static int _maxConcurrentBrowsers = 19;
        public static int MaxConcurrentBrowsers => _maxConcurrentBrowsers;
        private static FingerprintGlobalConfig? _globalConfig = null;
        private static DateTime _configLastFetchTime = DateTime.MinValue;
        private static readonly TimeSpan ConfigCacheDuration = TimeSpan.FromMinutes(5);

        public bool IsWindowAvailable => IsVisible;

        /// <summary>
        /// 指纹浏览器全局配置
        /// </summary>
        private class FingerprintGlobalConfig
        {
            public bool DisableImages { get; set; } = true;   // 默认不加载图片
            public bool DisableVideos { get; set; } = true;   // 默认不加载视频
            public int MaxConcurrent { get; set; } = 19;      // 8GB内存推荐值：(8192 * 0.7) / 300 ≈ 19
        }

        /// <summary>
        /// 从后端获取全局配置（带缓存）
        /// </summary>
        private static async Task<FingerprintGlobalConfig?> GetGlobalConfigAsync()
        {
            // 如果缓存未过期，直接返回
            if (_globalConfig != null && (DateTime.Now - _configLastFetchTime) < ConfigCacheDuration)
            {
                return _globalConfig;
            }

            try
            {
                using var httpClient = new System.Net.Http.HttpClient();
                httpClient.Timeout = TimeSpan.FromSeconds(3);

                // TODO: 替换为实际的后端 API 地址
                var response = await httpClient.GetStringAsync("http://localhost:48080/admin-api/facebook/global-config/all");
                var responseToken = JToken.Parse(response);
                var configToken = responseToken as JArray
                    ?? responseToken["data"] as JArray
                    ?? responseToken["result"] as JArray;
                var configs = configToken?.ToObject<List<Dictionary<string, string>>>();

                if (configs != null)
                {
                    var config = new FingerprintGlobalConfig();
                    foreach (var item in configs)
                    {
                        if (item.ContainsKey("configKey") && item.ContainsKey("configValue"))
                        {
                            switch (item["configKey"])
                            {
                                case "browser_disable_images":
                                    config.DisableImages = item["configValue"] == "true";
                                    break;
                                case "browser_disable_videos":
                                    config.DisableVideos = item["configValue"] == "true";
                                    break;
                                case "browser_max_concurrent":
                                    if (int.TryParse(item["configValue"], out int maxConcurrent))
                                    {
                                        config.MaxConcurrent = Math.Min(Math.Max(maxConcurrent, 1), 50);
                                    }
                                    break;
                            }
                        }
                    }

                    _globalConfig = config;
                    _configLastFetchTime = DateTime.Now;
                    _maxConcurrentBrowsers = config.MaxConcurrent;

                    System.Diagnostics.Debug.WriteLine($"✅ 全局配置加载成功: DisableImages={config.DisableImages}, DisableVideos={config.DisableVideos}, MaxConcurrent={config.MaxConcurrent}");
                    return config;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"⚠️ 加载全局配置失败: {ex.Message}，使用默认配置");
            }

            // 返回默认配置
            var defaultConfig = new FingerprintGlobalConfig();
            System.Diagnostics.Debug.WriteLine($"🔧 使用默认配置: DisableImages={defaultConfig.DisableImages}, DisableVideos={defaultConfig.DisableVideos}");
            return defaultConfig;
        }

        /// <summary>
        /// 公开方法：从前端接收配置并更新缓存（立即生效）
        /// </summary>
        public static void UpdateGlobalConfig(bool disableImages, bool disableVideos, int maxConcurrent)
        {
            _globalConfig = new FingerprintGlobalConfig
            {
                DisableImages = disableImages,
                DisableVideos = disableVideos,
                MaxConcurrent = Math.Min(Math.Max(maxConcurrent, 1), 50)
            };
            _configLastFetchTime = DateTime.Now;
            _maxConcurrentBrowsers = _globalConfig.MaxConcurrent;

            System.Diagnostics.Debug.WriteLine($"✅ 全局配置已更新（来自前端）: DisableImages={disableImages}, DisableVideos={disableVideos}, MaxConcurrent={maxConcurrent}");
        }

        public BrowserMatrixWindow()
        {
            InitializeComponent();

            // 监听窗口大小变化，重新计算布局
            this.SizeChanged += (sender, e) =>
            {
                UpdateLayout();
            };

            // 监听窗口关闭事件，清理所有资源
            this.Closed += (sender, e) =>
            {
                CleanupAllResources();
            };

            // 预拉取全局配置，创建浏览器时可直接使用拦截设置
            _ = GetGlobalConfigAsync();
        }

        /// <summary>
        /// 清理所有浏览器和 RequestContext 资源
        /// </summary>
        private void CleanupAllResources()
        {
            System.Diagnostics.Debug.WriteLine($"🧹 开始清理所有浏览器资源...");

            // 释放所有 RequestContext
            foreach (var kvp in _requestContexts)
            {
                try
                {
                    kvp.Value.Dispose();
                    System.Diagnostics.Debug.WriteLine($"🗑️ 已释放账号 {kvp.Key} 的请求上下文");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"⚠️ 释放账号 {kvp.Key} 的请求上下文失败: {ex.Message}");
                }
            }
            _requestContexts.Clear();

            // 释放所有浏览器
            foreach (var kvp in _browsers)
            {
                try
                {
                    kvp.Value.Dispose();
                    System.Diagnostics.Debug.WriteLine($"🗑️ 已释放账号 {kvp.Key} 的浏览器");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"⚠️ 释放账号 {kvp.Key} 的浏览器失败: {ex.Message}");
                }
            }
            _browsers.Clear();

            System.Diagnostics.Debug.WriteLine($"✅ 所有资源清理完成");
        }

        /// <summary>
        /// 自定义菜单处理器 - 启用开发者工具
        /// </summary>
        private class CustomMenuHandler : IContextMenuHandler
        {
            public void OnBeforeContextMenu(IWebBrowser browserControl, IBrowser browser, IFrame frame, IContextMenuParams parameters, IMenuModel model)
            {
                // 添加“检查元素”选项
                model.AddItem((CefMenuCommand)26501, "检查元素 (Inspect Element)");
            }

            public bool OnContextMenuCommand(IWebBrowser browserControl, IBrowser browser, IFrame frame, IContextMenuParams parameters, CefMenuCommand commandId, CefEventFlags eventFlags)
            {
                if ((int)commandId == 26501)
                {
                    // 打开开发者工具
                    browser.GetHost().ShowDevTools();
                    return true;
                }
                return false;
            }

            public void OnContextMenuDismissed(IWebBrowser browserControl, IBrowser browser, IFrame frame)
            {
            }

            public bool RunContextMenu(IWebBrowser browserControl, IBrowser browser, IFrame frame, IContextMenuParams parameters, IMenuModel model, IRunContextMenuCallback callback)
            {
                return false; // 使用默认行为
            }
        }

        /// <summary>
        /// 创建浏览器实例并启动自动化采集
        /// </summary>
        public void CreateBrowser(string accountId, string initialUrl = "https://www.facebook.com",
            string? cookie = null, string? searchUrl = null, int expectedCount = 100, long? deviceId = null, int taskType = 1, string? config = null, string? detailId = null, bool isOperation = false)
        {
            if (!string.IsNullOrEmpty(detailId))
            {
                _accountDetailIds[accountId] = detailId;
                CurrentDetailId = detailId;
            }

            // 存储是否为运营任务
            _accountIsOperation[accountId] = isOperation;
            System.Diagnostics.Debug.WriteLine($"📋 BrowserMatrixWindow 存储 isOperation={isOperation} for account={accountId}");

            // 记录配置信息（如果有）
            if (!string.IsNullOrEmpty(config))
            {
                System.Diagnostics.Debug.WriteLine($"📋 BrowserMatrixWindow 收到配置: {config}");

                // 群发私信任务：从config中解析私信参数并存储
                if (taskType == 14)
                {
                    try
                    {
                        var configObj = Newtonsoft.Json.Linq.JObject.Parse(config);
                        string? taskId = configObj.ContainsKey("taskId") ? configObj.Value<string>("taskId") : null;
                        string? fbUserId = configObj.ContainsKey("fbUserId") ? configObj.Value<string>("fbUserId") : null;
                        string? messageText = configObj.ContainsKey("messageText") ? configObj.Value<string>("messageText") : null;
                        if (!string.IsNullOrEmpty(taskId))
                        {
                            _dmTaskIds[accountId] = taskId;
                        }
                        if (!string.IsNullOrEmpty(fbUserId) && !string.IsNullOrEmpty(messageText))
                        {
                            _dmOperationParams[accountId] = (fbUserId, messageText);
                            System.Diagnostics.Debug.WriteLine($"📋 已存储私信参数: 任务={taskId}, 目标={fbUserId}, 消息长度={messageText.Length}");
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"⚠️ 解析私信配置失败: {ex.Message}");
                    }
                }
            }

            // 检查是否超过最大并发数
            if (_browsers.Count >= _maxConcurrentBrowsers)
            {
                System.Diagnostics.Debug.WriteLine($"⚠️ 已达到最大并发数限制 ({_maxConcurrentBrowsers})，无法为账号 {accountId} 创建新浏览器");
                OnCollectionError?.Invoke(accountId, $"已达到最大并发数限制 ({_maxConcurrentBrowsers})，请先关闭一些浏览器窗口");
                return;
            }

            // 如果浏览器已存在，检查是否需要重新采集
            if (_browsers.ContainsKey(accountId))
            {
                System.Diagnostics.Debug.WriteLine($"⚠️ 账号 {accountId} 的浏览器已存在");
                _accountTaskTypes[accountId] = taskType;

                // 如果提供了新的搜索 URL，重新启动任务
                if (!string.IsNullOrEmpty(searchUrl))
                {
                    var existingBrowser = _browsers[accountId];
                    System.Diagnostics.Debug.WriteLine($"🔄 为已存在的浏览器启动新任务: {searchUrl}, taskType={taskType}");

                    // 异步启动（不阻塞）
                    Task.Run(async () =>
                    {
                        if (isOperation)
                        {
                            // 运营任务走独立分发
                            await StartOperationTask(existingBrowser, accountId, searchUrl, expectedCount, taskType, config);
                        }
                        else
                        {
                            // 采集任务走采集逻辑
                            await StartAutoCollect(existingBrowser, accountId, searchUrl, expectedCount, taskType, config);
                        }
                    });
                }
                return;
            }

            // 为每个账号创建独立的 RequestContext（实现完全隔离）
            var cachePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "BrowserCache", $"account_{accountId}");
            if (!Directory.Exists(cachePath))
            {
                Directory.CreateDirectory(cachePath);
            }

            var requestContextSettings = new RequestContextSettings
            {
                CachePath = cachePath,
                PersistSessionCookies = false
            };

            var requestContext = new RequestContext(requestContextSettings);
            _requestContexts[accountId] = requestContext;

            System.Diagnostics.Debug.WriteLine($"🔒 为账号 {accountId} 创建独立缓存: {cachePath}");

            // 先用 about:blank，避免在未设置资源拦截/Cookie 前就加载 Facebook
            var browser = new ChromiumWebBrowser("about:blank")
            {
                RequestContext = requestContext,  // 使用独立的请求上下文
                Background = System.Windows.Media.Brushes.White  // 设置白色背景，避免灰色遮罩
            };
            browser.Tag = accountId;

            // 立即应用缓存的资源拦截配置（首次 Load 即生效）
            var cachedConfig = _globalConfig ?? new FingerprintGlobalConfig();
            FingerprintInjector.ApplyResourceFilter(browser, cachedConfig.DisableImages, cachedConfig.DisableVideos);

            // 仅在 Debug 模式下启用右键菜单和开发者工具
#if DEBUG
            browser.MenuHandler = new CustomMenuHandler();
#endif

            // 创建容器（StackPanel）来包含 URL 标签和浏览器
            var container = new System.Windows.Controls.StackPanel();
            container.Tag = accountId; // 保存 accountId 以便后续查找

            // 创建 URL 显示标签
            var urlLabel = new System.Windows.Controls.TextBlock
            {
                Text = initialUrl,
                FontSize = 9,
                Foreground = System.Windows.Media.Brushes.DarkGray,
                Padding = new System.Windows.Thickness(2, 1, 2, 1),
                Background = System.Windows.Media.Brushes.WhiteSmoke,
                TextTrimming = System.Windows.TextTrimming.CharacterEllipsis,
                Height = 18
            };

            // 监听 URL 变化并更新标签
            browser.AddressChanged += (s, args) =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    urlLabel.Text = browser.Address ?? "";
                });
            };

            container.Children.Add(urlLabel);
            container.Children.Add(browser);

            _browsers[accountId] = browser;
            _accountTaskTypes[accountId] = taskType; // 保存账号对应的任务类型
            BrowserGrid.Children.Add(container);

            // 更新布局
            UpdateLayout();

            System.Diagnostics.Debug.WriteLine($"✅ 已为账号 {accountId} 创建浏览器");

            // LoadingStateChanged 在 CEF 后台线程触发，访问 browser 属性必须切到 UI 线程
            browser.LoadingStateChanged += (sender, e) =>
            {
                if (e.IsLoading) return;

                _ = RunOnBrowserUiThreadAsync(browser, async () =>
                {
                    if (!browser.CanExecuteJavascriptInMainFrame) return;

                    var address = browser.Address ?? "";
                    if (address.StartsWith("about:", StringComparison.OrdinalIgnoreCase)) return;
                    if (_browserInitialized.ContainsKey(accountId) && _browserInitialized[accountId]) return;

                    _browserInitialized[accountId] = true;

                    try
                    {
                        var globalConfig = await GetGlobalConfigAsync();
                        FingerprintInjector.ApplyResourceFilter(
                            browser,
                            globalConfig?.DisableImages ?? true,
                            globalConfig?.DisableVideos ?? true);

                        var fingerprint = new FingerprintConfig
                        {
                            Area = "",
                            Latitude = null,
                            Longitude = null,
                            DeviceId = deviceId
                        };
                        await FingerprintInjector.InjectScriptAsync(browser, fingerprint);
                        System.Diagnostics.Debug.WriteLine($"✅ 账号 {accountId} 指纹脚本注入完成 (DeviceName={fingerprint.DeviceName})");

                        bool isCookieValid = true;
                        if (!string.IsNullOrEmpty(cookie))
                        {
                            if (await CheckIfLoginPage(browser))
                            {
                                System.Diagnostics.Debug.WriteLine($"❌ 账号 {accountId} Cookie 失效，停留在登录页");
                                OnCollectionError?.Invoke(accountId, "Cookie已失效，需要重新登录");
                                isCookieValid = false;
                            }
                            else
                            {
                                System.Diagnostics.Debug.WriteLine($"✅ 账号 {accountId} Cookie 验证通过");
                            }
                        }

                        if (isCookieValid && !string.IsNullOrEmpty(searchUrl))
                        {
                            if (isOperation)
                            {
                                await StartOperationTask(browser, accountId, searchUrl, expectedCount, taskType, config);
                            }
                            else
                            {
                                await StartAutoCollect(browser, accountId, searchUrl, expectedCount, taskType, config);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"❌ 账号 {accountId} 初始化失败: {ex.Message}");
                        OnCollectionError?.Invoke(accountId, $"浏览器初始化失败: {ex.Message}");
                    }
                });
            };

            // 异步：拉取配置 → 注入 Cookie → 首次加载（只加载一次 Facebook）
            _ = InitializeBrowserAsync(browser, accountId, cookie, initialUrl);
        }

        /// <summary>
        /// 浏览器创建后的异步初始化：配置资源拦截、注入 Cookie，再发起首次导航
        /// </summary>
        private async Task InitializeBrowserAsync(ChromiumWebBrowser browser, string accountId, string? cookie, string initialUrl)
        {
            try
            {
                var globalConfig = await GetGlobalConfigAsync() ?? new FingerprintGlobalConfig();
                System.Diagnostics.Debug.WriteLine($"🔍 全局配置: DisableImages={globalConfig.DisableImages}, DisableVideos={globalConfig.DisableVideos}");

                await RunOnBrowserUiThreadAsync(browser, async () =>
                {
                    FingerprintInjector.ApplyResourceFilter(browser, globalConfig.DisableImages, globalConfig.DisableVideos);

                    if (!string.IsNullOrEmpty(cookie))
                    {
                        System.Diagnostics.Debug.WriteLine($"🍪 为账号 {accountId} 预注入 Cookie（首次加载前）...");
                        await InjectCookies(browser, accountId, cookie);
                    }

                    System.Diagnostics.Debug.WriteLine($"🔗 首次加载: {initialUrl}");
                    browser.Load(initialUrl);
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ 账号 {accountId} 预初始化失败: {ex.Message}");
                OnCollectionError?.Invoke(accountId, $"浏览器预初始化失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 更新布局 - 固定2列布局,每行2个浏览器
        /// </summary>
        private void UpdateLayout()
        {
            int count = _browsers.Count;
            if (count == 0) return;

            double gridWidth = BrowserGrid.ActualWidth > 0 ? BrowserGrid.ActualWidth : 1180;
            double windowHeight = this.ActualHeight > 0 ? this.ActualHeight : 700;

            const double UrlLabelHeight = 18;
            const double MarginPadding = 20;

            double browserWidth;
            double browserHeight;

            // 每个账号独立一个浏览器窗口，占满整个窗口空间
            browserWidth = gridWidth - MarginPadding;
            browserHeight = windowHeight - MarginPadding - UrlLabelHeight;

            // 应用布局
            foreach (var container in BrowserGrid.Children.OfType<System.Windows.Controls.StackPanel>())
            {
                if (container.Children.Count >= 2 &&
                    container.Children[1] is ChromiumWebBrowser browser)
                {
                    browser.Width = browserWidth;
                    browser.Height = browserHeight;
                    browser.Margin = new System.Windows.Thickness(0);
                }
            }

            System.Diagnostics.Debug.WriteLine($"📐 布局更新: {count}个账号, 浏览器尺寸{browserWidth:F0}x{browserHeight:F0}px");
        }

        /// <summary>
        /// 关闭浏览器实例
        /// </summary>
        public void CloseBrowser(string accountId)
        {
            if (_browsers.TryGetValue(accountId, out var browser))
            {
                // 查找并移除容器
                var container = BrowserGrid.Children.OfType<System.Windows.Controls.StackPanel>()
                    .FirstOrDefault(c => c.Tag?.ToString() == accountId);

                if (container != null)
                {
                    // 释放浏览器
                    browser.Dispose();
                    _browsers.Remove(accountId);

                    // 释放 RequestContext
                    if (_requestContexts.TryGetValue(accountId, out var requestContext))
                    {
                        requestContext.Dispose();
                        _requestContexts.Remove(accountId);
                        System.Diagnostics.Debug.WriteLine($"🗑️ 已释放账号 {accountId} 的请求上下文");
                    }

                    BrowserGrid.Children.Remove(container);

                    // 更新布局
                    UpdateLayout();

                    System.Diagnostics.Debug.WriteLine($"✅ 已关闭账号 {accountId} 的浏览器");
                }
            }
        }

        /// <summary>
        /// 检查当前页面是否是登录页（通过 JavaScript 检测）
        /// </summary>
        private async Task<bool> CheckIfLoginPage(ChromiumWebBrowser browser)
        {
            try
            {
                // 首先获取当前 URL
                string currentUrl = "";
                Application.Current.Dispatcher.Invoke(() =>
                {
                    currentUrl = browser.Address ?? "";
                });

                System.Diagnostics.Debug.WriteLine($"🔍 检测登录页 - 当前URL: {currentUrl}");

                // 执行 JavaScript 检测是否是登录页
                var jsCheckLogin = @"
(function() {
    const url = window.location.href;
    const pathname = window.location.pathname;

    console.log('[登录检测] URL:', url);
    console.log('[登录检测] Pathname:', pathname);

    // 检测1: URL 包含登录/验证相关关键词
    const loginKeywords = [
        '/login',
        '/checkpoint',
        '/recover',
        '/confirmemail',
        '/disabled_account',
        '/account_disabled',
        '/security/checkpoint',
        'login.php',
        'checkpoint.php'
    ];

    for (const keyword of loginKeywords) {
        if (url.includes(keyword)) {
            console.log('[登录检测] 匹配关键词:', keyword);
            return true;
        }
    }

    // 检测2: 页面中有 Facebook 登录表单的特征元素
    const loginSelectors = [
        'form[action*=""/login""]',
        '#login_form',
        '[data-testid=""royal_login_form""]',
        '[data-testid=""login_form""]',
        'input[name=""email""]',
        'input[name=""pass""]',
        'input[type=""email""][aria-label*=""Email""]',
        'input[type=""password""][aria-label*=""Password""]',
        'button[type=""submit""][name=""login""]',
        '[aria-label*=""Log In""]',
        '[aria-label*=""登录""]'
    ];

    for (const selector of loginSelectors) {
        if (document.querySelector(selector)) {
            console.log('[登录检测] 找到登录元素:', selector);
            return true;
        }
    }

    // 检测3: 检查是否有账号被封/禁用的提示
    const bodyText = document.body.innerText.toLowerCase();
    const disabledKeywords = [
        'account has been disabled',
        'your account has been suspended',
        'account disabled',
        'violated our community standards',
        '您的账号已被禁用',
        '账号被封',
        '违反社区准则'
    ];

    for (const keyword of disabledKeywords) {
        if (bodyText.includes(keyword.toLowerCase())) {
            console.log('[登录检测] 发现封号关键词:', keyword);
            return true;
        }
    }

    // 检测4: 检查页面是否有主页特征（动态流、导航等）
    const mainFeatures = [
        '[role=""feed""]',
        '[aria-label=""Create a post""]',
        '[data-pagelet=""MainFeed""]',
        '[aria-label=""Home""]',
        '[aria-label=""首页""]',
        'nav[aria-label=""Primary""]'
    ];

    let hasMainFeature = false;
    for (const selector of mainFeatures) {
        if (document.querySelector(selector)) {
            hasMainFeature = true;
            console.log('[登录检测] 找到主页特征:', selector);
            break;
        }
    }

    // 如果是根路径且没有主页特征，判定为登录页
    if (!hasMainFeature &&
        (pathname === '/' || pathname === '' || pathname === '/login.php')) {
        console.log('[登录检测] 根路径且无主页特征，判定为登录页');
        return true;
    }

    console.log('[登录检测] 判定为非登录页');
    return false;
})();
                ";

                var result = await browser.EvaluateScriptAsync(jsCheckLogin);
                System.Diagnostics.Debug.WriteLine($"🔍 EvaluateScriptAsync 返回: Success={result.Success}, Result类型={result.Result?.GetType().FullName ?? "null"}");

                if (result.Success && result.Result != null)
                {
                    bool isLogin = Convert.ToBoolean(result.Result);
                    System.Diagnostics.Debug.WriteLine($"🔍 登录页检测结果: {isLogin}");
                    return isLogin;
                }

                System.Diagnostics.Debug.WriteLine($"⚠️ JavaScript执行失败，假设不是登录页");
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"⚠️ 检测登录页失败: {ex.Message}");
                return false; // 检测失败时假设不是登录页，继续执行
            }
        }

        /// <summary>
        /// 预注入 Cookie（在首次页面加载前写入，无需 Reload）
        /// </summary>
        /// <returns>true: 至少写入一个 Cookie</returns>
        private async Task<bool> InjectCookies(ChromiumWebBrowser browser, string accountId, string cookieJson)
        {
            try
            {
                // 使用动态类型解析，避免枚举转换问题
                var cookieList = JsonConvert.DeserializeObject<List<dynamic>>(cookieJson);
                if (cookieList == null) return false;

                // ❗ 关键修复：使用浏览器关联的 RequestContext 的 CookieManager，而不是全局的
                var cookieManager = browser.RequestContext.GetCookieManager(null);
                if (cookieManager == null)
                {
                    System.Diagnostics.Debug.WriteLine($"⚠️ 无法获取账号 {accountId} 的 CookieManager");
                    return false;
                }

                int successCount = 0;

                foreach (var cookieData in cookieList)
                {
                    try
                    {
                        var cookie = new CefSharp.Cookie
                        {
                            Name = cookieData.name?.ToString(),
                            Value = cookieData.value?.ToString(),
                            Domain = cookieData.domain?.ToString(),
                            Path = cookieData.path?.ToString() ?? "/",
                            Secure = cookieData.secure?.ToObject<bool>() ?? false,
                            HttpOnly = cookieData.httpOnly?.ToObject<bool>() ?? false,
                            Expires = cookieData.expirationDate != null
                                ? DateTimeOffset.FromUnixTimeSeconds(cookieData.expirationDate).DateTime
                                : DateTime.MaxValue
                        };

                        // 处理 sameSite 字段（可选）
                        if (cookieData.sameSite != null)
                        {
                            var sameSiteStr = cookieData.sameSite.ToString();
                            CefSharp.Enums.CookieSameSite sameSite;
                            if (Enum.TryParse<CefSharp.Enums.CookieSameSite>(sameSiteStr, true, out sameSite))
                            {
                                cookie.SameSite = sameSite;
                            }
                            else
                            {
                                // 默认设置为 NoRestriction（对应 None）
                                cookie.SameSite = CefSharp.Enums.CookieSameSite.NoRestriction;
                            }
                        }

                        await cookieManager.SetCookieAsync("https://www.facebook.com", cookie);
                        successCount++;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"⚠️ 单个 Cookie 注入失败: {ex.Message}");
                    }
                }

                System.Diagnostics.Debug.WriteLine($"✅ 已为账号 {accountId} 预注入 {successCount}/{cookieList.Count} 个 Cookie (使用独立RequestContext)");
                return successCount > 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"⚠️ Cookie 注入失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 启动运营任务（taskType >= 9）：群发私信/链接加组/转帖等
        /// 与采集任务分离，运营任务在 LoadingStateChanged 中等页面加载完成后执行
        /// </summary>
        private async Task StartOperationTask(ChromiumWebBrowser browser, string accountId,
            string searchUrl, int expectedCount, int taskType = 9, string? config = null)
        {
            System.Diagnostics.Debug.WriteLine($"🚀 开始运营任务: taskType={taskType}, url={searchUrl}");

            try
            {
                // 0. 验证浏览器是否仍然有效（需要在UI线程访问）
                bool isBrowserDisposed = false;
                bool canExecuteJavascript = false;
                Application.Current.Dispatcher.Invoke(() =>
                {
                    isBrowserDisposed = browser.IsDisposed;
                    canExecuteJavascript = browser.CanExecuteJavascriptInMainFrame;
                });

                if (isBrowserDisposed || !canExecuteJavascript)
                {
                    System.Diagnostics.Debug.WriteLine($"❌ 账号 {accountId} 浏览器已失效");
                    OnCollectionError?.Invoke(accountId, "浏览器已失效");
                    return;
                }

                // 1. 导航到目标任务 URL（Cookie 注入后浏览器停留在 facebook.com 首页，必须主动导航）
                string currentUrl = "";
                Application.Current.Dispatcher.Invoke(() =>
                {
                    currentUrl = browser.Address ?? "";
                });
                System.Diagnostics.Debug.WriteLine($"🔍 导航前 URL: {currentUrl}");

                if (!string.IsNullOrEmpty(searchUrl) && !IsOnTargetUrl(currentUrl, searchUrl))
                {
                    System.Diagnostics.Debug.WriteLine($"🔗 导航到任务 URL: {searchUrl}");
                    await NavigateBrowserToUrlAsync(browser, accountId, searchUrl);
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        currentUrl = browser.Address ?? "";
                    });
                    System.Diagnostics.Debug.WriteLine($"🔍 导航后 URL: {currentUrl}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"⏳ 已在目标页面，等待稳定...");
                    await Task.Delay(1500);
                }

                // 2. 检查是否被重定向到登录页
                if (currentUrl.Contains("/login") || currentUrl.Contains("/checkpoint"))
                {
                    System.Diagnostics.Debug.WriteLine($"❌ 账号 {accountId} Cookie 失效，被重定向到: {currentUrl}");
                    OnCollectionError?.Invoke(accountId, "Cookie已失效或账号被封");
                    return;
                }

                // 4. 根据任务类型分发
                switch (taskType)
                {
                    case 9:
                        // 链接加组：由 ExecuteAddGroupTaskAsync 处理
                        System.Diagnostics.Debug.WriteLine($"📋 执行链接加组任务...");
                        var addGroupJson = await ExecuteAddGroupTaskAsync(browser, accountId, config);
                        if (addGroupJson != null)
                        {
                            string detailId = _accountDetailIds.ContainsKey(accountId) ? _accountDetailIds[accountId] : (CurrentDetailId ?? "");
                            OnCollectionComplete?.Invoke(detailId, accountId, addGroupJson, 9);
                        }
                        break;

                    case 14:
                        // 群发私信：等待页面完全加载，然后执行私信发送
                        System.Diagnostics.Debug.WriteLine($"📨 执行群发私信任务...");
                        await WaitForPageReady(browser, timeoutMs: 15000);
                        System.Diagnostics.Debug.WriteLine($"✅ 私信页面已就绪");

                        if (_dmOperationParams.TryGetValue(accountId, out var dmParams))
                        {
                            string dmTaskId = _dmTaskIds.TryGetValue(accountId, out var tid) ? tid : "";
                            string dmDetailId = _accountDetailIds.TryGetValue(accountId, out var did) ? did : (CurrentDetailId ?? "");
                            await SendDirectMessage(accountId, dmParams.fbUserId, dmParams.messageText, dmTaskId, dmDetailId);
                        }
                        else
                        {
                            // 从 config JSON 中重新解析
                            if (!string.IsNullOrEmpty(config))
                            {
                                try
                                {
                                    var configObj = Newtonsoft.Json.Linq.JObject.Parse(config);
                                    string fbUserId = configObj.ContainsKey("fbUserId") ? configObj.Value<string>("fbUserId") ?? "" : "";
                                    string messageText = configObj.ContainsKey("messageText") ? configObj.Value<string>("messageText") ?? "" : "";
                                    if (!string.IsNullOrEmpty(fbUserId) && !string.IsNullOrEmpty(messageText))
                                    {
                                        string dmTaskId = configObj.ContainsKey("taskId") ? configObj.Value<string>("taskId") ?? "" : "";
                                        string dmDetailId = _accountDetailIds.TryGetValue(accountId, out var did) ? did : (CurrentDetailId ?? "");
                                        await SendDirectMessage(accountId, fbUserId, messageText, dmTaskId, dmDetailId);
                                    }
                                    else
                                    {
                                        System.Diagnostics.Debug.WriteLine($"⚠️ 私信参数无效: fbUserId={fbUserId}, messageText为空={string.IsNullOrEmpty(messageText)}");
                                        OnCollectionError?.Invoke(accountId, "私信参数无效");
                                    }
                                }
                                catch (Exception ex)
                                {
                                    System.Diagnostics.Debug.WriteLine($"⚠️ 解析私信参数失败: {ex.Message}");
                                    OnCollectionError?.Invoke(accountId, "解析私信参数失败");
                                }
                            }
                            else
                            {
                                System.Diagnostics.Debug.WriteLine($"⚠️ 未找到私信参数，config={config}");
                                OnCollectionError?.Invoke(accountId, "未找到私信参数");
                            }
                        }
                        break;

                    case 10:
                    case 15:
                        System.Diagnostics.Debug.WriteLine($"📋 执行转帖任务...");
                        await WaitForPageReady(browser, timeoutMs: 30000);
                        await Task.Delay(2500);
                        var repostScript = GenerateRepostScriptFromConfig(config);
                        if (string.IsNullOrWhiteSpace(repostScript))
                        {
                            OnCollectionError?.Invoke(accountId, "转帖脚本生成失败，请检查任务配置");
                            break;
                        }
                        System.Diagnostics.Debug.WriteLine($"🔍 转帖脚本长度: {repostScript.Length} 字符");
                        System.Diagnostics.Debug.WriteLine("⏳ 转帖脚本执行中（含等待目标帖，约 25s 内）...");
                        var evalTask = browser.EvaluateScriptAsync(repostScript);
                        var completed = await Task.WhenAny(evalTask, Task.Delay(120000));
                        if (completed != evalTask)
                        {
                            OnCollectionError?.Invoke(accountId, "转帖脚本执行超时（120s）");
                            break;
                        }
                        var result = await evalTask;
                        System.Diagnostics.Debug.WriteLine("⏳ 转帖脚本执行返回");
                        if (result.Success)
                        {
                            string detailId = _accountDetailIds.ContainsKey(accountId) ? _accountDetailIds[accountId] : (CurrentDetailId ?? "");
                            string resultStr = result.Result?.ToString() ?? "[]";
                            System.Diagnostics.Debug.WriteLine($"✅ 转帖执行完成: {resultStr}");
                            OnCollectionComplete?.Invoke(detailId, accountId, resultStr, taskType);
                        }
                        else
                        {
                            OnCollectionError?.Invoke(accountId, $"转帖JS执行失败: {result.Message}");
                        }
                        break;

                    case 16:
                        System.Diagnostics.Debug.WriteLine($"⭐ 执行刷粉任务...");
                        await WaitForPageReady(browser, timeoutMs: 30000);
                        await Task.Delay(2000);
                        var followScript = GenerateFollowScriptFromConfig(accountId, config);
                        if (string.IsNullOrWhiteSpace(followScript))
                        {
                            OnCollectionError?.Invoke(accountId, "刷粉脚本生成失败，请检查任务配置");
                            break;
                        }
                        System.Diagnostics.Debug.WriteLine($"🔍 刷粉脚本长度: {followScript.Length} 字符");
                        var followEvalTask = browser.EvaluateScriptAsync(followScript);
                        var followCompleted = await Task.WhenAny(followEvalTask, Task.Delay(420000));
                        if (followCompleted != followEvalTask)
                        {
                            OnCollectionError?.Invoke(accountId, "刷粉脚本执行超时（420s）");
                            break;
                        }
                        var followResult = await followEvalTask;
                        if (followResult.Success)
                        {
                            string detailId = _accountDetailIds.ContainsKey(accountId) ? _accountDetailIds[accountId] : (CurrentDetailId ?? "");
                            string resultStr = followResult.Result?.ToString() ?? "[]";
                            System.Diagnostics.Debug.WriteLine($"✅ 刷粉执行完成: {resultStr}");
                            OnCollectionComplete?.Invoke(detailId, accountId, resultStr, 16);
                        }
                        else
                        {
                            OnCollectionError?.Invoke(accountId, $"刷粉JS执行失败: {followResult.Message}");
                        }
                        break;

                    default:
                        System.Diagnostics.Debug.WriteLine($"⚠️ 未知运营任务类型: taskType={taskType}");
                        OnCollectionError?.Invoke(accountId, $"不支持的任务类型: {taskType}");
                        break;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ 运营任务执行异常: {ex.Message}\n{ex.StackTrace}");
                OnCollectionError?.Invoke(accountId, $"运营任务异常: {ex.Message}");
            }
        }

        /// <summary>
        /// 等待页面完全就绪（不仅是 IsLoading=false，还需要页面 DOM 稳定）
        /// </summary>
        private async Task WaitForPageReady(ChromiumWebBrowser browser, int timeoutMs = 15000)
        {
            var startTime = DateTime.Now;
            while (true)
            {
                // 检查浏览器是否已释放（需要在UI线程访问）
                bool isDisposed = false;
                bool canExecute = false;
                Application.Current.Dispatcher.Invoke(() =>
                {
                    isDisposed = browser.IsDisposed;
                    canExecute = browser.CanExecuteJavascriptInMainFrame;
                });

                if (isDisposed) return;
                if (canExecute)
                {
                    // 额外等待页面稳定
                    await Task.Delay(1500);
                    break;
                }
                if ((DateTime.Now - startTime).TotalMilliseconds > timeoutMs)
                {
                    System.Diagnostics.Debug.WriteLine($"⚠️ 等待页面就绪超时: {timeoutMs}ms");
                    break;
                }
                await Task.Delay(500);
            }
        }

        /// <summary>
        /// 启动自动化采集
        /// </summary>
        private async Task StartAutoCollect(ChromiumWebBrowser browser, string accountId,
            string searchUrl, int expectedCount, int taskType = 1, string? config = null)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"🚀 开始自动化采集: {searchUrl}");

                // 0. 验证浏览器是否仍然有效
                if (browser.IsDisposed || !browser.CanExecuteJavascriptInMainFrame)
                {
                    System.Diagnostics.Debug.WriteLine($"❌ 账号 {accountId} 浏览器已失效,无法执行采集");
                    OnCollectionError?.Invoke(accountId, "浏览器已失效,请重新创建");
                    return;
                }

                // 1. 导航到搜索页面（使用 Dispatcher 确保在 UI 线程执行）
                Application.Current.Dispatcher.Invoke(() =>
                {
                    browser.Load(searchUrl);
                });

                // 等待页面加载完成（参考项目 A 的方式）
                System.Diagnostics.Debug.WriteLine($"📌 等待搜索页面加载...");
                await Task.Delay(2000); // 先等待2秒

                // 循环检查 IsLoading 状态
                int checkCount = 0;
                while (checkCount < 20) // 最多检查20次，每次2秒，共40秒
                {
                    // ❗ 每次循环都检查浏览器是否被关闭
                    if (browser.IsDisposed)
                    {
                        System.Diagnostics.Debug.WriteLine($"❌ 账号 {accountId} 浏览器已被用户关闭，停止采集");
                        OnCollectionError?.Invoke(accountId, "浏览器已被关闭，请重新启动任务");
                        return;
                    }

                    bool isLoading = true;
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        isLoading = browser.IsLoading;
                    });

                    if (!isLoading)
                    {
                        System.Diagnostics.Debug.WriteLine($"📌 搜索页面加载完成");
                        break;
                    }

                    await Task.Delay(2000); // 继续等待2秒
                    checkCount++;

                    if (checkCount % 3 == 0)
                    {
                        System.Diagnostics.Debug.WriteLine($"⏳ 等待搜索页面加载中... ({checkCount * 2}秒)");
                    }
                }

                if (checkCount >= 20)
                {
                    System.Diagnostics.Debug.WriteLine($"⚠️ 搜索页面加载超时");
                }

                // ❗ 使用 JavaScript 检测 DOM 是否完全就绪（比 IsLoading 更可靠）
                try
                {
                    System.Diagnostics.Debug.WriteLine($"🔍 检查 DOM 就绪状态...");
                    var domReadyResult = await browser.EvaluateScriptAsync(@"
                        (function() {
                            try {
                                return {
                                    readyState: document.readyState,
                                    hasBody: !!document.body,
                                    hasFacebookContent: !!document.querySelector('[role=""main""]') || !!document.querySelector('div[data-pagelet]')
                                };
                            } catch(e) {
                                return { readyState: 'error', error: e.message };
                            }
                        })()
                    ");

                    if (domReadyResult.Success && domReadyResult.Result != null)
                    {
                        dynamic domState = domReadyResult.Result;
                        string readyState = domState?.readyState?.ToString() ?? "";

                        // 修复：不能对 dynamic 使用 ToObject，直接转换
                        object hasFacebookContentObj = domState?.hasFacebookContent;
                        bool hasFacebookContent = false;
                        if (hasFacebookContentObj is bool b)
                        {
                            hasFacebookContent = b;
                        }
                        else if (hasFacebookContentObj != null)
                        {
                            bool.TryParse(hasFacebookContentObj.ToString(), out hasFacebookContent);
                        }

                        System.Diagnostics.Debug.WriteLine($"📊 DOM 状态: readyState={readyState}, hasFacebookContent={hasFacebookContent}");

                        // 如果 DOM 还没完全就绪，等待直到 ready
                        if (readyState != "complete" || !hasFacebookContent)
                        {
                            System.Diagnostics.Debug.WriteLine($"⏳ 等待 DOM 完全就绪...");
                            await Task.Delay(1000);
                        }
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"⚠️ DOM 检测失败: {domReadyResult.Message}");
                        // 降级方案：等待固定时间
                        await Task.Delay(1000);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"⚠️ DOM 检测异常: {ex.Message}，使用降级方案");
                    await Task.Delay(1000);
                }

                // ❗ 再次检查浏览器是否被关闭
                if (browser.IsDisposed)
                {
                    System.Diagnostics.Debug.WriteLine($"❌ 账号 {accountId} 浏览器已被用户关闭，停止采集");
                    OnCollectionError?.Invoke(accountId, "浏览器已被关闭，请重新启动任务");
                    return;
                }

                System.Diagnostics.Debug.WriteLine($"🔍 浏览器状态检查: IsDisposed={browser.IsDisposed}, CanExecuteJavascript={browser.CanExecuteJavascriptInMainFrame}");

                // 2. 检查是否被重定向到登录页（Cookie 失效）
                string currentUrl = "";
                Application.Current.Dispatcher.Invoke(() =>
                {
                    currentUrl = browser.Address ?? "";
                });

                System.Diagnostics.Debug.WriteLine($"🔍 导航后URL检查: {currentUrl}");

                if (string.IsNullOrEmpty(currentUrl))
                {
                    System.Diagnostics.Debug.WriteLine($"❌ 账号 {accountId} 页面加载失败，可能是网络问题");
                    OnCollectionError?.Invoke(accountId, "页面加载失败，请检查网络连接");
                    return;
                }

                // 使用 JavaScript 再次检测是否是登录页（更准确）
                System.Diagnostics.Debug.WriteLine($"🔍 [DEBUG] 即将调用 CheckIfLoginPage");
                var isLoginPageAfterNav = await CheckIfLoginPage(browser);
                System.Diagnostics.Debug.WriteLine($"🔍 [DEBUG] CheckIfLoginPage 已返回,结果: {isLoginPageAfterNav}");

                if (isLoginPageAfterNav)
                {
                    System.Diagnostics.Debug.WriteLine($"❌ 账号 {accountId} 导航后被重定向到登录页: {currentUrl}");
                    OnCollectionError?.Invoke(accountId, "Cookie已失效或账号被封，需要重新登录");
                    return;
                }

                System.Diagnostics.Debug.WriteLine($"🔍 继续执行URL检查...");

                // 如果导航到搜索页后被重定向回主页或登录页，说明 Cookie 失效
                System.Diagnostics.Debug.WriteLine($"🔍 检查URL是否被重定向: {currentUrl}");

                if (currentUrl == "https://www.facebook.com/" ||
                    currentUrl == "https://www.facebook.com" ||
                    currentUrl.Contains("/checkpoint") ||
                    currentUrl.Contains("/login") ||
                    currentUrl.Contains("/disabled_account") ||
                    currentUrl.Contains("/account_disabled"))
                {
                    System.Diagnostics.Debug.WriteLine($"❌ 账号 {accountId} Cookie 失效，被重定向到: {currentUrl}");
                    OnCollectionError?.Invoke(accountId, "Cookie已失效或账号被封，需要重新登录");
                    return;
                }

                System.Diagnostics.Debug.WriteLine($"🔍 URL检查通过，准备生成采集脚本...");

                // 3. 注入采集脚本(根据任务类型)
                System.Diagnostics.Debug.WriteLine($"🔍 调用 GenerateCollectScript, taskType={taskType}");
                var collectScript = GenerateCollectScript(accountId, expectedCount, taskType, config);
                // 对于帖子评论点赞采集，显示实际目标数量
                if (taskType == 11 && !string.IsNullOrEmpty(config))
                {
                    try
                    {
                        var configObj = Newtonsoft.Json.Linq.JObject.Parse(config);
                        bool collectComment = configObj.ContainsKey("collectComment") ? configObj.Value<bool>("collectComment") : true;
                        bool collectLike = configObj.ContainsKey("collectLike") ? configObj.Value<bool>("collectLike") : true;
                        int commentCount = configObj.ContainsKey("commentExpectedCount") ? configObj.Value<int>("commentExpectedCount") : expectedCount;
                        int likeCount = configObj.ContainsKey("likeExpectedCount") ? configObj.Value<int>("likeExpectedCount") : expectedCount;
                        int actualTarget = (collectComment ? commentCount : 0) + (collectLike ? likeCount : 0);
                        System.Diagnostics.Debug.WriteLine($"🚀 开始执行采集脚本, 目标数量: {actualTarget}");
                    }
                    catch
                    {
                        System.Diagnostics.Debug.WriteLine($"🚀 开始执行采集脚本, 目标数量: {expectedCount}");
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"🚀 开始执行采集脚本, 目标数量: {expectedCount}");
                }
                System.Diagnostics.Debug.WriteLine($"🔍 脚本长度: {collectScript.Length} 字符");

                // ❗ 最后一次验证浏览器状态
                System.Diagnostics.Debug.WriteLine($"🔍 检查浏览器状态: IsDisposed={browser.IsDisposed}, CanExecuteJavascript={browser.CanExecuteJavascriptInMainFrame}");
                System.Diagnostics.Debug.WriteLine($"🔍 脚本内容预览（前500字符）: {collectScript.Substring(0, Math.Min(500, collectScript.Length))}");

                if (browser.IsDisposed || !browser.CanExecuteJavascriptInMainFrame)
                {
                    System.Diagnostics.Debug.WriteLine($"❌ 账号 {accountId} 浏览器在执行脚本前已失效或被关闭");
                    OnCollectionError?.Invoke(accountId, "浏览器已被关闭或失效，请重新启动任务");
                    return;
                }

                // ✅ 等待页面完全加载（使用 WaitForPageLoad）
                System.Diagnostics.Debug.WriteLine($"⏳ 调用 WaitForPageLoad 等待页面加载完成...");
                try
                {
                    await WaitForPageLoad(browser, 30000);
                    System.Diagnostics.Debug.WriteLine($"✅ 页面加载完成");
                }
                catch (TimeoutException ex)
                {
                    System.Diagnostics.Debug.WriteLine($"⚠️ 页面加载超时: {ex.Message}");
                    // 继续执行脚本，可能页面已部分加载
                }

                System.Diagnostics.Debug.WriteLine($"🔍 开始执行 EvaluateScriptAsync...");
                var result = await browser.EvaluateScriptAsync(collectScript);
                System.Diagnostics.Debug.WriteLine($"🔍 EvaluateScriptAsync 执行完成: Success={result.Success}");
                
                // 检查脚本执行是否失败
                if (!result.Success)
                {
                    System.Diagnostics.Debug.WriteLine($"❌ 脚本执行失败! ErrorMessage={result.Message}, Result={result.Result}");
                    OnCollectionError?.Invoke(accountId, $"脚本执行失败: {result.Message}");
                    return;
                }

                if (result.Result != null)
                {
                    // 重要: CefSharp返回的Result可能是各种类型,需要正确处理
                    string jsonData;

                    // 如果Result已经是字符串,直接使用
                    if (result.Result is string jsonString)
                    {
                        jsonData = jsonString;
                    }
                    else
                    {
                        // 否则序列化为JSON字符串
                        jsonData = System.Text.Json.JsonSerializer.Serialize(result.Result);
                    }

                    System.Diagnostics.Debug.WriteLine($"✅ 采集完成，数据长度: {jsonData.Length}");
                    System.Diagnostics.Debug.WriteLine($"📊 数据预览: {jsonData.Substring(0, Math.Min(200, jsonData.Length))}");

                    // 验证是否为有效的JSON数组
                    if (!jsonData.TrimStart().StartsWith("["))
                    {
                        System.Diagnostics.Debug.WriteLine($"⚠️ 警告: 返回的数据不是JSON数组格式!");
                        System.Diagnostics.Debug.WriteLine($"❌ 实际内容: {jsonData.Substring(0, Math.Min(500, jsonData.Length))}");
                        OnCollectionError?.Invoke(accountId, "采集返回数据格式错误,请检查浏览器控制台日志");
                        return;
                    }

                    // 解析JSON数组,检查实际采集到的数量
                    try
                    {
                        var parsedData = System.Text.Json.JsonSerializer.Deserialize<System.Collections.Generic.List<object>>(jsonData);
                        System.Diagnostics.Debug.WriteLine($"📈 实际采集到 {parsedData?.Count ?? 0} 条数据");
                    }
                    catch (Exception parseEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"⚠️ JSON解析失败: {parseEx.Message}");
                    }

                    // 4. 触发回调,将数据传回(包含 detailId)
                    int actualTaskType = _accountTaskTypes.ContainsKey(accountId) ? _accountTaskTypes[accountId] : 1;
                    string detailId = _accountDetailIds.ContainsKey(accountId)
                        ? _accountDetailIds[accountId]
                        : (CurrentDetailId ?? "");
                    OnCollectionComplete?.Invoke(detailId, accountId, jsonData, actualTaskType);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"⚠️ 采集脚本执行失败: {result.Message}");
                    System.Diagnostics.Debug.WriteLine($"❌ Result对象类型: {result.Result?.GetType().FullName ?? "null"}");
                    OnCollectionError?.Invoke(accountId, $"采集脚本执行失败: {result.Message}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ 自动化采集异常: {ex.Message}");
                OnCollectionError?.Invoke(accountId, ex.Message);
            }
        }

        /// <summary>
        /// 生成采集脚本（根据任务类型）
        /// </summary>
        private string GenerateCollectScript(string accountId, int expectedCount, int taskType = 1, string? config = null)
        {
            System.Diagnostics.Debug.WriteLine($"🔍 GenerateCollectScript 被调用: taskType={taskType}, expectedCount={expectedCount}");

            // 根据任务类型选择不同的解析器
            if (taskType == 2) // 帖子采集
            {
                System.Diagnostics.Debug.WriteLine("✅ 进入帖子采集分支，调用 GeneratePostCollectScript");
                return GeneratePostCollectScript(expectedCount);
            }
            else if (taskType == 3) // 用户采集
            {
                System.Diagnostics.Debug.WriteLine("✅ 进入用户采集分支");
                return GenerateUserCollectScript(expectedCount);
            }
            else if (taskType == 4) // 群组采集
            {
                System.Diagnostics.Debug.WriteLine("✅ 进入群组采集分支");
                return GenerateGroupCollectScript(expectedCount);
            }
            else if (taskType == 7) // 群组成员采集
            {
                System.Diagnostics.Debug.WriteLine("✅ 进入群组成员采集分支");
                return GenerateGroupMemberCollectScript(expectedCount);
            }
            else if (taskType == 8) // 用户关系采集
            {
                System.Diagnostics.Debug.WriteLine("✅ 进入用户关系采集分支");
                return GenerateUserRelationCollectScript(expectedCount);
            }
            else if (taskType == 9) // 链接加组
            {
                System.Diagnostics.Debug.WriteLine("✅ 进入链接加组分支");
                return GenerateAddGroupCollectScript(accountId, expectedCount, config);
            }
            else if (taskType == 10 || taskType == 15) // 转帖/帖子评论任务
            {
                System.Diagnostics.Debug.WriteLine("✅ 进入转帖/帖子评论任务分支");
                return GenerateRepostScriptFromConfig(config);
            }
            else if (taskType == 16) // 刷粉任务
            {
                System.Diagnostics.Debug.WriteLine("✅ 进入刷粉任务分支");
                return GenerateFollowScriptFromConfig(accountId, config);
            }
            else if (taskType == 11) // 帖子评论点赞采集
            {
                System.Diagnostics.Debug.WriteLine("✅ 进入帖子评论点赞采集分支");
                return GenerateCommentLikeCollectScript(expectedCount, config);
            }
            else if (taskType == 12) // 深度采集
            {
                System.Diagnostics.Debug.WriteLine("✅ 进入深度采集分支");
                return GenerateDeepProfileCollectScript(accountId, config);
            }
            else // 默认主页采集
            {
                System.Diagnostics.Debug.WriteLine($"⚠️ 使用默认主页采集分支 (taskType={taskType})");
                return GeneratePageCollectScript(expectedCount);
            }
        }

        /// <summary>
        /// 生成主页采集脚本（简化版）
        /// </summary>
        private string GeneratePageCollectScript(int expectedCount)
        {
            var (keywords, units) = LoadFollowerKeywordsAndUnits();
            var js = new System.Text.StringBuilder();

            js.AppendLine($"        const targetCount = {expectedCount};");
            js.AppendLine("        const seenUrls = new Set();");
            js.AppendLine(JsScriptHelper.GetRandomDelayFunction());
            js.AppendLine(JsScriptHelper.GetMouseMovementFunction());
            js.AppendLine($"        const FOLLOWER_KEYWORDS = {keywords};");
            js.AppendLine($"        const FOLLOWER_UNITS = {units};");

            // 数据提取函数
            js.AppendLine(@"
        const extractCardData = (card) => {
            try {
                const nameLinkEl = card.querySelector('a[aria-hidden=""true""]');
                if (!nameLinkEl) return null;

                const url = nameLinkEl.href;
                if (!url || seenUrls.has(url)) return null;

                const name = nameLinkEl.textContent.trim();
                if (!name) return null;

                const cleanName = name.replace(/\s*(Akun Terverifikasi|Verified|Compte certifié)/gi, '').trim();
                const isVerifiedInName = /akun terverifikasi|verified|compte certifi/i.test(name);

                const avatarLinkEl = card.querySelector('a[aria-label*=""profil""]') || card.querySelector('a[aria-label*=""photo""]');
                let avatar = '';
                if (avatarLinkEl) {
                    const imgEl = avatarLinkEl.querySelector('image') || avatarLinkEl.querySelector('img');
                    if (imgEl) avatar = imgEl.getAttribute('xlink:href') || imgEl.src || '';
                }

                const allSpans = Array.from(card.querySelectorAll('span[dir=""auto""]'));
                let followers = '', category = '', snippet = '';

                const normalizeFollowers = (numberPart, unit) => {
                    if (!numberPart) return '';
                    const normalizedNumber = numberPart.replace(',', '.');
                    if (!/^\d+(?:\.\d+)?$/.test(normalizedNumber)) return '';
                    let value = Number(normalizedNumber);
                    if (!Number.isFinite(value) || value <= 0) return '';
                    const lowerUnit = (unit || '').toLowerCase();
                    if (['rb', 'rbu', 'ribu', 'k', '千', '천'].includes(lowerUnit)) value *= 1000;
                    else if (['万'].includes(lowerUnit)) value *= 10000;
                    else if (['jt', 'juta', 'm', '百万', '만', '백만'].includes(lowerUnit)) value *= 1000000;
                    else if (['千万'].includes(lowerUnit)) value *= 10000000;
                    else if (['亿', '億', '억'].includes(lowerUnit)) value *= 100000000;
                    else if (['b'].includes(lowerUnit)) value *= 1000000000;
                    else if (['t', '万亿'].includes(lowerUnit)) value *= 1000000000000;
                    const rounded = Math.floor(value);
                    if (rounded > 1000000000) return '';
                    return String(rounded);
                };

                const hasFollowerKeyword = (text) => {
                    const lower = (text || '').toLowerCase();
                    return FOLLOWER_KEYWORDS.some(k => k && lower.includes(String(k).toLowerCase()));
                };

                const escapeRegex = (value) => String(value || '').replace(/[.*+?^${}()|[\]\\]/g, '\\$&');

                for (const span of allSpans) {
                    const text = span.textContent.trim();
                    if (!text) continue;
                    if (!hasFollowerKeyword(text)) continue;

                    const keywordsPattern = FOLLOWER_KEYWORDS.map(escapeRegex).join('|');
                    const unitsPattern = FOLLOWER_UNITS.map(escapeRegex).join('|');
                    const followerRegex = new RegExp('([\\d]+[\\.,]?\\d*)[\\s]*(?:(' + unitsPattern + '))?[\\s]*(?:' + keywordsPattern + ')|(?:' + keywordsPattern + ')[\\s:]*([\\d]+[\\.,]?\\d*)[\\s]*(?:(' + unitsPattern + '))?', 'i');
                    const followerMatch = text.match(followerRegex);

                    if (followerMatch) {
                        let numberPart = followerMatch[1] || followerMatch[3] || '';
                        if (numberPart && /^\d+[\.,]?\d*$/.test(numberPart)) {
                            const fullMatch = followerMatch[0];
                            const unit = followerMatch[2] || followerMatch[4] || '';
                            followers = normalizeFollowers(numberPart, unit);
                            if (!followers) continue;
                            const beforeFollowers = text.substring(0, text.indexOf(followerMatch[0])).trim();
                            if (beforeFollowers) category = beforeFollowers.split('·')[0].trim();
                            break;
                        }
                    }
                }

                if (!category && allSpans.length >= 2) {
                    const infoText = allSpans[1].textContent.trim();
                    const categoryMatch = infoText.match(/^([^·]+)/);
                    if (categoryMatch) category = categoryMatch[1].trim();
                }

                if (allSpans.length >= 3) snippet = allSpans[allSpans.length - 1].textContent.trim().substring(0, 200);

                const isVerified = isVerifiedInName || card.querySelector('[aria-label*=""Verified""]') !== null || card.querySelector('[aria-label*=""verifi""]') !== null;
                const idMatch = url.match(/[?&]id=(\d+)/);
                const id = idMatch ? idMatch[1] : (url.match(/facebook\.com\/([^\?]+)/) || [])[1] || '';

                seenUrls.add(url);
                return { id, name: cleanName, url, avatar, followers, category, snippet, isVerified, collectedAt: new Date().toISOString() };
            } catch (e) {
                console.warn('Extract failed:', e);
                return null;
            }
        };
");

            // 使用通用采集循环
            js.AppendLine(JsScriptHelper.GetCollectionLoopTemplate("extractCardData", "[role=\"article\"]"));

            return JsScriptHelper.CreatePromiseWrapper(js.ToString());
        }

        /// <summary>
        /// 从 JSON 文件加载粉丝数关键词和单位（返回元组）
        /// </summary>
        private (string keywords, string units) LoadFollowerKeywordsAndUnits()
        {
            try
            {
                var jsonPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "follower_keywords.json");

                if (!System.IO.File.Exists(jsonPath))
                {
                    System.Diagnostics.Debug.WriteLine($"⚠️ 关键词配置文件不存在: {jsonPath}，使用默认配置");
                    return (GetDefaultKeywords(), GetDefaultUnits());
                }

                var jsonContent = System.IO.File.ReadAllText(jsonPath, System.Text.Encoding.UTF8);
                var config = Newtonsoft.Json.Linq.JObject.Parse(jsonContent);

                // 提取所有关键词（不包含单位）
                var allKeywords = new System.Collections.Generic.List<string>();

                // 欧洲语言
                var european = config["keywords"]?["european"];
                if (european != null)
                {
                    foreach (var prop in european.Children())
                    {
                        var jArray = prop as Newtonsoft.Json.Linq.JArray;
                        if (jArray != null)
                        {
                            var keywords = jArray.Select(t => t.ToString()).ToList();
                            allKeywords.AddRange(keywords);
                        }
                    }
                }

                // 亚洲语言
                var asian = config["keywords"]?["asian"];
                if (asian != null)
                {
                    foreach (var prop in asian.Children())
                    {
                        var jArray = prop as Newtonsoft.Json.Linq.JArray;
                        if (jArray != null)
                        {
                            var keywords = jArray.Select(t => t.ToString()).ToList();
                            allKeywords.AddRange(keywords);
                        }
                    }
                }

                // 提取所有单位
                var allUnits = new System.Collections.Generic.List<string>();
                var units = config["keywords"]?["units"];
                if (units != null)
                {
                    foreach (var unitType in units.Children())
                    {
                        foreach (var prop in unitType.Children())
                        {
                            var jArray = prop as Newtonsoft.Json.Linq.JArray;
                            if (jArray != null)
                            {
                                var unitKeywords = jArray.Select(t => t.ToString()).ToList();
                                allUnits.AddRange(unitKeywords);
                            }
                        }
                    }
                }

                // 去重并转换为 JavaScript 数组格式
                var uniqueKeywords = allKeywords.Distinct().ToList();
                var uniqueUnits = allUnits.Distinct().ToList();
                var jsKeywords = "[" + string.Join(", ", uniqueKeywords.Select(k => $"'{k}'")) + "]";
                var jsUnits = "[" + string.Join(", ", uniqueUnits.Select(u => $"'{u}'")) + "]";

                System.Diagnostics.Debug.WriteLine($"✅ 加载了 {uniqueKeywords.Count} 个关键词, {uniqueUnits.Count} 个单位");
                return (jsKeywords, jsUnits);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ 加载关键词失败: {ex.Message}，使用默认配置");
                return (GetDefaultKeywords(), GetDefaultUnits());
            }
        }

        /// <summary>
        /// 获取默认单位（备用）
        /// </summary>
        private string GetDefaultUnits()
        {
            return "['rb', 'rbu', 'ribu', 'jt', 'juta', 'k', 'K', 'm', 'M', 'b', 'B', 't', 'T', '千', '万', '百万', '千万', '亿', '万亿']";
        }

        /// <summary>
        /// 从 JSON 文件加载粉丝数关键词
        /// </summary>
        [System.Obsolete("Use LoadFollowerKeywordsAndUnits instead")]
        private string LoadFollowerKeywords()
        {
            try
            {
                var jsonPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "follower_keywords.json");

                if (!System.IO.File.Exists(jsonPath))
                {
                    System.Diagnostics.Debug.WriteLine($"⚠️ 关键词配置文件不存在: {jsonPath}，使用默认配置");
                    return GetDefaultKeywords();
                }

                var jsonContent = System.IO.File.ReadAllText(jsonPath, System.Text.Encoding.UTF8);
                var config = Newtonsoft.Json.Linq.JObject.Parse(jsonContent);

                // 提取所有关键词并合并为一个数组
                var allKeywords = new System.Collections.Generic.List<string>();

                // 欧洲语言
                var european = config["keywords"]?["european"];
                if (european != null)
                {
                    foreach (var prop in european.Children())
                    {
                        var jArray = prop as Newtonsoft.Json.Linq.JArray;
                        if (jArray != null)
                        {
                            var keywords = jArray.Select(t => t.ToString()).ToList();
                            allKeywords.AddRange(keywords);
                        }
                    }
                }

                // 亚洲语言
                var asian = config["keywords"]?["asian"];
                if (asian != null)
                {
                    foreach (var prop in asian.Children())
                    {
                        var jArray = prop as Newtonsoft.Json.Linq.JArray;
                        if (jArray != null)
                        {
                            var keywords = jArray.Select(t => t.ToString()).ToList();
                            allKeywords.AddRange(keywords);
                        }
                    }
                }

                // 单位
                var units = config["keywords"]?["units"];
                if (units != null)
                {
                    foreach (var unitType in units.Children())
                    {
                        foreach (var prop in unitType.Children())
                        {
                            var jArray = prop as Newtonsoft.Json.Linq.JArray;
                            if (jArray != null)
                            {
                                var keywords = jArray.Select(t => t.ToString()).ToList();
                                allKeywords.AddRange(keywords);
                            }
                        }
                    }
                }

                // 去重并转换为 JavaScript 数组格式
                var uniqueKeywords = allKeywords.Distinct().ToList();
                var jsArray = "[" + string.Join(", ", uniqueKeywords.Select(k => $"'{k}'")) + "]";

                System.Diagnostics.Debug.WriteLine($"✅ 加载了 {uniqueKeywords.Count} 个粉丝数关键词");
                return jsArray;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ 加载关键词失败: {ex.Message}，使用默认配置");
                return GetDefaultKeywords();
            }
        }

        /// <summary>
        /// 生成用户采集脚本（简化版）
        /// </summary>
        private string GenerateUserCollectScript(int expectedCount)
        {
            var js = new System.Text.StringBuilder();

            js.AppendLine($"        const targetCount = {expectedCount};");
            js.AppendLine("        const seenUrls = new Set();");
            js.AppendLine(JsScriptHelper.GetRandomDelayFunction());
            js.AppendLine(JsScriptHelper.GetMouseMovementFunction());

            js.AppendLine(@"
        const extractUserCardData = (card) => {
            try {
                const nameLinkEl = card.querySelector('a[aria-hidden=""true""]');
                if (!nameLinkEl) return null;

                const url = nameLinkEl.href;
                if (!url || seenUrls.has(url)) return null;

                const name = nameLinkEl.textContent.trim();
                if (!name) return null;

                const cleanName = name.replace(/\s*(Akun Terverifikasi|Verified|Compte certifié)/gi, '').trim();
                const isVerifiedInName = /akun terverifikasi|verified|compte certifi/i.test(name);

                const avatarLinkEl = card.querySelector('a[aria-label*=""profil""]') || card.querySelector('a[aria-label*=""photo""]');
                let avatar = '';
                if (avatarLinkEl) {
                    const imgEl = avatarLinkEl.querySelector('image') || avatarLinkEl.querySelector('img');
                    if (imgEl) avatar = imgEl.getAttribute('xlink:href') || imgEl.src || '';
                }

                const allSpans = Array.from(card.querySelectorAll('span[dir=""auto""]'));
                let followers = '', location = '', bio = '', category = '';

                for (let i = 0; i < allSpans.length; i++) {
                    const span = allSpans[i];
                    const text = span.textContent.trim();
                    if (!text) continue;

                    const followerPattern = /(\d+[\.,]?\d*)\s*(rb|ribu|jt|juta|k|m|b|t|pengikut|followers|follower|abonnes|seguidores|fans|千|万|百万|千万|亿)/i;
                    const followerMatch = text.match(followerPattern);
                    if (followerMatch && !followers) {
                        followers = followerMatch[0].replace(/&nbsp;/g, ' ').trim();
                        continue;
                    }

                    if ((text.includes('Tinggal di') || text.includes('@')) && !location) {
                        location = text;
                        continue;
                    }

                    if ((text.includes('Kreator digital') || text.includes('di PT.') || text.includes('Founder') || text.includes('Blogger') || text.includes('Tokoh Publik')) && !category) {
                        category = text.split('·')[0].trim();
                        continue;
                    }

                    if (text.length > 20 && !bio && i >= allSpans.length - 2) {
                        bio = text.substring(0, 200);
                    }
                }

                const isVerified = isVerifiedInName || card.querySelector('[aria-label*=""Verified""]') !== null || card.querySelector('[aria-label*=""verifi""]') !== null;
                const idMatch = url.match(/[?&]id=(\d+)/);
                const id = idMatch ? idMatch[1] : (url.match(/facebook\.com\/([^\/?]+)/) || [])[1] || '';

                seenUrls.add(url);
                return { fbUserId: id, userName: cleanName, url, avatar, followers, city: location, bio: bio || category, isVerified, collectedAt: new Date().toISOString() };
            } catch (e) {
                console.warn('Extract user failed:', e);
                return null;
            }
        };
");

            js.AppendLine(JsScriptHelper.GetCollectionLoopTemplate("extractUserCardData", "[role=\"article\"]"));

            return JsScriptHelper.CreatePromiseWrapper(js.ToString());
        }

        /// <summary>
        /// 生成帖子采集脚本（简化版）
        /// </summary>
        private string GeneratePostCollectScript(int expectedCount)
        {
            var js = new System.Text.StringBuilder();

            js.AppendLine($"        const targetCount = {expectedCount};");
            js.AppendLine("        const seenUrls = new Set();");
            js.AppendLine($"        const maxScrolls = {Math.Max(expectedCount * 3, 10)};");
            js.AppendLine("        let consecutiveNoNewItems = 0;");
            js.AppendLine("        const maxConsecutiveNoNew = 5;");
            js.AppendLine("        let lastCardCount = 0;");
            js.AppendLine("        let scrollCount = 0;");
            js.AppendLine(JsScriptHelper.GetRandomDelayFunction());
            js.AppendLine(JsScriptHelper.GetMouseMovementFunction());

            // 提取帖子数据的函数
            js.AppendLine(@"
        const cleanText = (text) => (text || '').replace(/\s+/g, ' ').trim();

        const canonicalPostUrl = (href) => {
            try {
                const u = new URL(href);
                if (!/(\.|^)facebook\.com$/i.test(u.hostname)) return null;
                const path = u.pathname;
                const q = u.searchParams;
                if ((path.includes('/permalink.php') || path.includes('/story.php')) && q.get('story_fbid')) {
                    const idPart = q.get('id') ? '&id=' + encodeURIComponent(q.get('id')) : '';
                    return 'https://www.facebook.com/permalink.php?story_fbid=' + encodeURIComponent(q.get('story_fbid')) + idPart;
                }
                if (path.includes('/groups/') && q.get('multi_permalinks')) {
                    const groupMatch = path.match(/\/groups\/([^/]+)/);
                    if (!groupMatch) return null;
                    return 'https://www.facebook.com/groups/' + groupMatch[1] + '/permalink/' + q.get('multi_permalinks') + '/';
                }
                const groupPermalink = path.match(/\/groups\/([^/]+)\/permalink\/([^/]+)/);
                if (groupPermalink) {
                    return 'https://www.facebook.com/groups/' + groupPermalink[1] + '/permalink/' + groupPermalink[2] + '/';
                }
                if (/\/posts\//i.test(path)) return 'https://www.facebook.com' + path.replace(/\/+$/, '');
                if (path.includes('/photo/') && q.get('fbid')) {
                    const setPart = q.get('set') ? '&set=' + encodeURIComponent(q.get('set')) : '';
                    return 'https://www.facebook.com/photo/?fbid=' + encodeURIComponent(q.get('fbid')) + setPart;
                }
                if (path.includes('/videos/') || path.includes('/watch/')) return 'https://www.facebook.com' + path.replace(/\/+$/, '');
                return null;
            } catch {
                return null;
            }
        };

        const getPostItemId = (url) => {
            if (!url) return '';
            try {
                const u = new URL(url);
                if (u.searchParams.get('story_fbid')) return u.searchParams.get('story_fbid');
                if (u.searchParams.get('fbid')) return u.searchParams.get('fbid');
                const permalink = u.pathname.match(/\/permalink\/([^/]+)/);
                if (permalink) return permalink[1];
                const post = u.pathname.match(/\/posts\/([^/]+)/);
                if (post) return post[1];
                const video = u.pathname.match(/\/videos\/([^/]+)/);
                if (video) return video[1];
            } catch {}
            return '';
        };

        const isTimeLikeText = (text) => {
            const t = cleanText(text);
            if (!t || t.length > 40) return false;
            return /^(\d+\s*(m|h|d|w|mo|y|min|hr|hrs|分钟|小时|天|周|月|年)|Yesterday|Today|Just now|刚刚|昨天|[A-Za-z]{3,9}\s+\d{1,2}|\d{4})/i.test(t) ||
                /\bat\s+\d{1,2}:\d{2}/i.test(t);
        };

        const findPostTimeLink = (card) => {
            const links = Array.from(card.querySelectorAll('a[href]'));
            const candidates = links.filter(link => {
                const text = cleanText(link.textContent || link.getAttribute('aria-label'));
                return isTimeLikeText(text) && canonicalPostUrl(link.href);
            });
            return candidates.find(link => {
                try {
                    const u = new URL(link.href);
                    return !u.searchParams.has('comment_id') && !u.searchParams.has('reply_comment_id');
                } catch {
                    return false;
                }
            }) || null;
        };

        const getHeaderLinks = (card) => Array.from(card.querySelectorAll('a[href]'))
            .filter(link => cleanText(link.textContent || link.getAttribute('aria-label')))
            .slice(0, 12);

        const getAuthorName = (card, timeLink) => {
            const groupUserLink = Array.from(card.querySelectorAll('a[href*=""/groups/""][href*=""/user/""]'))
                .find(link => cleanText(link.textContent || link.getAttribute('aria-label')));
            if (groupUserLink) return cleanText(groupUserLink.textContent || groupUserLink.getAttribute('aria-label'));
            const profileName = card.querySelector('[data-ad-rendering-role=""profile_name""] a:not([href*=""/groups/""])');
            if (profileName && cleanText(profileName.textContent)) return cleanText(profileName.textContent);
            const timeText = cleanText(timeLink?.textContent || timeLink?.getAttribute('aria-label'));
            for (const link of getHeaderLinks(card)) {
                const href = link.href || '';
                const text = cleanText(link.textContent || link.getAttribute('aria-label'));
                if (!text || text === timeText) continue;
                if (href.includes('/groups/') || href.includes('/hashtag/') || href.includes('/photo/')) continue;
                if (/^profile photo of /i.test(text)) continue;
                return text;
            }
            const bodyText = cleanText(card.innerText || card.textContent);
            const timeIndex = bodyText.indexOf(timeText);
            if (timeText && timeIndex > 0 && timeIndex < 80) {
                return bodyText.slice(0, timeIndex).replace(/\s*·?\s*$/, '').trim();
            }
            return '';
        };

        const getGroupName = (card) => {
            if (location.pathname.includes('/groups/')) {
                const title = cleanText(document.querySelector('h1')?.textContent || document.title);
                const groupTitle = title.replace(/\s*\|\s*Facebook\s*$/i, '');
                if (groupTitle && !/^Facebook$/i.test(groupTitle)) return groupTitle;
            }
            for (const link of getHeaderLinks(card)) {
                const href = link.href || '';
                const text = cleanText(link.textContent || link.getAttribute('aria-label'));
                if (
                    href.includes('/groups/') &&
                    !href.includes('/user/') &&
                    !href.includes('/posts/') &&
                    !href.includes('/permalink/') &&
                    text &&
                    !isTimeLikeText(text) &&
                    !/^profile photo of /i.test(text)
                ) return text;
            }
            return '';
        };

        const getPostContent = (card) => {
            const contentEl = card.querySelector('[data-ad-comet-preview=""message""]') || card.querySelector('[data-testid=""post_message""]');
            if (contentEl && cleanText(contentEl.textContent)) return cleanText(contentEl.textContent);
            return cleanText(card.innerText || card.textContent)
                .replace(/^Groups\b.*?See all/i, '')
                .replace(/^Pages\b.*?See all/i, '')
                .slice(0, 1000);
        };

        const extractPostData = (card) => {
            try {
                const hasContent = card.querySelector('[data-ad-comet-preview=""message""]') ||
                                  card.querySelector('[data-testid=""post_message""]') ||
                                  card.querySelector('span[dir=""auto""]');
                if (!hasContent) return null;

                const svgCount = card.querySelectorAll('svg').length;
                const imgCount = card.querySelectorAll('img').length;
                const linkCount = card.querySelectorAll('a').length;
                if (svgCount > 0 && imgCount > 0 && linkCount < 3) return null;

                const postLinkEl = findPostTimeLink(card);

                if (!postLinkEl) return null;

                const url = canonicalPostUrl(postLinkEl.href);
                if (!url || seenUrls.has(url)) return null;

                const postUser = getAuthorName(card, postLinkEl);
                const groupName = getGroupName(card);
                const isGroupPost = !!groupName || url.includes('/groups/') || location.pathname.includes('/groups/');
                const postContent = getPostContent(card);

                let reactionCount = '', commentCount = '', reshareCount = '';
                const numberSpans = Array.from(card.querySelectorAll('span[dir=""auto""]'));
                for (const span of numberSpans) {
                    const text = span.textContent.trim();
                    if (!text || !/^[\d]/.test(text)) continue;

                    const numMatch = text.match(/^([\d]+[\.,]?\d*\s*[kKmMrbjtRBJT]*)/);
                    if (!numMatch) continue;

                    const rawValue = numMatch[1].trim();
                    const parentText = span.parentElement?.textContent || '';
                    if (parentText.includes('komentar') || parentText.includes('comment')) {
                        commentCount = rawValue;
                    } else if (parentText.includes('bagikan') || parentText.includes('share')) {
                        reshareCount = rawValue;
                    } else if (parentText.includes('suka') || parentText.includes('like') || parentText.includes('reaksi')) {
                        reactionCount = rawValue;
                    }
                }

                const itemId = getPostItemId(url);

                seenUrls.add(url);
                return {
                    itemId, postUser, url, fromResource: isGroupPost ? 'group' : 'page',
                    groupName, reshareCount, commentCount, reactionCount,
                    usedCount: 0, postContent, fbAccount: '', postCreateTime: new Date().toISOString()
                };
            } catch (e) {
                console.warn('Extract post failed:', e);
                return null;
            }
        };
");

            // 主循环 - 滚动加载
            js.AppendLine(@"
        let isCompleted = false;
        const doScroll = async () => {
            if (isCompleted) return;
            try {
                const cards = document.querySelectorAll('[role=""article""]');
                if (cards.length === lastCardCount && cards.length > 0) {
                    consecutiveNoNewItems++;
                } else {
                    lastCardCount = cards.length;
                    consecutiveNoNewItems = 0;
                }

                let newItemsFound = 0;
                for (let i = 0; i < cards.length && results.length < targetCount; i++) {
                    const data = extractPostData(cards[i]);
                    if (data) {
                        results.push(data);
                        newItemsFound++;
                    }
                }

                if (results.length >= targetCount) {
                    isCompleted = true;
                    resolve(JSON.stringify(results.slice(0, targetCount)));
                    return;
                }

                if (consecutiveNoNewItems >= maxConsecutiveNoNew || scrollCount >= maxScrolls) {
                    isCompleted = true;
                    resolve(JSON.stringify(results));
                    return;
                }

                const viewportHeight = window.innerHeight || document.documentElement.clientHeight;
                const minScroll = Math.max(600, viewportHeight * 0.8);
                const maxScroll = Math.max(1000, viewportHeight * 1.2);
                const scrollDistance = randomDelay(Math.floor(minScroll), Math.floor(maxScroll));

                const scrollSteps = randomDelay(3, 7);
                const stepSize = scrollDistance / scrollSteps;
                for (let i = 0; i < scrollSteps; i++) {
                    window.scrollBy({ top: stepSize + randomDelay(-10, 10), behavior: 'auto' });
                    await new Promise(resolve => setTimeout(resolve, randomDelay(50, 150)));
                }

                const readPause = randomDelay(1000, 3000);
                await new Promise(resolve => setTimeout(resolve, readPause));

                scrollCount++;
                setTimeout(() => doScroll(), randomDelay(2000, 3500));
            } catch (e) {
                console.error('[采集错误]', e);
                setTimeout(() => doScroll(), 3000);
            }
        };

        doScroll();
");

            // 超时保护（5分钟）
            js.AppendLine("        setTimeout(() => {");
            js.AppendLine("            if (results.length > 0) resolve(JSON.stringify(results));");
            js.AppendLine("            else reject(new Error('Collection timeout with no data'));");
            js.AppendLine("        }, 300000);");

            return JsScriptHelper.CreatePromiseWrapper(js.ToString());
        }

        /// <summary>
        /// 生成群组采集脚本（简化版）
        /// </summary>
        private string GenerateGroupCollectScript(int expectedCount)
        {
            var js = new System.Text.StringBuilder();

            js.AppendLine($"        const targetCount = {expectedCount};");
            js.AppendLine("        const seenUrls = new Set();");
            js.AppendLine(JsScriptHelper.GetRandomDelayFunction());
            js.AppendLine(JsScriptHelper.GetMouseMovementFunction());

            js.AppendLine(@"
        const extractGroupData = (card) => {
            try {
                const groupLinkEl = card.querySelector('a[href*=""/groups/""]');
                if (!groupLinkEl) return null;

                const url = groupLinkEl.href.split('?')[0];
                if (!url || seenUrls.has(url)) return null;

                // ✅ 从 aria-label 属性获取群组名称（Facebook 群组页面的名称在这个属性中）
                const groupName = groupLinkEl.getAttribute('aria-label') || groupLinkEl.textContent.trim();
                if (!groupName) return null;

                let type = 'Public';
                const typeEl = card.querySelector('[aria-label*=""Public""], [aria-label*=""Private""], [aria-label*=""Closed""]');
                if (typeEl) {
                    const ariaLabel = typeEl.getAttribute('aria-label') || '';
                    if (ariaLabel.includes('Private') || ariaLabel.includes('Closed')) type = 'Private';
                }

                let memberQuantity = '', activeQuantity = '';
                const allSpans = Array.from(card.querySelectorAll('span[dir=""auto""]'));
                for (const span of allSpans) {
                    const text = span.textContent.trim();
                    if (!text) continue;

                    const memberMatch = text.match(/([\d]+[\.,]?\d*)\s*(K|M|B|members?)/i);
                    if (memberMatch && !memberQuantity) {
                        memberQuantity = text;
                        continue;
                    }

                    const activeMatch = text.match(/[\d]+\s*(posts?).*?(day|week|month)/i);
                    if (activeMatch && !activeQuantity) activeQuantity = text;
                }

                seenUrls.add(url);
                return { groupName, url, type, memberQuantity, activeQuantity, collectedAt: new Date().toISOString() };
            } catch (e) {
                console.warn('Extract group failed:', e);
                return null;
            }
        };
");

            js.AppendLine(JsScriptHelper.GetCollectionLoopTemplate("extractGroupData", "[role=\"article\"]"));

            return JsScriptHelper.CreatePromiseWrapper(js.ToString());
        }

        /// <summary>
        /// 生成群组成员采集脚本（简化版）
        /// </summary>
        private string GenerateGroupMemberCollectScript(int expectedCount)
        {
            var js = new System.Text.StringBuilder();

            js.AppendLine($"        const targetCount = {expectedCount};");
            js.AppendLine("        const seenUserIds = new Set();");
            js.AppendLine(JsScriptHelper.GetRandomDelayFunction());
            js.AppendLine(JsScriptHelper.GetMouseMovementFunction());

            js.AppendLine(@"
        const extractMemberData = (listItem) => {
            try {
                // ✅ 匹配群组成员页面的链接格式: /groups/{groupId}/user/{userId}/
                const userLinkEl = listItem.querySelector('a[href*=""/groups/""]');
                if (!userLinkEl) {
                    console.log('❌ No link found');
                    return null;
                }
                
                const url = userLinkEl.href.split('?')[0];
                // 验证是否包含 /user/ 路径
                if (!url.includes('/user/')) {
                    console.log('❌ URL without /user/:', url);
                    return null;
                }
                if (!url || seenUserIds.has(url)) return null;
                
                // ✅ 从 aria-label 属性获取用户名（Facebook 群成员页面的用户名在这个属性中）
                const userName = userLinkEl.getAttribute('aria-label') || userLinkEl.textContent.trim();
                if (!userName) {
                    console.log('❌ Empty username');
                    return null;
                }
                
                // ✅ 从完整URL中提取用户ID
                const userIdMatch = url.match(/\/user\/(\d+)/);
                const fbUserId = userIdMatch ? userIdMatch[1] : '';
                console.log('✅ Member found:', userName, fbUserId);

                // ✅ 提取角色信息(Admin/Moderator等)
                let role = 'Member';
                const badgeEl = listItem.querySelector('[aria-label*=""Admin""]') ||
                               listItem.querySelector('[aria-label*=""Moderator""]');
                if (badgeEl) {
                    const ariaLabel = badgeEl.getAttribute('aria-label') || '';
                    if (ariaLabel.includes('Admin')) role = 'Admin';
                    else if (ariaLabel.includes('Moderator')) role = 'Moderator';
                }

                // ✅ 提取加入时间等信息
                const allTexts = Array.from(listItem.querySelectorAll('span, div'))
                    .map(el => el.textContent.trim())
                    .filter(t => t.length > 0);

                let joinTime = '', workInfo = '', location = '';
                for (const text of allTexts) {
                    if (text.includes('Created group on') && !joinTime) {
                        joinTime = text;
                    } else if (text.includes('加入') && !joinTime) {
                        joinTime = text;
                    } else if ((text.includes('在') && text.includes('工作')) || text.includes('studied at')) {
                        workInfo = text;
                    }
                }

                // ✅ 提取头像
                const imgEl = listItem.querySelector('img') || listItem.querySelector('svg image');
                const avatar = imgEl ? (imgEl.getAttribute('xlink:href') || imgEl.src || '') : '';

                seenUserIds.add(url);
                return {
                    fbUserId,
                    userName,
                    url,
                    avatar,
                    role,  // ✅ 添加角色字段
                    joinTime,
                    workExperience: workInfo,
                    location,
                    dataType: 1,
                    fromResource: 'group_member',
                    syncTime: new Date().toISOString()
                };
            } catch (e) {
                console.warn('Extract member failed:', e);
                return null;
            }
        };
");

            js.AppendLine(JsScriptHelper.GetCollectionLoopTemplate("extractMemberData", "div.x78zum5.xdt5ytf.x1xmf6yo.x1e56ztr"));

            return JsScriptHelper.CreatePromiseWrapper(js.ToString());
        }

        /// <summary>
        /// 生成用户关系采集脚本（简化版）
        /// </summary>
        private string GenerateUserRelationCollectScript(int expectedCount)
        {
            var js = new System.Text.StringBuilder();

            js.AppendLine($"        const targetCount = {expectedCount};");
            js.AppendLine("        const seenUserIds = new Set();");
            js.AppendLine(JsScriptHelper.GetRandomDelayFunction());
            js.AppendLine(JsScriptHelper.GetMouseMovementFunction());

            js.AppendLine(@"
        const extractUserData = (container) => {
            try {
                // ✅ 直接从卡片结构获取用户名区域
                var nameArea = container.querySelector('div.x1iyjqo2');
                if (!nameArea) {
                    console.log('No name area');
                    return null;
                }
                var nameLink = nameArea.querySelector('a');
                if (!nameLink) {
                    console.log('No name link');
                    return null;
                }
                
                // ✅ 获取URL
                var url = nameLink.href;
                if (!url || !url.includes('facebook.com')) {
                    console.log('Invalid URL');
                    return null;
                }
                
                // 跳过当前用户导航链接和已见过的链接
                if (url.includes('&sk=') || seenUserIds.has(url)) {
                    console.log('Skip link');
                    return null;
                }

                // ✅ 获取用户名
                var userName = nameLink.textContent.trim();
                if (!userName || userName.length < 2 || userName.length > 150) {
                    console.log('Invalid username');
                    return null;
                }
                
                // ✅ 获取用户ID（支持两种格式）
                var fbUserId = '';
                var idMatch = url.match(/[?&]id=(\\d+)/);
                if (idMatch) {
                    // profile.php?id=123格式，只取数字部分
                    fbUserId = idMatch[1];
                } else {
                    // 用户名格式：https://www.facebook.com/username
                    var cleanUrl = url.replace('https://www.facebook.com/', '');
                    var urlParts = cleanUrl.split('?')[0].split('/');
                    fbUserId = urlParts[0];
                }
                
                if (!fbUserId) {
                    console.log('No user ID');
                    return null;
                }

                // ✅ 获取头像
                var avatar = '';
                var imgEl = container.querySelector('img');
                if (imgEl) {
                    avatar = imgEl.src || '';
                }

                var fromResource = 'peer_follower';
                if (window.location.href.includes('&sk=following')) fromResource = 'peer_following';
                else if (window.location.href.includes('&sk=friends')) fromResource = 'peer_friend';

                console.log('Found:', userName, fbUserId);
                seenUserIds.add(url);
                return { fbUserId: fbUserId, userName: userName, url: url, avatar: avatar, dataType: 1, fromResource: fromResource, syncTime: new Date().toISOString() };
            } catch (e) {
                console.warn('Extract user relation failed:', e);
                return null;
            }
        };
");

            js.AppendLine(JsScriptHelper.GetCollectionLoopTemplate("extractUserData", "div.x6s0dn4.x1obq294.x5a5i1n.xde0f50.x15x8krk.x1olyfxc.x9f619.x78zum5.x1e56ztr.xyamay9.xv54qhq"));

            return JsScriptHelper.CreatePromiseWrapper(js.ToString());
        }

        private class AddGroupConfigItem
        {
            public string GroupId { get; set; } = "";
            public string GroupName { get; set; } = "";
            public string GroupUrl { get; set; } = "";
        }

        /// <summary>
        /// 链接加组：按群组逐个导航并执行脚本，汇总结果
        /// </summary>
        private async Task<string?> ExecuteAddGroupTaskAsync(ChromiumWebBrowser browser, string accountId, string? config)
        {
            var groups = ParseAddGroupConfig(config);
            if (groups.Count == 0)
            {
                OnCollectionError?.Invoke(accountId, "加组配置为空，请检查群组选择");
                return null;
            }

            var allResults = new List<object>();

            for (int i = 0; i < groups.Count; i++)
            {
                var group = groups[i];
                if (string.IsNullOrWhiteSpace(group.GroupUrl))
                {
                    continue;
                }

                System.Diagnostics.Debug.WriteLine($"🔄 加组进度 {i + 1}/{groups.Count}: {group.GroupName}");

                if (browser.IsDisposed || !browser.CanExecuteJavascriptInMainFrame)
                {
                    OnCollectionError?.Invoke(accountId, "浏览器已失效，加组任务中断");
                    return null;
                }

                Application.Current.Dispatcher.Invoke(() => browser.Load(group.GroupUrl));
                try
                {
                    await WaitForPageLoad(browser, 30000);
                }
                catch (TimeoutException ex)
                {
                    System.Diagnostics.Debug.WriteLine($"⚠️ 群组页面加载超时: {ex.Message}");
                }
                await Task.Delay(2000);

                var singleGroupConfig = JsonConvert.SerializeObject(new
                {
                    groups = new[]
                    {
                        new { groupId = group.GroupId, groupName = group.GroupName, groupUrl = group.GroupUrl }
                    }
                });

                var script = GenerateAddGroupCollectScript(accountId, 1, singleGroupConfig);
                var result = await browser.EvaluateScriptAsync(script);
                if (!result.Success || result.Result == null)
                {
                    System.Diagnostics.Debug.WriteLine($"❌ 群组 {group.GroupName} 脚本执行失败: {result.Message}");
                    continue;
                }

                string jsonData = result.Result is string jsonString
                    ? jsonString
                    : System.Text.Json.JsonSerializer.Serialize(result.Result);

                try
                {
                    var items = JsonConvert.DeserializeObject<List<object>>(jsonData);
                    if (items != null && items.Count > 0)
                    {
                        allResults.AddRange(items);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"⚠️ 解析群组 {group.GroupName} 结果失败: {ex.Message}");
                }

                if (i < groups.Count - 1)
                {
                    await Task.Delay(3000);
                }
            }

            if (allResults.Count == 0)
            {
                OnCollectionError?.Invoke(accountId, "加组任务完成但未获取到任何结果");
                return null;
            }

            return JsonConvert.SerializeObject(allResults);
        }

        private List<AddGroupConfigItem> ParseAddGroupConfig(string? config)
        {
            var groups = new List<AddGroupConfigItem>();
            if (string.IsNullOrEmpty(config))
            {
                return groups;
            }

            try
            {
                var configObj = JsonConvert.DeserializeObject<Newtonsoft.Json.Linq.JObject>(config);
                var groupArray = configObj?["groups"] as Newtonsoft.Json.Linq.JArray;
                if (groupArray == null)
                {
                    return groups;
                }

                foreach (var item in groupArray)
                {
                    groups.Add(new AddGroupConfigItem
                    {
                        GroupId = item["groupId"]?.ToString() ?? "",
                        GroupName = item["groupName"]?.ToString() ?? "",
                        GroupUrl = item["groupUrl"]?.ToString() ?? ""
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"⚠️ 解析加组配置失败: {ex.Message}");
            }

            return groups;
        }

        /// <summary>
        /// 生成链接加组采集脚本（支持群组列表）
        /// </summary>
        private string GenerateAddGroupCollectScript(string accountId, int expectedCount, string? config = null)
        {
            var js = new System.Text.StringBuilder();

            string groupsJson = "[]";
            if (!string.IsNullOrEmpty(config))
            {
                try
                {
                    var configObj = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(config);
                    if (configObj.TryGetProperty("groups", out var groups))
                    {
                        groupsJson = groups.GetRawText();
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"⚠️ 解析群组配置失败: {ex.Message}");
                }
            }

            js.AppendLine("        const GROUP_LIST = " + groupsJson + ";");
            js.AppendLine("        const ACCOUNT_ID = \"" + accountId + "\";");

            string scriptPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Scripts", "addGroupScript.js");
            if (System.IO.File.Exists(scriptPath))
            {
                string scriptContent = System.IO.File.ReadAllText(scriptPath, System.Text.Encoding.UTF8);
                js.AppendLine(scriptContent);
                System.Diagnostics.Debug.WriteLine($"✅ 已从文件加载加组脚本: {scriptPath}");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"⚠️ 加组脚本文件不存在: {scriptPath}");
                js.AppendLine(@"
        console.log('🚀 开始执行加组任务');
        console.log('📋 群组数: ' + GROUP_LIST.length);
        
        const randomDelay = (min, max) => {
            return new Promise(resolve => setTimeout(resolve, min + Math.floor(Math.random() * (max - min))));
        };

        async function execute() {
            for (var i = 0; i < GROUP_LIST.length; i++) {
                var group = GROUP_LIST[i];
                console.log('🔄 处理第 ' + (i + 1) + '/' + GROUP_LIST.length + ' 个群组: ' + group.groupName);

                window.location.href = group.groupUrl;
                console.log('📍 导航到: ' + group.groupUrl);

                await randomDelay(5000, 7000);
                await randomDelay(2000, 3000);

                var joined = false;
                var allSpans = document.querySelectorAll('span');
                for (var j = 0; j < allSpans.length; j++) {
                    var text = allSpans[j].textContent;
                    if (text && text.trim() === 'Joined') {
                        joined = true;
                        break;
                    }
                }

                if (joined) {
                    console.log('✅ 已加入该群组');
                    results.push({ accountId: ACCOUNT_ID || '', targetUrl: group.groupUrl, groupId: group.groupId, groupName: group.groupName, groupUrl: group.groupUrl, joinStatus: 3, failReason: '', joinTime: new Date().toISOString(), syncTime: new Date().toISOString() });
                    continue;
                }

                var joinButton = document.querySelector('[aria-label*=""Join""]');
                
                if (!joinButton) {
                    var buttons = document.querySelectorAll('button');
                    for (var j = 0; j < buttons.length; j++) {
                        var text = buttons[j].textContent;
                        if (text && text.trim().toLowerCase() === 'join') {
                            joinButton = buttons[j];
                            break;
                        }
                    }
                }

                if (!joinButton) {
                    joinButton = document.querySelector('[aria-label*=""group""]');
                }

                if (!joinButton) {
                    console.log('❌ 未找到加入按钮');
                    results.push({ accountId: ACCOUNT_ID || '', targetUrl: group.groupUrl, groupId: group.groupId, groupName: group.groupName, groupUrl: group.groupUrl, joinStatus: 2, failReason: 'No join button found', joinTime: new Date().toISOString(), syncTime: new Date().toISOString() });
                    continue;
                }

                console.log('✅ 找到加入按钮，准备点击...');
                joinButton.click();
                console.log('✅ 已点击加入按钮');

                await randomDelay(3000, 4000);

                var status = 1;
                results.push({ accountId: ACCOUNT_ID || '', targetUrl: group.groupUrl, groupId: group.groupId, groupName: group.groupName, groupUrl: group.groupUrl, joinStatus: status, failReason: '', joinTime: new Date().toISOString(), syncTime: new Date().toISOString() });

                console.log('✅ 群组 ' + group.groupName + ' 处理完成');

                if (i < GROUP_LIST.length - 1) {
                    await randomDelay(3000, 5000);
                }
            }

            console.log('🎉 加组任务完成');
            resolve(JSON.stringify(results));
        }

        execute().catch(function(err) {
            console.error('❌ 加组任务出错:', err);
            reject(err);
        });
");
            }

            return JsScriptHelper.CreatePromiseWrapper(js.ToString());
        }

        /// <summary>
        /// 获取默认关键词（备用）
        /// </summary>
        private string GetDefaultKeywords()
        {
            return "['followers', 'follower', 'pengikut', 'abonnes', 'seguidores', 'fans', '粉丝', '粉絲', '关注者', '關注者', 'フォロワー', '팔로워']";
        }

        /// <summary>
        /// 获取活跃浏览器数量
        /// </summary>
        public int GetActiveBrowserCount()
        {
            return _browsers.Count;
        }

        /// <summary>
        /// 为指定账号切换Facebook语言设置
        /// </summary>
        /// <param name="accountId">账号ID</param>
        /// <param name="language">语言：1-英文，2-中文</param>
        public async Task SwitchLanguageForAccount(string accountId, int language)
        {
            if (!_browsers.TryGetValue(accountId, out var browser))
            {
                throw new InvalidOperationException($"账号 {accountId} 的浏览器实例不存在");
            }

            System.Diagnostics.Debug.WriteLine($"🔄 开始为账号 {accountId} 切换语言为 {(language == 1 ? "英文" : "中文")}");

            try
            {
                // 1. 导航到Facebook语言设置页面
                string languageUrl = "https://www.facebook.com/settings/?tab=language_and_region";
                Application.Current.Dispatcher.Invoke(() =>
                {
                    browser.Load(languageUrl);
                });

                System.Diagnostics.Debug.WriteLine($"📌 导航到语言设置页面: {languageUrl}");

                // 2. 等待页面加载完成
                await WaitForPageLoad(browser, 15000); // 最多等待15秒

                // 3. 注入JavaScript脚本执行语言切换
                var switchScript = GenerateLanguageSwitchScript(language);
                System.Diagnostics.Debug.WriteLine($"🚀 执行语言切换脚本");

                var result = await browser.EvaluateScriptAsync(switchScript);

                if (result.Success && result.Result != null)
                {
                    var response = JsonConvert.DeserializeObject<dynamic>(result.Result.ToString());
                    bool success = response?.success ?? false;
                    string message = response?.message ?? "";

                    if (success)
                    {
                        System.Diagnostics.Debug.WriteLine($"✅ 账号 {accountId} 语言切换成功: {message}");
                    }
                    else
                    {
                        throw new Exception($"语言切换失败: {message}");
                    }
                }
                else
                {
                    throw new Exception("JavaScript执行失败");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ 账号 {accountId} 语言切换异常: {ex.Message}");
                throw;
            }
        }

        private static readonly HashSet<string> FacebookUrlNoiseSegments = new(StringComparer.OrdinalIgnoreCase)
        {
            "facebook", "com", "share", "posts", "groups", "photo", "videos", "watch", "permalink", "permalink.php"
        };

        private static bool IsFacebookUrlNoiseSegment(string segment)
        {
            if (string.IsNullOrWhiteSpace(segment)) return true;
            var s = segment.Trim();
            if (s.Length < 6) return true;
            if (FacebookUrlNoiseSegments.Contains(s)) return true;
            return s.StartsWith("pfbid", StringComparison.OrdinalIgnoreCase) && s.Length <= 12;
        }

        private static string? GetQueryParamValue(Uri uri, string paramName)
        {
            if (uri == null || string.IsNullOrEmpty(paramName)) return null;
            var query = uri.Query.TrimStart('?');
            if (string.IsNullOrEmpty(query)) return null;
            foreach (var pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var idx = pair.IndexOf('=');
                if (idx <= 0) continue;
                var key = Uri.UnescapeDataString(pair[..idx]);
                if (!string.Equals(key, paramName, StringComparison.OrdinalIgnoreCase)) continue;
                return Uri.UnescapeDataString(pair[(idx + 1)..]);
            }
            return null;
        }

        /// <summary>
        /// 判断当前 URL 是否已在目标任务页（私信页允许 e2ee 子路径）
        /// </summary>
        private static bool IsOnTargetUrl(string currentUrl, string targetUrl)
        {
            if (string.IsNullOrEmpty(currentUrl) || string.IsNullOrEmpty(targetUrl)) return false;

            try
            {
                var current = new Uri(currentUrl);
                var target = new Uri(targetUrl);

                if (target.AbsolutePath.Contains("/messages/", StringComparison.OrdinalIgnoreCase))
                {
                    if (!current.AbsolutePath.Contains("/messages/", StringComparison.OrdinalIgnoreCase))
                        return false;

                    // 私信线程 URL 可能在点击 Continue 后变为 /messages/e2ee/t/{id}
                    var segments = target.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
                    for (int i = 0; i < segments.Length; i++)
                    {
                        if (segments[i] == "t" && i + 1 < segments.Length)
                        {
                            var userId = segments[i + 1];
                            if (currentUrl.Contains(userId, StringComparison.Ordinal))
                                return true;
                        }
                    }
                    return current.AbsolutePath.Contains("/messages/", StringComparison.OrdinalIgnoreCase);
                }

                if (string.Equals(
                    current.AbsolutePath.TrimEnd('/'),
                    target.AbsolutePath.TrimEnd('/'),
                    StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                // 转帖：share/p/xxx 常会跳转到 permalink.php?story_fbid=pfbid...&share_url=原链接
                if (target.Host.Contains("facebook.com", StringComparison.OrdinalIgnoreCase))
                {
                    var currentLower = currentUrl.ToLowerInvariant();
                    var targetLower = targetUrl.ToLowerInvariant();

                    var targetKeys = target.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries)
                        .Where(s => s.Length >= 6 && !IsFacebookUrlNoiseSegment(s))
                        .Select(s => s.ToLowerInvariant())
                        .ToList();
                    if (targetKeys.Any(k => currentLower.Contains(k)))
                    {
                        return true;
                    }

                    var shareUrlParam = GetQueryParamValue(current, "share_url");
                    if (!string.IsNullOrEmpty(shareUrlParam))
                    {
                        var decodedShareUrl = Uri.UnescapeDataString(shareUrlParam).ToLowerInvariant();
                        var targetPath = target.AbsolutePath.TrimEnd('/').ToLowerInvariant();
                        if (decodedShareUrl.Contains(targetPath)
                            || string.Equals(decodedShareUrl.TrimEnd('/'), targetLower.TrimEnd('/'), StringComparison.Ordinal))
                        {
                            return true;
                        }
                        if (targetKeys.Any(k => decodedShareUrl.Contains(k)))
                        {
                            return true;
                        }
                    }

                    if (current.AbsolutePath.Contains("permalink.php", StringComparison.OrdinalIgnoreCase)
                        && current.Query.Contains("story_fbid=", StringComparison.OrdinalIgnoreCase)
                        && (target.AbsolutePath.Contains("/share/p/", StringComparison.OrdinalIgnoreCase)
                            || target.AbsolutePath.Contains("/posts/", StringComparison.OrdinalIgnoreCase)
                            || targetKeys.Count > 0))
                    {
                        return true;
                    }
                }

                return false;
            }
            catch
            {
                return currentUrl.StartsWith(targetUrl, StringComparison.OrdinalIgnoreCase);
            }
        }

        /// <summary>
        /// 导航浏览器到指定 URL 并等待加载完成
        /// </summary>
        private async Task NavigateBrowserToUrlAsync(ChromiumWebBrowser browser, string accountId, string targetUrl, int timeoutMs = 40000)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                browser.Load(targetUrl);
            });

            System.Diagnostics.Debug.WriteLine($"📌 等待页面加载: {targetUrl}");
            await Task.Delay(2000);

            var startTime = DateTime.Now;
            while ((DateTime.Now - startTime).TotalMilliseconds < timeoutMs)
            {
                bool isDisposed = false;
                bool isLoading = true;
                Application.Current.Dispatcher.Invoke(() =>
                {
                    isDisposed = browser.IsDisposed;
                    isLoading = browser.IsLoading;
                });

                if (isDisposed)
                {
                    throw new InvalidOperationException($"账号 {accountId} 浏览器已被关闭");
                }

                if (!isLoading)
                {
                    await Task.Delay(1000);
                    return;
                }

                await Task.Delay(500);
            }

            throw new TimeoutException($"导航超时 ({timeoutMs}ms): {targetUrl}");
        }

        /// <summary>
        /// CEF 事件可能在非 UI 线程触发，通过此方法安全访问 ChromiumWebBrowser
        /// </summary>
        private static void RunOnBrowserUiThread(ChromiumWebBrowser browser, Action action)
        {
            if (browser.Dispatcher.CheckAccess())
            {
                action();
            }
            else
            {
                browser.Dispatcher.Invoke(action);
            }
        }

        private static Task RunOnBrowserUiThreadAsync(ChromiumWebBrowser browser, Func<Task> action)
        {
            if (browser.Dispatcher.CheckAccess())
            {
                return action();
            }

            return browser.Dispatcher.InvokeAsync(action).Task.Unwrap();
        }

        /// <summary>
        /// 等待页面加载完成
        /// </summary>
        private async Task WaitForPageLoad(ChromiumWebBrowser browser, int timeoutMs = 15000)
        {
            var startTime = DateTime.Now;
            int checkInterval = 500; // 每500ms检查一次

            while ((DateTime.Now - startTime).TotalMilliseconds < timeoutMs)
            {
                bool isLoading = true;
                Application.Current.Dispatcher.Invoke(() =>
                {
                    isLoading = browser.IsLoading;
                });

                if (!isLoading)
                {
                    System.Diagnostics.Debug.WriteLine($"📌 页面加载完成");
                    await Task.Delay(1000); // 额外等待1秒确保DOM完全渲染
                    return;
                }

                await Task.Delay(checkInterval);
            }

            throw new TimeoutException($"页面加载超时 ({timeoutMs}ms)");
        }

        /// <summary>
        /// 生成语言切换JavaScript脚本
        /// </summary>
        private string GenerateLanguageSwitchScript(int language)
        {
            var js = new System.Text.StringBuilder();

            js.AppendLine("(async function() {");
            js.AppendLine("    try {");
            js.AppendLine("        console.log('[语言切换] 开始执行');");
            js.AppendLine("");
            js.AppendLine("        // 1. 查找并点击编辑按钮");
            js.AppendLine("        const editButton = document.querySelector('div[role=main] div[role=button]');");
            js.AppendLine("        if (!editButton) {");
            js.AppendLine("            throw new Error('未找到编辑按钮');");
            js.AppendLine("        }");
            js.AppendLine("        editButton.click();");
            js.AppendLine("        console.log('[语言切换] 已点击编辑按钮');");
            js.AppendLine("");
            js.AppendLine("        // 2. 等待对话框出现");
            js.AppendLine("        await new Promise(resolve => setTimeout(resolve, 2000));");
            js.AppendLine("");
            js.AppendLine("        // 3. 查找对应的语言选项");
            js.AppendLine($"        const targetLang = '{(language == 1 ? "English" : "中文")}';");
            js.AppendLine($"        const subLang = '{(language == 1 ? "US" : "简体")}';");
            js.AppendLine("");
            js.AppendLine("        const radios = Array.from(document.querySelectorAll('div[role=dialog] div[data-visualcompletion]>div[role=radio] span[id]'));");
            js.AppendLine("        const targetRadio = radios.find(span => ");
            js.AppendLine("            span.innerText.includes(targetLang) && span.innerText.includes(subLang)");
            js.AppendLine("        );");
            js.AppendLine("");
            js.AppendLine("        if (!targetRadio) {");
            js.AppendLine("            throw new Error(`未找到语言选项: ${targetLang} (${subLang})`);");
            js.AppendLine("        }");
            js.AppendLine("");
            js.AppendLine("        // 4. 点击语言选项");
            js.AppendLine("        targetRadio.click();");
            js.AppendLine("        console.log('[语言切换] 已选择语言:', targetLang);");
            js.AppendLine("");
            js.AppendLine("        // 5. 等待一下让UI更新");
            js.AppendLine("        await new Promise(resolve => setTimeout(resolve, 1000));");
            js.AppendLine("");
            js.AppendLine("        // 6. 查找并点击保存按钮");
            js.AppendLine("        const saveButton = Array.from(document.querySelectorAll('div[role=dialog] button[type=submit], div[role=dialog] div[role=button]'))");
            js.AppendLine("            .find(btn => btn.innerText.includes('Save') || btn.innerText.includes('保存') || btn.innerText.includes('Simpan'));");
            js.AppendLine("");
            js.AppendLine("        if (saveButton) {");
            js.AppendLine("            saveButton.click();");
            js.AppendLine("            console.log('[语言切换] 已点击保存按钮');");
            js.AppendLine("        } else {");
            js.AppendLine("            console.warn('[语言切换] 未找到保存按钮，可能已自动保存');");
            js.AppendLine("        }");
            js.AppendLine("");
            js.AppendLine("        // 7. 等待操作完成");
            js.AppendLine("        await new Promise(resolve => setTimeout(resolve, 2000));");
            js.AppendLine("");
            js.AppendLine("        console.log('[语言切换] 完成');");
            js.AppendLine("");
            js.AppendLine("        return JSON.stringify({");
            js.AppendLine("            success: true,");
            js.AppendLine("            message: '语言切换成功'");
            js.AppendLine("        });");
            js.AppendLine("    } catch (e) {");
            js.AppendLine("        console.error('[语言切换] 错误:', e);");
            js.AppendLine("        return JSON.stringify({");
            js.AppendLine("            success: false,");
            js.AppendLine("            message: e.message");
            js.AppendLine("        });");
            js.AppendLine("    }");
            js.AppendLine("})();");

            return js.ToString();
        }

        /// <summary>
        /// 获取指定账号的浏览器实例
        /// </summary>
        protected ChromiumWebBrowser? GetBrowser(string accountId)
        {
            if (_browsers.TryGetValue(accountId, out var browser))
            {
                return browser;
            }
            return null;
        }

        /// <summary>
        /// 检查是否已存在指定账号的浏览器
        /// </summary>
        public bool HasBrowser(string accountId)
        {
            return _browsers.ContainsKey(accountId);
        }

        /// <summary>
        /// 添加人类行为模拟辅助函数
        /// </summary>
        protected void AddHumanBehaviorHelpers(System.Text.StringBuilder js)
        {
            // 随机延迟（使用正态分布）
            js.AppendLine("        // ===== 人类行为模拟辅助函数 =====");
            js.AppendLine("        const randomDelay = (min, max) => {");
            js.AppendLine("            const u1 = Math.random();");
            js.AppendLine("            const u2 = Math.random();");
            js.AppendLine("            const z = Math.sqrt(-2.0 * Math.log(u1)) * Math.cos(2.0 * Math.PI * u2);");
            js.AppendLine("            const mean = (min + max) / 2;");
            js.AppendLine("            const stdDev = (max - min) / 6;");
            js.AppendLine("            const delay = Math.max(min, Math.min(max, mean + z * stdDev));");
            js.AppendLine("            return new Promise(resolve => setTimeout(resolve, Math.floor(delay)));");
            js.AppendLine("        };");
            js.AppendLine("");

            // 贝塞尔曲线鼠标轨迹
            js.AppendLine("        const simulateMouseMovement = async (targetElement) => {");
            js.AppendLine("            try {");
            js.AppendLine("                if (!targetElement) return;");
            js.AppendLine("                const rect = targetElement.getBoundingClientRect();");
            js.AppendLine("                const targetX = rect.left + rect.width / 2;");
            js.AppendLine("                const targetY = rect.top + rect.height / 2;");
            js.AppendLine("                const startX = Math.random() * window.innerWidth;");
            js.AppendLine("                const startY = Math.random() * window.innerHeight;");
            js.AppendLine("                const controlX = (startX + targetX) / 2 + (Math.random() - 0.5) * 200;");
            js.AppendLine("                const controlY = (startY + targetY) / 2 + (Math.random() - 0.5) * 200;");
            js.AppendLine("                const steps = 20;");
            js.AppendLine("                for (let i = 0; i <= steps; i++) {");
            js.AppendLine("                    const t = i / steps;");
            js.AppendLine("                    const x = Math.pow(1-t, 2) * startX + 2 * (1-t) * t * controlX + Math.pow(t, 2) * targetX;");
            js.AppendLine("                    const y = Math.pow(1-t, 2) * startY + 2 * (1-t) * t * controlY + Math.pow(t, 2) * targetY;");
            js.AppendLine("                    const jitterX = x + (Math.random() - 0.5) * 4;");
            js.AppendLine("                    const jitterY = y + (Math.random() - 0.5) * 4;");
            js.AppendLine("                    const event = new MouseEvent('mousemove', { view: window, bubbles: true, cancelable: true, clientX: jitterX, clientY: jitterY });");
            js.AppendLine("                    document.dispatchEvent(event);");
            js.AppendLine("                    await randomDelay(30, 80);");
            js.AppendLine("                }");
            js.AppendLine("            } catch (e) { console.warn('[人类行为] 鼠标轨迹失败:', e); }");
            js.AppendLine("        };");
            js.AppendLine("");

            // 人类点击
            js.AppendLine("        const humanClick = async (element) => {");
            js.AppendLine("            try {");
            js.AppendLine("                if (!element) return false;");
            js.AppendLine("                await simulateMouseMovement(element);");
            js.AppendLine("                await randomDelay(100, 300);");
            js.AppendLine("                element.click();");
            js.AppendLine("                return true;");
            js.AppendLine("            } catch (e) { console.warn('[人类行为] 点击失败:', e); return false; }");
            js.AppendLine("        };");
            js.AppendLine("");

            // 人类打字
            js.AppendLine("        const humanTypeText = async (element, text) => {");
            js.AppendLine("            try {");
            js.AppendLine("                if (!element || !text) return false;");
            js.AppendLine("                element.focus();");
            js.AppendLine("                await randomDelay(200, 500);");
            js.AppendLine("                for (let i = 0; i < text.length; i++) {");
            js.AppendLine("                    document.execCommand('insertText', false, text[i]);");
            js.AppendLine("                    let delay = randomDelay(80, 200);");
            js.AppendLine("                    if (Math.random() < 0.1) await randomDelay(500, 1500);");
            js.AppendLine("                    if (['.', ',', '!', '?', '。', '，', '！', '？'].includes(text[i])) await randomDelay(300, 800);");
            js.AppendLine("                    await delay;");
            js.AppendLine("                }");
            js.AppendLine("                return true;");
            js.AppendLine("            } catch (e) { console.warn('[人类行为] 打字失败:', e); return false; }");
            js.AppendLine("        };");
            js.AppendLine("");
        }

        /// <summary>
        /// 生成帖子评论点赞采集脚本
        /// </summary>
        private string GenerateCommentLikeCollectScript(int expectedCount, string? configJson = null)
        {
            // 解析配置
            bool collectComment = true;
            bool collectLike = true;
            int commentExpectedCount = expectedCount;
            int likeExpectedCount = expectedCount;

            if (!string.IsNullOrEmpty(configJson))
            {
                try
                {
                    var config = Newtonsoft.Json.Linq.JObject.Parse(configJson);
                    collectComment = config.ContainsKey("collectComment") ? config.Value<bool>("collectComment") : true;
                    collectLike = config.ContainsKey("collectLike") ? config.Value<bool>("collectLike") : true;
                    commentExpectedCount = config.ContainsKey("commentExpectedCount") ? config.Value<int>("commentExpectedCount") : expectedCount;
                    likeExpectedCount = config.ContainsKey("likeExpectedCount") ? config.Value<int>("likeExpectedCount") : expectedCount;
                    System.Diagnostics.Debug.WriteLine($"📋 帖子评论点赞采集配置: collectComment={collectComment}, collectLike={collectLike}, commentExpectedCount={commentExpectedCount}, likeExpectedCount={likeExpectedCount}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"⚠️ 解析配置失败: {ex.Message}，使用默认配置");
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("⚠️ 未提供配置，使用默认配置（同时采集评论和点赞）");
            }

            var js = new System.Text.StringBuilder();

            js.AppendLine("(function() {");
            js.AppendLine("    return new Promise((resolve, reject) => {");

            // 根据配置设置目标数量
            int totalTargetCount = (collectComment ? commentExpectedCount : 0) + (collectLike ? likeExpectedCount : 0);
            System.Diagnostics.Debug.WriteLine($"🎯 计算目标数量: 评论={commentExpectedCount}, 点赞={likeExpectedCount}, 总计={totalTargetCount}");
            
            js.AppendLine($"        const targetCount = {totalTargetCount};");
            js.AppendLine($"        const commentTarget = {commentExpectedCount};");
            js.AppendLine($"        const likeTarget = {likeExpectedCount};");

            js.AppendLine("        const seenUserIds = new Set();");
            js.AppendLine("        const results = [];");
            js.AppendLine("");
            js.AppendLine("        let scrollCount = 0;");
            js.AppendLine("        const maxScrolls = 50;");
            js.AppendLine("        let consecutiveNoNewItems = 0;");
            js.AppendLine("        const maxConsecutiveNoNew = 5;");
            js.AppendLine("");

            // 注入配置常量到 JavaScript
            js.AppendLine($"        const COLLECT_COMMENT = {(collectComment ? "true" : "false")};");
            js.AppendLine($"        const COLLECT_LIKE = {(collectLike ? "true" : "false")};");
            js.AppendLine("");

            js.AppendLine("        const randomDelay = (min, max) => {");
            js.AppendLine("            return Math.floor(Math.random() * (max - min + 1)) + min;");
            js.AppendLine("        };");
            js.AppendLine("");

            // 添加贝塞尔曲线鼠标轨迹模拟函数（参考其他采集脚本）
            js.AppendLine("        // 贝塞尔曲线鼠标轨迹模拟");
            js.AppendLine("        const simulateMouseMovement = async (targetX, targetY) => {");
            js.AppendLine("            const startX = Math.random() * window.innerWidth;");
            js.AppendLine("            const startY = Math.random() * window.innerHeight;");
            js.AppendLine("            const steps = randomDelay(5, 10);");
            js.AppendLine("            const controlX = (startX + targetX) / 2 + randomDelay(-100, 100);");
            js.AppendLine("            const controlY = (startY + targetY) / 2 + randomDelay(-100, 100);");
            js.AppendLine("            ");
            js.AppendLine("            for (let i = 1; i <= steps; i++) {");
            js.AppendLine("                const t = i / steps;");
            js.AppendLine("                const x = Math.pow(1-t, 2) * startX + 2 * (1-t) * t * controlX + Math.pow(t, 2) * targetX + randomDelay(-2, 2);");
            js.AppendLine("                const y = Math.pow(1-t, 2) * startY + 2 * (1-t) * t * controlY + Math.pow(t, 2) * targetY + randomDelay(-2, 2);");
            js.AppendLine("                ");
            js.AppendLine("                const event = new MouseEvent('mousemove', {");
            js.AppendLine("                    view: window,");
            js.AppendLine("                    bubbles: true,");
            js.AppendLine("                    cancelable: true,");
            js.AppendLine("                    clientX: x,");
            js.AppendLine("                    clientY: y");
            js.AppendLine("                });");
            js.AppendLine("                document.dispatchEvent(event);");
            js.AppendLine("                await new Promise(resolve => setTimeout(resolve, randomDelay(30, 80)));");
            js.AppendLine("            }");
            js.AppendLine("        };");
            js.AppendLine("");

            // 人类点击（带鼠标轨迹）
            js.AppendLine("        const humanClick = async (element) => {");
            js.AppendLine("            try {");
            js.AppendLine("                if (!element) return false;");
            js.AppendLine("                const rect = element.getBoundingClientRect();");
            js.AppendLine("                const targetX = rect.left + rect.width / 2;");
            js.AppendLine("                const targetY = rect.top + rect.height / 2;");
            js.AppendLine("                await simulateMouseMovement(targetX, targetY);");
            js.AppendLine("                await new Promise(resolve => setTimeout(resolve, randomDelay(100, 300)));");
            js.AppendLine("                element.click();");
            js.AppendLine("                return true;");
            js.AppendLine("            } catch (e) { console.warn('[人类行为] 点击失败:', e); return false; }");
            js.AppendLine("        };");
            js.AppendLine("");

            // extractCommentData 函数 - 基于实际HTML结构优化
            js.AppendLine("        // 步骤1: 提取评论用户数据");
            js.AppendLine("        const extractCommentData = (commentElement) => {");
            js.AppendLine("            try {");
            js.AppendLine("                // 评论元素应该有 aria-label=\"Comment by ...\"");
            js.AppendLine("                const ariaLabel = commentElement.getAttribute('aria-label') || '';");
            js.AppendLine("                if (!ariaLabel.startsWith('Comment by')) {");
            js.AppendLine("                    console.log('❌ 不是评论元素');");
            js.AppendLine("                    return null;");
            js.AppendLine("                }");
            js.AppendLine("");
            js.AppendLine("                // 查找用户链接（在 span.xjp7ctv 内的 a 标签）");
            js.AppendLine("                const authorSpan = commentElement.querySelector('span.xjp7ctv');");
            js.AppendLine("                if (!authorSpan) {");
            js.AppendLine("                    console.log('❌ 未找到用户span');");
            js.AppendLine("                    return null;");
            js.AppendLine("                }");
            js.AppendLine("                const authorLink = authorSpan.querySelector('a');");
            js.AppendLine("                if (!authorLink) {");
            js.AppendLine("                    console.log('❌ 未找到用户链接');");
            js.AppendLine("                    return null;");
            js.AppendLine("                }");
            js.AppendLine("");
            js.AppendLine("                // 提取用户名（从父元素的 aria-label，移除时间后缀）");
            js.AppendLine("                // 处理各种时间格式: 'Comment by Name about an hour ago', 'Comment by Name 4 hours ago', 'Comment by Name a minute ago'");
            js.AppendLine("                let userName = ariaLabel.replace(/Comment by\\s*/i, '').replace(/\\s*(about\\s*)?(\\d+|a)\\s*(second|minute|hour|day|week|month|year)s?\\s*ago.*$/i, '').trim();");
            js.AppendLine("                if (!userName || userName.length < 2) {");
            js.AppendLine("                    console.log('❌ 未找到用户名');");
            js.AppendLine("                    return null;");
            js.AppendLine("                }");
            js.AppendLine("");
            js.AppendLine("                const originalUrl = authorLink.href;");
            js.AppendLine("                // 清理URL，保留查询参数中的id");
            js.AppendLine("                let url = originalUrl.split('&__cft__')[0].split('&__tn__')[0];");
            js.AppendLine("                if (url.includes('?id=')) {");
            js.AppendLine("                    url = url.split('?')[0] + '?' + url.split('?')[1].split('&')[0];");
            js.AppendLine("                }");
            js.AppendLine("");
            js.AppendLine("                if (seenUserIds.has(url)) {");
            js.AppendLine("                    console.log('❌ URL已存在:', url);");
            js.AppendLine("                    return null;");
            js.AppendLine("                }");
            js.AppendLine("");
            js.AppendLine("                // 提取头像");
            js.AppendLine("                let avatar = '';");
            js.AppendLine("                const svgImg = commentElement.querySelector('svg image');");
            js.AppendLine("                if (svgImg) {");
            js.AppendLine("                    avatar = svgImg.getAttribute('xlink:href') || svgImg.getAttribute('href') || '';");
            js.AppendLine("                }");
            js.AppendLine("");
            js.AppendLine("                // 提取用户ID");
            js.AppendLine("                let fbUserId = '';");
            js.AppendLine("                const idMatch = originalUrl.match(/[?&]id=(\\d+)/);");
            js.AppendLine("                if (idMatch) {");
            js.AppendLine("                    fbUserId = idMatch[1];");
            js.AppendLine("                } else {");
            js.AppendLine("                    const cleanUrl = url.replace(/https:\\/\\/www\\.facebook\\.com\\//i, '');");
            js.AppendLine("                    const nameMatch = cleanUrl.match(/^([^\\/?]+)/);");
            js.AppendLine("                    if (nameMatch && nameMatch[1]) {");
            js.AppendLine("                        fbUserId = nameMatch[1];");
            js.AppendLine("                    }");
            js.AppendLine("                }");
            js.AppendLine("                if (!fbUserId) {");
            js.AppendLine("                    console.log('❌ 未找到用户ID');");
            js.AppendLine("                    return null;");
            js.AppendLine("                }");
            js.AppendLine("");
            js.AppendLine("                seenUserIds.add(url);");
            js.AppendLine("                console.log('✅ 采集到评论用户:', userName, fbUserId);");
            js.AppendLine("");
            js.AppendLine("                return {");
            js.AppendLine("                    fbUserId: fbUserId,");
            js.AppendLine("                    userName: userName,");
            js.AppendLine("                    url: url,");
            js.AppendLine("                    avatar: avatar,");
            js.AppendLine("                    followers: 0,");
            js.AppendLine("                    profileStatus: '',");
            js.AppendLine("                    fromResource: '帖子评论采集'");
            js.AppendLine("                };");
            js.AppendLine("            } catch (e) {");
            js.AppendLine("                console.error('解析评论数据失败:', e);");
            js.AppendLine("                return null;");
            js.AppendLine("            }");
            js.AppendLine("        };");
            js.AppendLine("");

            // extractLikeUserData 函数 - 提取点赞用户数据
            js.AppendLine("        // 步骤3: 提取点赞用户数据");
            js.AppendLine("        const extractLikeUserData = (userLink) => {");
            js.AppendLine("            try {");
            js.AppendLine("                // 现在 userLink 已经是 a 元素");
            js.AppendLine("                // 验证用户链接有正确的 aria-label");
            js.AppendLine("                const ariaLabel = userLink.getAttribute('aria-label') || '';");
            js.AppendLine("                if (!ariaLabel.startsWith('Profile picture of')) {");
            js.AppendLine("                    console.log('❌ 不是点赞用户链接');");
            js.AppendLine("                    return null;");
            js.AppendLine("                }");
            js.AppendLine("");
            js.AppendLine("                // 提取用户名（从 aria-label 属性）");
            js.AppendLine("                let userName = ariaLabel.replace(/Profile picture of\\s*/i, '').trim();");
            js.AppendLine("                if (!userName || userName.length < 2) {");
            js.AppendLine("                    console.log('❌ 未找到点赞用户名');");
            js.AppendLine("                    return null;");
            js.AppendLine("                }");
            js.AppendLine("");
            js.AppendLine("                const originalUrl = userLink.href;");
            js.AppendLine("                // 清理URL，保留查询参数中的id");
            js.AppendLine("                let url = originalUrl.split('&__cft__')[0].split('&__tn__')[0];");
            js.AppendLine("                if (url.includes('?id=')) {");
            js.AppendLine("                    url = url.split('?')[0] + '?' + url.split('?')[1].split('&')[0];");
            js.AppendLine("                }");
            js.AppendLine("");
            js.AppendLine("                if (seenUserIds.has(url)) {");
            js.AppendLine("                    console.log('❌ 点赞用户URL已存在:', url);");
            js.AppendLine("                    return null;");
            js.AppendLine("                }");
            js.AppendLine("");
            js.AppendLine("                // 提取头像（从链接内部的 svg image）");
            js.AppendLine("                let avatar = '';");
            js.AppendLine("                const svgImg = userLink.querySelector('svg image');");
            js.AppendLine("                if (svgImg) {");
            js.AppendLine("                    avatar = svgImg.getAttribute('xlink:href') || svgImg.getAttribute('href') || '';");
            js.AppendLine("                }");
            js.AppendLine("");
            js.AppendLine("                // 提取用户ID");
            js.AppendLine("                let fbUserId = '';");
            js.AppendLine("                const idMatch = originalUrl.match(/[?&]id=(\\d+)/);");
            js.AppendLine("                if (idMatch) {");
            js.AppendLine("                    fbUserId = idMatch[1];");
            js.AppendLine("                } else {");
            js.AppendLine("                    const cleanUrl = url.replace(/https:\\/\\/www\\.facebook\\.com\\//i, '');");
            js.AppendLine("                    const nameMatch = cleanUrl.match(/^([^\\/?]+)/);");
            js.AppendLine("                    if (nameMatch && nameMatch[1]) {");
            js.AppendLine("                        fbUserId = nameMatch[1];");
            js.AppendLine("                    }");
            js.AppendLine("                }");
            js.AppendLine("                if (!fbUserId) {");
            js.AppendLine("                    console.log('❌ 未找到点赞用户ID');");
            js.AppendLine("                    return null;");
            js.AppendLine("                }");
            js.AppendLine("");
            js.AppendLine("                seenUserIds.add(url);");
            js.AppendLine("                console.log('✅ 采集到点赞用户:', userName, fbUserId);");
            js.AppendLine("");
            js.AppendLine("                return {");
            js.AppendLine("                    fbUserId: fbUserId,");
            js.AppendLine("                    userName: userName,");
            js.AppendLine("                    url: url,");
            js.AppendLine("                    avatar: avatar,");
            js.AppendLine("                    followers: 0,");
            js.AppendLine("                    profileStatus: '',");
            js.AppendLine("                    fromResource: '帖子点赞采集'");
            js.AppendLine("                };");
            js.AppendLine("            } catch (e) {");
            js.AppendLine("                console.error('解析点赞用户数据失败:', e);");
            js.AppendLine("                return null;");
            js.AppendLine("            }");
            js.AppendLine("        };");
            js.AppendLine("");
            js.AppendLine("        // 步骤4: 点击点赞列表按钮展开");
            js.AppendLine("        const clickLikeList = async () => {");
            js.AppendLine("            try {");
            js.AppendLine("                // 根据HTML结构：点赞按钮包含特定class的图标 img.x16dsc37");
            js.AppendLine("                let foundBtn = null;");
            js.AppendLine("                ");
            js.AppendLine("                // 方法1: 查找包含点赞图标 img.x16dsc37 的按钮");
            js.AppendLine("                const likeIcons = Array.from(document.querySelectorAll('img.x16dsc37'));");
            js.AppendLine("                console.log('🔍 找到点赞图标:', likeIcons.length, '个');");
            js.AppendLine("                ");
            js.AppendLine("                for (const icon of likeIcons) {");
            js.AppendLine("                    // 找到图标的最接近的可点击父元素");
            js.AppendLine("                    const parentBtn = icon.closest('[role=\"button\"]') || icon.closest('div[onclick]') || icon.parentElement;");
            js.AppendLine("                    if (parentBtn) {");
            js.AppendLine("                        // 排除评论内的按钮");
            js.AppendLine("                        const articleParent = parentBtn.closest('div[role=\"article\"]');");
            js.AppendLine("                        if (articleParent && articleParent.getAttribute('aria-label')?.startsWith('Comment by')) {");
            js.AppendLine("                            console.log('⚠️ 跳过评论内的点赞按钮');");
            js.AppendLine("                            continue;");
            js.AppendLine("                        }");
            js.AppendLine("                        foundBtn = parentBtn;");
            js.AppendLine("                        console.log('✅ 找到帖子点赞按钮（通过图标）');");
            js.AppendLine("                        break;");
            js.AppendLine("                    }");
            js.AppendLine("                }");
            js.AppendLine("                ");
            js.AppendLine("                // 方法2: 如果没找到图标，查找aria-label包含Like的按钮");
            js.AppendLine("                if (!foundBtn) {");
            js.AppendLine("                    console.log('🔍 方法1失败，尝试方法2: 查找aria-label包含Like的按钮...');");
            js.AppendLine("                    const allButtons = Array.from(document.querySelectorAll('[role=\"button\"]'));");
            js.AppendLine("                    for (const btn of allButtons) {");
            js.AppendLine("                        const ariaLabel = btn.getAttribute('aria-label') || '';");
            js.AppendLine("                        if (ariaLabel.toLowerCase().includes('like')) {");
            js.AppendLine("                            // 排除评论内的按钮");
            js.AppendLine("                            const articleParent = btn.closest('div[role=\"article\"]');");
            js.AppendLine("                            if (articleParent && articleParent.getAttribute('aria-label')?.startsWith('Comment by')) {");
            js.AppendLine("                                continue;");
            js.AppendLine("                            }");
            js.AppendLine("                            foundBtn = btn;");
            js.AppendLine("                            console.log('✅ 找到帖子点赞按钮（通过aria-label）:', ariaLabel);");
            js.AppendLine("                            break;");
            js.AppendLine("                        }");
            js.AppendLine("                    }");
            js.AppendLine("                }");
            js.AppendLine("                ");
            js.AppendLine("                if (!foundBtn) {");
            js.AppendLine("                    console.error('❌ 未找到点赞按钮');");
            js.AppendLine("                    return false;");
            js.AppendLine("                }");
            js.AppendLine("                ");
            js.AppendLine("                // 点击按钮");
            js.AppendLine("                foundBtn.scrollIntoView({ behavior: 'smooth', block: 'center' });");
            js.AppendLine("                await new Promise(resolve => setTimeout(resolve, randomDelay(500, 1000)));");
            js.AppendLine("                await humanClick(foundBtn);");
            js.AppendLine("                console.log('✅ 已点击点赞按钮');");
            js.AppendLine("                ");
            js.AppendLine("                // 等待弹窗加载 - 增加等待时间到 3-5 秒");
            js.AppendLine("                const waitTime = randomDelay(3000, 5000);");
            js.AppendLine("                console.log('⏳ 等待弹窗加载:', waitTime, 'ms');");
            js.AppendLine("                await new Promise(resolve => setTimeout(resolve, waitTime));");
            js.AppendLine("                ");
            js.AppendLine("                // 验证弹窗是否打开");
            js.AppendLine("                const likeModal = document.querySelector('div.xb57i2i');");
            js.AppendLine("                if (likeModal) {");
            js.AppendLine("                    console.log('✅ 点赞弹窗已打开');");
            js.AppendLine("                    return true;");
            js.AppendLine("                } else {");
            js.AppendLine("                    console.warn('⚠️ 点赞弹窗未找到，尝试重试...');");
            js.AppendLine("                    // 重试一次");
            js.AppendLine("                    await humanClick(foundBtn);");
            js.AppendLine("                    await new Promise(resolve => setTimeout(resolve, randomDelay(3000, 5000)));");
            js.AppendLine("                    const modalRetry = document.querySelector('div.xb57i2i');");
            js.AppendLine("                    if (modalRetry) {");
            js.AppendLine("                        console.log('✅ 重试成功: 点赞弹窗已打开');");
            js.AppendLine("                        return true;");
            js.AppendLine("                    }");
            js.AppendLine("                    console.error('❌ 重试后仍未找到弹窗');");
            js.AppendLine("                    return false;");
            js.AppendLine("                }");
            js.AppendLine("            } catch (e) {");
            js.AppendLine("                console.error('❌ 点击点赞按钮失败:', e);");
            js.AppendLine("                return false;");
            js.AppendLine("            }");
            js.AppendLine("        };");
            js.AppendLine("");
            js.AppendLine("        // collectComments 主函数 - 基于实际HTML结构优化");
            js.AppendLine("        // 步骤2: 采集所有评论");
            js.AppendLine("        const collectComments = () => {");
            js.AppendLine("            // 查找所有评论元素（必须有 role=\"article\" 且 aria-label 以 \"Comment by\" 开头）");
            js.AppendLine("            const allDivs = Array.from(document.querySelectorAll('div[role=\"article\"]'));");
            js.AppendLine("            const commentElements = allDivs.filter(el => {");
            js.AppendLine("                const label = el.getAttribute('aria-label') || '';");
            js.AppendLine("                return label.startsWith('Comment by');");
            js.AppendLine("            });");
            js.AppendLine("            console.log('🔍 找到评论元素:', commentElements.length, '个');");
            js.AppendLine("");
            js.AppendLine("            if (commentElements.length === 0) {");
            js.AppendLine("                console.warn('⚠️ 未找到评论区元素');");
            js.AppendLine("                return false;");
            js.AppendLine("            }");
            js.AppendLine("");
            js.AppendLine("            let newCount = 0;");
            js.AppendLine("            let commentCount = 0;");
            js.AppendLine("            for (let i = 0; i < commentElements.length; i++) {");
            js.AppendLine("                const element = commentElements[i];");
            js.AppendLine("                if (results.length >= targetCount) break;");
            js.AppendLine("");
            js.AppendLine("                if (COLLECT_COMMENT && commentCount < commentTarget) {");
            js.AppendLine("                    const data = extractCommentData(element);");
            js.AppendLine("                    if (data) {");
            js.AppendLine("                        results.push(data);");
            js.AppendLine("                        commentCount++;");
            js.AppendLine("                        newCount++;");
            js.AppendLine("                    }");
            js.AppendLine("                }");
            js.AppendLine("            }");
            js.AppendLine("");
            js.AppendLine("            console.log(`📊 当前已采集: ${results.length}/${targetCount}, 本轮新增评论: ${newCount}`);");
            js.AppendLine("            return newCount > 0;");
            js.AppendLine("        };");
            js.AppendLine("");
            js.AppendLine("        // collectLikes 主函数 - 采集点赞用户");
            js.AppendLine("        // 步骤4: 采集点赞用户");
            js.AppendLine("        const collectLikes = () => {");
            js.AppendLine("            // 1. 先找到点赞弹框容器");
            js.AppendLine("            const likeModal = document.querySelector('div.xb57i2i.x1q594ok');");
            js.AppendLine("            if (!likeModal) {");
            js.AppendLine("                console.warn('⚠️ 未找到点赞弹框');");
            js.AppendLine("                return false;");
            js.AppendLine("            }");
            js.AppendLine("            console.log('✅ 找到点赞弹框');");
            js.AppendLine("            ");
            js.AppendLine("            // 2. 全局查找点赞用户元素（不限制在弹框内）");
            js.AppendLine("            // 结构: span.xjp7ctv > a (用户头像链接)");
            js.AppendLine("            const userSpans = Array.from(document.querySelectorAll('span.xjp7ctv'));");
            js.AppendLine("            console.log('🔍 全局找到用户span:', userSpans.length, '个');");
            js.AppendLine("            ");
            js.AppendLine("            // 从每个span中获取a标签");
            js.AppendLine("            const likeElements = [];");
            js.AppendLine("            for (const span of userSpans) {");
            js.AppendLine("                const link = span.querySelector('a');");
            js.AppendLine("                if (link) {");
            js.AppendLine("                    // 过滤掉已采集的用户（去重）");
            js.AppendLine("                    const url = link.href;");
            js.AppendLine("                    if (!seenUserIds.has(url)) {");
            js.AppendLine("                        likeElements.push(link);");
            js.AppendLine("                    }");
            js.AppendLine("                }");
            js.AppendLine("            }");
            js.AppendLine("            console.log('🔍 找到未采集的用户链接:', likeElements.length, '个');");
            js.AppendLine("");
            js.AppendLine("            if (likeElements.length === 0) {");
            js.AppendLine("                console.warn('⚠️ 未找到点赞用户元素');");
            js.AppendLine("                return false;");
            js.AppendLine("            }");
            js.AppendLine("");
            js.AppendLine("            let newCount = 0;");
            js.AppendLine("            let likeCount = 0;");
            js.AppendLine("            for (let i = 0; i < likeElements.length; i++) {");
            js.AppendLine("                const element = likeElements[i];");
            js.AppendLine("                if (results.length >= targetCount) break;");
            js.AppendLine("");
            js.AppendLine("                if (COLLECT_LIKE && likeCount < likeTarget) {");
            js.AppendLine("                    const data = extractLikeUserData(element);");
            js.AppendLine("                    if (data) {");
            js.AppendLine("                        results.push(data);");
            js.AppendLine("                        likeCount++;");
            js.AppendLine("                        newCount++;");
            js.AppendLine("                    }");
            js.AppendLine("                }");
            js.AppendLine("            }");
            js.AppendLine("");
            js.AppendLine("            console.log(`📊 当前已采集: ${results.length}/${targetCount}, 本轮新增点赞用户: ${newCount}`);");
            js.AppendLine("            return newCount > 0;");
            js.AppendLine("        };");
            js.AppendLine("");

            // 滚动加载更多 - 人类化滚动（参考帖子采集脚本）
            js.AppendLine("        const scrollToLoadMore = async () => {");
            js.AppendLine("            const scrollHeight = document.documentElement.scrollHeight;");
            js.AppendLine("            const currentScroll = document.documentElement.scrollTop + window.innerHeight;");
            js.AppendLine("");
            js.AppendLine("            if (currentScroll < scrollHeight - 200) {");
            js.AppendLine("                // 根据可视区域高度动态计算滚动距离");
            js.AppendLine("                const viewportHeight = window.innerHeight || document.documentElement.clientHeight;");
            js.AppendLine("                const minScroll = Math.max(600, viewportHeight * 0.8);");
            js.AppendLine("                const maxScroll = Math.max(1000, viewportHeight * 1.2);");
            js.AppendLine("                const scrollDistance = randomDelay(Math.floor(minScroll), Math.floor(maxScroll));");
            js.AppendLine("");
            js.AppendLine("                // 模拟人手滚动：分多次小幅度滚动");
            js.AppendLine("                const scrollSteps = randomDelay(3, 7);");
            js.AppendLine("                const stepSize = scrollDistance / scrollSteps;");
            js.AppendLine("                for (let i = 0; i < scrollSteps; i++) {");
            js.AppendLine("                    window.scrollBy({ top: stepSize + randomDelay(-10, 10), behavior: 'auto' });");
            js.AppendLine("                    await new Promise(resolve => setTimeout(resolve, randomDelay(50, 150)));");
            js.AppendLine("                }");
            js.AppendLine("                ");
            js.AppendLine("                // 滚动后随机停顿，模拟阅读评论");
            js.AppendLine("                const readPause = randomDelay(1000, 3000);");
            js.AppendLine("                console.log('[阅读停顿]', readPause, 'ms');");
            js.AppendLine("                await new Promise(resolve => setTimeout(resolve, readPause));");
            js.AppendLine("");
            js.AppendLine("                return true;");
            js.AppendLine("            }");
            js.AppendLine("            return false;");
            js.AppendLine("        };");
            js.AppendLine("");

            // 主循环 - 先采集评论，再采集点赞用户
            js.AppendLine("        // 步骤7: 主循环");
            js.AppendLine("        const mainLoop = async () => {");
            js.AppendLine("            try {");
            js.AppendLine("                // 第一步：采集评论用户");
            js.AppendLine("                if (COLLECT_COMMENT) {");
            js.AppendLine("                    console.log('🔍 步骤1: 开始采集评论用户...');");
            js.AppendLine("                    while (results.length < targetCount && scrollCount < maxScrolls) {");
            js.AppendLine("                        const foundNew = collectComments();");
            js.AppendLine("");
            js.AppendLine("                        if (foundNew) {");
            js.AppendLine("                            consecutiveNoNewItems = 0;");
            js.AppendLine("                        } else {");
            js.AppendLine("                            consecutiveNoNewItems++;");
            js.AppendLine("                        }");
            js.AppendLine("");
            js.AppendLine("                        if (consecutiveNoNewItems >= maxConsecutiveNoNew) {");
            js.AppendLine("                            console.log('⚠️ 连续多次未发现新评论，停止采集');");
            js.AppendLine("                            break;");
            js.AppendLine("                        }");
            js.AppendLine("");
            js.AppendLine("                        if (results.length >= targetCount) {");
            js.AppendLine("                            console.log('✅ 已达到目标数量');");
            js.AppendLine("                            break;");
            js.AppendLine("                        }");
            js.AppendLine("");
            js.AppendLine("                        const canScroll = await scrollToLoadMore();");
            js.AppendLine("                        if (!canScroll) {");
            js.AppendLine("                            console.log('⚠️ 无法继续滚动，停止采集');");
            js.AppendLine("                            break;");
            js.AppendLine("                        }");
            js.AppendLine("");
            js.AppendLine("                        scrollCount++;");
            js.AppendLine("                        await randomDelay(1000, 2000);");
            js.AppendLine("                    }");
            js.AppendLine("                }");
            js.AppendLine("");
            js.AppendLine("                // 第二步：点击点赞按钮并采集点赞用户");
            js.AppendLine("                if (COLLECT_LIKE && results.length < targetCount) {");
            js.AppendLine("                    console.log('🔍 步骤2: 点击点赞按钮...');");
            js.AppendLine("                    await clickLikeList();");
            js.AppendLine("");
            js.AppendLine("                    console.log('🔍 步骤3: 开始采集点赞用户...');");
            js.AppendLine("                    let likeScrollCount = 0;");
            js.AppendLine("                    while (results.length < targetCount && likeScrollCount < 20) {");
            js.AppendLine("                        const foundNew = collectLikes();");
            js.AppendLine("");
            js.AppendLine("                        if (!foundNew) {");
            js.AppendLine("                            likeScrollCount++;");
            js.AppendLine("                            if (likeScrollCount >= 5) {");
            js.AppendLine("                                console.log('⚠️ 连续多次未发现新点赞用户，停止采集');");
            js.AppendLine("                                break;");
            js.AppendLine("                            }");
            js.AppendLine("                        } else {");
            js.AppendLine("                            likeScrollCount = 0;");
            js.AppendLine("                        }");
            js.AppendLine("");
            js.AppendLine("                        if (results.length >= targetCount) {");
            js.AppendLine("                            console.log('✅ 已达到目标数量');");
            js.AppendLine("                            break;");
            js.AppendLine("                        }");
            js.AppendLine("");
            js.AppendLine("                        await scrollToLoadMore();");
            js.AppendLine("                        await randomDelay(1000, 2000);");
            js.AppendLine("                    }");
            js.AppendLine("                }");
            js.AppendLine("");
            js.AppendLine("                console.log(`🎉 采集完成！共采集 ${results.length} 条数据`);");
            js.AppendLine("                resolve(results);");
            js.AppendLine("            } catch (error) {");
            js.AppendLine("                console.error('❌ 采集过程出错:', error);");
            js.AppendLine("                reject(error);");
            js.AppendLine("            }");
            js.AppendLine("        };");
            js.AppendLine("");
            js.AppendLine("        mainLoop();");
            js.AppendLine("    });");
            js.AppendLine("})();");

            return js.ToString();
        }
    }
}
