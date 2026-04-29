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
            string? cookie = null, string? searchUrl = null, int expectedCount = 100, long? deviceId = null, int taskType = 1)
        {
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
                        await StartAutoCollect(existingBrowser, accountId, searchUrl, expectedCount, taskType);
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
                PersistSessionCookies = true
            };
            
            var requestContext = new RequestContext(requestContextSettings);
            _requestContexts[accountId] = requestContext;
            
            System.Diagnostics.Debug.WriteLine($"🔒 为账号 {accountId} 创建独立缓存: {cachePath}");

            var browser = new ChromiumWebBrowser(initialUrl)
            {
                RequestContext = requestContext  // 使用独立的请求上下文
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
                                isCookieValid = await InjectCookies(browser, accountId, cookie);
                            }

                            // 3. 如果 Cookie 有效且提供了搜索 URL，启动自动化采集
                            if (isCookieValid && !string.IsNullOrEmpty(searchUrl))
                            {
                                await StartAutoCollect(browser, accountId, searchUrl, expectedCount, taskType);
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
                // 执行 JavaScript 检测是否是登录页
                var jsCheckLogin = @"
                    (function() {
                        // 检测1: URL 包含 /login 或 /checkpoint
                        if (window.location.href.includes('/login') || 
                            window.location.href.includes('/checkpoint')) {
                            return true;
                        }
                        
                        // 检测2: 页面中有 Facebook 登录表单的特征元素
                        const loginSelectors = [
                            'form[action*=""/login""]',
                            '#login_form',
                            '[data-testid=""royal_login_form""]',
                            '[data-testid=""login_form""]',
                            'input[name=""email""]',
                            'input[name=""pass""]'
                        ];
                        
                        for (const selector of loginSelectors) {
                            if (document.querySelector(selector)) {
                                return true;
                            }
                        }
                        
                        // 检测3: 检查页面是否有主页特征（动态流、导航等）
                        // 如果 URL 是根路径且没有主页特征，很可能是登录页
                        const mainFeatures = [
                            '[role=""feed""]',
                            '[aria-label=""Create a post""]',
                            '[data-pagelet=""MainFeed""]'
                        ];
                        
                        let hasMainFeature = false;
                        for (const selector of mainFeatures) {
                            if (document.querySelector(selector)) {
                                hasMainFeature = true;
                                break;
                            }
                        }
                        
                        // 如果是根路径且没有主页特征，判定为登录页
                        if (!hasMainFeature && 
                            (window.location.pathname === '/' || window.location.pathname === '')) {
                            return true;
                        }
                        
                        return false;
                    })();
                ";
                
                var result = await browser.EvaluateScriptAsync(jsCheckLogin);
                
                if (result.Success && result.Result != null)
                {
                    return Convert.ToBoolean(result.Result);
                }
                
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

                var cookieManager = Cef.GetGlobalCookieManager();
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

                System.Diagnostics.Debug.WriteLine($"✅ 已为账号注入 {successCount}/{cookieList.Count} 个 Cookie");
                
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
            string searchUrl, int expectedCount, int taskType = 1)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"🚀 开始自动化采集: {searchUrl}");

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

                // 2. 检查是否被重定向到登录页（Cookie 失效）
                string currentUrl = "";
                Application.Current.Dispatcher.Invoke(() =>
                {
                    currentUrl = browser.Address ?? "";
                });
                
                if (string.IsNullOrEmpty(currentUrl))
                {
                    System.Diagnostics.Debug.WriteLine($"❌ 账号 {accountId} 页面加载失败，可能是网络问题");
                    OnCollectionError?.Invoke(accountId, "页面加载失败，请检查网络连接");
                    return;
                }
                
                // 如果导航到搜索页后被重定向回主页或登录页，说明 Cookie 失效
                if (currentUrl == "https://www.facebook.com/" || 
                    currentUrl == "https://www.facebook.com" ||
                    currentUrl.Contains("/checkpoint") ||
                    currentUrl.Contains("/login"))
                {
                    System.Diagnostics.Debug.WriteLine($"❌ 账号 {accountId} Cookie 失效，被重定向到: {currentUrl}");
                    OnCollectionError?.Invoke(accountId, "Cookie已失效，需要重新登录");
                    return;
                }

                // 3. 注入采集脚本(根据任务类型)
                var collectScript = GenerateCollectScript(expectedCount, taskType);
                System.Diagnostics.Debug.WriteLine($"🚀 开始执行采集脚本, 目标数量: {expectedCount}");
                                
                var result = await browser.EvaluateScriptAsync(collectScript);
                
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
        private string GenerateCollectScript(int expectedCount, int taskType = 1)
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
            else // 默认主页采集
            {
                System.Diagnostics.Debug.WriteLine($"⚠️ 使用默认主页采集分支 (taskType={taskType})");
                return GeneratePageCollectScript(expectedCount);
            }
        }

        /// <summary>
        /// 生成主页采集脚本（增强版 - 支持60+种语言）
        /// </summary>
        private string GeneratePageCollectScript(int expectedCount)
        {
            // 从 JSON 文件加载关键词和单位
            var (keywords, units) = LoadFollowerKeywordsAndUnits();
            
            // 使用 StringBuilder 构建 JavaScript，避免转义问题
            var js = new System.Text.StringBuilder();
                    
            js.AppendLine("(function() {");
            js.AppendLine("    return new Promise((resolve, reject) => {");
            js.AppendLine("        const results = [];");
            js.AppendLine($"        const targetCount = {expectedCount};");
            js.AppendLine("        const seenUrls = new Set();");
            js.AppendLine("");
            js.AppendLine("        let scrollCount = 0;");
            js.AppendLine("        const maxScrolls = 50;");
            js.AppendLine("        let consecutiveNoNewItems = 0;");
            js.AppendLine("        const maxConsecutiveNoNew = 5;");
            js.AppendLine("");
            js.AppendLine("        const randomDelay = (min, max) => {");
            js.AppendLine("            return Math.floor(Math.random() * (max - min + 1)) + min;");
            js.AppendLine("        };");
            js.AppendLine("");
            
            // 添加贝塞尔曲线鼠标轨迹模拟函数
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
            js.AppendLine("                const event = new MouseEvent('mousemove', { clientX: Math.floor(x), clientY: Math.floor(y), bubbles: true });");
            js.AppendLine("                document.dispatchEvent(event);");
            js.AppendLine("                await new Promise(resolve => setTimeout(resolve, randomDelay(20, 50)));");
            js.AppendLine("            }");
            js.AppendLine("        };");
            js.AppendLine("");
                    
            // 从 JSON 加载的多语言关键词数组
            js.AppendLine($"        const FOLLOWER_KEYWORDS = {keywords};");
            js.AppendLine($"        const FOLLOWER_UNITS = {units};");
            js.AppendLine("");
                    
            // extractCardData 函数
            js.AppendLine("        const extractCardData = (card) => {");
            js.AppendLine("            try {");
            js.AppendLine("                const nameLinkEl = card.querySelector('a[aria-hidden=\"true\"]');");
            js.AppendLine("                if (!nameLinkEl) return null;");
            js.AppendLine("");
            js.AppendLine("                const url = nameLinkEl.href;");
            js.AppendLine("                if (!url || seenUrls.has(url)) return null;");
            js.AppendLine("");
            js.AppendLine("                const name = nameLinkEl.textContent.trim();");
            js.AppendLine("                if (!name) return null;");
            js.AppendLine("");
            js.AppendLine("                // 清理名称中的'已验证'等标记");
            js.AppendLine("                const cleanName = name.replace(/\\s*(Akun Terverifikasi|Verified|Compte certifié)/gi, '').trim();");
            js.AppendLine("                // 检测名称中是否包含'已验证'标记");
            js.AppendLine("                const isVerifiedInName = /akun terverifikasi|verified|compte certifi/i.test(name);");
            js.AppendLine("");

            js.AppendLine("                const avatarLinkEl = card.querySelector('a[aria-label*=\"profil\"]') ||");
            js.AppendLine("                                    card.querySelector('a[aria-label*=\"photo\"]');");
            js.AppendLine("                ");
            js.AppendLine("                let avatar = '';");
            js.AppendLine("                if (avatarLinkEl) {");
            js.AppendLine("                    const imgEl = avatarLinkEl.querySelector('image') || avatarLinkEl.querySelector('img');");
            js.AppendLine("                    if (imgEl) {");
            js.AppendLine("                        avatar = imgEl.getAttribute('xlink:href') || imgEl.src || '';");
            js.AppendLine("                    }");
            js.AppendLine("                }");
            js.AppendLine("");
            js.AppendLine("                const allSpans = Array.from(card.querySelectorAll('span[dir=\"auto\"]'));");
            js.AppendLine("                ");
            js.AppendLine("                let followers = '';");
            js.AppendLine("                let category = '';");
            js.AppendLine("                let snippet = '';");
            js.AppendLine("                ");
            js.AppendLine("                // 遍历所有 span，查找包含粉丝数的文本");
            js.AppendLine("                for (const span of allSpans) {");
            js.AppendLine("                    const text = span.textContent.trim();");
            js.AppendLine("                    if (!text) continue;");
            js.AppendLine("                    ");
            js.AppendLine("                    // 检查是否包含粉丝数关键词");
            js.AppendLine("                    const keywordsPattern = FOLLOWER_KEYWORDS.join('|');");
            js.AppendLine("                    const unitsPattern = FOLLOWER_UNITS.join('|');");
            js.AppendLine("                    // 支持多种格式：");
            js.AppendLine("                    // 1. 数字 + 可选单位(k/M/rb/jt等) + 关键词");
            js.AppendLine("                    // 2. 关键词 + 数字 + 可选单位");
            js.AppendLine("                    const followerRegex = new RegExp(");
            js.AppendLine("                        '([\\d]+[\\.,]?\\d*)[\\s]*(?:' + unitsPattern + ')?[\\s]*(?:' + keywordsPattern + ')|(?:' + keywordsPattern + ')[\\s:]*([\\d]+[\\.,]?\\d*)[\\s]*(?:' + unitsPattern + ')?',");
            js.AppendLine("                        'i'");
            js.AppendLine("                    );");
            js.AppendLine("                    ");
            js.AppendLine("                    const followerMatch = text.match(followerRegex);");
            js.AppendLine("                    if (followerMatch) {");
            js.AppendLine("                        // 提取数字部分（Group 1 或 Group 2）");
            js.AppendLine("                        let numberPart = followerMatch[1] || followerMatch[2] || '';");
            js.AppendLine("                        ");
            js.AppendLine("                        // 验证是否为有效数字");
            js.AppendLine("                        if (numberPart && /^\\d+[\\.,]?\\d*$/.test(numberPart)) {");
            js.AppendLine("                            // 从完整匹配中提取单位（如果有）");
            js.AppendLine("                            const fullMatch = followerMatch[0];");
            js.AppendLine("                            const unitRegex = new RegExp('(?:^|[\\s])(' + unitsPattern + ')(?:[\\s]|$)', 'i');");
            js.AppendLine("                            const unitMatch = fullMatch.match(unitRegex);");
            js.AppendLine("                            followers = numberPart + (unitMatch ? unitMatch[1] : '');");
            js.AppendLine("                            // 粉丝数之前的部分是类别");
            js.AppendLine("                            const beforeFollowers = text.substring(0, text.indexOf(followerMatch[0])).trim();");
            js.AppendLine("                            if (beforeFollowers) {");
            js.AppendLine("                                category = beforeFollowers.split('·')[0].trim();");
            js.AppendLine("                            }");
            js.AppendLine("                            break;");
            js.AppendLine("                        }");
            js.AppendLine("                    }");
            js.AppendLine("                }");
            js.AppendLine("                ");
            js.AppendLine("                // 如果没有找到粉丝数，尝试从第2个span提取类别");
            js.AppendLine("                if (!category && allSpans.length >= 2) {");
            js.AppendLine("                    const infoText = allSpans[1].textContent.trim();");
            js.AppendLine("                    const categoryMatch = infoText.match(/^([^·]+)/);");
            js.AppendLine("                    if (categoryMatch) {");
            js.AppendLine("                        category = categoryMatch[1].trim();");
            js.AppendLine("                    }");
            js.AppendLine("                }");
            js.AppendLine("                ");
            js.AppendLine("                // 提取简介（最后一个长文本）");
            js.AppendLine("                if (allSpans.length >= 3) {");
            js.AppendLine("                    snippet = allSpans[allSpans.length - 1].textContent.trim().substring(0, 200);");
            js.AppendLine("                }");
            js.AppendLine("");
            js.AppendLine("                // 检查是否已验证（多种方式）");
            js.AppendLine("                const isVerified = isVerifiedInName ||");
            js.AppendLine("                                  card.querySelector('[aria-label*=\"Verified\"]') !== null ||");
            js.AppendLine("                                  card.querySelector('[aria-label*=\"verifi\"]') !== null ||");
            js.AppendLine("                                  card.querySelector('svg[title*=\"Verified\"]') !== null ||");
            js.AppendLine("                                  card.querySelector('svg[title*=\"Terverifikasi\"]') !== null;");
            js.AppendLine("");
            js.AppendLine("                const idMatch = url.match(/[?&]id=(\\d+)/);");
            js.AppendLine("                const id = idMatch ? idMatch[1] : (url.match(/facebook\\.com\\/([^\\/?]+)/) || [])[1] || '';");
            js.AppendLine("");
            js.AppendLine("                seenUrls.add(url);");
            js.AppendLine("");
            js.AppendLine("                return {");
            js.AppendLine("                    id: id,");
            js.AppendLine("                    name: cleanName,");
            js.AppendLine("                    url: url,");
            js.AppendLine("                    avatar: avatar,");
            js.AppendLine("                    followers: followers,");
            js.AppendLine("                    category: category,");
            js.AppendLine("                    snippet: snippet,");
            js.AppendLine("                    isVerified: isVerified,");
            js.AppendLine("                    collectedAt: new Date().toISOString()");
            js.AppendLine("                };");
            js.AppendLine("            } catch (e) {");
            js.AppendLine("                console.warn('Extract failed:', e);");
            js.AppendLine("                return null;");
            js.AppendLine("            }");
            js.AppendLine("        };");
            js.AppendLine("");
                    
            // 主循环
            js.AppendLine("        const interval = setInterval(() => {");
            js.AppendLine("            try {");
            js.AppendLine("                const cards = document.querySelectorAll('[role=\"article\"]');");
            js.AppendLine("                let newItemsFound = 0;");
            js.AppendLine("");
            js.AppendLine("                cards.forEach(card => {");
            js.AppendLine("                    if (results.length >= targetCount) return;");
            js.AppendLine("");
            js.AppendLine("                    const data = extractCardData(card);");
            js.AppendLine("                    if (data) {");
            js.AppendLine("                        results.push(data);");
            js.AppendLine("                        newItemsFound++;");
            js.AppendLine("                    }");
            js.AppendLine("                });");
            js.AppendLine("");
            js.AppendLine("                if (newItemsFound > 0) {");
            js.AppendLine("                    consecutiveNoNewItems = 0;");
            js.AppendLine("                } else {");
            js.AppendLine("                    consecutiveNoNewItems++;");
            js.AppendLine("                }");
            js.AppendLine("");
            js.AppendLine("                if (results.length >= targetCount) {");
            js.AppendLine("                    clearInterval(interval);");
            js.AppendLine("                    console.log('Collection complete: ' + results.length + '/' + targetCount);");
            js.AppendLine("                    resolve(JSON.stringify(results.slice(0, targetCount)));");
            js.AppendLine("                    return;");
            js.AppendLine("                }");
            js.AppendLine("");
            js.AppendLine("                if (consecutiveNoNewItems >= maxConsecutiveNoNew || scrollCount >= maxScrolls) {");
            js.AppendLine("                    clearInterval(interval);");
            js.AppendLine("                    console.log('Collection ended: ' + results.length + ' items');");
            js.AppendLine("                    resolve(JSON.stringify(results));");
            js.AppendLine("                    return;");
            js.AppendLine("                }");
            js.AppendLine("");
            js.AppendLine("                const scrollDistance = randomDelay(600, 1000);");
            js.AppendLine("                window.scrollBy({ top: scrollDistance, behavior: 'smooth' });");
            js.AppendLine("                scrollCount++;");
            js.AppendLine("");
            js.AppendLine("                const nextDelay = randomDelay(1500, 3000);");
            js.AppendLine("                clearInterval(interval);");
            js.AppendLine("                setTimeout(() => {");
            js.AppendLine("                    interval = setInterval(arguments.callee, 2000);");
            js.AppendLine("                }, nextDelay);");
            js.AppendLine("");
            js.AppendLine("            } catch (e) {");
            js.AppendLine("                console.error('Collection error:', e);");
            js.AppendLine("            }");
            js.AppendLine("        }, 2000);");
            js.AppendLine("");
                    
            // 超时保护
            js.AppendLine("        setTimeout(() => {");
            js.AppendLine("            clearInterval(interval);");
            js.AppendLine("            if (results.length > 0) {");
            js.AppendLine("                console.log('Timeout: returning ' + results.length + ' items');");
            js.AppendLine("                resolve(JSON.stringify(results));");
            js.AppendLine("            } else {");
            js.AppendLine("                reject(new Error('Collection timeout with no data'));");
            js.AppendLine("            }");
            js.AppendLine("        }, 300000);");
            js.AppendLine("    });");
            js.AppendLine("})();");
                    
            return js.ToString();
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
        /// 生成用户采集脚本
        /// </summary>
        private string GenerateUserCollectScript(int expectedCount)
        {
            var js = new System.Text.StringBuilder();
                    
            js.AppendLine("(function() {");
            js.AppendLine("    return new Promise((resolve, reject) => {");
            js.AppendLine("        const results = [];");
            js.AppendLine($"        const targetCount = {expectedCount};");
            js.AppendLine("        const seenUrls = new Set();");
            js.AppendLine("");
            js.AppendLine("        let scrollCount = 0;");
            js.AppendLine("        const maxScrolls = 50;");
            js.AppendLine("        let consecutiveNoNewItems = 0;");
            js.AppendLine("        const maxConsecutiveNoNew = 5;");
            js.AppendLine("");
            js.AppendLine("        const randomDelay = (min, max) => {");
            js.AppendLine("            return Math.floor(Math.random() * (max - min + 1)) + min;");
            js.AppendLine("        };");
            js.AppendLine("");
            
            // 添加贝塞尔曲线鼠标轨迹模拟函数
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
            js.AppendLine("                const event = new MouseEvent('mousemove', { clientX: Math.floor(x), clientY: Math.floor(y), bubbles: true });");
            js.AppendLine("                document.dispatchEvent(event);");
            js.AppendLine("                await new Promise(resolve => setTimeout(resolve, randomDelay(20, 50)));");
            js.AppendLine("            }");
            js.AppendLine("        };");
            js.AppendLine("");
            
            // extractUserCardData 函数 - 专门解析用户卡片
            js.AppendLine("        const extractUserCardData = (card) => {");
            js.AppendLine("            try {");
            // 提取用户名链接
            js.AppendLine("                const nameLinkEl = card.querySelector('a[aria-hidden=\"true\"]');");
            js.AppendLine("                if (!nameLinkEl) return null;");
            js.AppendLine("");
            js.AppendLine("                const url = nameLinkEl.href;");
            js.AppendLine("                if (!url || seenUrls.has(url)) return null;");
            js.AppendLine("");
            js.AppendLine("                const name = nameLinkEl.textContent.trim();");
            js.AppendLine("                if (!name) return null;");
            js.AppendLine("");
            // 清理名称中的'已验证'标记
            js.AppendLine("                const cleanName = name.replace(/\\s*(Akun Terverifikasi|Verified|Compte certifié)/gi, '').trim();");
            js.AppendLine("                const isVerifiedInName = /akun terverifikasi|verified|compte certifi/i.test(name);");
            js.AppendLine("");
            // 提取头像
            js.AppendLine("                const avatarLinkEl = card.querySelector('a[aria-label*=\"profil\"]') ||");
            js.AppendLine("                                    card.querySelector('a[aria-label*=\"photo\"]');");
            js.AppendLine("                ");
            js.AppendLine("                let avatar = '';");
            js.AppendLine("                if (avatarLinkEl) {");
            js.AppendLine("                    const imgEl = avatarLinkEl.querySelector('image') || avatarLinkEl.querySelector('img');");
            js.AppendLine("                    if (imgEl) {");
            js.AppendLine("                        avatar = imgEl.getAttribute('xlink:href') || imgEl.src || '';");
            js.AppendLine("                    }");
            js.AppendLine("                }");
            js.AppendLine("");
            // 提取所有span元素
            js.AppendLine("                const allSpans = Array.from(card.querySelectorAll('span[dir=\"auto\"]'));");
            js.AppendLine("                ");
            js.AppendLine("                let followers = '';");
            js.AppendLine("                let location = '';");
            js.AppendLine("                let bio = '';");
            js.AppendLine("                let category = '';");
            js.AppendLine("                ");
            // 遍历所有span，查找粉丝数、位置、简介等信息
            js.AppendLine("                for (let i = 0; i < allSpans.length; i++) {");
            js.AppendLine("                    const span = allSpans[i];");
            js.AppendLine("                    const text = span.textContent.trim();");
            js.AppendLine("                    if (!text) continue;");
            js.AppendLine("                    ");
            // 检查是否包含粉丝数关键词（支持多种语言）
            js.AppendLine("                    const followerPattern = /(\\d+[\\.,]?\\d*)\\s*(rb|ribu|jt|juta|k|m|b|t|pengikut|followers|follower|abonnes|seguidores|fans|千|万|百万|千万|亿)/i;");
            js.AppendLine("                    const followerMatch = text.match(followerPattern);");
            js.AppendLine("                    if (followerMatch && !followers) {");
            js.AppendLine("                        followers = followerMatch[0].replace(/&nbsp;/g, ' ').trim();");
            js.AppendLine("                        continue;");
            js.AppendLine("                    }");
            js.AppendLine("                    ");
            // 提取位置信息（包含“Tinggal di”、“@”等关键词）
            js.AppendLine("                    if ((text.includes('Tinggal di') || text.includes('@')) && !location) {");
            js.AppendLine("                        location = text;");
            js.AppendLine("                        continue;");
            js.AppendLine("                    }");
            js.AppendLine("                    ");
            // 提取职业/类别（Kreator digital、Marketing Specialist等）
            js.AppendLine("                    if ((text.includes('Kreator digital') || text.includes('di PT.') || text.includes('Founder') || text.includes('Blogger') || text.includes('Tokoh Publik')) && !category) {");
            js.AppendLine("                        category = text.split('·')[0].trim();");
            js.AppendLine("                        continue;");
            js.AppendLine("                    }");
            js.AppendLine("                    ");
            // 提取简介（较长的文本，通常是最后一个span）
            js.AppendLine("                    if (text.length > 20 && !bio && i >= allSpans.length - 2) {");
            js.AppendLine("                        bio = text.substring(0, 200);");
            js.AppendLine("                    }");
            js.AppendLine("                }");
            js.AppendLine("                ");
            // 检查是否已验证
            js.AppendLine("                const isVerified = isVerifiedInName ||");
            js.AppendLine("                                  card.querySelector('[aria-label*=\"Verified\"]') !== null ||");
            js.AppendLine("                                  card.querySelector('[aria-label*=\"verifi\"]') !== null ||");
            js.AppendLine("                                  card.querySelector('svg[title*=\"Verified\"]') !== null ||");
            js.AppendLine("                                  card.querySelector('svg[title*=\"Terverifikasi\"]') !== null;");
            js.AppendLine("");
            // 提取Facebook ID
            js.AppendLine("                const idMatch = url.match(/[?&]id=(\\d+)/);");
            js.AppendLine("                const id = idMatch ? idMatch[1] : (url.match(/facebook\\.com\\/([^\\/?]+)/) || [])[1] || '';");
            js.AppendLine("");
            js.AppendLine("                seenUrls.add(url);");
            js.AppendLine("");
            // 返回结果对象
            js.AppendLine("                return {");
            js.AppendLine("                    fbUserId: id,");
            js.AppendLine("                    userName: cleanName,");
            js.AppendLine("                    url: url,");
            js.AppendLine("                    avatar: avatar,");
            js.AppendLine("                    followers: followers,");
            js.AppendLine("                    city: location,");
            js.AppendLine("                    bio: bio || category,");
            js.AppendLine("                    isVerified: isVerified,");
            js.AppendLine("                    collectedAt: new Date().toISOString()");
            js.AppendLine("                };");
            js.AppendLine("            } catch (e) {");
            js.AppendLine("                console.warn('Extract user failed:', e);");
            js.AppendLine("                return null;");
            js.AppendLine("            }");
            js.AppendLine("        };");
            js.AppendLine("");
                    
            // 主循环 - 滚动加载
            js.AppendLine("        const interval = setInterval(() => {");
            js.AppendLine("            try {");
            js.AppendLine("                const cards = document.querySelectorAll('[role=\"article\"]');");
            js.AppendLine("                let newItemsFound = 0;");
            js.AppendLine("");
            js.AppendLine("                cards.forEach(card => {");
            js.AppendLine("                    if (results.length >= targetCount) return;");
            js.AppendLine("");
            js.AppendLine("                    const data = extractUserCardData(card);");
            js.AppendLine("                    if (data) {");
            js.AppendLine("                        results.push(data);");
            js.AppendLine("                        newItemsFound++;");
            js.AppendLine("                    }");
            js.AppendLine("                });");
            js.AppendLine("");
            js.AppendLine("                if (newItemsFound > 0) {");
            js.AppendLine("                    consecutiveNoNewItems = 0;");
            js.AppendLine("                } else {");
            js.AppendLine("                    consecutiveNoNewItems++;");
            js.AppendLine("                }");
            js.AppendLine("");
            js.AppendLine("                if (results.length >= targetCount) {");
            js.AppendLine("                    clearInterval(interval);");
            js.AppendLine("                    console.log('User collection complete: ' + results.length + '/' + targetCount);");
            js.AppendLine("                    resolve(JSON.stringify(results.slice(0, targetCount)));");
            js.AppendLine("                    return;");
            js.AppendLine("                }");
            js.AppendLine("");
            js.AppendLine("                if (consecutiveNoNewItems >= maxConsecutiveNoNew || scrollCount >= maxScrolls) {");
            js.AppendLine("                    clearInterval(interval);");
            js.AppendLine("                    console.log('User collection ended: ' + results.length + ' items');");
            js.AppendLine("                    resolve(JSON.stringify(results));");
            js.AppendLine("                    return;");
            js.AppendLine("                }");
            js.AppendLine("");
            js.AppendLine("                const scrollDistance = randomDelay(600, 1000);");
            js.AppendLine("                window.scrollBy({ top: scrollDistance, behavior: 'smooth' });");
            js.AppendLine("                scrollCount++;");
            js.AppendLine("");
            js.AppendLine("                const nextDelay = randomDelay(1500, 3000);");
            js.AppendLine("                clearInterval(interval);");
            js.AppendLine("                setTimeout(() => {");
            js.AppendLine("                    interval = setInterval(arguments.callee, 2000);");
            js.AppendLine("                }, nextDelay);");
            js.AppendLine("");
            js.AppendLine("            } catch (e) {");
            js.AppendLine("                console.error('User collection error:', e);");
            js.AppendLine("            }");
            js.AppendLine("        }, 2000);");
            js.AppendLine("");
                    
            // 超时保护（5分钟）
            js.AppendLine("        setTimeout(() => {");
            js.AppendLine("            clearInterval(interval);");
            js.AppendLine("            if (results.length > 0) {");
            js.AppendLine("                console.log('Timeout: returning ' + results.length + ' users');");
            js.AppendLine("                resolve(JSON.stringify(results));");
            js.AppendLine("            } else {");
            js.AppendLine("                reject(new Error('Collection timeout with no data'));");
            js.AppendLine("            }");
            js.AppendLine("        }, 300000);");
            js.AppendLine("    });");
            js.AppendLine("})();");

            return js.ToString();
        }

        /// <summary>
        /// 生成帖子采集脚本
        /// </summary>
        private string GeneratePostCollectScript(int expectedCount)
        {
            var js = new System.Text.StringBuilder();
            
            js.AppendLine("(function() {");
            js.AppendLine("    return new Promise((resolve, reject) => {");
            js.AppendLine("        const results = [];");
            js.AppendLine($"        const targetCount = {expectedCount};");
            js.AppendLine("        const seenUrls = new Set();");
            js.AppendLine("");
            js.AppendLine("        let scrollCount = 0;");
            js.AppendLine($"        const maxScrolls = {Math.Max(expectedCount * 3, 10)}; // 根据期望数量动态计算,最少10次");
            js.AppendLine("        let consecutiveNoNewItems = 0;");
            js.AppendLine("        const maxConsecutiveNoNew = 5; // 连续5次无新数据才停止,给懒加载更多时间");
            js.AppendLine("        let lastCardCount = 0;");
            js.AppendLine("");
            js.AppendLine("        console.log('[采集开始] 目标数量:', targetCount);");
            js.AppendLine("");
            js.AppendLine("        const randomDelay = (min, max) => {");
            js.AppendLine("            return Math.floor(Math.random() * (max - min + 1)) + min;");
            js.AppendLine("        };");
            js.AppendLine("");
            
            // 添加贝塞尔曲线鼠标轨迹模拟函数
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
            js.AppendLine("                const event = new MouseEvent('mousemove', { clientX: Math.floor(x), clientY: Math.floor(y), bubbles: true });");
            js.AppendLine("                document.dispatchEvent(event);");
            js.AppendLine("                await new Promise(resolve => setTimeout(resolve, randomDelay(20, 50)));");
            js.AppendLine("            }");
            js.AppendLine("        };");
            js.AppendLine("");
            
            // 提取帖子数据的函数
            js.AppendLine("        const extractPostData = (card) => {");
            js.AppendLine("            try {");
            js.AppendLine("                // 预检查: 过滤掉明显不是帖子的卡片");
            js.AppendLine("                // 1. 检查是否有帖子内容区域");
            js.AppendLine("                const hasContent = card.querySelector('[data-ad-comet-preview=\"message\"]') ||")
            .AppendLine("                                  card.querySelector('[data-testid=\"post_message\"]') ||")
            .AppendLine("                                  card.querySelector('span[dir=\"auto\"]');");
            js.AppendLine("                if (!hasContent) {");
            js.AppendLine("                    console.log('[跳过卡片] 无帖子内容区域');");
            js.AppendLine("                    return null;");
            js.AppendLine("                }");
            js.AppendLine("");
            js.AppendLine("                // 2. 排除纯头像/故事卡片(通常只有SVG和图片)");
            js.AppendLine("                const svgCount = card.querySelectorAll('svg').length;");
            js.AppendLine("                const imgCount = card.querySelectorAll('img').length;");
            js.AppendLine("                const linkCount = card.querySelectorAll('a').length;");
            js.AppendLine("                if (svgCount > 0 && imgCount > 0 && linkCount < 3) {");
            js.AppendLine("                    console.log('[跳过卡片] 可能是头像/故事卡片');");
            js.AppendLine("                    return null;");
            js.AppendLine("                }");
            js.AppendLine("");
            js.AppendLine("                // 提取帖子链接 - 支持多种URL格式(posts/permalink/photos/videos/story/search)");
            js.AppendLine("                // 策略1: 查找包含帖子相关路径的链接");
            js.AppendLine("                let postLinkEl = card.querySelector('a[href*=\"/posts/\"]') ||")
            .AppendLine("                                  card.querySelector('a[href*=\"/permalink/\"]') ||")
            .AppendLine("                                  card.querySelector('a[href*=\"/photos/\"]') ||")
            .AppendLine("                                  card.querySelector('a[href*=\"/videos/\"]') ||")
            .AppendLine("                                  card.querySelector('a[href*=\"story.php\"]') ||")
            .AppendLine("                                  card.querySelector('a[href*=\"/search/posts/\"]');"); // 搜索结果页面的帖子链接
            js.AppendLine("");
            js.AppendLine("                // 策略2: 如果没找到,尝试查找时间戳链接(通常是帖子的永久链接)");
            js.AppendLine("                if (!postLinkEl) {");
            js.AppendLine("                    const timeLinks = card.querySelectorAll('a[href*=\"facebook.com\"]');");
            js.AppendLine("                    for (const link of timeLinks) {");
            js.AppendLine("                        const href = link.href;");
            js.AppendLine("                        // 排除头像、故事、个人主页等非帖子链接");
            js.AppendLine("                        if (href.includes('/stories/') || href.includes('/profile.php') || ")
            .AppendLine("                            href.includes('/groups/') || !href.includes('?')) continue;");
            js.AppendLine("                        // 检查是否有帖子相关的查询参数");
            js.AppendLine("                        if (href.includes('fbid=') || href.includes('story_fbid=') || ")
            .AppendLine("                            href.includes('id=') && href.match(/\\d{15,}/)) {");
            js.AppendLine("                            postLinkEl = link;");
            js.AppendLine("                            break;");
            js.AppendLine("                        }");
            js.AppendLine("                    }");
            js.AppendLine("                }");
            js.AppendLine("");
            js.AppendLine("                // 策略3: 查找带有data-ft属性的链接(Facebook内部追踪属性,通常用于帖子)");
            js.AppendLine("                if (!postLinkEl) {");
            js.AppendLine("                    postLinkEl = card.querySelector('a[data-ft]');");
            js.AppendLine("                }");
            js.AppendLine("");
            js.AppendLine("                if (!postLinkEl) {");
            js.AppendLine("                    // 输出卡片的HTML结构用于调试");
            js.AppendLine("                    console.warn('[提取失败] 未找到帖子链接, 卡片HTML:', card.innerHTML.substring(0, 500));");
            js.AppendLine("                    return null;");
            js.AppendLine("                }");
            js.AppendLine("");
            js.AppendLine("                const url = postLinkEl.href.split('?')[0]; // 移除查询参数");
            js.AppendLine("                if (!url || seenUrls.has(url)) return null;");
            js.AppendLine("");
            js.AppendLine("                // 提取发帖人 - 多语言支持");
            js.AppendLine("                // 优先匹配有文本内容的链接,避免匹配到只有图片的头像链接");
            js.AppendLine("                const authorEl = card.querySelector('[data-ad-rendering-role=\"profile_name\"] a') ||")
            .AppendLine("                               card.querySelector('h3 a[href*=\"facebook.com\"]:not([href*=\"/groups/\"])') ||")
            .AppendLine("                               card.querySelector('strong a[href*=\"facebook.com\"]:not([href*=\"/groups/\"])') ||")
            .AppendLine("                               card.querySelector('a[aria-label]:not([href*=\"/groups/\"]):not([href*=\"/hashtag/\"])') ||")
            .AppendLine("                               card.querySelector('span[dir=\"auto\"] strong');");
            js.AppendLine("                const postUser = authorEl ? authorEl.textContent.trim() : '';");
            js.AppendLine("");
            js.AppendLine("                // 提取群组名称 - 优先使用h3中的群组链接");
            js.AppendLine("                const groupLinkEl = card.querySelector('h3 a[href*=\"/groups/\"]') ||")
            .AppendLine("                               card.querySelector('[data-ad-rendering-role=\"profile_name\"] a[href*=\"/groups/\"]:not([href*=\"/user/\"])');");
            js.AppendLine("                const groupName = groupLinkEl ? groupLinkEl.textContent.trim() : '';");
            js.AppendLine("");
            js.AppendLine("                // 提取帖子内容 - 尝试多种选择器");
            js.AppendLine("                const contentEl = card.querySelector('[data-ad-comet-preview=\"message\"]') ||")
            .AppendLine("                                card.querySelector('[data-testid=\"post_message\"]') ||")
            .AppendLine("                                card.querySelector('span[dir=\"auto\"]');");
            js.AppendLine("                const postContent = contentEl ? contentEl.textContent.trim() : '';");
            js.AppendLine("");
            js.AppendLine("                // 提取点赞数、评论数、转发数（返回原始字符串，由前端解析）");
            js.AppendLine("                let reactionCount = '';");
            js.AppendLine("                let commentCount = '';");
            js.AppendLine("                let reshareCount = '';");
            js.AppendLine("");
            js.AppendLine("                // 查找所有数字span");
            js.AppendLine("                const numberSpans = Array.from(card.querySelectorAll('span[dir=\"auto\"]'));");
            js.AppendLine("                for (const span of numberSpans) {");
            js.AppendLine("                    const text = span.textContent.trim();");
            js.AppendLine("                    if (!text || !/^[\\d]/.test(text)) continue;");
            js.AppendLine("");
            js.AppendLine("                    // 提取带单位的原始字符串（如 \"1.5K\", \"27\", \"48\"）");
            js.AppendLine("                    const numMatch = text.match(/^([\\d]+[\\.,]?\\d*\\s*[kKmMrbjtRBJT]*)/);");
            js.AppendLine("                    if (!numMatch) continue;");
            js.AppendLine("");
            js.AppendLine("                    const rawValue = numMatch[1].trim();");
            js.AppendLine("");
            js.AppendLine("                    // 根据上下文判断是哪个计数");
            js.AppendLine("                    const parentText = span.parentElement?.textContent || '';");
            js.AppendLine("                    if (parentText.includes('komentar') || parentText.includes('comment')) {");
            js.AppendLine("                        commentCount = rawValue;");
            js.AppendLine("                    } else if (parentText.includes('bagikan') || parentText.includes('share')) {");
            js.AppendLine("                        reshareCount = rawValue;");
            js.AppendLine("                    } else if (parentText.includes('suka') || parentText.includes('like') || parentText.includes('reaksi')) {");
            js.AppendLine("                        reactionCount = rawValue;");
            js.AppendLine("                    }");
            js.AppendLine("                }");
            js.AppendLine("");
            js.AppendLine("                // 提取帖子ID - 支持多种URL格式(posts/permalink/photos/videos)");
            js.AppendLine("                let itemIdMatch = url.match(/(?:posts|permalink|photos|videos)\\/([^\\/?]+)/);");
            js.AppendLine("                if (!itemIdMatch) {");
            js.AppendLine("                    // 尝试从视频URL提取: /videos/pcb.{postId}/{videoId}");
            js.AppendLine("                    itemIdMatch = url.match(/pcb\\.([0-9]+)/);");
            js.AppendLine("                }");
            js.AppendLine("                const itemId = itemIdMatch ? itemIdMatch[1] : '';");
            js.AppendLine("");
            js.AppendLine("                seenUrls.add(url);");
            js.AppendLine("");
            js.AppendLine("                return {");
            js.AppendLine("                    itemId: itemId,");
            js.AppendLine("                    postUser: postUser,");
            js.AppendLine("                    url: url,");
            js.AppendLine("                    fromResource: groupName ? 'group' : 'page',");
            js.AppendLine("                    groupName: groupName,");
            js.AppendLine("                    reshareCount: reshareCount,");
            js.AppendLine("                    commentCount: commentCount,");
            js.AppendLine("                    reactionCount: reactionCount,");
            js.AppendLine("                    usedCount: 0,");
            js.AppendLine("                    postContent: postContent,");
            js.AppendLine("                    fbAccount: '',");
            js.AppendLine("                    postCreateTime: new Date().toISOString()");
            js.AppendLine("                };");
            js.AppendLine("            } catch (e) {");
            js.AppendLine("                console.warn('Extract post failed:', e);");
            js.AppendLine("                return null;");
            js.AppendLine("            }");
            js.AppendLine("        };");
            js.AppendLine("");
            
            // 主循环 - 滚动加载
            js.AppendLine("        let isCompleted = false; // 防止重复resolve");
            js.AppendLine("        const doScroll = async () => {");
            js.AppendLine("            if (isCompleted) return; // 已经完成,不再执行");
            js.AppendLine("            try {");
            js.AppendLine("                const cards = document.querySelectorAll('[role=\"article\"]');");
            js.AppendLine("                console.log('[滚动检查] 当前卡片数:', cards.length, ', 已采集:', results.length, ', 滚动次数:', scrollCount);");
            js.AppendLine("");
            js.AppendLine("                // 如果卡片数量没有变化,说明页面可能已经到底了");
            js.AppendLine("                if (cards.length === lastCardCount && cards.length > 0) {");
            js.AppendLine("                    consecutiveNoNewItems++;");
            js.AppendLine("                    console.log('[警告] 卡片数量未变化,连续次数:', consecutiveNoNewItems);");
            js.AppendLine("                } else {");
            js.AppendLine("                    lastCardCount = cards.length;");
            js.AppendLine("                    consecutiveNoNewItems = 0;");
            js.AppendLine("                }");
            js.AppendLine("");
            js.AppendLine("                let newItemsFound = 0;");
            js.AppendLine("                for (let i = 0; i < cards.length && results.length < targetCount; i++) {");
            js.AppendLine("                    const card = cards[i];");
            js.AppendLine("");
            js.AppendLine("                    const data = extractPostData(card);");
            js.AppendLine("                    if (data) {");
            js.AppendLine("                        results.push(data);");
            js.AppendLine("                        newItemsFound++;");
            js.AppendLine("                        console.log('[成功提取] URL:', data.url.substring(0, 80));");
            js.AppendLine("                    }");
            js.AppendLine("                }");
            js.AppendLine("");
            js.AppendLine("                console.log('[本轮结果] 新发现:', newItemsFound, ', 总计:', results.length);");
            js.AppendLine("");
            js.AppendLine("                // 达到目标数量,立即停止");
            js.AppendLine("                if (results.length >= targetCount) {");
            js.AppendLine("                    isCompleted = true;");
            js.AppendLine("                    console.log('[采集完成] 达到目标数量:', results.length);");
            js.AppendLine("                    resolve(JSON.stringify(results.slice(0, targetCount)));");
            js.AppendLine("                    return;");
            js.AppendLine("                }");
            js.AppendLine("");
            js.AppendLine("                // 连续多次无新数据或达到最大滚动次数,停止采集");
            js.AppendLine("                console.log('[停止检查] scrollCount:', scrollCount, '/', maxScrolls, ', consecutiveNoNewItems:', consecutiveNoNewItems, '/', maxConsecutiveNoNew);");
            js.AppendLine("                if (consecutiveNoNewItems >= maxConsecutiveNoNew || scrollCount >= maxScrolls) {");
            js.AppendLine("                    isCompleted = true;");
            js.AppendLine("                    console.log('[采集结束] 原因:', consecutiveNoNewItems >= maxConsecutiveNoNew ? '无新数据' : '达到最大滚动次数', ', 总数:', results.length);");
            js.AppendLine("                    resolve(JSON.stringify(results));");
            js.AppendLine("                    return;");
            js.AppendLine("                }");
            js.AppendLine("");
            js.AppendLine("                // 执行滚动");
            js.AppendLine("                console.log('[执行滚动] 第', scrollCount + 1, '次滚动');");
            js.AppendLine("                // 根据可视区域高度动态计算滚动距离，确保能触发懒加载");
            js.AppendLine("                const viewportHeight = window.innerHeight || document.documentElement.clientHeight;");
            js.AppendLine("                const minScroll = Math.max(600, viewportHeight * 0.8); // 最小600px或80%窗口高度");
            js.AppendLine("                const maxScroll = Math.max(1000, viewportHeight * 1.2); // 最小1000px或120%窗口高度");
            js.AppendLine("                const scrollDistance = randomDelay(Math.floor(minScroll), Math.floor(maxScroll));");
            js.AppendLine("                console.log('[滚动距离]', scrollDistance, 'px, 窗口高度:', viewportHeight, 'px');");
            js.AppendLine("");
            js.AppendLine("                // 模拟人手滚动：分多次小幅度滚动");
            js.AppendLine("                const scrollSteps = randomDelay(3, 7);");
            js.AppendLine("                const stepSize = scrollDistance / scrollSteps;");
            js.AppendLine("                for (let i = 0; i < scrollSteps; i++) {");
            js.AppendLine("                    window.scrollBy({ top: stepSize + randomDelay(-10, 10), behavior: 'auto' });");
            js.AppendLine("                    await new Promise(resolve => setTimeout(resolve, randomDelay(50, 150)));");
            js.AppendLine("                }");
            js.AppendLine("                ");
            js.AppendLine("                // 滚动后随机停顿，模拟阅读内容");
            js.AppendLine("                const readPause = randomDelay(1000, 3000);");
            js.AppendLine("                console.log('[阅读停顿]', readPause, 'ms');");
            js.AppendLine("                await new Promise(resolve => setTimeout(resolve, readPause));");
            js.AppendLine("");
            js.AppendLine("                scrollCount++;");
            js.AppendLine("                console.log('[滚动完成] scrollCount现在是:', scrollCount);");
            js.AppendLine("");
            js.AppendLine("                // 随机延迟后继续下一轮");
            js.AppendLine("                const nextDelay = randomDelay(2000, 3500);");
            js.AppendLine("                setTimeout(() => doScroll(), nextDelay);");
            js.AppendLine("");
            js.AppendLine("            } catch (e) {");
            js.AppendLine("                console.error('[采集错误]', e);");
            js.AppendLine("                // 出错后也继续尝试");
            js.AppendLine("                setTimeout(() => doScroll(), 3000);");
            js.AppendLine("            }");
            js.AppendLine("        };");
            js.AppendLine("");
            js.AppendLine("        // 启动第一次滚动");
            js.AppendLine("        doScroll();");
            js.AppendLine("");
            
            // 超时保护（5分钟）
            js.AppendLine("        setTimeout(() => {");
            js.AppendLine("            if (results.length > 0) {");
            js.AppendLine("                console.log('Timeout: returning ' + results.length + ' posts');");
            js.AppendLine("                resolve(JSON.stringify(results));");
            js.AppendLine("            } else {");
            js.AppendLine("                reject(new Error('Collection timeout with no data'));");
            js.AppendLine("            }");
            js.AppendLine("        }, 300000);");
            js.AppendLine("    });");
            js.AppendLine("})();");
            
            return js.ToString();
        }

        /// <summary>
        /// 生成群组采集脚本
        /// </summary>
        private string GenerateGroupCollectScript(int expectedCount)
        {
            var js = new System.Text.StringBuilder();
            
            js.AppendLine("(function() {");
            js.AppendLine("    return new Promise((resolve, reject) => {");
            js.AppendLine("        const results = [];");
            js.AppendLine($"        const targetCount = {expectedCount};");
            js.AppendLine("        const seenUrls = new Set();");
            js.AppendLine("");
            js.AppendLine("        let scrollCount = 0;");
            js.AppendLine("        const maxScrolls = 50;");
            js.AppendLine("        let consecutiveNoNewItems = 0;");
            js.AppendLine("        const maxConsecutiveNoNew = 5;");
            js.AppendLine("");
            js.AppendLine("        const randomDelay = (min, max) => {");
            js.AppendLine("            return Math.floor(Math.random() * (max - min + 1)) + min;");
            js.AppendLine("        };");
            js.AppendLine("");
            
            // 添加贝塞尔曲线鼠标轨迹模拟函数
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
            js.AppendLine("                const event = new MouseEvent('mousemove', { clientX: Math.floor(x), clientY: Math.floor(y), bubbles: true });");
            js.AppendLine("                document.dispatchEvent(event);");
            js.AppendLine("                await new Promise(resolve => setTimeout(resolve, randomDelay(20, 50)));");
            js.AppendLine("            }");
            js.AppendLine("        };");
            js.AppendLine("");
            
            // 提取群组数据的函数
            js.AppendLine("        const extractGroupData = (card) => {");
            js.AppendLine("            try {");
            js.AppendLine("                // 提取群组链接");
            js.AppendLine("                const groupLinkEl = card.querySelector('a[href*=\"/groups/\"]');");
            js.AppendLine("                if (!groupLinkEl) return null;");
            js.AppendLine("");
            js.AppendLine("                const url = groupLinkEl.href.split('?')[0];");
            js.AppendLine("                if (!url || seenUrls.has(url)) return null;");
            js.AppendLine("");
            js.AppendLine("                // 提取群组名称");
            js.AppendLine("                const groupName = groupLinkEl.textContent.trim();");
            js.AppendLine("                if (!groupName) return null;");
            js.AppendLine("");
            js.AppendLine("                // 提取群组类型（公开/私密）- 英语环境");
            js.AppendLine("                let type = 'Public';");
            js.AppendLine("                const typeEl = card.querySelector('[aria-label*=\"Public\"], [aria-label*=\"Private\"], [aria-label*=\"Closed\"]');");
            js.AppendLine("                if (typeEl) {");
            js.AppendLine("                    const ariaLabel = typeEl.getAttribute('aria-label') || '';");
            js.AppendLine("                    if (ariaLabel.includes('Private') || ariaLabel.includes('Closed')) {");
            js.AppendLine("                        type = 'Private';");
            js.AppendLine("                    }");
            js.AppendLine("                }");
            js.AppendLine("");
            js.AppendLine("                // 提取成员数和活跃度");
            js.AppendLine("                let memberQuantity = '';");
            js.AppendLine("                let activeQuantity = '';");
            js.AppendLine("");
            js.AppendLine("                const allSpans = Array.from(card.querySelectorAll('span[dir=\"auto\"]'));");
            js.AppendLine("                for (const span of allSpans) {");
            js.AppendLine("                    const text = span.textContent.trim();");
            js.AppendLine("                    if (!text) continue;");
            js.AppendLine("");
            js.AppendLine("                    // 匹配成员数：如 \"2K members\", \"18.5K members\"");
            js.AppendLine("                    const memberMatch = text.match(/([\\d]+[\\.,]?\\d*)\\s*(K|M|B|members?)/i);");
            js.AppendLine("                    if (memberMatch && !memberQuantity) {");
            js.AppendLine("                        memberQuantity = text;");
            js.AppendLine("                        continue;");
            js.AppendLine("                    }");
            js.AppendLine("");
            js.AppendLine("                    // 匹配活跃度：如 \"2 posts per day\", \"5 posts per week\"");
            js.AppendLine("                    const activeMatch = text.match(/[\\d]+\\s*(posts?).*?(day|week|month)/i);");
            js.AppendLine("                    if (activeMatch && !activeQuantity) {");
            js.AppendLine("                        activeQuantity = text;");
            js.AppendLine("                    }");
            js.AppendLine("                }");
            js.AppendLine("");
            js.AppendLine("                seenUrls.add(url);");
            js.AppendLine("");
            js.AppendLine("                return {");
            js.AppendLine("                    groupName: groupName,");
            js.AppendLine("                    url: url,");
            js.AppendLine("                    type: type,");
            js.AppendLine("                    memberQuantity: memberQuantity,");
            js.AppendLine("                    activeQuantity: activeQuantity,");
            js.AppendLine("                    collectedAt: new Date().toISOString()");
            js.AppendLine("                };");
            js.AppendLine("            } catch (e) {");
            js.AppendLine("                console.warn('Extract group failed:', e);");
            js.AppendLine("                return null;");
            js.AppendLine("            }");
            js.AppendLine("        };");
            js.AppendLine("");
            
            // 主循环 - 滚动加载
            js.AppendLine("        const interval = setInterval(() => {");
            js.AppendLine("            try {");
            js.AppendLine("                const cards = document.querySelectorAll('[role=\"article\"]');");
            js.AppendLine("                let newItemsFound = 0;");
            js.AppendLine("");
            js.AppendLine("                cards.forEach(card => {");
            js.AppendLine("                    if (results.length >= targetCount) return;");
            js.AppendLine("");
            js.AppendLine("                    const data = extractGroupData(card);");
            js.AppendLine("                    if (data) {");
            js.AppendLine("                        results.push(data);");
            js.AppendLine("                        newItemsFound++;");
            js.AppendLine("                    }");
            js.AppendLine("                });");
            js.AppendLine("");
            js.AppendLine("                if (newItemsFound > 0) {");
            js.AppendLine("                    consecutiveNoNewItems = 0;");
            js.AppendLine("                } else {");
            js.AppendLine("                    consecutiveNoNewItems++;");
            js.AppendLine("                }");
            js.AppendLine("");
            js.AppendLine("                if (results.length >= targetCount) {");
            js.AppendLine("                    clearInterval(interval);");
            js.AppendLine("                    console.log('Group collection complete: ' + results.length + '/' + targetCount);");
            js.AppendLine("                    resolve(JSON.stringify(results.slice(0, targetCount)));");
            js.AppendLine("                    return;");
            js.AppendLine("                }");
            js.AppendLine("");
            js.AppendLine("                if (consecutiveNoNewItems >= maxConsecutiveNoNew || scrollCount >= maxScrolls) {");
            js.AppendLine("                    clearInterval(interval);");
            js.AppendLine("                    console.log('Group collection ended: ' + results.length + ' items');");
            js.AppendLine("                    resolve(JSON.stringify(results));");
            js.AppendLine("                    return;");
            js.AppendLine("                }");
            js.AppendLine("");
            js.AppendLine("                const scrollDistance = randomDelay(600, 1000);");
            js.AppendLine("                window.scrollBy({ top: scrollDistance, behavior: 'smooth' });");
            js.AppendLine("                scrollCount++;");
            js.AppendLine("");
            js.AppendLine("                const nextDelay = randomDelay(1500, 3000);");
            js.AppendLine("                clearInterval(interval);");
            js.AppendLine("                setTimeout(() => {");
            js.AppendLine("                    interval = setInterval(arguments.callee, 2000);");
            js.AppendLine("                }, nextDelay);");
            js.AppendLine("");
            js.AppendLine("            } catch (e) {");
            js.AppendLine("                console.error('Group collection error:', e);");
            js.AppendLine("            }");
            js.AppendLine("        }, 2000);");
            js.AppendLine("");
            
            // 超时保护（5分钟）
            js.AppendLine("        setTimeout(() => {");
            js.AppendLine("            clearInterval(interval);");
            js.AppendLine("            if (results.length > 0) {");
            js.AppendLine("                console.log('Timeout: returning ' + results.length + ' groups');");
            js.AppendLine("                resolve(JSON.stringify(results));");
            js.AppendLine("            } else {");
            js.AppendLine("                reject(new Error('Collection timeout with no data'));");
            js.AppendLine("            }");
            js.AppendLine("        }, 300000);");
            js.AppendLine("    });");
            js.AppendLine("})();");
            
            return js.ToString();
        }

        /// <summary>
        /// 生成群组成员采集脚本
        /// </summary>
        private string GenerateGroupMemberCollectScript(int expectedCount)
        {
            var js = new System.Text.StringBuilder();
            
            js.AppendLine("(function() {");
            js.AppendLine("    return new Promise((resolve, reject) => {");
            js.AppendLine("        const results = [];");
            js.AppendLine($"        const targetCount = {expectedCount};");
            js.AppendLine("        const seenUserIds = new Set();");
            js.AppendLine("");
            js.AppendLine("        let scrollCount = 0;");
            js.AppendLine("        const maxScrolls = 50;");
            js.AppendLine("        let consecutiveNoNewItems = 0;");
            js.AppendLine("        const maxConsecutiveNoNew = 5;");
            js.AppendLine("");
            js.AppendLine("        const randomDelay = (min, max) => {");
            js.AppendLine("            return Math.floor(Math.random() * (max - min + 1)) + min;");
            js.AppendLine("        };");
            js.AppendLine("");
            
            // 添加贝塞尔曲线鼠标轨迹模拟函数
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
            js.AppendLine("                const event = new MouseEvent('mousemove', { clientX: Math.floor(x), clientY: Math.floor(y), bubbles: true });");
            js.AppendLine("                document.dispatchEvent(event);");
            js.AppendLine("                await new Promise(resolve => setTimeout(resolve, randomDelay(20, 50)));");
            js.AppendLine("            }");
            js.AppendLine("        };");
            js.AppendLine("");
            
            // 提取群组成员数据的函数
            js.AppendLine("        const extractMemberData = (listItem) => {");
            js.AppendLine("            try {");
            js.AppendLine("                // 提取用户链接");
            js.AppendLine("                const userLinkEl = listItem.querySelector('a[href*=\"/user/\"]');");
            js.AppendLine("                if (!userLinkEl) return null;");
            js.AppendLine("");
            js.AppendLine("                const url = userLinkEl.href.split('?')[0];");
            js.AppendLine("                if (!url || seenUserIds.has(url)) return null;");
            js.AppendLine("");
            js.AppendLine("                // 提取用户名");
            js.AppendLine("                const userName = userLinkEl.textContent.trim();");
            js.AppendLine("                if (!userName) return null;");
            js.AppendLine("");
            js.AppendLine("                // 从URL中提取FB用户ID");
            js.AppendLine("                const userIdMatch = url.match(/\\/user\\/(\\d+)/);");
            js.AppendLine("                const fbUserId = userIdMatch ? userIdMatch[1] : '';");
            js.AppendLine("");
            js.AppendLine("                // 提取其他信息（加入时间、工作/学校等）");
            js.AppendLine("                const infoDivs = listItem.querySelectorAll('div > div > div > div > div');");
            js.AppendLine("                let joinTime = '';");
            js.AppendLine("                let workInfo = '';");
            js.AppendLine("                let location = '';");
            js.AppendLine("");
            js.AppendLine("                for (const div of infoDivs) {");
            js.AppendLine("                    const text = div.textContent.trim();");
            js.AppendLine("                    if (!text) continue;");
            js.AppendLine("");
            js.AppendLine("                    // 检测加入时间（包含'加入'关键词）");
            js.AppendLine("                    if (text.includes('加入') && !joinTime) {");
            js.AppendLine("                        joinTime = text;");
            js.AppendLine("                    }");
            js.AppendLine("                    // 检测工作信息（包含'在'和'工作'）");
            js.AppendLine("                    else if ((text.includes('在') && text.includes('工作')) || text.includes('studied at')) {");
            js.AppendLine("                        workInfo = text;");
            js.AppendLine("                    }");
            js.AppendLine("                    // 其他文本可能是地点");
            js.AppendLine("                    else if (!joinTime && !workInfo && text.length > 2) {");
            js.AppendLine("                        location = text;");
            js.AppendLine("                    }");
            js.AppendLine("                }");
            js.AppendLine("");
            js.AppendLine("                seenUserIds.add(url);");
            js.AppendLine("");
            js.AppendLine("                return {");
            js.AppendLine("                    fbUserId: fbUserId,");
            js.AppendLine("                    userName: userName,");
            js.AppendLine("                    url: url,");
            js.AppendLine("                    location: location,");
            js.AppendLine("                    workExperience: workInfo,");
            js.AppendLine("                    dataType: 1,");
            js.AppendLine("                    fromResource: 'group_member',");
            js.AppendLine("                    syncTime: new Date().toISOString()");
            js.AppendLine("                };");
            js.AppendLine("            } catch (e) {");
            js.AppendLine("                console.warn('Extract member failed:', e);");
            js.AppendLine("                return null;");
            js.AppendLine("            }");
            js.AppendLine("        };");
            js.AppendLine("");
            
            // 主循环 - 滚动加载
            js.AppendLine("        const interval = setInterval(() => {");
            js.AppendLine("            try {");
            js.AppendLine("                // 查找所有 listitem 元素");
            js.AppendLine("                const listItems = document.querySelectorAll('div[role=\"listitem\"]');");
            js.AppendLine("                let newItemsFound = 0;");
            js.AppendLine("");
            js.AppendLine("                listItems.forEach(item => {");
            js.AppendLine("                    if (results.length >= targetCount) return;");
            js.AppendLine("");
            js.AppendLine("                    const data = extractMemberData(item);");
            js.AppendLine("                    if (data) {");
            js.AppendLine("                        results.push(data);");
            js.AppendLine("                        newItemsFound++;");
            js.AppendLine("                    }");
            js.AppendLine("                });");
            js.AppendLine("");
            js.AppendLine("                if (newItemsFound > 0) {");
            js.AppendLine("                    consecutiveNoNewItems = 0;");
            js.AppendLine("                } else {");
            js.AppendLine("                    consecutiveNoNewItems++;");
            js.AppendLine("                }");
            js.AppendLine("");
            js.AppendLine("                if (results.length >= targetCount) {");
            js.AppendLine("                    clearInterval(interval);");
            js.AppendLine("                    console.log('Group member collection complete: ' + results.length + '/' + targetCount);");
            js.AppendLine("                    resolve(JSON.stringify(results.slice(0, targetCount)));");
            js.AppendLine("                    return;");
            js.AppendLine("                }");
            js.AppendLine("");
            js.AppendLine("                if (consecutiveNoNewItems >= maxConsecutiveNoNew || scrollCount >= maxScrolls) {");
            js.AppendLine("                    clearInterval(interval);");
            js.AppendLine("                    console.log('Group member collection ended: ' + results.length + ' items');");
            js.AppendLine("                    resolve(JSON.stringify(results));");
            js.AppendLine("                    return;");
            js.AppendLine("                }");
            js.AppendLine("");
            js.AppendLine("                const scrollDistance = randomDelay(600, 1000);");
            js.AppendLine("                window.scrollBy({ top: scrollDistance, behavior: 'smooth' });");
            js.AppendLine("                scrollCount++;");
            js.AppendLine("");
            js.AppendLine("                const nextDelay = randomDelay(1500, 3000);");
            js.AppendLine("                clearInterval(interval);");
            js.AppendLine("                setTimeout(() => {");
            js.AppendLine("                    interval = setInterval(arguments.callee, 2000);");
            js.AppendLine("                }, nextDelay);");
            js.AppendLine("");
            js.AppendLine("            } catch (e) {");
            js.AppendLine("                console.error('Group member collection error:', e);");
            js.AppendLine("            }");
            js.AppendLine("        }, 2000);");
            js.AppendLine("");
            
            // 超时保护（5分钟）
            js.AppendLine("        setTimeout(() => {");
            js.AppendLine("            clearInterval(interval);");
            js.AppendLine("            if (results.length > 0) {");
            js.AppendLine("                console.log('Timeout: returning ' + results.length + ' members');");
            js.AppendLine("                resolve(JSON.stringify(results));");
            js.AppendLine("            } else {");
            js.AppendLine("                reject(new Error('Collection timeout with no data'));");
            js.AppendLine("            }");
            js.AppendLine("        }, 300000);");
            js.AppendLine("    });");
            js.AppendLine("})();");
            
            return js.ToString();
        }

        /// <summary>
        /// 生成用户关系采集脚本（粉丝/关注/好友）
        /// </summary>
        private string GenerateUserRelationCollectScript(int expectedCount)
        {
            var js = new System.Text.StringBuilder();
            
            js.AppendLine("(function() {");
            js.AppendLine("    return new Promise((resolve, reject) => {");
            js.AppendLine("        const results = [];");
            js.AppendLine($"        const targetCount = {expectedCount};");
            js.AppendLine("        const seenUserIds = new Set();");
            js.AppendLine("");
            js.AppendLine("        let scrollCount = 0;");
            js.AppendLine("        const maxScrolls = 50;");
            js.AppendLine("        let consecutiveNoNewItems = 0;");
            js.AppendLine("        const maxConsecutiveNoNew = 5;");
            js.AppendLine("");
            js.AppendLine("        const randomDelay = (min, max) => {");
            js.AppendLine("            return Math.floor(Math.random() * (max - min + 1)) + min;");
            js.AppendLine("        };");
            js.AppendLine("");
            
            // 添加贝塞尔曲线鼠标轨迹模拟函数
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
            js.AppendLine("                const event = new MouseEvent('mousemove', { clientX: Math.floor(x), clientY: Math.floor(y), bubbles: true });");
            js.AppendLine("                document.dispatchEvent(event);");
            js.AppendLine("                await new Promise(resolve => setTimeout(resolve, randomDelay(20, 50)));");
            js.AppendLine("            }");
            js.AppendLine("        };");
            js.AppendLine("");
            
            // 提取用户关系数据的函数
            js.AppendLine("        const extractUserData = (container) => {");
            js.AppendLine("            try {");
            js.AppendLine("                // 查找用户链接");
            js.AppendLine("                const userLinkEl = container.querySelector('a[href*=\"/profile.php?id=\"]') ||");
            js.AppendLine("                                  container.querySelector('a[href*=\"/user/\"]');");
            js.AppendLine("                if (!userLinkEl) return null;");
            js.AppendLine("");
            js.AppendLine("                const url = userLinkEl.href.split('?')[0];");
            js.AppendLine("                if (!url || seenUserIds.has(url)) return null;");
            js.AppendLine("");
            js.AppendLine("                // 提取用户名");
            js.AppendLine("                const userName = userLinkEl.textContent.trim();");
            js.AppendLine("                if (!userName) return null;");
            js.AppendLine("");
            js.AppendLine("                // 从URL中提取FB用户ID");
            js.AppendLine("                let fbUserId = '';");
            js.AppendLine("                const idMatch = url.match(/[?&]id=(\\d+)/);");
            js.AppendLine("                if (idMatch) {");
            js.AppendLine("                    fbUserId = idMatch[1];");
            js.AppendLine("                } else {");
            js.AppendLine("                    const userIdMatch = url.match(/\\/user\\/(\\d+)/);");
            js.AppendLine("                    fbUserId = userIdMatch ? userIdMatch[1] : '';");
            js.AppendLine("                }");
            js.AppendLine("");
            js.AppendLine("                // 提取头像");
            js.AppendLine("                const imgEl = container.querySelector('img');");
            js.AppendLine("                const avatar = imgEl ? (imgEl.src || '') : '';");
            js.AppendLine("");
            js.AppendLine("                // 判断关系类型（根据URL参数）");
            js.AppendLine("                let fromResource = 'peer_follower';");
            js.AppendLine("                if (window.location.href.includes('&sk=following')) {");
            js.AppendLine("                    fromResource = 'peer_following';");
            js.AppendLine("                } else if (window.location.href.includes('&sk=friends')) {");
            js.AppendLine("                    fromResource = 'peer_friend';");
            js.AppendLine("                }");
            js.AppendLine("");
            js.AppendLine("                seenUserIds.add(url);");
            js.AppendLine("");
            js.AppendLine("                return {");
            js.AppendLine("                    fbUserId: fbUserId,");
            js.AppendLine("                    userName: userName,");
            js.AppendLine("                    url: url,");
            js.AppendLine("                    avatar: avatar,");
            js.AppendLine("                    dataType: 1,");
            js.AppendLine("                    fromResource: fromResource,");
            js.AppendLine("                    syncTime: new Date().toISOString()");
            js.AppendLine("                };");
            js.AppendLine("            } catch (e) {");
            js.AppendLine("                console.warn('Extract user relation failed:', e);");
            js.AppendLine("                return null;");
            js.AppendLine("            }");
            js.AppendLine("        };");
            js.AppendLine("");
            
            // 主循环 - 滚动加载
            js.AppendLine("        const interval = setInterval(() => {");
            js.AppendLine("            try {");
            js.AppendLine("                // 查找所有用户容器元素");
            js.AppendLine("                const containers = document.querySelectorAll('div[class*=\"x6s0dn4\"]');");
            js.AppendLine("                let newItemsFound = 0;");
            js.AppendLine("");
            js.AppendLine("                containers.forEach(container => {");
            js.AppendLine("                    if (results.length >= targetCount) return;");
            js.AppendLine("");
            js.AppendLine("                    const data = extractUserData(container);");
            js.AppendLine("                    if (data) {");
            js.AppendLine("                        results.push(data);");
            js.AppendLine("                        newItemsFound++;");
            js.AppendLine("                    }");
            js.AppendLine("                });");
            js.AppendLine("");
            js.AppendLine("                if (newItemsFound > 0) {");
            js.AppendLine("                    consecutiveNoNewItems = 0;");
            js.AppendLine("                } else {");
            js.AppendLine("                    consecutiveNoNewItems++;");
            js.AppendLine("                }");
            js.AppendLine("");
            js.AppendLine("                if (results.length >= targetCount) {");
            js.AppendLine("                    clearInterval(interval);");
            js.AppendLine("                    console.log('User relation collection complete: ' + results.length + '/' + targetCount);");
            js.AppendLine("                    resolve(JSON.stringify(results.slice(0, targetCount)));");
            js.AppendLine("                    return;");
            js.AppendLine("                }");
            js.AppendLine("");
            js.AppendLine("                if (consecutiveNoNewItems >= maxConsecutiveNoNew || scrollCount >= maxScrolls) {");
            js.AppendLine("                    clearInterval(interval);");
            js.AppendLine("                    console.log('User relation collection ended: ' + results.length + ' items');");
            js.AppendLine("                    resolve(JSON.stringify(results));");
            js.AppendLine("                    return;");
            js.AppendLine("                }");
            js.AppendLine("");
            js.AppendLine("                const scrollDistance = randomDelay(600, 1000);");
            js.AppendLine("                window.scrollBy({ top: scrollDistance, behavior: 'smooth' });");
            js.AppendLine("                scrollCount++;");
            js.AppendLine("");
            js.AppendLine("                const nextDelay = randomDelay(1500, 3000);");
            js.AppendLine("                clearInterval(interval);");
            js.AppendLine("                setTimeout(() => {");
            js.AppendLine("                    interval = setInterval(arguments.callee, 2000);");
            js.AppendLine("                }, nextDelay);");
            js.AppendLine("");
            js.AppendLine("            } catch (e) {");
            js.AppendLine("                console.error('User relation collection error:', e);");
            js.AppendLine("            }");
            js.AppendLine("        }, 2000);");
            js.AppendLine("");
            
            // 超时保护（5分钟）
            js.AppendLine("        setTimeout(() => {");
            js.AppendLine("            clearInterval(interval);");
            js.AppendLine("            if (results.length > 0) {");
            js.AppendLine("                console.log('Timeout: returning ' + results.length + ' users');");
            js.AppendLine("                resolve(JSON.stringify(results));");
            js.AppendLine("            } else {");
            js.AppendLine("                reject(new Error('Collection timeout with no data'));");
            js.AppendLine("            }");
            js.AppendLine("        }, 300000);");
            js.AppendLine("    });");
            js.AppendLine("})();");
            
            return js.ToString();
        }

        /// <summary>
        /// 生成链接加组采集脚本（直接访问群组页面并执行加组操作）
        /// 注意：C#层面已经通过IsLoading智能等待页面加载完成，这里不需要再等待
        /// </summary>
        private string GenerateAddGroupCollectScript(int expectedCount)
        {
            var js = new System.Text.StringBuilder();
                    
            js.AppendLine("(function() {");
            js.AppendLine("    return new Promise((resolve, reject) => {");
            js.AppendLine("        const results = [];");
            js.AppendLine("");
            js.AppendLine("        const randomDelay = (min, max) => {");
            js.AppendLine("            return Math.floor(Math.random() * (max - min + 1)) + min;");
            js.AppendLine("        };");
            js.AppendLine("");
            
            // 添加贝塞尔曲线鼠标轨迹模拟函数
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
            js.AppendLine("                const event = new MouseEvent('mousemove', { clientX: Math.floor(x), clientY: Math.floor(y), bubbles: true });");
            js.AppendLine("                document.dispatchEvent(event);");
            js.AppendLine("                await new Promise(resolve => setTimeout(resolve, randomDelay(20, 50)));");
            js.AppendLine("            }");
            js.AppendLine("        };");
            js.AppendLine("");
                    
            // 获取当前用户信息
            js.AppendLine("        const getCurrentUserInfo = () => {");
            js.AppendLine("            try {");
            js.AppendLine("                const currentUrl = window.location.href.split('?')[0];");
            js.AppendLine("                const userIdMatch = currentUrl.match(/[?&]id=(\\d+)/);");
            js.AppendLine("                const accountId = userIdMatch ? userIdMatch[1] : '';");
            js.AppendLine("                return { accountId, targetUrl: currentUrl };");
            js.AppendLine("            } catch (e) {");
            js.AppendLine("                console.warn('Get user info failed:', e);");
            js.AppendLine("                return { accountId: '', targetUrl: window.location.href };");
            js.AppendLine("            }");
            js.AppendLine("        };");
            js.AppendLine("");
                    
            // 查找并点击加入群组按钮
            js.AppendLine("        const findAndClickJoinButton = () => {");
            js.AppendLine("            return new Promise((resolve) => {");
            js.AppendLine("                try {");
            js.AppendLine("                    // 首先检查是否已经是'Joined'状态（已加入）");
            js.AppendLine("                    const joinedEl = Array.from(document.querySelectorAll('span')).find(el => {");
            js.AppendLine("                        return el.textContent.trim() === 'Joined';");
            js.AppendLine("                    });");
            js.AppendLine("                    ");
            js.AppendLine("                    if (joinedEl) {");
            js.AppendLine("                        console.log('Already joined this group');");
            js.AppendLine("                        resolve({ success: true, status: 3, reason: 'Already joined' });");
            js.AppendLine("                        return;");
            js.AppendLine("                    }");
            js.AppendLine("");
            js.AppendLine("                    // 检查是否是'pending'状态（待审核）");
            js.AppendLine("                    const pendingEl = Array.from(document.querySelectorAll('span')).find(el => {");
            js.AppendLine("                        return el.textContent.includes('membership is pending');");
            js.AppendLine("                    });");
            js.AppendLine("                    ");
            js.AppendLine("                    if (pendingEl) {");
            js.AppendLine("                        console.log('Membership is pending approval');");
            js.AppendLine("                        resolve({ success: true, status: 3, reason: 'Pending approval' });");
            js.AppendLine("                        return;");
            js.AppendLine("                    }");
            js.AppendLine("");
            js.AppendLine("                    // 查找'Join group'按钮（使用aria-label精确定位）");
            js.AppendLine("                    const joinButton = document.querySelector('[aria-label=\"Join group\"]');");
            js.AppendLine("                    ");
            js.AppendLine("                    if (!joinButton) {");
            js.AppendLine("                        console.warn('Join button not found');");
            js.AppendLine("                        resolve({ success: false, reason: 'No join button found' });");
            js.AppendLine("                        return;");
            js.AppendLine("                    }");
            js.AppendLine("");
            js.AppendLine("                    // 点击加入按钮");
            js.AppendLine("                    joinButton.click();");
            js.AppendLine("                    console.log('Clicked join button');");
            js.AppendLine("");
            js.AppendLine("                    // 等待3-4秒后检查结果（无需处理弹窗）");
            js.AppendLine("                    setTimeout(() => {");
            js.AppendLine("                        checkJoinResult(resolve);");
            js.AppendLine("                    }, randomDelay(3000, 4000));");
            js.AppendLine("                } catch (e) {");
            js.AppendLine("                    console.error('Find join button error:', e);");
            js.AppendLine("                    resolve({ success: false, reason: e.message });");
            js.AppendLine("                }");
            js.AppendLine("            });");
            js.AppendLine("        };");
            js.AppendLine("");
            
            // 检查加组结果（点击按钮后直接检查）
            js.AppendLine("        const checkJoinResult = (resolve) => {");
            js.AppendLine("            try {");
            js.AppendLine("                // 检查是否显示'Joined'（已成功加入）");
            js.AppendLine("                const joinedEl = Array.from(document.querySelectorAll('span')).find(el => {");
            js.AppendLine("                    return el.textContent.trim() === 'Joined';");
            js.AppendLine("                });");
            js.AppendLine("                ");
            js.AppendLine("                if (joinedEl) {");
            js.AppendLine("                    console.log('Successfully joined the group');");
            js.AppendLine("                    resolve({ success: true, status: 1, reason: 'Joined successfully' });");
            js.AppendLine("                    return;");
            js.AppendLine("                }");
            js.AppendLine("");
            js.AppendLine("                // 检查是否显示'pending'（待审核）");
            js.AppendLine("                const pendingEl = Array.from(document.querySelectorAll('span')).find(el => {");
            js.AppendLine("                    return el.textContent.includes('membership is pending');");
            js.AppendLine("                });");
            js.AppendLine("                ");
            js.AppendLine("                if (pendingEl) {");
            js.AppendLine("                    console.log('Membership request is pending approval');");
            js.AppendLine("                    resolve({ success: true, status: 3, reason: 'Pending approval' });");
            js.AppendLine("                    return;");
            js.AppendLine("                }");
            js.AppendLine("");
            js.AppendLine("                // 如果没有找到明确的状态，假设成功");
            js.AppendLine("                console.log('Join operation completed, status unclear');");
            js.AppendLine("                resolve({ success: true, status: 1, reason: 'Completed' });");
            js.AppendLine("            } catch (e) {");
            js.AppendLine("                console.error('Check result error:', e);");
            js.AppendLine("                resolve({ success: false, reason: e.message });");
            js.AppendLine("            }");
            js.AppendLine("        };");
            js.AppendLine("");
                    
            // 提取群组信息
            js.AppendLine("        const extractGroupInfo = () => {");
            js.AppendLine("            try {");
            js.AppendLine("                const groupUrl = window.location.href.split('?')[0];");
            js.AppendLine("                const groupIdMatch = groupUrl.match(/\\/groups\\/(\\d+)/);");
            js.AppendLine("                const groupId = groupIdMatch ? groupIdMatch[1] : '';");
            js.AppendLine("");
            js.AppendLine("                // 尝试提取群组名称");
            js.AppendLine("                let groupName = '';");
            js.AppendLine("                const titleEl = document.querySelector('h1, [data-testid=\"group_name\"]');");
            js.AppendLine("                if (titleEl) {");
            js.AppendLine("                    groupName = titleEl.textContent.trim();");
            js.AppendLine("                }");
            js.AppendLine("");
            js.AppendLine("                return { groupId, groupName, groupUrl };");
            js.AppendLine("            } catch (e) {");
            js.AppendLine("                console.warn('Extract group info failed:', e);");
            js.AppendLine("                return { groupId: '', groupName: '', groupUrl: window.location.href };");
            js.AppendLine("            }");
            js.AppendLine("        };");
            js.AppendLine("");
                    
            // 主执行逻辑（无需等待页面加载，C#已处理）
            js.AppendLine("        const executeJoinGroup = async () => {");
            js.AppendLine("            try {");
            js.AppendLine("                console.log('Starting join group operation...');");
            js.AppendLine("");
            js.AppendLine("                // 获取用户信息");
            js.AppendLine("                const userInfo = getCurrentUserInfo();");
            js.AppendLine("");
            js.AppendLine("                // 获取群组信息");
            js.AppendLine("                const groupInfo = extractGroupInfo();");
            js.AppendLine("");
            js.AppendLine("                // 执行加组操作");
            js.AppendLine("                const result = await findAndClickJoinButton();");
            js.AppendLine("");
            js.AppendLine("                // 构建结果对象");
            js.AppendLine("                const joinResult = {");
            js.AppendLine("                    accountId: userInfo.accountId,");
            js.AppendLine("                    targetUrl: userInfo.targetUrl,");
            js.AppendLine("                    groupId: groupInfo.groupId,");
            js.AppendLine("                    groupName: groupInfo.groupName,");
            js.AppendLine("                    groupUrl: groupInfo.groupUrl,");
            js.AppendLine("                    joinStatus: result.status || (result.success ? 1 : 2),");
            js.AppendLine("                    failReason: result.reason || '',");
            js.AppendLine("                    joinTime: new Date().toISOString(),");
            js.AppendLine("                    syncTime: new Date().toISOString()");
            js.AppendLine("                };");
            js.AppendLine("");
            js.AppendLine("                results.push(joinResult);");
            js.AppendLine("");
            js.AppendLine("                console.log('Join result:', joinResult);");
            js.AppendLine("");
            js.AppendLine("                // 返回结果");
            js.AppendLine("                resolve(JSON.stringify(results));");
            js.AppendLine("            } catch (e) {");
            js.AppendLine("                console.error('Execute join group error:', e);");
            js.AppendLine("                reject(new Error(e.message));");
            js.AppendLine("            }");
            js.AppendLine("        };");
            js.AppendLine("");
                    
            // 立即启动执行（页面已由C#智能等待加载完成）
            js.AppendLine("        executeJoinGroup();");
            js.AppendLine("");
                    
            // 超时保护（30秒）
            js.AppendLine("        setTimeout(() => {");
            js.AppendLine("            if (results.length === 0) {");
            js.AppendLine("                reject(new Error('Join group timeout'));");
            js.AppendLine("            }");
            js.AppendLine("        }, 30000);");
            js.AppendLine("    });");
            js.AppendLine("})();");
                    
            return js.ToString();
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
    }
}
