using CefSharp;
using CefSharp.Wpf;
using Newtonsoft.Json;
using SocialMatrix.WpfHost.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
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
        
        // 当前明细ID
        public string? CurrentDetailId { get; set; }
        
        // 采集结果回调
        public event Action<string, string, string>? OnCollectionComplete; // (detailId, accountId, jsonData)
        public event Action<string, string>? OnCollectionError;    // (accountId, errorMessage)

        public BrowserMatrixWindow()
        {
            InitializeComponent();
            
            // 监听窗口大小变化，重新计算布局
            this.SizeChanged += (sender, e) =>
            {
                UpdateLayout();
            };
        }

        /// <summary>
        /// 创建浏览器实例并启动自动化采集
        /// </summary>
        public void CreateBrowser(string accountId, string initialUrl = "https://www.facebook.com", 
            string? cookie = null, string? searchUrl = null, int expectedCount = 100, long? deviceId = null, int taskType = 1)
        {
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

            var browser = new ChromiumWebBrowser(initialUrl);
            browser.Tag = accountId;

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
                            var fingerprint = new FingerprintConfig
                            {
                                Area = "",
                                Latitude = null,
                                Longitude = null,
                                DeviceId = deviceId
                            };
                            await FingerprintInjector.InjectAsync(browser, fingerprint);
                            System.Diagnostics.Debug.WriteLine($"✅ 账号 {accountId} 指纹注入完成 (DeviceName={fingerprint.DeviceName})");

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
        /// 更新布局 - 根据浏览器数量动态调整大小
        /// </summary>
        private void UpdateLayout()
        {
            int count = _browsers.Count;
            double gridWidth = BrowserGrid.ActualWidth > 0 ? BrowserGrid.ActualWidth : 1180; // 减去 margin
            
            int columns;
            double browserWidth;
            double browserHeight;

            // 根据数量决定列数
            if (count == 1)
            {
                // 1个：填满
                columns = 1;
                browserWidth = gridWidth;
                browserHeight = 650;
            }
            else if (count == 2)
            {
                // 2个：左右分
                columns = 2;
                browserWidth = gridWidth / 2 - 5;
                browserHeight = 650;
            }
            else if (count <= 4)
            {
                // 3-4个：2列
                columns = 2;
                browserWidth = gridWidth / 2 - 5;
                browserHeight = count == 3 ? 320 : 300; // 3个时第一行2个，第二行1个
            }
            else if (count <= 6)
            {
                // 5-6个：3列
                columns = 3;
                browserWidth = gridWidth / 3 - 5;
                browserHeight = 300;
            }
            else if (count <= 9)
            {
                // 7-9个：3列
                columns = 3;
                browserWidth = gridWidth / 3 - 5;
                browserHeight = 210;
            }
            else if (count <= 12)
            {
                // 10-12个：4列
                columns = 4;
                browserWidth = gridWidth / 4 - 5;
                browserHeight = 210;
            }
            else
            {
                // 超过12个：4列，滚动显示
                columns = 4;
                browserWidth = gridWidth / 4 - 5;
                browserHeight = 210;
            }

            // 设置每个浏览器的大小
            foreach (var container in BrowserGrid.Children.OfType<System.Windows.Controls.StackPanel>())
            {
                if (container.Children.Count >= 2 && 
                    container.Children[1] is ChromiumWebBrowser browser)
                {
                    browser.Width = browserWidth;
                    browser.Height = browserHeight - 18; // 减去 URL 标签高度
                    browser.Margin = new System.Windows.Thickness(0);
                }
            }
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
                    browser.Dispose();
                    _browsers.Remove(accountId);
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

                // 3. 注入采集脚本（根据任务类型）
                var collectScript = GenerateCollectScript(expectedCount, taskType);
                var result = await browser.EvaluateScriptAsync(collectScript);

                if (result.Success && result.Result != null)
                {
                    var jsonData = result.Result.ToString();
                    System.Diagnostics.Debug.WriteLine($"✅ 采集完成，数据长度: {jsonData.Length}");

                    // 4. 触发回调，将数据传回（包含 detailId）
                    OnCollectionComplete?.Invoke(CurrentDetailId ?? "", accountId, jsonData);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"⚠️ 采集脚本执行失败: {result.Message}");
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
            // 根据任务类型选择不同的解析器
            if (taskType == 3) // 用户采集
            {
                return GenerateUserCollectScript(expectedCount);
            }
            else // 默认主页采集
            {
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
    }
}
