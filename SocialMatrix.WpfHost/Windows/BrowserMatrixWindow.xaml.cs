using CefSharp;
using CefSharp.Wpf;
using Newtonsoft.Json;
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
    /// 浏览器矩阵窗口 - 独立弹窗显示 12 宫格
    /// </summary>
    public partial class BrowserMatrixWindow : Window
    {
        private readonly Dictionary<string, ChromiumWebBrowser> _browsers = new();
        private readonly Dictionary<string, bool> _browserInitialized = new(); // 跟踪指纹注入状态
        private readonly Dictionary<string, int> _accountTaskTypes = new(); // 账号 -> 任务类型映射
        private readonly Dictionary<string, IRequestContext> _requestContexts = new(); // 账号 -> 独立请求上下文

        // 当前明细ID(用于回传,单任务场景)
        public string? CurrentDetailId { get; set; }

        // 采集结果回调
        public event Action<string, string, string, int>? OnCollectionComplete; // (detailId, accountId, jsonData, taskType)
        public event Action<string, string>? OnCollectionError;    // (accountId, errorMessage)

        // 最大并发数配置（从后端读取，默认19 - 8GB内存推荐值）
        private static int _maxConcurrentBrowsers = 19;
        private static FingerprintGlobalConfig? _globalConfig = null;
        private static DateTime _configLastFetchTime = DateTime.MinValue;
        private static readonly TimeSpan ConfigCacheDuration = TimeSpan.FromMinutes(5);

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
                var configs = JsonConvert.DeserializeObject<List<Dictionary<string, string>>>(response);

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
            string? cookie = null, string? searchUrl = null, int expectedCount = 100, long? deviceId = null, int taskType = 1, string? config = null)
        {
            // 记录配置信息（如果有）
            if (!string.IsNullOrEmpty(config))
            {
                System.Diagnostics.Debug.WriteLine($"📋 BrowserMatrixWindow 收到配置: {config}");
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

                // 如果提供了新的搜索 URL，重新启动采集
                if (!string.IsNullOrEmpty(searchUrl))
                {
                    var existingBrowser = _browsers[accountId];
                    System.Diagnostics.Debug.WriteLine($"🔄 为已存在的浏览器启动新采集: {searchUrl}");

                    // 异步启动采集（不阻塞）
                    Task.Run(async () =>
                    {
                        await StartAutoCollect(existingBrowser, accountId, searchUrl, expectedCount, taskType, config);
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

            var browser = new ChromiumWebBrowser(initialUrl)
            {
                RequestContext = requestContext,  // 使用独立的请求上下文
                Background = System.Windows.Media.Brushes.White  // 设置白色背景，避免灰色遮罩
            };
            browser.Tag = accountId;

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

            // 页面加载状态变化事件（项目 A 的逻辑）
            browser.LoadingStateChanged += async (sender, e) =>
            {
                if (!e.IsLoading)  // 页面加载完成
                {
                    if (browser.CanExecuteJavascriptInMainFrame)
                    {
                        // 只在第一次加载时注入指纹（避免重复注入）
                        if (!_browserInitialized.ContainsKey(accountId) || !_browserInitialized[accountId])
                        {
                            _browserInitialized[accountId] = true;

                            // 1. 注入指纹（在 Cookie 之前）
                            var globalConfig = await GetGlobalConfigAsync();
                            System.Diagnostics.Debug.WriteLine($"🔍 全局配置读取结果: DisableImages={globalConfig?.DisableImages}, DisableVideos={globalConfig?.DisableVideos}");

                            var fingerprint = new FingerprintConfig
                            {
                                Area = "",
                                Latitude = null,
                                Longitude = null,
                                DeviceId = deviceId,
                                DisableImages = globalConfig?.DisableImages ?? false,
                                DisableVideos = globalConfig?.DisableVideos ?? false
                            };
                            await FingerprintInjector.InjectAsync(browser, fingerprint);
                            System.Diagnostics.Debug.WriteLine($"✅ 账号 {accountId} 指纹注入完成 (DeviceName={fingerprint.DeviceName}, DisableImages={fingerprint.DisableImages}, DisableVideos={fingerprint.DisableVideos})");

                            // 2. 注入 Cookie（如果有）并验证
                            bool isCookieValid = true;
                            if (!string.IsNullOrEmpty(cookie))
                            {
                                System.Diagnostics.Debug.WriteLine($"🍪 开始为账号 {accountId} 注入 Cookie...");
                                isCookieValid = await InjectCookies(browser, accountId, cookie);
                            }
                            else
                            {
                                System.Diagnostics.Debug.WriteLine($"⚠️ 账号 {accountId} 未提供 Cookie，跳过注入步骤");
                            }

                            // 3. 如果 Cookie 有效且提供了搜索 URL，启动自动化采集
                            if (isCookieValid && !string.IsNullOrEmpty(searchUrl))
                            {
                                await StartAutoCollect(browser, accountId, searchUrl, expectedCount, taskType, config);
                            }
                        }
                    }
                }
            };
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

            if (count == 1)
            {
                // 1个账号: 占满整个窗口
                browserWidth = gridWidth - MarginPadding;
                browserHeight = windowHeight - MarginPadding - UrlLabelHeight;
            }
            else
            {
                // 2个及以上: 固定2列布局
                const int columns = 2;
                const double MinBrowserHeight = 350; // 最小高度350px,保证足够空间

                // 计算每个浏览器的宽度(2列平分)
                browserWidth = (gridWidth - MarginPadding) / columns - 10;

                // 高度固定为最小值
                browserHeight = MinBrowserHeight;
            }

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

            System.Diagnostics.Debug.WriteLine($"📐 布局更新: {count}个账号, 每个{browserWidth:F0}x{browserHeight:F0}px");
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
        /// 注入 Cookie（并验证是否有效）
        /// </summary>
        /// <returns>true: Cookie 有效, false: Cookie 失效或网络问题</returns>
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

                System.Diagnostics.Debug.WriteLine($"✅ 已为账号 {accountId} 注入 {successCount}/{cookieList.Count} 个 Cookie (使用独立RequestContext)");

                // 刷新页面使 Cookie 生效（使用 Dispatcher 确保在 UI 线程执行）
                Application.Current.Dispatcher.Invoke(() =>
                {
                    browser.Reload();
                });

                // 等待页面加载完成（参考项目 A 的方式）
                System.Diagnostics.Debug.WriteLine($"📌 等待页面重新加载...");
                await Task.Delay(2000); // 先等待2秒

                // 循环检查 IsLoading 状态
                int checkCount = 0;
                while (checkCount < 15) // 最多检查15次，每次2秒，共30秒
                {
                    bool isLoading = true;
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        isLoading = browser.IsLoading;
                    });

                    if (!isLoading)
                    {
                        System.Diagnostics.Debug.WriteLine($"📌 页面加载完成");
                        break;
                    }

                    await Task.Delay(2000); // 继续等待2秒
                    checkCount++;

                    if (checkCount % 3 == 0)
                    {
                        System.Diagnostics.Debug.WriteLine($"⏳ 等待页面加载中... ({checkCount * 2}秒)");
                    }
                }

                if (checkCount >= 15)
                {
                    System.Diagnostics.Debug.WriteLine($"⚠️ 页面加载超时");
                }

                // 检查 Cookie 是否有效（通过页面内容判断）
                var isLoginPage = await CheckIfLoginPage(browser);
                if (isLoginPage)
                {
                    System.Diagnostics.Debug.WriteLine($"❌ 账号 {accountId} Cookie 失效，停留在登录页");
                    OnCollectionError?.Invoke(accountId, "Cookie已失效，需要重新登录");
                    return false;
                }

                System.Diagnostics.Debug.WriteLine($"✅ 账号 {accountId} Cookie 验证通过");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"⚠️ Cookie 注入失败: {ex.Message}");
                return false;
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
                var collectScript = GenerateCollectScript(expectedCount, taskType, config);
                System.Diagnostics.Debug.WriteLine($"🚀 开始执行采集脚本, 目标数量: {expectedCount}");
                System.Diagnostics.Debug.WriteLine($"🔍 脚本长度: {collectScript.Length} 字符");

                // ❗ 最后一次验证浏览器状态
                System.Diagnostics.Debug.WriteLine($"🔍 检查浏览器状态: IsDisposed={browser.IsDisposed}, CanExecuteJavascript={browser.CanExecuteJavascriptInMainFrame}");

                if (browser.IsDisposed || !browser.CanExecuteJavascriptInMainFrame)
                {
                    System.Diagnostics.Debug.WriteLine($"❌ 账号 {accountId} 浏览器在执行脚本前已失效或被关闭");
                    OnCollectionError?.Invoke(accountId, "浏览器已被关闭或失效，请重新启动任务");
                    return;
                }

                System.Diagnostics.Debug.WriteLine($"🔍 开始执行 EvaluateScriptAsync...");
                var result = await browser.EvaluateScriptAsync(collectScript);
                System.Diagnostics.Debug.WriteLine($"🔍 EvaluateScriptAsync 执行完成: Success={result.Success}");

                if (result.Success && result.Result != null)
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
                    OnCollectionComplete?.Invoke(CurrentDetailId ?? "", accountId, jsonData, actualTaskType);
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
        private string GenerateCollectScript(int expectedCount, int taskType = 1, string? config = null)
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
                return GenerateAddGroupCollectScript(expectedCount);
            }
            else if (taskType == 10) // 转帖任务
            {
                System.Diagnostics.Debug.WriteLine("✅ 进入转帖任务分支");
                // 转帖任务需要额外参数，这里返回空脚本，实际执行在 StartAutoCollect 中处理
                return "(function() { return JSON.stringify([]); })();";
            }
            else if (taskType == 11) // 帖子评论点赞采集
            {
                System.Diagnostics.Debug.WriteLine("✅ 进入帖子评论点赞采集分支");
                return GenerateCommentLikeCollectScript(expectedCount, config);
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

                for (const span of allSpans) {
                    const text = span.textContent.trim();
                    if (!text) continue;

                    const keywordsPattern = FOLLOWER_KEYWORDS.join('|');
                    const unitsPattern = FOLLOWER_UNITS.join('|');
                    const followerRegex = new RegExp('([\\d]+[\\.,]?\\d*)[\\s]*(?:' + unitsPattern + ')?[\\s]*(?:' + keywordsPattern + ')|(?:' + keywordsPattern + ')[\\s:]*([\\d]+[\\.,]?\\d*)[\\s]*(?:' + unitsPattern + ')?', 'i');
                    const followerMatch = text.match(followerRegex);

                    if (followerMatch) {
                        let numberPart = followerMatch[1] || followerMatch[2] || '';
                        if (numberPart && /^\d+[\.,]?\d*$/.test(numberPart)) {
                            const fullMatch = followerMatch[0];
                            const unitRegex = new RegExp('(?:^|[\s])(' + unitsPattern + ')(?:[\s]|$)', 'i');
                            const unitMatch = fullMatch.match(unitRegex);
                            followers = numberPart + (unitMatch ? unitMatch[1] : '');
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

                let postLinkEl = card.querySelector('a[href*=""/posts/""]') ||
                                card.querySelector('a[href*=""/permalink/""]') ||
                                card.querySelector('a[href*=""/photos/""]') ||
                                card.querySelector('a[href*=""/videos/""]') ||
                                card.querySelector('a[href*=""story.php""]') ||
                                card.querySelector('a[href*=""/search/posts/""]');

                if (!postLinkEl) {
                    const timeLinks = card.querySelectorAll('a[href*=""facebook.com""]');
                    for (const link of timeLinks) {
                        const href = link.href;
                        if (href.includes('/stories/') || href.includes('/profile.php') ||
                            href.includes('/groups/') || !href.includes('?')) continue;
                        if (href.includes('fbid=') || href.includes('story_fbid=') ||
                            href.includes('id=') && href.match(/\d{15,}/)) {
                            postLinkEl = link;
                            break;
                        }
                    }
                }

                if (!postLinkEl) {
                    postLinkEl = card.querySelector('a[data-ft]');
                }

                if (!postLinkEl) return null;

                const url = postLinkEl.href.split('?')[0];
                if (!url || seenUrls.has(url)) return null;

                const authorEl = card.querySelector('[data-ad-rendering-role=""profile_name""] a') ||
                               card.querySelector('h3 a[href*=""facebook.com""]:not([href*=""/groups/""])') ||
                               card.querySelector('strong a[href*=""facebook.com""]:not([href*=""/groups/""])') ||
                               card.querySelector('a[aria-label]:not([href*=""/groups/""]):not([href*=""/hashtag/""])') ||
                               card.querySelector('span[dir=""auto""] strong');
                const postUser = authorEl ? authorEl.textContent.trim() : '';

                const groupLinkEl = card.querySelector('h3 a[href*=""/groups/""]') ||
                               card.querySelector('[data-ad-rendering-role=""profile_name""] a[href*=""/groups/""]:not([href*=""/user/""])');
                const groupName = groupLinkEl ? groupLinkEl.textContent.trim() : '';

                const contentEl = card.querySelector('[data-ad-comet-preview=""message""]') ||
                                card.querySelector('[data-testid=""post_message""]') ||
                                card.querySelector('span[dir=""auto""]');
                const postContent = contentEl ? contentEl.textContent.trim() : '';

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

                let itemIdMatch = url.match(/(?:posts|permalink|photos|videos)\/([^\?]+)/);
                if (!itemIdMatch) itemIdMatch = url.match(/pcb\.([0-9]+)/);
                const itemId = itemIdMatch ? itemIdMatch[1] : '';

                seenUrls.add(url);
                return {
                    itemId, postUser, url, fromResource: groupName ? 'group' : 'page',
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

                const groupName = groupLinkEl.textContent.trim();
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
                const userLinkEl = container.querySelector('a[href*=""/profile.php?id=""]') || container.querySelector('a[href*=""/user/""]');
                if (!userLinkEl) return null;

                const url = userLinkEl.href.split('?')[0];
                if (!url || seenUserIds.has(url)) return null;

                const userName = userLinkEl.textContent.trim();
                if (!userName) return null;

                let fbUserId = '';
                const idMatch = url.match(/[?&]id=(\d+)/);
                if (idMatch) {
                    fbUserId = idMatch[1];
                } else {
                    const userIdMatch = url.match(/\/user\/(\d+)/);
                    fbUserId = userIdMatch ? userIdMatch[1] : '';
                }

                const imgEl = container.querySelector('img');
                const avatar = imgEl ? (imgEl.src || '') : '';

                let fromResource = 'peer_follower';
                if (window.location.href.includes('&sk=following')) fromResource = 'peer_following';
                else if (window.location.href.includes('&sk=friends')) fromResource = 'peer_friend';

                seenUserIds.add(url);
                return { fbUserId, userName, url, avatar, dataType: 1, fromResource, syncTime: new Date().toISOString() };
            } catch (e) {
                console.warn('Extract user relation failed:', e);
                return null;
            }
        };
");

            js.AppendLine(JsScriptHelper.GetCollectionLoopTemplate("extractUserData", "div[class*=\"x6s0dn4\"]"));

            return JsScriptHelper.CreatePromiseWrapper(js.ToString());
        }

        /// <summary>
        /// 生成链接加组采集脚本（简化版）
        /// </summary>
        private string GenerateAddGroupCollectScript(int expectedCount)
        {
            var js = new System.Text.StringBuilder();

            js.AppendLine(JsScriptHelper.GetRandomDelayFunction());
            js.AppendLine(JsScriptHelper.GetMouseMovementFunction());

            // 获取当前用户信息
            js.AppendLine(@"
        const getCurrentUserInfo = () => {
            try {
                const currentUrl = window.location.href.split('?')[0];
                const userIdMatch = currentUrl.match(/[?&]id=(\d+)/);
                return { accountId: userIdMatch ? userIdMatch[1] : '', targetUrl: currentUrl };
            } catch (e) {
                return { accountId: '', targetUrl: window.location.href };
            }
        };
");

            // 查找并点击加入群组按钮
            js.AppendLine(@"
        const findAndClickJoinButton = async () => {
            try {
                const joinedEl = Array.from(document.querySelectorAll('span')).find(el => el.textContent.trim() === 'Joined');
                if (joinedEl) return { success: true, status: 3, reason: 'Already joined' };

                const pendingEl = Array.from(document.querySelectorAll('span')).find(el => el.textContent.includes('membership is pending'));
                if (pendingEl) return { success: true, status: 3, reason: 'Pending approval' };

                const joinButton = document.querySelector('[aria-label=""Join group""]');
                if (!joinButton) return { success: false, reason: 'No join button found' };

                joinButton.click();
                await new Promise(resolve => setTimeout(resolve, randomDelay(3000, 4000)));
                return checkJoinResult();
            } catch (e) {
                return { success: false, reason: e.message };
            }
        };
");

            // 检查加组结果
            js.AppendLine(@"
        const checkJoinResult = () => {
            try {
                const joinedEl = Array.from(document.querySelectorAll('span')).find(el => el.textContent.trim() === 'Joined');
                if (joinedEl) return { success: true, status: 1, reason: 'Joined successfully' };

                const pendingEl = Array.from(document.querySelectorAll('span')).find(el => el.textContent.includes('membership is pending'));
                if (pendingEl) return { success: true, status: 3, reason: 'Pending approval' };

                return { success: true, status: 1, reason: 'Completed' };
            } catch (e) {
                return { success: false, reason: e.message };
            }
        };
");

            // 提取群组信息
            js.AppendLine(@"
        const extractGroupInfo = () => {
            try {
                const groupUrl = window.location.href.split('?')[0];
                const groupIdMatch = groupUrl.match(/\/groups\/(\d+)/);
                const groupId = groupIdMatch ? groupIdMatch[1] : '';

                let groupName = '';
                const titleEl = document.querySelector('h1, [data-testid=""group_name""]');
                if (titleEl) groupName = titleEl.textContent.trim();

                return { groupId, groupName, groupUrl };
            } catch (e) {
                return { groupId: '', groupName: '', groupUrl: window.location.href };
            }
        };
");

            // 主执行逻辑
            js.AppendLine(@"
        const executeJoinGroup = async () => {
            try {
                const userInfo = getCurrentUserInfo();
                const groupInfo = extractGroupInfo();
                const result = await findAndClickJoinButton();

                results.push({
                    accountId: userInfo.accountId,
                    targetUrl: userInfo.targetUrl,
                    groupId: groupInfo.groupId,
                    groupName: groupInfo.groupName,
                    groupUrl: groupInfo.groupUrl,
                    joinStatus: result.status || (result.success ? 1 : 2),
                    failReason: result.reason || '',
                    joinTime: new Date().toISOString(),
                    syncTime: new Date().toISOString()
                });

                resolve(JSON.stringify(results));
            } catch (e) {
                reject(new Error(e.message));
            }
        };

        executeJoinGroup();
");

            // 超时保护（30秒）
            js.AppendLine("        setTimeout(() => {");
            js.AppendLine("            if (results.length === 0) reject(new Error('Join group timeout'));");
            js.AppendLine("        }, 30000);");

            return JsScriptHelper.CreatePromiseWrapper(js.ToString());
        }

        /// <summary>
        /// 获取默认关键词（备用）
        /// </summary>
        private string GetDefaultKeywords()
        {
            return "['followers', 'follower', 'pengikut', 'abonnes', 'seguidores', 'fans', 'rb', 'jt', 'k', 'K', 'm', 'M', 'b', 'B', 't', 'T']";
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
            int likeExpectedCount = expectedCount;

            if (!string.IsNullOrEmpty(configJson))
            {
                try
                {
                    var config = Newtonsoft.Json.Linq.JObject.Parse(configJson);
                    collectComment = config.ContainsKey("collectComment") ? config.Value<bool>("collectComment") : true;
                    collectLike = config.ContainsKey("collectLike") ? config.Value<bool>("collectLike") : true;
                    likeExpectedCount = config.ContainsKey("likeExpectedCount") ? config.Value<int>("likeExpectedCount") : expectedCount;
                    System.Diagnostics.Debug.WriteLine($"📋 帖子评论点赞采集配置: collectComment={collectComment}, collectLike={collectLike}, likeExpectedCount={likeExpectedCount}");
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
            if (collectComment && collectLike)
            {
                js.AppendLine($"        const targetCount = {expectedCount}; // 同时采集评论和点赞");
            }
            else if (collectComment)
            {
                js.AppendLine($"        const targetCount = {expectedCount}; // 只采集评论");
            }
            else
            {
                js.AppendLine($"        const targetCount = {likeExpectedCount}; // 只采集点赞");
            }

            js.AppendLine("        const seenUserIds = new Set();");
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

            // 第一步：点击 "All comments" 按钮展开评论区
            js.AppendLine("        // 步骤1: 点击 'All comments' 按钮展开评论区");
            js.AppendLine("        const clickAllComments = async () => {");
            js.AppendLine("            try {");
            js.AppendLine("                // 查找 'All comments' 或 'Leave a comment' 按钮");
            js.AppendLine("                const allCommentsSelectors = [");
            js.AppendLine("                    '[aria-label=\"Leave a comment\"]',");
            js.AppendLine("                    'span[dir=\"auto\"]:contains(\"All comments\")',");
            js.AppendLine("                    'div[role=\"button\"]:has(span[dir=\"auto\"])'");
            js.AppendLine("                ];");
            js.AppendLine("");
            js.AppendLine("                for (const selector of allCommentsSelectors) {");
            js.AppendLine("                    const buttons = Array.from(document.querySelectorAll(selector));");
            js.AppendLine("                    for (const btn of buttons) {");
            js.AppendLine("                        const text = btn.textContent.trim();");
            js.AppendLine("                        if (text.includes('All comments') || text.includes('comments')) {");
            js.AppendLine("                            console.log('🔍 找到 All comments 按钮，准备点击...');");
            js.AppendLine("                            btn.scrollIntoView({ behavior: 'smooth', block: 'center' });");
            js.AppendLine("                            await new Promise(resolve => setTimeout(resolve, randomDelay(500, 1000)));");
            js.AppendLine("                            await humanClick(btn);  // 使用人类点击模拟");
            js.AppendLine("                            console.log('✅ 已点击 All comments 按钮');");
            js.AppendLine("                            await new Promise(resolve => setTimeout(resolve, randomDelay(2000, 3000)));  // 等待评论加载");
            js.AppendLine("                            return true;");
            js.AppendLine("                        }");
            js.AppendLine("                    }");
            js.AppendLine("                }");
            js.AppendLine("");
            js.AppendLine("                // 如果没找到特定按钮，尝试查找评论区容器");
            js.AppendLine("                const commentContainer = document.querySelector('[data-testid=\"UFI2CommentsRoot\"]') || document.querySelector('[role=\"article\"]');");
            js.AppendLine("                if (commentContainer) {");
            js.AppendLine("                    console.log('⚠️ 未找到 All comments 按钮，但检测到评论区已存在');");
            js.AppendLine("                    return true;");
            js.AppendLine("                }");
            js.AppendLine("");
            js.AppendLine("                console.warn('❌ 未找到评论区元素');");
            js.AppendLine("                return false;");
            js.AppendLine("            } catch (e) {");
            js.AppendLine("                console.error('点击 All comments 失败:', e);");
            js.AppendLine("                return false;");
            js.AppendLine("            }");
            js.AppendLine("        };");
            js.AppendLine("");

            // extractCommentData 函数 - 基于实际HTML结构优化
            js.AppendLine("        // 步骤2: 提取评论数据");
            js.AppendLine("        const extractCommentData = (commentElement) => {");
            js.AppendLine("            try {");
            js.AppendLine("                // 查找用户链接（在 role='article' 内）");
            js.AppendLine("                const authorLinks = commentElement.querySelectorAll('a[href*=\"facebook.com/\"]');");
            js.AppendLine("                let authorLink = null;");
            js.AppendLine("                for (const link of authorLinks) {");
            js.AppendLine("                    // 排除包含 'comment_id' 的链接（这些是评论本身的链接）");
            js.AppendLine("                    if (!link.href.includes('comment_id=')) {");
            js.AppendLine("                        authorLink = link;");
            js.AppendLine("                        break;");
            js.AppendLine("                    }");
            js.AppendLine("                }");
            js.AppendLine("");
            js.AppendLine("                if (!authorLink) return null;");
            js.AppendLine("");
            js.AppendLine("                const url = authorLink.href.split('?')[0];");
            js.AppendLine("                if (seenUserIds.has(url)) return null;  // 去重");
            js.AppendLine("");
            js.AppendLine("                // 提取用户名（从 aria-label 或文本内容）");
            js.AppendLine("                let userName = authorLink.getAttribute('aria-label');");
            js.AppendLine("                if (!userName) {");
            js.AppendLine("                    userName = authorLink.textContent.trim();");
            js.AppendLine("                }");
            js.AppendLine("                // 清理用户名（移除 ', view story' 等后缀）");
            js.AppendLine("                if (userName) {");
            js.AppendLine("                    userName = userName.replace(/,\\s*view\\s+story/i, '').trim();");
            js.AppendLine("                }");
            js.AppendLine("                if (!userName) return null;");
            js.AppendLine("");
            js.AppendLine("                // 提取头像（SVG 内的 image xlink:href）");
            js.AppendLine("                const svgImage = commentElement.querySelector('svg[mask] image[xlink\\:href], svg[mask] img');");
            js.AppendLine("                let avatar = '';");
            js.AppendLine("                if (svgImage) {");
            js.AppendLine("                    avatar = svgImage.getAttribute('xlink:href') || svgImage.getAttribute('src') || '';");
            js.AppendLine("                }");
            js.AppendLine("");
            js.AppendLine("                // 提取评论内容（dir='auto' 的 div）");
            js.AppendLine("                const contentDiv = commentElement.querySelector('div[dir=\"auto\"]');");
            js.AppendLine("                const commentContent = contentDiv ? contentDiv.textContent.trim() : '';");
            js.AppendLine("");
            js.AppendLine("                // 提取评论时间（如 '5w'）");
            js.AppendLine("                const timeLink = commentElement.querySelector('a[role=\"link\"]:not([href*=\"facebook.com/\"])');");
            js.AppendLine("                const commentTime = timeLink ? timeLink.textContent.trim() : '';");
            js.AppendLine("");
            js.AppendLine("                // 提取点赞数（查找 'Like' 按钮旁边的数字）- 仅在配置允许时提取");
            js.AppendLine("                let likeCount = 0;");
            js.AppendLine("                if (COLLECT_LIKE) {");
            js.AppendLine("                    const likeElements = commentElement.querySelectorAll('[aria-label=\"Like\"], [aria-label=\"React\"]');");
            js.AppendLine("                    for (const likeEl of likeElements) {");
            js.AppendLine("                        const parent = likeEl.closest('ul') || likeEl.parentElement;");
            js.AppendLine("                        if (parent) {");
            js.AppendLine("                            const likeText = parent.textContent.match(/\\d+/);");
            js.AppendLine("                            if (likeText) {");
            js.AppendLine("                                likeCount = parseInt(likeText[0]);");
            js.AppendLine("                                break;");
            js.AppendLine("                            }");
            js.AppendLine("                        }");
            js.AppendLine("                    }");
            js.AppendLine("                } else {");
            js.AppendLine("                    console.log('⚠️ 跳过点赞数提取（配置未启用）');");
            js.AppendLine("                }");
            js.AppendLine("");
            js.AppendLine("                // 提取回复数（查找 'Reply' 或 'See translation'）");
            js.AppendLine("                const replyElement = commentElement.querySelector('[role=\"button\"]:has(div):not([aria-label])');");
            js.AppendLine("                let replyCount = 0;");
            js.AppendLine("                if (replyElement) {");
            js.AppendLine("                    const replyText = replyElement.textContent.match(/\\d+/);");
            js.AppendLine("                    replyCount = replyText ? parseInt(replyText[0]) : 0;");
            js.AppendLine("                }");
            js.AppendLine("");
            js.AppendLine("                // 提取用户ID");
            js.AppendLine("                const idMatch = url.match(/[?&]id=(\\d+)/);");
            js.AppendLine("                const fbUserId = idMatch ? idMatch[1] : (url.match(/facebook\\.com\\/([^\\/?]+)/) || [])[1] || '';");
            js.AppendLine("");
            js.AppendLine("                seenUserIds.add(url);");
            js.AppendLine("");
            js.AppendLine("                return {");
            js.AppendLine("                    fbUserId: fbUserId,");
            js.AppendLine("                    userName: userName,");
            js.AppendLine("                    url: url,");
            js.AppendLine("                    avatar: avatar,");
            js.AppendLine("                    followers: likeCount,  // 使用followers字段存储点赞数");
            js.AppendLine("                    profileStatus: commentContent,  // 使用profileStatus存储评论内容");
            js.AppendLine("                    lastPostSummary: commentTime,  // 使用时间字段");
            js.AppendLine("                    fromResource: '帖子评论采集',");
            js.AppendLine("                    config: JSON.stringify({ replyCount: replyCount })  // 存储回复数");
            js.AppendLine("                };");
            js.AppendLine("            } catch (e) {");
            js.AppendLine("                console.error('解析评论数据失败:', e);");
            js.AppendLine("                return null;");
            js.AppendLine("            }");
            js.AppendLine("        };");
            js.AppendLine("");

            // collectComments 主函数 - 基于实际HTML结构优化
            js.AppendLine("        // 步骤3: 采集所有评论");
            js.AppendLine("        const collectComments = () => {");
            js.AppendLine("            // 查找所有评论元素（role='article'）");
            js.AppendLine("            const commentElements = Array.from(document.querySelectorAll('[role=\"article\"]'));");
            js.AppendLine("");
            js.AppendLine("            if (commentElements.length === 0) {");
            js.AppendLine("                console.warn('⚠️ 未找到评论区元素');");
            js.AppendLine("                return false;");
            js.AppendLine("            }");
            js.AppendLine("");
            js.AppendLine("            let newCount = 0;");
            js.AppendLine("            for (const element of commentElements) {");
            js.AppendLine("                if (results.length >= targetCount) break;");
            js.AppendLine("");

            // 根据配置决定是否提取数据
            js.AppendLine("                // 如果只采集点赞，跳过评论提取");
            js.AppendLine("                if (!COLLECT_COMMENT && COLLECT_LIKE) {");
            js.AppendLine("                    // 只提取点赞数，不保存评论数据");
            js.AppendLine("                    const likeElements = element.querySelectorAll('[aria-label=\"Like\"], [aria-label=\"React\"]');");
            js.AppendLine("                    for (const likeEl of likeElements) {");
            js.AppendLine("                        const parent = likeEl.closest('ul') || likeEl.parentElement;");
            js.AppendLine("                        if (parent) {");
            js.AppendLine("                            const likeText = parent.textContent.match(/\\d+/);");
            js.AppendLine("                            if (likeText) {");
            js.AppendLine("                                const likeCount = parseInt(likeText[0]);");
            js.AppendLine("                                if (likeCount > 0) {");
            js.AppendLine("                                    results.push({ fbUserId: 'like_' + likeCount, userName: '点赞用户', url: '', avatar: '', followers: likeCount, profileStatus: '', lastPostSummary: '', fromResource: '帖子点赞采集', config: '{}' });");
            js.AppendLine("                                    newCount++;");
            js.AppendLine("                                    console.log(`✅ 采集到点赞: ${likeCount}个`);");
            js.AppendLine("                                }");
            js.AppendLine("                                break;");
            js.AppendLine("                            }");
            js.AppendLine("                        }");
            js.AppendLine("                    }");
            js.AppendLine("                } else if (COLLECT_COMMENT) {");
            js.AppendLine("                    // 正常提取评论数据");
            js.AppendLine("                    const data = extractCommentData(element);");
            js.AppendLine("                    if (data) {");
            js.AppendLine("                        results.push(data);");
            js.AppendLine("                        newCount++;");
            js.AppendLine("                        console.log(`✅ 采集到评论: ${data.userName} (${data.followers}个赞)`);");
            js.AppendLine("                    }");
            js.AppendLine("                }");
            js.AppendLine("            }");
            js.AppendLine("");
            js.AppendLine("            console.log(`📊 当前已采集: ${results.length}/${targetCount}, 本轮新增: ${newCount}`);");
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

            // 主循环 - 先点击All comments，再滚动采集
            js.AppendLine("        // 步骤4: 主循环");
            js.AppendLine("        const mainLoop = async () => {");
            js.AppendLine("            try {");
            js.AppendLine("                // 第一步：点击 All comments 按钮");
            js.AppendLine("                console.log('🔍 步骤1: 点击 All comments 按钮...');");
            js.AppendLine("                const clicked = await clickAllComments();");
            js.AppendLine("                if (!clicked) {");
            js.AppendLine("                    console.warn('⚠️ 未能点击 All comments，但仍尝试采集...');");
            js.AppendLine("                }");
            js.AppendLine("");
            js.AppendLine("                // 第二步：滚动采集评论");
            js.AppendLine("                console.log('🔍 步骤2: 开始滚动采集评论...');");
            js.AppendLine("                while (results.length < targetCount && scrollCount < maxScrolls) {");
            js.AppendLine("                    const foundNew = collectComments();");
            js.AppendLine("");
            js.AppendLine("                    if (foundNew) {");
            js.AppendLine("                        consecutiveNoNewItems = 0;");
            js.AppendLine("                    } else {");
            js.AppendLine("                        consecutiveNoNewItems++;");
            js.AppendLine("                    }");
            js.AppendLine("");
            js.AppendLine("                    if (consecutiveNoNewItems >= maxConsecutiveNoNew) {");
            js.AppendLine("                        console.log('⚠️ 连续多次未发现新评论，停止采集');");
            js.AppendLine("                        break;");
            js.AppendLine("                    }");
            js.AppendLine("");
            js.AppendLine("                    if (results.length >= targetCount) {");
            js.AppendLine("                        console.log('✅ 已达到目标数量');");
            js.AppendLine("                        break;");
            js.AppendLine("                    }");
            js.AppendLine("");
            js.AppendLine("                    const canScroll = await scrollToLoadMore();");
            js.AppendLine("                    if (!canScroll) {");
            js.AppendLine("                        console.log('⚠️ 无法继续滚动，停止采集');");
            js.AppendLine("                        break;");
            js.AppendLine("                    }");
            js.AppendLine("");
            js.AppendLine("                    scrollCount++;");
            js.AppendLine("                    await randomDelay(1000, 2000);");
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
