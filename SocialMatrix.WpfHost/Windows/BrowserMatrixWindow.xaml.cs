using CefSharp;
using CefSharp.Wpf;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SocialMatrix.WpfHost.Helpers;
using SocialMatrix.WpfHost.Services;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Windows;

namespace SocialMatrix.WpfHost.Windows
{
    /// <summary>
    /// 浏览器矩阵窗口 - 独立弹窗显示
    /// </summary>
    public partial class BrowserMatrixWindow : Window
    {
        private readonly Dictionary<string, ChromiumWebBrowser> _browsers = new();
        private readonly Dictionary<string, System.Windows.Controls.TabItem> _browserTabs = new();
        private readonly Dictionary<string, System.Windows.Controls.Grid> _browserContainers = new();
        private readonly Dictionary<string, bool> _browserInitialized = new(); // 跟踪指纹注入状态
        private readonly Dictionary<string, TaskCompletionSource<FacebookPageState>> _browserReadySignals = new();
        private readonly Dictionary<string, string> _browserLoadErrors = new(); // 账号最近一次页面加载错误
        private readonly object _browserLoadErrorLock = new();
        private readonly ConcurrentDictionary<string, int> _accountTaskTypes = new(); // 账号 -> 任务类型映射
        private readonly ConcurrentDictionary<string, string> _accountDetailIds = new(); // 账号 -> 任务明细ID
        // 一个账号只有一个浏览器 Tab，也只能同时执行一个业务任务。
        // 采集、AI 获客、运营、私信和资料任务都必须经过这里。
        private readonly ConcurrentDictionary<string, string> _activeAccountTasks = new(); // 账号 -> 当前任务明细ID
        private readonly Dictionary<string, IRequestContext> _requestContexts = new(); // 账号 -> 独立请求上下文
        private readonly ConcurrentDictionary<string, (string fbUserId, string messageText)> _dmOperationParams = new(); // 账号 -> 私信参数
        private readonly ConcurrentDictionary<string, string> _dmTaskIds = new(); // 账号 -> 私信主任务ID
        private readonly ConcurrentDictionary<string, bool> _accountIsOperation = new(); // 账号 -> 是否为运营任务
        private readonly ConcurrentDictionary<string, byte> _dmSendingAccounts = new(); // 账号 -> 私信发送中，避免同账号并发串消息
        private readonly ConcurrentDictionary<string, ChromiumWebBrowser> _initialLoadBrowsers = new();
        private volatile bool _isClosing;

        public enum FacebookPageState
        {
            Authenticated,
            LoginPage,
            NetworkError,
            Checkpoint,
            AccountDisabled,
            VerificationRequired,
            PageLoading,
            Unknown
        }

        // 采集结果回调
        public event Action<string, string, string, int>? OnCollectionComplete; // (detailId, accountId, jsonData, taskType)
        public event Action<string, string, string, int>? OnCollectionBatch; // (detailId, accountId, jsonData, taskType)
        public event Action<string, string>? OnCollectionError;    // (accountId, errorMessage)

        public void NotifyCollectionComplete(string detailId, string accountId, string jsonData, int taskType)
        {
            OnCollectionComplete?.Invoke(detailId, accountId, jsonData, taskType);
        }

        /// <summary>
        /// 注册一个账号的首页初始化完成等待。调用方必须在创建浏览器前注册。
        /// </summary>
        public Task<FacebookPageState> WaitForBrowserReadyAsync(string accountId)
        {
            var signal = new TaskCompletionSource<FacebookPageState>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            _browserReadySignals[accountId] = signal;
            return signal.Task;
        }

        public string? GetActiveDetailId(string accountId)
        {
            return _activeAccountTasks.TryGetValue(accountId, out var detailId) ? detailId : null;
        }

        public int GetActiveTaskType(string accountId)
        {
            return _accountTaskTypes.TryGetValue(accountId, out var taskType) ? taskType : 0;
        }

        // 最大并发数配置（从后端读取，默认19 - 8GB内存推荐值）
        private static int _maxConcurrentBrowsers = 19;
        public static int MaxConcurrentBrowsers => _maxConcurrentBrowsers;
        private static FingerprintGlobalConfig? _globalConfig = null;
        private static DateTime _configLastFetchTime = DateTime.MinValue;
        private static readonly HashSet<BrowserMatrixWindow> _instances = new();

        // IsVisible 在窗口进入关闭动画/清理阶段仍可能为 true，不能据此复用旧窗口。
        public bool IsWindowAvailable => IsVisible && !_isClosing && IsLoaded;

        /// <summary>
        /// 指纹浏览器全局配置
        /// </summary>
        private class FingerprintGlobalConfig
        {
            public bool DisableImages { get; set; } = false;  // 默认加载图片，按配置关闭
            public bool DisableVideos { get; set; } = true;   // 默认不加载视频
            public int MaxConcurrent { get; set; } = 19;      // 8GB内存推荐值：(8192 * 0.7) / 300 ≈ 19
        }

        // 测试排查时设为 true，任务结束后保留浏览器；正常运行改为 false。
        private const bool KeepBrowserAfterTask = true;
        internal static bool KeepBrowserAfterTaskForDebug => KeepBrowserAfterTask;

        /// <summary>
        /// 全局配置由Vue通过JsBridge同步，WPF不直接访问后台。
        /// </summary>
        private static async Task<FingerprintGlobalConfig?> GetGlobalConfigAsync()
        {
            await Task.CompletedTask;
            return _globalConfig ?? new FingerprintGlobalConfig();
        }

        /// <summary>
        /// 公开方法：从前端接收配置并更新缓存（立即生效）
        /// </summary>
        public static void UpdateGlobalConfig(bool disableImages, bool disableVideos, int maxConcurrent)
        {
            var previousDisableImages = _globalConfig?.DisableImages ?? false;
            _globalConfig = new FingerprintGlobalConfig
            {
                DisableImages = disableImages,
                DisableVideos = disableVideos,
                MaxConcurrent = Math.Min(Math.Max(maxConcurrent, 1), 50)
            };
            _configLastFetchTime = DateTime.Now;
            _maxConcurrentBrowsers = _globalConfig.MaxConcurrent;
            FbFingerprintBrowserFactory.UpdateGlobalConfig(disableImages, disableVideos, maxConcurrent);

            // Existing tabs already own a RequestHandler. Apply the new policy to
            // every live matrix window and reload only when image blocking changed,
            // otherwise previously cancelled image requests stay absent.
            var reloadImages = previousDisableImages != disableImages;
            foreach (var window in _instances.ToList())
            {
                window.ApplyGlobalResourcePolicy(reloadImages);
            }

            System.Diagnostics.Debug.WriteLine($"✅ 全局配置已更新（来自前端）: DisableImages={disableImages}, DisableVideos={disableVideos}, MaxConcurrent={maxConcurrent}");
        }

        public BrowserMatrixWindow()
        {
            InitializeComponent();
            _instances.Add(this);
            BrowserTabs.Loaded += (_, _) => UpdateBrowserTabNavigationVisibility();
            BrowserTabs.SelectionChanged += (_, _) =>
            {
                ShowSelectedBrowserTab();
                ScrollSelectedBrowserTabIntoView();
                UpdateBrowserTabNavigationVisibility();
            };

            // 监听窗口大小变化，重新计算布局
            this.SizeChanged += (sender, e) =>
            {
                UpdateLayout();
                UpdateBrowserTabNavigationVisibility();
            };

            // 监听窗口关闭事件，清理所有资源
            this.Closed += (sender, e) =>
            {
                _isClosing = true;
                // 用户关闭整个矩阵窗口时，所有仍在运行的明细都必须通知 Vue 结束，
                // 否则前端账号队列会保留到超时，阻塞下一次立即执行。
                foreach (var item in _accountDetailIds.ToArray())
                {
                    if (!string.IsNullOrWhiteSpace(item.Value))
                    {
                        OnCollectionError?.Invoke(item.Key, "浏览器矩阵窗口已关闭，当前任务已停止");
                    }
                }
                _instances.Remove(this);
                CleanupAllResources();
            };

            // Closing 比 Closed 更早触发，避免新任务在资源清理期间拿到旧窗口。
            this.Closing += (sender, e) =>
            {
                _isClosing = true;
            };

            // 预拉取全局配置，创建浏览器时可直接使用拦截设置
            _ = GetGlobalConfigAsync();
        }

        private void ApplyGlobalResourcePolicy(bool reloadImages)
        {
            var config = _globalConfig ?? new FingerprintGlobalConfig();
            foreach (var browser in _browsers.Values.ToList())
            {
                if (browser.IsDisposed) continue;
                FingerprintInjector.ApplyResourceFilter(browser, config.DisableImages, config.DisableVideos);
                if (reloadImages && !browser.IsLoading)
                {
                    browser.Reload(ignoreCache: true);
                }
            }
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
            _browserTabs.Clear();
            _browserContainers.Clear();
            _browserInitialized.Clear();
            _initialLoadBrowsers.Clear();
            _browserReadySignals.Clear();
            lock (_browserLoadErrorLock)
            {
                _browserLoadErrors.Clear();
            }
            _accountTaskTypes.Clear();
            _accountDetailIds.Clear();
            _activeAccountTasks.Clear();
            _dmOperationParams.Clear();
            _dmTaskIds.Clear();
            _accountIsOperation.Clear();
            _dmSendingAccounts.Clear();

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
            string? cookie = null, string? searchUrl = null, int expectedCount = 100, long? deviceId = null, int taskType = 1, string? config = null, string? detailId = null, bool isOperation = false,
            string? password = null, string? tfa = null, string? loginAccountId = null, string? proxyConfigJson = null)
        {
            var taskLockAcquired = false;
            if (!string.IsNullOrWhiteSpace(detailId) && !string.IsNullOrWhiteSpace(searchUrl))
            {
                if (!_activeAccountTasks.TryAdd(accountId, detailId))
                {
                    var activeDetailId = _activeAccountTasks[accountId];
                    if (string.Equals(activeDetailId, detailId, StringComparison.Ordinal))
                    {
                        System.Diagnostics.Debug.WriteLine(
                            $"⏭️ 忽略重复任务启动: account={accountId}, detailId={detailId}, taskType={taskType}");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine(
                            $"⛔ 拒绝账号并行任务: account={accountId}, 当前明细={activeDetailId}, 新明细={detailId}, taskType={taskType}");
                        OnCollectionError?.Invoke(accountId,
                            $"账号正在执行任务（明细 {activeDetailId}），当前任务已排队等待前一任务完成");
                    }
                    return;
                }

                taskLockAcquired = true;
                System.Diagnostics.Debug.WriteLine(
                    $"🔒 获取账号任务锁: account={accountId}, detailId={detailId}, taskType={taskType}, operation={isOperation}");
            }

            if (!string.IsNullOrEmpty(detailId))
            {
                _accountDetailIds[accountId] = detailId;
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

            // 如果浏览器已存在，检查是否需要重新采集
            if (_browsers.TryGetValue(accountId, out var staleBrowser) && staleBrowser.IsDisposed)
            {
                System.Diagnostics.Debug.WriteLine($"♻️ 账号 {accountId} 的浏览器已失效，清理旧 Tab 后重新创建");
                RemoveBrowserState(accountId, disposeBrowser: false);
            }

            if (_browsers.ContainsKey(accountId))
            {
                System.Diagnostics.Debug.WriteLine($"⚠️ 账号 {accountId} 的浏览器已存在");
                _accountTaskTypes[accountId] = taskType;

                // 如果提供了新的搜索 URL，重新启动任务
                if (!string.IsNullOrEmpty(searchUrl))
                {
                    var existingBrowser = _browsers[accountId];
                    System.Diagnostics.Debug.WriteLine($"🔄 为已存在的浏览器启动新任务: {searchUrl}, taskType={taskType}");

                    // Profile editing needs image requests enabled before navigating to
                    // the profile page. Otherwise an existing tab keeps the old
                    // image-blocking handler during the navigation itself.
                    if (taskType == 18)
                    {
                        var profileConfig = _globalConfig ?? new FingerprintGlobalConfig();
                        FingerprintInjector.ApplyResourceFilter(existingBrowser, false, profileConfig.DisableVideos);
                    }

                    // 异步启动（不阻塞）
                    Task.Run(async () =>
                    {
                        if (isOperation)
                        {
                            // 运营任务走独立分发
                            await StartOperationTask(existingBrowser, accountId, searchUrl, expectedCount, taskType, config, detailId);
                        }
                        else
                        {
                            // 采集任务走采集逻辑
                            await StartAutoCollect(existingBrowser, accountId, searchUrl, expectedCount, taskType, config, detailId);
                        }
                    });
                }
                return;
            }

            // 只有创建新账号 Tab 时才占用新的浏览器槽位
            if (_browsers.Count >= _maxConcurrentBrowsers)
            {
                if (taskLockAcquired) ReleaseAccountTask(accountId, detailId);
                System.Diagnostics.Debug.WriteLine($"⚠️ 已达到最大并发数限制 ({_maxConcurrentBrowsers})，无法为账号 {accountId} 创建新浏览器");
                OnCollectionError?.Invoke(accountId, $"已达到最大并发数限制 ({_maxConcurrentBrowsers})，请先关闭一些浏览器窗口");
                return;
            }

            // 为每个账号创建独立的 RequestContext（实现完全隔离）
            ChromiumWebBrowser browser;
            IRequestContext requestContext;
            try
            {
                browser = FbFingerprintBrowserFactory.Create(accountId, deviceId, proxyConfigJson, out requestContext);
            }
            catch (Exception ex)
            {
                if (taskLockAcquired) ReleaseAccountTask(accountId, detailId);
                System.Diagnostics.Debug.WriteLine($"❌ 账号 {accountId} 浏览器创建失败: {ex.Message}");
                OnCollectionError?.Invoke(accountId, $"浏览器创建失败: {ex.Message}");
                return;
            }
            _requestContexts[accountId] = requestContext;

            browser.JavascriptMessageReceived += (_, args) =>
            {
                try
                {
                    var message = args.Message?.ToString();
                    if (string.IsNullOrWhiteSpace(message)) return;
                    var payload = JObject.Parse(message);
                    if (!string.Equals(payload["type"]?.ToString(), "collection-batch", StringComparison.Ordinal)) return;
                    var results = payload["results"] as JArray;
                    if (results == null || results.Count == 0) return;
                    var activeDetailId = _accountDetailIds.TryGetValue(accountId, out var value) ? value : "";
                    var activeTaskType = _accountTaskTypes.TryGetValue(accountId, out var type) ? type : 1;
                    OnCollectionBatch?.Invoke(activeDetailId, accountId, results.ToString(Formatting.None), activeTaskType);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"⚠️ 采集分批回传解析失败: {ex.Message}");
                }
            };

            browser.LoadError += (_, args) =>
            {
                // CefSharp uses ERR_ABORTED for normal redirects/navigation cancellation.
                if (args.ErrorCode == CefSharp.CefErrorCode.Aborted) return;
                lock (_browserLoadErrorLock)
                {
                    _browserLoadErrors[accountId] = $"{args.ErrorCode}: {args.ErrorText}";
                }
                System.Diagnostics.Debug.WriteLine(
                    $"⚠️ 账号 {accountId} 页面加载错误: {args.ErrorCode}, url={args.FailedUrl}");
            };

            // 立即应用缓存的资源拦截配置（首次 Load 即生效）
            var cachedConfig = _globalConfig ?? new FingerprintGlobalConfig();
            FingerprintInjector.ApplyResourceFilter(browser, cachedConfig.DisableImages, cachedConfig.DisableVideos);

            // 仅在 Debug 模式下启用右键菜单和开发者工具
#if DEBUG
            browser.MenuHandler = new CustomMenuHandler();
#endif

            // 创建 Tab 内容容器：顶部显示 URL，浏览器占满剩余空间
            var tabDisplayAccount = ResolveTabDisplayAccount(accountId, cookie);
            var container = new System.Windows.Controls.Grid();
            container.Tag = accountId; // 保存 accountId 以便后续查找
            container.Visibility = Visibility.Hidden;
            container.RowDefinitions.Add(new System.Windows.Controls.RowDefinition
            {
                Height = new System.Windows.GridLength(18)
            });
            container.RowDefinitions.Add(new System.Windows.Controls.RowDefinition
            {
                Height = new System.Windows.GridLength(1, System.Windows.GridUnitType.Star)
            });

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

            System.Windows.Controls.Grid.SetRow(urlLabel, 0);
            System.Windows.Controls.Grid.SetRow(browser, 1);
            container.Children.Add(urlLabel);
            container.Children.Add(browser);

            _browsers[accountId] = browser;
            _initialLoadBrowsers[accountId] = browser;
            _accountTaskTypes[accountId] = taskType; // 保存账号对应的任务类型

            // 每个账号 Tab 使用独立的关闭按钮，关闭时只释放当前账号的浏览器资源。
            var tabHeader = new System.Windows.Controls.StackPanel
            {
                Orientation = System.Windows.Controls.Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };
            tabHeader.Children.Add(new System.Windows.Controls.TextBlock
            {
                Text = tabDisplayAccount,
                MaxWidth = 180,
                TextTrimming = System.Windows.TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center
            });

            var closeTabButton = new System.Windows.Controls.Button
            {
                Content = "x",
                Width = 20,
                Height = 20,
                Margin = new Thickness(6, 0, 0, 0),
                Padding = new Thickness(0),
                BorderThickness = new Thickness(0),
                Background = System.Windows.Media.Brushes.Transparent,
                Foreground = System.Windows.Media.Brushes.DimGray,
                ToolTip = "关闭此账号"
            };
            closeTabButton.Click += (sender, args) =>
            {
                args.Handled = true;
                // 手动关闭 Tab 等同于终止当前任务。先通知 Vue 持久化明细失败并释放前端账号队列，
                // 否则 Vue 会一直认为该账号仍在运行，后续任务无法领取到可用浏览器槽位。
                if (_accountDetailIds.TryGetValue(accountId, out var detailId)
                    && !string.IsNullOrWhiteSpace(detailId))
                {
                    OnCollectionError?.Invoke(accountId, "浏览器已被手动关闭，当前任务已停止");
                }
                CloseBrowser(accountId);
                if (GetActiveBrowserCount() == 0)
                {
                    Close();
                }
            };
            tabHeader.Children.Add(closeTabButton);

            var tab = new System.Windows.Controls.TabItem
            {
                Header = tabHeader,
                Tag = accountId
            };
            _browserTabs[accountId] = tab;
            _browserContainers[accountId] = container;
            BrowserHostGrid.Children.Add(container);
            BrowserTabs.Items.Add(tab);
            BrowserTabs.SelectedItem = tab;
            ShowSelectedBrowserTab();
            Dispatcher.BeginInvoke(new Action(UpdateBrowserTabNavigationVisibility),
                System.Windows.Threading.DispatcherPriority.Loaded);

            // 更新布局
            UpdateLayout();

            System.Diagnostics.Debug.WriteLine($"✅ 已为账号 {accountId} 创建浏览器");

            // LoadingStateChanged 在 CEF 后台线程触发，访问 browser 属性必须切到 UI 线程
            browser.LoadingStateChanged += (sender, e) =>
            {
                if (e.IsLoading)
                {
                    lock (_browserLoadErrorLock)
                    {
                        _browserLoadErrors.Remove(accountId);
                    }
                    return;
                }

                _ = RunOnBrowserUiThreadAsync(browser, async () =>
                {
                    if (!browser.CanExecuteJavascriptInMainFrame) return;

                    var address = browser.Address ?? "";
                    if (address.StartsWith("about:", StringComparison.OrdinalIgnoreCase)) return;
                    if (_browserInitialized.ContainsKey(accountId) && _browserInitialized[accountId]) return;

                    // CEF 偶尔会先发出加载结束事件，但页面仍是空 DOM；此时不能标记为已初始化，
                    // 否则首次加载兜底逻辑会被跳过。
                    if (await IsBrowserBlankPageAsync(browser)) return;

                    _browserInitialized[accountId] = true;

                    try
                    {
                        var globalConfig = await GetGlobalConfigAsync();
                        FingerprintInjector.ApplyResourceFilter(
                            browser,
                            taskType == 18 ? false : globalConfig?.DisableImages ?? false,
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

                        var pageState = await DetectFacebookPageStateWithRetryAsync(browser, accountId);
                        if (pageState != FacebookPageState.Authenticated
                            && (!string.IsNullOrWhiteSpace(cookie)
                                || !string.IsNullOrWhiteSpace(password)
                                || !string.IsNullOrWhiteSpace(tfa)))
                        {
                            var loginRequest = new AccountLoginRequest(
                                long.TryParse(accountId, out var accountDbId) ? accountDbId : 0,
                                string.IsNullOrWhiteSpace(loginAccountId) ? accountId : loginAccountId,
                                password,
                                tfa,
                                cookie);
                            var loginResult = await LoginAccountInBrowserAsync(browser, loginRequest);
                            if (loginResult.Status == "success")
                            {
                                pageState = FacebookPageState.Authenticated;
                                System.Diagnostics.Debug.WriteLine($"✅ 账号 {accountId} 已复用账号管理登录流程完成登录");
                            }
                            else
                            {
                                System.Diagnostics.Debug.WriteLine($"❌ 账号 {accountId} 复用账号管理登录失败: {loginResult.ErrorReason}");
                            }
                        }
                        if (_browserReadySignals.TryGetValue(accountId, out var readySignal))
                        {
                            readySignal.TrySetResult(pageState);
                        }
                        // 账号管理登录没有采集 URL，登录状态由 LoginAccountWithBrowserAsync
                        // 统一判断；不要在首页 DOM 尚未完成时用通用采集回调误报 Unknown。
                        if (!string.IsNullOrEmpty(cookie) && !string.IsNullOrEmpty(searchUrl))
                        {
                            if (pageState != FacebookPageState.Authenticated)
                            {
                                var message = GetPageStateMessage(pageState);
                                System.Diagnostics.Debug.WriteLine($"⚠️ 账号 {accountId} 页面状态: {pageState}，{message}");
                                ReleaseAccountTask(accountId, detailId);
                                OnCollectionError?.Invoke(accountId, message);
                            }
                            else if (!string.IsNullOrEmpty(searchUrl))
                            {
                                System.Diagnostics.Debug.WriteLine($"✅ 账号 {accountId} Cookie 验证通过");
                                if (isOperation)
                                {
                                    await StartOperationTask(browser, accountId, searchUrl, expectedCount, taskType, config, detailId);
                                }
                                else
                                {
                                    await StartAutoCollect(browser, accountId, searchUrl, expectedCount, taskType, config, detailId);
                                }
                            }
                        }
                        else if (!string.IsNullOrEmpty(searchUrl))
                        {
                            if (isOperation)
                            {
                                await StartOperationTask(browser, accountId, searchUrl, expectedCount, taskType, config, detailId);
                            }
                            else
                            {
                                await StartAutoCollect(browser, accountId, searchUrl, expectedCount, taskType, config, detailId);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"❌ 账号 {accountId} 初始化失败: {ex.Message}");
                        if (_browserReadySignals.TryGetValue(accountId, out var failedReadySignal))
                        {
                            failedReadySignal.TrySetException(ex);
                        }
                        ReleaseAccountTask(accountId, detailId);
                        OnCollectionError?.Invoke(accountId, $"浏览器初始化失败: {ex.Message}");
                    }
                });
            };

            // 异步：拉取配置 → 注入 Cookie → 首次加载（只加载一次 Facebook）
            _ = InitializeBrowserAsync(browser, accountId, cookie, initialUrl, taskType == 18,
                password, tfa, string.IsNullOrWhiteSpace(loginAccountId) ? accountId : loginAccountId);
        }

        /// <summary>
        /// 浏览器创建后的异步初始化：配置资源拦截、注入 Cookie，再发起首次导航
        /// </summary>
        private async Task InitializeBrowserAsync(ChromiumWebBrowser browser, string accountId, string? cookie, string initialUrl, bool profileTask = false,
            string? password = null, string? tfa = null, string? loginAccountId = null)
        {
            try
            {
                var globalConfig = await GetGlobalConfigAsync() ?? new FingerprintGlobalConfig();
                System.Diagnostics.Debug.WriteLine($"🔍 全局配置: DisableImages={(profileTask ? false : globalConfig.DisableImages)}, DisableVideos={globalConfig.DisableVideos}");

                await RunOnBrowserUiThreadAsync(browser, async () =>
                {
                    FingerprintInjector.ApplyResourceFilter(browser, profileTask ? false : globalConfig.DisableImages, globalConfig.DisableVideos);

                    if (HasUsableFacebookCookie(cookie))
                    {
                        System.Diagnostics.Debug.WriteLine($"🍪 为账号 {accountId} 预注入 Cookie（首次加载前）...");
                        await InjectCookies(browser, accountId, cookie);
                    }
                    else if (!string.IsNullOrWhiteSpace(cookie))
                    {
                        System.Diagnostics.Debug.WriteLine($"ℹ️ 账号 {accountId} Cookie 为空或为 []，跳过 Cookie 注入，等待账号密码登录");
                    }

                    System.Diagnostics.Debug.WriteLine($"🔗 首次加载: {initialUrl}");
                    browser.Load(initialUrl);
                    _ = WatchInitialLoadAsync(browser, accountId, initialUrl);
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ 账号 {accountId} 预初始化失败: {ex.Message}");
                ReleaseAccountTask(accountId);
                OnCollectionError?.Invoke(accountId, $"浏览器预初始化失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 首次导航兜底：CEF 偶发停在 about:blank/空白 DOM 时，主动重载 Facebook。
        /// </summary>
        private async Task WatchInitialLoadAsync(ChromiumWebBrowser browser, string accountId, string initialUrl)
        {
            const int maxAttempts = 2;
            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                await Task.Delay(attempt == 1 ? 8000 : 12000);

                if (!IsInitialLoadBrowserActive(browser, accountId))
                {
                    return;
                }

                bool isBlank = await IsBrowserBlankPageAsync(browser);
                if (!IsInitialLoadBrowserActive(browser, accountId))
                {
                    return;
                }
                if (!isBlank)
                {
                    return;
                }

                System.Diagnostics.Debug.WriteLine($"⚠️ 账号 {accountId} 首次加载空白，重试 {attempt}/{maxAttempts}: {initialUrl}");
                await RunOnBrowserUiThreadAsync(browser, () =>
                {
                    if (IsInitialLoadBrowserActive(browser, accountId))
                    {
                        _browserInitialized[accountId] = false;
                        browser.Load(initialUrl);
                    }
                    return Task.CompletedTask;
                });
            }

            if (!IsInitialLoadBrowserActive(browser, accountId))
            {
                return;
            }
            if (await IsBrowserBlankPageAsync(browser))
            {
                var err = $"账号 {accountId} 首次加载仍为空白页，请重试登录";
                System.Diagnostics.Debug.WriteLine($"❌ {err}");
                OnCollectionError?.Invoke(accountId, err);
                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (_browsers.ContainsKey(accountId))
                    {
                        RemoveBrowserState(accountId, disposeBrowser: true);
                    }
                }));
            }
        }

        private bool IsInitialLoadBrowserActive(ChromiumWebBrowser browser, string accountId)
        {
            return !_isClosing
                && !browser.IsDisposed
                && _initialLoadBrowsers.TryGetValue(accountId, out var activeBrowser)
                && ReferenceEquals(activeBrowser, browser);
        }

        private async Task<bool> IsBrowserBlankPageAsync(ChromiumWebBrowser browser)
        {
            try
            {
                return await RunOnBrowserUiThreadAsync(browser, async () =>
                {
                    if (browser.IsDisposed)
                    {
                        return true;
                    }

                    var address = browser.Address ?? "";
                    if (string.IsNullOrWhiteSpace(address) ||
                        address.StartsWith("about:", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }

                    if (browser.IsLoading || !browser.CanExecuteJavascriptInMainFrame)
                    {
                        return false;
                    }

                    var result = await browser.EvaluateScriptAsync(@"
(function() {
    try {
        const bodyText = (document.body && document.body.innerText || '').trim();
        const elementCount = document.body ? document.body.querySelectorAll('*').length : 0;
        return { readyState: document.readyState, title: document.title || '', bodyLength: bodyText.length, elementCount };
    } catch (e) {
        return { error: String(e), bodyLength: 0, elementCount: 0 };
    }
})();");
                    if (!result.Success || result.Result == null)
                    {
                        return true;
                    }

                    var json = Newtonsoft.Json.JsonConvert.SerializeObject(result.Result);
                    var state = Newtonsoft.Json.Linq.JObject.Parse(json);
                    var bodyLength = state.Value<int?>("bodyLength") ?? 0;
                    var elementCount = state.Value<int?>("elementCount") ?? 0;
                    return bodyLength == 0 && elementCount == 0;
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"⚠️ 检测空白页失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 更新当前 Tab 内浏览器尺寸
        /// </summary>
        private void UpdateLayout()
        {
            foreach (var browser in _browsers.Values)
            {
                browser.Width = double.NaN;
                browser.Height = double.NaN;
                browser.HorizontalAlignment = HorizontalAlignment.Stretch;
                browser.VerticalAlignment = VerticalAlignment.Stretch;
                browser.Margin = new System.Windows.Thickness(0);
            }
        }

        /// <summary>
        /// 所有浏览器都常驻在 BrowserHostGrid 中，Tab 切换只改变显示状态。
        /// 使用 Hidden 而不是 Collapsed，避免切换时重新创建或重新布局浏览器。
        /// </summary>
        private static string ResolveTabDisplayAccount(string accountId, string? cookieJson)
        {
            if (string.IsNullOrWhiteSpace(cookieJson)) return accountId;

            try
            {
                var token = JToken.Parse(cookieJson);
                var cookie = token is JArray array
                    ? array.FirstOrDefault(item => string.Equals(item["name"]?.ToString(), "c_user", StringComparison.OrdinalIgnoreCase))
                    : token["c_user"] != null ? token["c_user"] : null;
                var cUser = cookie?["value"]?.ToString();
                if (!string.IsNullOrWhiteSpace(cUser)) return cUser;
            }
            catch (JsonException)
            {
                var match = Regex.Match(cookieJson, @"(?:^|;\s*)c_user=(?<id>[^;]+)", RegexOptions.IgnoreCase);
                if (match.Success && !string.IsNullOrWhiteSpace(match.Groups["id"].Value))
                {
                    return match.Groups["id"].Value;
                }
            }

            return accountId;
        }

        private void ShowSelectedBrowserTab()
        {
            var selectedAccountId = (BrowserTabs.SelectedItem as System.Windows.Controls.TabItem)?.Tag?.ToString();
            foreach (var pair in _browserContainers)
            {
                pair.Value.Visibility = string.Equals(pair.Key, selectedAccountId, StringComparison.Ordinal)
                    ? Visibility.Visible
                    : Visibility.Hidden;
            }
        }

        private void PreviousBrowserTab_Click(object sender, RoutedEventArgs e)
        {
            SelectBrowserTabByOffset(-1);
        }

        private void NextBrowserTab_Click(object sender, RoutedEventArgs e)
        {
            SelectBrowserTabByOffset(1);
        }

        private void SelectBrowserTabByOffset(int offset)
        {
            var count = BrowserTabs.Items.Count;
            if (count == 0) return;

            var currentIndex = BrowserTabs.SelectedIndex;
            if (currentIndex < 0) currentIndex = 0;
            var nextIndex = Math.Clamp(currentIndex + offset, 0, count - 1);
            BrowserTabs.SelectedIndex = nextIndex;
            ScrollSelectedBrowserTabIntoView();
        }

        private void ScrollSelectedBrowserTabIntoView()
        {
            if (BrowserTabs.SelectedItem is System.Windows.Controls.TabItem tab)
            {
                tab.BringIntoView();
            }
        }

        private void UpdateBrowserTabNavigationVisibility()
        {
            if (!BrowserTabs.IsLoaded) return;
            BrowserTabs.ApplyTemplate();
            if (BrowserTabs.Template.FindName("HeaderScrollViewer", BrowserTabs) is not System.Windows.Controls.ScrollViewer scrollViewer)
            {
                return;
            }

            void UpdateButtons()
            {
                var overflow = scrollViewer.ExtentWidth > scrollViewer.ViewportWidth + 1;
                var visibility = overflow ? Visibility.Visible : Visibility.Collapsed;
                if (BrowserTabs.Template.FindName("PreviousTabButton", BrowserTabs) is System.Windows.Controls.Button previous)
                {
                    previous.Visibility = visibility;
                }
                if (BrowserTabs.Template.FindName("NextTabButton", BrowserTabs) is System.Windows.Controls.Button next)
                {
                    next.Visibility = visibility;
                }
            }

            scrollViewer.SizeChanged -= BrowserTabHeaderScrollViewer_SizeChanged;
            scrollViewer.SizeChanged += BrowserTabHeaderScrollViewer_SizeChanged;
            UpdateButtons();

            void BrowserTabHeaderScrollViewer_SizeChanged(object sender, SizeChangedEventArgs args)
            {
                UpdateButtons();
            }
        }

        /// <summary>
        /// 关闭浏览器实例
        /// </summary>
        public void CloseBrowser(string accountId)
        {
            if (Application.Current?.Dispatcher != null
                && !Application.Current.Dispatcher.CheckAccess())
            {
                Application.Current.Dispatcher.Invoke(() => CloseBrowser(accountId));
                return;
            }

            if (!_browsers.ContainsKey(accountId)) return;
            RemoveBrowserState(accountId, disposeBrowser: true);
            System.Diagnostics.Debug.WriteLine($"✅ 已关闭账号 {accountId} 的浏览器");
        }

        private void RemoveBrowserState(string accountId, bool disposeBrowser)
        {
            // 手动关闭或异常清理必须立即释放账号任务锁；旧任务稍后返回时会按明细ID校验，不能误释放新任务。
            ReleaseAccountTask(accountId);

            if (_browsers.TryGetValue(accountId, out var browser))
            {
                if (disposeBrowser && !browser.IsDisposed)
                {
                    browser.Dispose();
                }
                _browsers.Remove(accountId);
            }
            _initialLoadBrowsers.TryRemove(accountId, out _);

            if (_requestContexts.TryGetValue(accountId, out var requestContext))
            {
                try { requestContext.Dispose(); } catch { }
                _requestContexts.Remove(accountId);
                System.Diagnostics.Debug.WriteLine($"🗑️ 已释放账号 {accountId} 的请求上下文");
            }

            if (_browserTabs.TryGetValue(accountId, out var tab))
            {
                BrowserTabs.Items.Remove(tab);
                _browserTabs.Remove(accountId);
            }

            if (_browserContainers.TryGetValue(accountId, out var container))
            {
                BrowserHostGrid.Children.Remove(container);
                _browserContainers.Remove(accountId);
            }

            _browserInitialized.Remove(accountId);
            _accountDetailIds.TryRemove(accountId, out _);
            _accountTaskTypes.TryRemove(accountId, out _);
            _accountIsOperation.TryRemove(accountId, out _);
            ShowSelectedBrowserTab();
            UpdateLayout();
        }

        private void ReleaseAccountTask(string accountId, string? detailId = null)
        {
            if (!_activeAccountTasks.TryGetValue(accountId, out var activeDetailId)) return;
            if (!string.IsNullOrWhiteSpace(detailId)
                && !string.Equals(activeDetailId, detailId, StringComparison.Ordinal)) return;

            if (_activeAccountTasks.TryRemove(
                    new KeyValuePair<string, string>(accountId, activeDetailId)))
            {
                System.Diagnostics.Debug.WriteLine(
                    $"🔓 释放账号任务锁: account={accountId}, detailId={activeDetailId}");
            }
        }

        private async Task<FacebookPageState> DetectFacebookPageStateWithRetryAsync(ChromiumWebBrowser browser, string accountId)
        {
            FacebookPageState state = FacebookPageState.Unknown;
            for (var attempt = 0; attempt < 5; attempt++)
            {
                state = await DetectFacebookPageStateAsync(browser, accountId);
                if (state == FacebookPageState.Authenticated
                    || state == FacebookPageState.LoginPage
                    || state == FacebookPageState.NetworkError
                    || state == FacebookPageState.Checkpoint
                    || state == FacebookPageState.AccountDisabled
                    || state == FacebookPageState.VerificationRequired)
                {
                    return state;
                }

                if (attempt < 4)
                {
                    await Task.Delay(1000);
                }
            }
            return state;
        }

        private async Task<FacebookPageState> DetectFacebookPageStateAsync(ChromiumWebBrowser browser, string accountId = "")
        {
            string loadError = "";
            lock (_browserLoadErrorLock)
            {
                _browserLoadErrors.TryGetValue(accountId, out var recordedError);
                loadError = recordedError ?? "";
            }
            if (!string.IsNullOrWhiteSpace(loadError))
            {
                System.Diagnostics.Debug.WriteLine($"⚠️ 账号 {accountId} 网络异常: {loadError}");
                return FacebookPageState.NetworkError;
            }

            try
            {
                if (!browser.CanExecuteJavascriptInMainFrame)
                {
                    return FacebookPageState.PageLoading;
                }

                var result = await browser.EvaluateScriptAsync(@"
(function() {
    const url = location.href.toLowerCase();
    const bodyText = (document.body?.innerText || '').toLowerCase();
    const visible = (el) => !!el && !!(el.offsetWidth || el.offsetHeight || el.getClientRects().length);
    const has = (selectors) => selectors.some(selector => {
        try { return visible(document.querySelector(selector)); } catch { return false; }
    });

    if (url.includes('/disabled_account') || url.includes('/account_disabled') ||
        bodyText.includes('account has been disabled') || bodyText.includes('account has been suspended') ||
        bodyText.includes('violated our community standards') || bodyText.includes('您的账号已被禁用')) {
        return 'ACCOUNT_DISABLED';
    }
    if (url.includes('/checkpoint') || url.includes('/recover') || url.includes('/confirmemail') ||
        bodyText.includes('confirm your identity') || bodyText.includes('verify your identity') ||
        bodyText.includes('需要验证身份')) {
        return 'VERIFICATION_REQUIRED';
    }
    // Facebook 2FA 页面可能仍保留 c_user Cookie，不能被首页 Cookie 兜底误判为已登录。
    if (url.includes('/two_step_verification/two_factor') ||
        url.includes('/two_factor/remember_browser')) {
        return 'VERIFICATION_REQUIRED';
    }

    const hasAuthCookie = /(?:^|;\s*)c_user=\d+/.test(document.cookie || '');
    const hasLoginForm = has([
        'form[action*=""/login""]', '#login_form', '[data-testid=""royal_login_form""]',
        '[data-testid=""login_form""]', 'input[name=""email""]', 'input[name=""pass""]',
        'input[type=""email""]', 'input[type=""password""]',
        'input[autocomplete=""username""]', 'input[autocomplete=""current-password""]',
        'input[aria-label*=""password"" i]', 'input[placeholder*=""password"" i]',
        'button[type=""submit""][name=""login""]', '[aria-label*=""Log In"" i]', '[aria-label*=""登录""]'
    ]);
    const hasPasswordInput = has([
        'input[type=""password""]', 'input[name=""pass""]',
        'input[autocomplete=""current-password""]',
        'input[aria-label*=""password"" i]', 'input[placeholder*=""password"" i]'
    ]);
    const hasLoginButton = has([
        '#loginbutton', 'button[name=""login""]', 'button[type=""submit""]',
        'input[type=""submit""]', '[role=""button""][aria-label*=""Log In"" i]',
        '[role=""button""][aria-label*=""登录""]',
        '[aria-label=""Log in"" i]', '[aria-label=""登录""]'
    ]);
    const hasEmailInput = has([
        'input[name=""email""]', 'input[type=""email""]',
        'input[autocomplete=""username""]'
    ]);
    const hasCredentialPair = hasEmailInput && hasPasswordInput;
    const hasLoginCopy = bodyText.includes('log in to facebook') ||
        bodyText.includes('email address or mobile number') ||
        bodyText.includes('登录 facebook');
    // Facebook 新版登录页经常使用 / 而不是 /login，且按钮是带 Log in aria-label 的 DIV。
    if ((hasCredentialPair && (hasLoginButton || hasLoginCopy)) ||
        (hasLoginForm && (url.includes('/login') || url.includes('login.php') || !has(['[role=""feed""]', '[data-pagelet=""MainFeed""]'])))) {
        return 'LOGIN_PAGE';
    }
    if ((url.includes('/login') || url.includes('login.php')) && !hasAuthCookie) {
        return 'LOGIN_PAGE';
    }
    if (hasPasswordInput && hasLoginButton && !hasAuthCookie) {
        return 'LOGIN_PAGE';
    }

    if (has(['[role=""feed""]', '[data-pagelet=""MainFeed""]', '[role=""main""]'])) {
        return 'AUTHENTICATED';
    }

    // Facebook 主页面由 React 异步渲染，弹窗、语言或新版 DOM 变化时可能暂时没有上述 feed 特征。
    // c_user 只能作为登录兜底，登录表单和明确异常页面仍优先返回，不会掩盖 Cookie 失效。
    const hasAuthenticatedChrome = has([
        '[role=""main""]', 'a[href*=""/profile.php""]', 'a[href*=""/friends""]', '[data-pagelet*=""Feed"" i]'
    ]);
    if (hasAuthCookie && (hasAuthenticatedChrome || bodyText.length > 80)) {
        return 'AUTHENTICATED';
    }
    if (document.readyState !== 'complete') return 'PAGE_LOADING';
    return 'UNKNOWN';
})();");

                if (!result.Success || result.Result == null)
                {
                    return FacebookPageState.Unknown;
                }

                // 页面脚本使用大写下划线格式（LOGIN_PAGE），而 C# 枚举使用 PascalCase（LoginPage）。
                // 直接 Enum.TryParse 会因下划线不一致失败，最终把明确状态误报为 Unknown。
                var rawState = result.Result.ToString() ?? "";
                var normalizedState = rawState.Replace("_", "", StringComparison.Ordinal);
                foreach (var candidate in Enum.GetValues<FacebookPageState>())
                {
                    if (string.Equals(normalizedState, candidate.ToString(), StringComparison.OrdinalIgnoreCase))
                    {
                        return candidate;
                    }
                }

                return FacebookPageState.Unknown;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"⚠️ 账号 {accountId} 页面状态检测失败: {ex.Message}");
                return FacebookPageState.NetworkError;
            }
        }

        private static string GetPageStateMessage(FacebookPageState state) => state switch
        {
            FacebookPageState.LoginPage => "Cookie已失效，需要重新登录",
            FacebookPageState.AccountDisabled => "账号被封或已停用",
            FacebookPageState.Checkpoint => "账号需要验证",
            FacebookPageState.VerificationRequired => "账号需要验证",
            FacebookPageState.NetworkError => "网络异常，暂未判定 Cookie 失效",
            FacebookPageState.PageLoading => "页面仍在加载，暂未判定 Cookie 失效",
            _ => "账号状态暂时无法确认"
        };

        /// <summary>
        /// 预注入 Cookie（在首次页面加载前写入，无需 Reload）
        /// </summary>
        /// <returns>true: 至少写入一个 Cookie</returns>
        private static bool HasUsableFacebookCookie(string? cookieJson)
        {
            if (string.IsNullOrWhiteSpace(cookieJson)) return false;

            try
            {
                var token = JToken.Parse(cookieJson);
                if (token is JArray array)
                {
                    return array.Any(item =>
                        string.Equals(item["name"]?.ToString(), "c_user", StringComparison.OrdinalIgnoreCase)
                        && !string.IsNullOrWhiteSpace(item["value"]?.ToString()));
                }
                return !string.IsNullOrWhiteSpace(token["c_user"]?.ToString());
            }
            catch (JsonException)
            {
                return Regex.IsMatch(cookieJson, @"(?:^|;\s*)c_user=[^;\s]+", RegexOptions.IgnoreCase);
            }
        }

        private async Task<bool> InjectCookies(ChromiumWebBrowser browser, string accountId, string cookieJson)
        {
            try
            {
                // 使用动态类型解析，避免枚举转换问题
                var cookieList = JArray.Parse(cookieJson);
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
                            Name = cookieData["name"]?.ToString(),
                            Value = cookieData["value"]?.ToString(),
                            Domain = cookieData["domain"]?.ToString(),
                            Path = cookieData["path"]?.ToString() ?? "/",
                            Secure = cookieData["secure"]?.Value<bool>() ?? false,
                            HttpOnly = cookieData["httpOnly"]?.Value<bool>() ?? false,
                            Expires = FacebookCookieExpirationHelper.Parse(cookieData["expirationDate"])
                        };

                        // 处理 sameSite 字段（可选）
                        if (cookieData["sameSite"] != null)
                        {
                            var sameSiteStr = cookieData["sameSite"]!.ToString();
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
            string searchUrl, int expectedCount, int taskType = 9, string? config = null, string? detailId = null)
        {
            System.Diagnostics.Debug.WriteLine(
                $"🚀 开始运营任务: account={accountId}, detailId={detailId}, taskType={taskType}, " +
                $"activeAccounts={_activeAccountTasks.Count}, thread={Environment.CurrentManagedThreadId}, url={searchUrl}");

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
                            string callbackDetailId = detailId
                                ?? (_accountDetailIds.TryGetValue(accountId, out var mappedDetailId) ? mappedDetailId : "");
                            OnCollectionComplete?.Invoke(callbackDetailId, accountId, addGroupJson, 9);
                        }
                        break;

                    case 14:
                        // 群发私信：等待页面完全加载，然后执行私信发送
                        System.Diagnostics.Debug.WriteLine($"📨 执行群发私信任务...");
                        await WaitForPageReady(browser, timeoutMs: 15000);
                        System.Diagnostics.Debug.WriteLine($"✅ 私信页面已就绪");

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
                                    string dmDetailId = detailId
                                        ?? (_accountDetailIds.TryGetValue(accountId, out var mappedDmDetailId) ? mappedDmDetailId : "");
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
                        else if (_dmOperationParams.TryGetValue(accountId, out var dmParams))
                        {
                            string dmTaskId = _dmTaskIds.TryGetValue(accountId, out var tid) ? tid : "";
                            string dmDetailId = detailId
                                ?? (_accountDetailIds.TryGetValue(accountId, out var mappedDmDetailId) ? mappedDmDetailId : "");
                            await SendDirectMessage(accountId, dmParams.fbUserId, dmParams.messageText, dmTaskId, dmDetailId);
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"⚠️ 未找到私信参数，config={config}");
                            OnCollectionError?.Invoke(accountId, "未找到私信参数");
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
                            string callbackDetailId = detailId
                                ?? (_accountDetailIds.TryGetValue(accountId, out var mappedDetailId) ? mappedDetailId : "");
                            string resultStr = result.Result?.ToString() ?? "[]";
                            System.Diagnostics.Debug.WriteLine($"✅ 转帖执行完成: {resultStr}");
                            OnCollectionComplete?.Invoke(callbackDetailId, accountId, resultStr, taskType);
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
                            string callbackDetailId = detailId
                                ?? (_accountDetailIds.TryGetValue(accountId, out var mappedDetailId) ? mappedDetailId : "");
                            string resultStr = followResult.Result?.ToString() ?? "[]";
                            System.Diagnostics.Debug.WriteLine($"✅ 刷粉执行完成: {resultStr}");
                            OnCollectionComplete?.Invoke(callbackDetailId, accountId, resultStr, 16);
                        }
                        else
                        {
                            OnCollectionError?.Invoke(accountId, $"刷粉JS执行失败: {followResult.Message}");
                        }
                        break;

                    case 17:
                        System.Diagnostics.Debug.WriteLine($"🌱 执行养号任务...");
                        await ExecuteWarmupTaskAsync(browser, accountId, config);
                        break;

                    case 18:
                        System.Diagnostics.Debug.WriteLine($"👤 执行 Facebook 资料上传任务...");
                        // 资料编辑必须能看到图片；已有账号 Tab 也切换到资料任务专用资源策略。
                        var profileGlobalConfig = await GetGlobalConfigAsync() ?? new FingerprintGlobalConfig();
                        FingerprintInjector.ApplyResourceFilter(browser, false, profileGlobalConfig.DisableVideos);
                        string profileJson;
                        try
                        {
                            profileJson = await ExecuteProfileUpdateAsync(browser, accountId, config);
                        }
                        finally
                        {
                            // Profile editing temporarily enables images. Restore the
                            // account's normal matrix policy after the task finishes.
                            FingerprintInjector.ApplyResourceFilter(
                                browser,
                                profileGlobalConfig.DisableImages,
                                profileGlobalConfig.DisableVideos);
                        }
                        var profileDetailId = detailId
                            ?? (_accountDetailIds.TryGetValue(accountId, out var mappedProfileDetailId) ? mappedProfileDetailId : "");
                        OnCollectionComplete?.Invoke(profileDetailId, accountId, profileJson, 18);
                        break;

                    case 19:
                        System.Diagnostics.Debug.WriteLine($"📨 执行消息监控任务...");
                        await WaitForPageReady(browser, timeoutMs: 30000);
                        var monitorResult = await ExecuteMessageMonitorTaskAsync(browser);
                        var monitorDetailId = detailId
                            ?? (_accountDetailIds.TryGetValue(accountId, out var mappedMonitorDetailId) ? mappedMonitorDetailId : "");
                        OnCollectionComplete?.Invoke(monitorDetailId, accountId, monitorResult, 19);
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
            finally
            {
                ReleaseAccountTask(accountId, detailId);
                System.Diagnostics.Debug.WriteLine(
                    $"🏁 运营任务结束: account={accountId}, detailId={detailId}, " +
                    $"activeAccounts={_activeAccountTasks.Count}, thread={Environment.CurrentManagedThreadId}");
            }
        }

        private async Task ExecuteWarmupTaskAsync(ChromiumWebBrowser browser, string accountId, string? configJson)
        {
            var config = string.IsNullOrWhiteSpace(configJson)
                ? new Newtonsoft.Json.Linq.JObject()
                : Newtonsoft.Json.Linq.JObject.Parse(configJson);
            var actions = config["actions"] is Newtonsoft.Json.Linq.JArray actionArray
                ? actionArray.Values<string>().Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.OrdinalIgnoreCase).ToList()
                : new List<string>();

            if (actions.Count == 0)
            {
                OnCollectionError?.Invoke(accountId, "未选择养号动作");
                if (!KeepBrowserAfterTask)
                {
                    CloseBrowser(accountId);
                }
                return;
            }

            int durationMinutes = Math.Clamp(config.Value<int?>("durationMinutes") ?? 20, 1, 1440);
            int minStaySeconds = Math.Clamp(config.Value<int?>("minStaySeconds") ?? 15, 3, 3600);
            int maxStaySeconds = Math.Clamp(config.Value<int?>("maxStaySeconds") ?? 45, minStaySeconds, 3600);
            int maxFriendProfiles = Math.Clamp(config.Value<int?>("maxFriendProfiles") ?? 5, 1, 100);
            int maxReels = Math.Clamp(config.Value<int?>("maxReels") ?? 20, 1, 500);
            bool enableLike = config.Value<bool?>("enableLike") ?? false;
            int likeProbability = Math.Clamp(config.Value<int?>("likeProbability") ?? 0, 0, 100);
            var random = new Random();
            var deadline = DateTime.UtcNow.AddMinutes(durationMinutes);
            using var durationCts = new CancellationTokenSource();
            var durationWatchdog = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(TimeSpan.FromMinutes(durationMinutes), durationCts.Token);
                    if (!durationCts.IsCancellationRequested && !browser.IsDisposed)
                    {
                        System.Diagnostics.Debug.WriteLine(
                            KeepBrowserAfterTask
                                ? $"⏱️ 账号 {accountId} 养号达到设定时长 {durationMinutes} 分钟，测试保留浏览器"
                                : $"⏱️ 账号 {accountId} 养号达到设定时长 {durationMinutes} 分钟，立即关闭浏览器");
                        durationCts.Cancel();
                        if (!KeepBrowserAfterTask)
                        {
                            CloseBrowser(accountId);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    // 任务提前完成时取消 watchdog。
                }
            });
            int friendProfiles = 0;
            int reels = 0;

            System.Diagnostics.Debug.WriteLine($"🌱 养号配置: actions={string.Join(',', actions)}, duration={durationMinutes}m");

            try
            {
                while (DateTime.UtcNow < deadline && !durationCts.IsCancellationRequested && !browser.IsDisposed)
                {
                    var round = actions.OrderBy(_ => random.Next()).ToList();
                    foreach (var action in round)
                    {
                        if (DateTime.UtcNow >= deadline || durationCts.IsCancellationRequested || browser.IsDisposed)
                            break;

                        switch (action.ToLowerInvariant())
                        {
                            case "feed_scroll":
                                System.Diagnostics.Debug.WriteLine($"🌱 养号动作开始: account={accountId}, action=feed_scroll");
                                await NavigateBrowserToUrlAsync(browser, accountId, "https://www.facebook.com", 30000);
                                await RunWarmupScriptAsync(browser,
                                    GenerateWarmupFeedScript(minStaySeconds, maxStaySeconds),
                                    GetWarmupScriptTimeoutMs(maxStaySeconds));
                                break;
                            case "safe_click":
                                System.Diagnostics.Debug.WriteLine($"🌱 养号动作开始: account={accountId}, action=safe_click");
                                await RunWarmupScriptAsync(browser,
                                    GenerateWarmupSafeClickScript(minStaySeconds, maxStaySeconds),
                                    GetWarmupScriptTimeoutMs(maxStaySeconds));
                                break;
                            case "friend_profile":
                                if (friendProfiles >= maxFriendProfiles) break;
                                System.Diagnostics.Debug.WriteLine($"🌱 养号动作开始: account={accountId}, action=friend_profile");
                                await NavigateBrowserToUrlAsync(browser, accountId, "https://www.facebook.com/friends", 30000);
                                var profileResult = await EvaluateWarmupScriptAsync(
                                    browser, GenerateWarmupFriendLinkScript(), 15000);
                                var profileUrl = profileResult?.Success == true ? profileResult.Result?.ToString() : null;
                                if (!string.IsNullOrWhiteSpace(profileUrl))
                                {
                                    await NavigateBrowserToUrlAsync(browser, accountId, profileUrl, 30000);
                                    await RunWarmupScriptAsync(browser,
                                        GenerateWarmupFeedScript(minStaySeconds, maxStaySeconds),
                                        GetWarmupScriptTimeoutMs(maxStaySeconds));
                                    friendProfiles++;
                                    await NavigateBrowserToUrlAsync(browser, accountId, "https://www.facebook.com/friends", 30000);
                                }
                                break;
                            case "reels":
                                if (reels >= maxReels) break;
                                System.Diagnostics.Debug.WriteLine($"🌱 养号动作开始: account={accountId}, action=reels");
                                await NavigateBrowserToUrlAsync(browser, accountId, "https://www.facebook.com/reel", 30000);
                                await RunWarmupScriptAsync(browser,
                                    GenerateWarmupReelsScript(minStaySeconds, maxStaySeconds, enableLike, likeProbability),
                                    GetWarmupScriptTimeoutMs(maxStaySeconds));
                                reels++;
                                break;
                        }

                        if (DateTime.UtcNow < deadline && !durationCts.IsCancellationRequested)
                            await Task.Delay(random.Next(1000, 3500));
                    }

                    if (friendProfiles >= maxFriendProfiles && reels >= maxReels &&
                        actions.All(action => action.Equals("friend_profile", StringComparison.OrdinalIgnoreCase) || action.Equals("reels", StringComparison.OrdinalIgnoreCase)))
                        break;
                }

                if (!durationCts.IsCancellationRequested && !browser.IsDisposed)
                {
                    var detailId = _accountDetailIds.TryGetValue(accountId, out var currentDetailId)
                        ? currentDetailId
                        : "";
                    OnCollectionComplete?.Invoke(detailId, accountId, "{\"success\":true,\"type\":\"warmup\"}", 17);
                    System.Diagnostics.Debug.WriteLine($"✅ 账号 {accountId} 养号任务完成");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"⏱️ 账号 {accountId} 养号达到设定时长，跳过成功回传");
                }
            }
            catch (Exception ex)
                when (durationCts.IsCancellationRequested || browser.IsDisposed)
            {
                // 到达养号时长后 watchdog 会先关闭浏览器；正在等待的 CEF JS
                // 任务随后可能返回 TimeoutException，这属于正常收尾，不算任务失败。
                System.Diagnostics.Debug.WriteLine(
                    $"⏱️ 账号 {accountId} 养号已结束，忽略浏览器关闭后的脚本异常: {ex.Message}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ 账号 {accountId} 养号任务失败: {ex.Message}");
                OnCollectionError?.Invoke(accountId, $"养号任务失败: {ex.Message}");
            }
            finally
            {
                durationCts.Cancel();
                await durationWatchdog;
                // 养号完成事件不会经过采集结果回传链路，必须由 WPF 在到时或异常后主动关闭浏览器。
                if (!KeepBrowserAfterTask && !browser.IsDisposed)
                {
                    CloseBrowser(accountId);
                    System.Diagnostics.Debug.WriteLine($"🛑 账号 {accountId} 养号结束，已关闭浏览器并释放资源");
                }
            }
        }

        private static int GetWarmupScriptTimeoutMs(int maxStaySeconds)
        {
            // JS 停留时间之外保留少量余量；页面脚本异常时不能无限等待。
            return Math.Clamp((maxStaySeconds + 15) * 1000, 30000, 120000);
        }

        private static async Task<JavascriptResponse?> EvaluateWarmupScriptAsync(
            ChromiumWebBrowser browser, string script, int timeoutMs)
        {
            var evaluateTask = browser.EvaluateScriptAsync(script);
            var completed = await Task.WhenAny(evaluateTask, Task.Delay(timeoutMs));
            if (completed != evaluateTask)
            {
                if (browser.IsDisposed)
                {
                    return null;
                }

                System.Diagnostics.Debug.WriteLine($"⚠️ 养号脚本等待超时 ({timeoutMs}ms)，跳过当前动作");
                return null;
            }

            try
            {
                return await evaluateTask;
            }
            catch (Exception) when (browser.IsDisposed)
            {
                return null;
            }
        }

        private static async Task RunWarmupScriptAsync(ChromiumWebBrowser browser, string script, int timeoutMs)
        {
            if (browser.IsDisposed || !browser.CanExecuteJavascriptInMainFrame) return;
            var result = await EvaluateWarmupScriptAsync(browser, script, timeoutMs);
            if (result == null) return;
            if (!result.Success)
                System.Diagnostics.Debug.WriteLine($"⚠️ 养号脚本执行失败: {result.Message}");
        }

        private static string GenerateWarmupFeedScript(int minStaySeconds, int maxStaySeconds)
        {
            return $@"(async function() {{
                const delay = (min, max) => Math.floor(Math.random() * (max - min + 1)) + min;
                const wait = (ms) => new Promise(resolve => setTimeout(resolve, ms));
                const steps = delay(3, 7);
                for (let i = 0; i < steps; i++) {{
                    window.scrollBy({{ top: delay(350, 900) * (Math.random() > 0.18 ? 1 : -1), behavior: 'auto' }});
                    await wait(delay(180, 520));
                }}
                await wait(delay({minStaySeconds * 1000}, {maxStaySeconds * 1000}));
                return true;
            }})();";
        }

        private static string GenerateWarmupSafeClickScript(int minStaySeconds, int maxStaySeconds)
        {
            return $@"(async function() {{
                const delay = (min, max) => Math.floor(Math.random() * (max - min + 1)) + min;
                const wait = (ms) => new Promise(resolve => setTimeout(resolve, ms));
                const forbidden = /log ?out|delete|remove|send|invite|add friend|comment|like|share|举报|删除|退出|发送|加好友/i;
                const candidates = Array.from(document.querySelectorAll('a[href], button, [role=""button""]')).filter(el => {{
                    const rect = el.getBoundingClientRect();
                    const text = (el.innerText || el.getAttribute('aria-label') || '').trim();
                    return rect.width > 20 && rect.height > 16 && rect.top >= 40 && rect.bottom <= window.innerHeight - 20 && !forbidden.test(text);
                }});
                if (candidates.length === 0) return false;
                const el = candidates[Math.floor(Math.random() * candidates.length)];
                const rect = el.getBoundingClientRect();
                const x = rect.left + rect.width / 2;
                const y = rect.top + rect.height / 2;
                const startX = Math.random() * window.innerWidth;
                const startY = Math.random() * window.innerHeight;
                const steps = delay(6, 12);
                for (let i = 1; i <= steps; i++) {{
                    const t = i / steps;
                    const cx = (startX + x) / 2 + delay(-80, 80);
                    const cy = (startY + y) / 2 + delay(-80, 80);
                    document.dispatchEvent(new MouseEvent('mousemove', {{ bubbles: true, clientX: startX * (1-t) + 2 * cx * (1-t) * t + x * t * t, clientY: startY * (1-t) + 2 * cy * (1-t) * t + y * t * t }}));
                    await wait(delay(20, 60));
                }}
                await wait(delay(120, 350));
                el.dispatchEvent(new MouseEvent('mousedown', {{ bubbles: true, clientX: x, clientY: y }}));
                await wait(delay(60, 180));
                el.dispatchEvent(new MouseEvent('mouseup', {{ bubbles: true, clientX: x, clientY: y }}));
                el.click();
                await wait(delay({minStaySeconds * 1000}, {maxStaySeconds * 1000}));
                return true;
            }})();";
        }

        private static string GenerateWarmupFriendLinkScript()
        {
            return @"(function() {
                const links = Array.from(document.querySelectorAll('a[href]')).filter(a => {
                    const href = a.href || '';
                    const rect = a.getBoundingClientRect();
                    return rect.width > 20 && rect.height > 20 && rect.top >= 0 && rect.bottom <= window.innerHeight &&
                        /facebook\\.com\\/(profile\\.php\\?id=|[A-Za-z0-9.]+$)/i.test(href) &&
                        !/\\/friends|\\/home|\\/groups|\\/watch|\\/reel|\\/marketplace/i.test(href);
                });
                if (!links.length) return '';
                return links[Math.floor(Math.random() * links.length)].href;
            })();";
        }

        private static string GenerateWarmupReelsScript(int minStaySeconds, int maxStaySeconds, bool enableLike, int likeProbability)
        {
            var likeCode = enableLike && likeProbability > 0
                ? $"if (Math.random() * 100 < {likeProbability}) {{ const like = Array.from(document.querySelectorAll('[aria-label]')).find(el => /like|赞/i.test(el.getAttribute('aria-label') || '')); if (like) like.click(); }}"
                : "";
            return $@"(async function() {{
                const delay = (min, max) => Math.floor(Math.random() * (max - min + 1)) + min;
                const wait = (ms) => new Promise(resolve => setTimeout(resolve, ms));
                await wait(delay({minStaySeconds * 1000}, {maxStaySeconds * 1000}));
                {likeCode}
                const next = Array.from(document.querySelectorAll('[aria-label], [role=""button""]')).find(el => /next|下一个|下一条/i.test(el.getAttribute('aria-label') || el.innerText || ''));
                if (next) next.click(); else window.scrollBy({{ top: delay(500, 900), behavior: 'auto' }});
                await wait(delay(500, 1600));
                return true;
            }})();";
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
            string searchUrl, int expectedCount, int taskType = 1, string? config = null, string? detailId = null)
        {
            System.Diagnostics.Debug.WriteLine(
                $"🚀 开始采集任务: account={accountId}, detailId={detailId}, taskType={taskType}, " +
                $"activeAccounts={_activeAccountTasks.Count}, thread={Environment.CurrentManagedThreadId}, url={searchUrl}");
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

                // 同行采集的一个目标可能包含粉丝、关注等多个关系页；
                // 在同一个账号任务内依次采集，避免为每种关系重复创建任务。
                if (taskType == 8 && TryGetRelationUrls(config, out var relationUrls) && relationUrls.Count > 1)
                {
                    await ExecuteUserRelationBatchAsync(browser, accountId, expectedCount, relationUrls, detailId);
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

                // 使用页面状态检测，网络异常不再误判为 Cookie 失效。
                var pageStateAfterNav = await DetectFacebookPageStateWithRetryAsync(browser, accountId);
                System.Diagnostics.Debug.WriteLine($"🔍 导航后账号状态: {pageStateAfterNav}");

                if (pageStateAfterNav != FacebookPageState.Authenticated)
                {
                    var message = GetPageStateMessage(pageStateAfterNav);
                    System.Diagnostics.Debug.WriteLine($"⚠️ 账号 {accountId} 导航后状态为 {pageStateAfterNav}: {message}");
                    OnCollectionError?.Invoke(accountId, message);
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
                    System.Diagnostics.Debug.WriteLine($"📦 完整采集数据: {jsonData}");
                    try
                    {
                        var postItems = Newtonsoft.Json.Linq.JArray.Parse(jsonData);
                        foreach (var item in postItems)
                        {
                            var itemId = item["itemId"]?.ToString() ?? "";
                            var url = item["url"]?.ToString() ?? "";
                            var postCreateTime = item["postCreateTime"]?.ToString() ?? "null";
                            System.Diagnostics.Debug.WriteLine(
                                $"🕒 帖子时间: itemId={itemId}, url={url}, postCreateTime={postCreateTime}");
                        }
                    }
                    catch (Exception logEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"⚠️ 帖子时间日志解析失败: {logEx.Message}");
                    }

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
                    string callbackDetailId = detailId
                        ?? (_accountDetailIds.TryGetValue(accountId, out var mappedDetailId) ? mappedDetailId : "");
                    OnCollectionComplete?.Invoke(callbackDetailId, accountId, jsonData, actualTaskType);
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
            finally
            {
                // 采集任务是一次性任务。结果回传事件已发出后关闭当前账号 Tab，
                // 避免只释放任务锁而遗留浏览器和 RequestContext。
                if (!KeepBrowserAfterTask
                    && _browsers.TryGetValue(accountId, out var completedBrowser)
                    && ReferenceEquals(completedBrowser, browser)
                    && !completedBrowser.IsDisposed)
                {
                    CloseBrowser(accountId);
                    if (GetActiveBrowserCount() == 0)
                    {
                        Application.Current.Dispatcher.Invoke(Close);
                    }
                }
                else if (KeepBrowserAfterTask)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"🧪 全局配置已开启，保留账号 {accountId} 的浏览器用于测试排查");
                }
                ReleaseAccountTask(accountId, detailId);
                System.Diagnostics.Debug.WriteLine(
                    $"🏁 采集任务结束: account={accountId}, detailId={detailId}, " +
                    $"activeAccounts={_activeAccountTasks.Count}, thread={Environment.CurrentManagedThreadId}");
            }
        }

        private async Task<string> ExecuteMessageMonitorTaskAsync(ChromiumWebBrowser browser)
        {
            const string script = @"(function(){
const login=!!document.querySelector('input[name=""email""],input[name=""pass""]')||/\/login\.php/i.test(location.pathname);
if(login)return JSON.stringify({success:false,errorMessage:'Cookie已失效',messengerUnreadCount:0,notificationUnreadCount:0});
const labels=[...document.querySelectorAll('[aria-label]')].map(e=>e.getAttribute('aria-label')||'');
const count=(names)=>{const value=labels.find(label=>names.some(name=>new RegExp('^'+name+'[,，].*(\\d+)\\s*(?:unread|未读)','i').test(label)));const match=value?.match(/(\\d+)\\s*(?:unread|未读)/i);return match?Number(match[1]):0;};
const titleMatch=document.title.match(/^\\((\\d+)\\)\\s*Messenger/i);
return JSON.stringify({success:true,messengerUnreadCount:count(['Messenger','Messages','消息'])||(titleMatch?Number(titleMatch[1]):0),notificationUnreadCount:count(['Notifications','通知']),page:location.href});
})();";
            var result = await browser.EvaluateScriptAsync(script);
            if (!result.Success || result.Result == null) throw new InvalidOperationException(result.Message ?? "消息监控脚本执行失败");
            return result.Result.ToString() ?? "{}";
        }

        private bool TryGetRelationUrls(string? config, out List<string> relationUrls)
        {
            relationUrls = new List<string>();
            if (string.IsNullOrWhiteSpace(config)) return false;
            try
            {
                var configObj = Newtonsoft.Json.Linq.JObject.Parse(config);
                if (configObj["relationUrls"] is not Newtonsoft.Json.Linq.JArray urls) return false;
                relationUrls = urls.Values<string>()
                    .Where(url => !string.IsNullOrWhiteSpace(url))
                    .Select(url => url!.Trim())
                    .ToList();
                return relationUrls.Count > 0;
            }
            catch
            {
                return false;
            }
        }

        private async Task ExecuteUserRelationBatchAsync(ChromiumWebBrowser browser, string accountId,
            int expectedCount, List<string> relationUrls, string? detailId)
        {
            var allResults = new Newtonsoft.Json.Linq.JArray();
            int baseCount = expectedCount / relationUrls.Count;
            int remainder = expectedCount % relationUrls.Count;

            for (int i = 0; i < relationUrls.Count; i++)
            {
                int relationTarget = baseCount + (i < remainder ? 1 : 0);
                if (relationTarget <= 0) continue;
                Application.Current.Dispatcher.Invoke(() => browser.Load(relationUrls[i]));
                await WaitForPageLoad(browser, 30000);
                await Task.Delay(1200);

                var script = GenerateUserRelationCollectScript(relationTarget);
                var result = await browser.EvaluateScriptAsync(script);
                if (!result.Success || result.Result == null) continue;
                var json = result.Result is string value
                    ? value
                    : System.Text.Json.JsonSerializer.Serialize(result.Result);
                try
                {
                    var items = Newtonsoft.Json.Linq.JArray.Parse(json);
                    foreach (var item in items) allResults.Add(item);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"⚠️ 关系页结果解析失败: {ex.Message}");
                }
            }

            int actualTaskType = _accountTaskTypes.ContainsKey(accountId) ? _accountTaskTypes[accountId] : 8;
            string callbackDetailId = detailId
                ?? (_accountDetailIds.TryGetValue(accountId, out var mappedDetailId) ? mappedDetailId : "");
            OnCollectionComplete?.Invoke(callbackDetailId, accountId, allResults.ToString(Newtonsoft.Json.Formatting.None), actualTaskType);
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
                return GeneratePostCollectScript(expectedCount, config);
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
                return { id, name: cleanName, url, avatar, followers, category, snippet, isVerified, dataType: 1, fromResource: 'page_search', syncTime: new Date().toISOString(), collectedAt: new Date().toISOString() };
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
        private string GeneratePostCollectScript(int expectedCount, string? config = null)
        {
            var js = new System.Text.StringBuilder();
            var safeConfig = string.IsNullOrWhiteSpace(config) ? "{}" : config;

            js.AppendLine($"        let targetCount = {expectedCount};");
            js.AppendLine($"        const runtimeConfig = {Newtonsoft.Json.JsonConvert.SerializeObject(safeConfig)};");
            js.AppendLine("        let aiGroupPostConfig = {}; try { aiGroupPostConfig = JSON.parse(runtimeConfig || '{}') || {}; } catch (e) { aiGroupPostConfig = {}; }");
            js.AppendLine("        const isAiGroupPostCollect = aiGroupPostConfig.source === 'ai_group_post' || aiGroupPostConfig.source === 'ai_group_comment_post' || aiGroupPostConfig.source === 'ai_competitor_post';");
            js.AppendLine("        const isAiPostLeadCollect = aiGroupPostConfig.source === 'ai_post_lead';");
            js.AppendLine("        const isSearchLatestPostCollect = aiGroupPostConfig.source === 'post_search' && aiGroupPostConfig.latestPosts;");
            js.AppendLine("        if (isAiPostLeadCollect && aiGroupPostConfig.latestPosts) console.log('[AI帖子获客] 使用最新帖子过滤');");
            js.AppendLine("        if (isSearchLatestPostCollect) console.log('[帖子采集] 使用最新帖子过滤');");
            js.AppendLine("        if (isAiGroupPostCollect) targetCount = Number(aiGroupPostConfig.maxPostsPerGroup || aiGroupPostConfig.maxPostsPerPage || 1000);");
            js.AppendLine("        const recentDays = Number(aiGroupPostConfig.recentDays || 0);");
            js.AppendLine("        let stopCurrentGroup = false;");
            js.AppendLine("        const seenPostKeys = new Set();");
            js.AppendLine($"        const maxScrolls = isAiGroupPostCollect ? Number(aiGroupPostConfig.maxScrolls || 240) : {Math.Max(expectedCount * 3, 10)};");
            js.AppendLine("        let consecutiveNoNewItems = 0;");
            // 部分 Facebook 搜索会话会延迟数十秒才追加下一批。页面有新卡片或扩展时计数会归零。
            js.AppendLine("        const maxConsecutiveNoNew = 10;");
        js.AppendLine("        let lastScrollHeight = document.documentElement.scrollHeight || 0;");
            js.AppendLine("        let lastCardsSignature = '';");
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
                // /watch/hashtag/* 是话题视频列表页，不是具体帖子，不能作为采集结果。
                if (/^\/watch\/hashtag(?:\/|$)/i.test(path) || /^\/hashtag(?:\/|$)/i.test(path)) {
                    return null;
                }
                if ((path.includes('/permalink.php') || path.includes('/story.php')) && q.get('story_fbid')) {
                    return u.origin + path + u.search;
                }
                if (path.includes('/groups/') && q.get('multi_permalinks')) {
                    return u.origin + path + u.search;
                }
                const groupPost = path.match(/\/groups\/([^/]+)\/posts\/([^/]+)/);
                if (groupPost) {
                    return u.origin + path + u.search;
                }
                const groupPermalink = path.match(/\/groups\/([^/]+)\/permalink\/([^/]+)/);
                if (groupPermalink) {
                    return u.origin + path + u.search;
                }
                if (/\/posts\//i.test(path)) return u.origin + path + u.search;
                if (path.includes('/photo/') && q.get('fbid')) {
                    return u.origin + path + u.search;
                }
                if (path.includes('/videos/') || path.includes('/watch/') || path.includes('/reel/')) return u.origin + path + u.search;
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
                if (u.searchParams.get('multi_permalinks')) return u.searchParams.get('multi_permalinks');
                const groupPost = u.pathname.match(/\/groups\/[^/]+\/posts\/([^/]+)/);
                if (groupPost) return groupPost[1];
                const permalink = u.pathname.match(/\/permalink\/([^/]+)/);
                if (permalink) return permalink[1];
                const post = u.pathname.match(/\/posts\/([^/]+)/);
                if (post) return post[1];
                const video = u.pathname.match(/\/videos\/([^/]+)/);
                if (video) return video[1];
                const reel = u.pathname.match(/\/reel\/([^/]+)/);
                if (reel) return reel[1];
            } catch {}
            return '';
        };

        const parseAuthorFromGroupUserLink = (link) => {
            if (!link) return { postAuthorId: '', postAuthorUrl: '' };
            try {
                const u = new URL(link.href);
                const match = u.pathname.match(/\/groups\/[^/]+\/user\/([^/]+)/);
                const postAuthorId = match ? match[1] : '';
                return {
                    postAuthorId,
                    postAuthorUrl: postAuthorId ? 'https://www.facebook.com/profile.php?id=' + postAuthorId : link.href.split('?')[0]
                };
            } catch {
                return { postAuthorId: '', postAuthorUrl: '' };
            }
        };

        const parseAuthorFromLink = (link) => {
            if (!link) return { postAuthorId: '', postAuthorUrl: '' };
            try {
                const u = new URL(link.href);
                const groupMatch = u.pathname.match(/\/groups\/[^/]+\/user\/([^/]+)/);
                const profileId = u.searchParams.get('id') || (u.pathname.match(/\/profile\.php\/([^/]+)/) || [])[1] || '';
                const postAuthorId = groupMatch ? groupMatch[1] : profileId;
                return {
                    postAuthorId,
                    postAuthorUrl: postAuthorId ? 'https://www.facebook.com/profile.php?id=' + postAuthorId : link.href.split('?')[0]
                };
            } catch {
                return { postAuthorId: '', postAuthorUrl: '' };
            }
        };

        const findAuthorLink = (card) => {
            const groupLinks = Array.from(card.querySelectorAll('a[href*=""/groups/""][href*=""/user/""]'));
            const profileLinks = Array.from(card.querySelectorAll('a[href*=""profile.php?id=""]'));
            const links = [...groupLinks, ...profileLinks];
            return links.find(link => cleanText(link.textContent || link.getAttribute('aria-label'))) || links[0] || null;
        };

        const parsePostTime = (text) => {
            const raw = cleanText(text);
            if (!raw) return { date: null, daysAgo: null, raw: '' };
            const now = new Date();
            const m = raw.match(/^(\d+)\s*(m|min|mins|分钟)$/i);
            if (m) return { date: new Date(now.getTime() - Number(m[1]) * 60000), daysAgo: 0, raw };
            const h = raw.match(/^(\d+)\s*(h|hr|hrs|小时)$/i);
            if (h) return { date: new Date(now.getTime() - Number(h[1]) * 3600000), daysAgo: 0, raw };
            const d = raw.match(/^(\d+)\s*(d|day|days|天)$/i);
            if (d) {
                const days = Number(d[1]);
                return { date: new Date(now.getTime() - days * 86400000), daysAgo: days, raw };
            }
            const w = raw.match(/^(\d+)\s*(w|week|weeks|周)$/i);
            if (w) {
                const days = Number(w[1]) * 7;
                return { date: new Date(now.getTime() - days * 86400000), daysAgo: days, raw };
            }
            if (/^Yesterday|昨天$/i.test(raw)) return { date: new Date(now.getTime() - 86400000), daysAgo: 1, raw };
            if (/^Today|Just now|刚刚$/i.test(raw)) return { date: now, daysAgo: 0, raw };
            // Facebook English timestamps commonly use ""July 14 at 6:35 PM"".
            // Normalize the separator before handing it to the browser date parser.
            const normalizedDate = raw.replace(/\bat\b/gi, ' ').replace(/\s+/g, ' ').trim();
            // Date.parse(""July 14 6:35 PM"") may fall back to year 2001 in Chromium.
            // Facebook omits the year for recent posts, so add the current year first.
            const dateWithYear = /^(?:Jan(?:uary)?|Feb(?:ruary)?|Mar(?:ch)?|Apr(?:il)?|May|Jun(?:e)?|Jul(?:y)?|Aug(?:ust)?|Sep(?:tember)?|Oct(?:ober)?|Nov(?:ember)?|Dec(?:ember)?)\s+\d{1,2}(?:\s|$)/i.test(normalizedDate) && !/\b\d{4}\b/.test(normalizedDate)
                ? normalizedDate + ' ' + now.getFullYear()
                : normalizedDate;
            const parsed = Date.parse(dateWithYear);
            if (!Number.isNaN(parsed)) {
                const date = new Date(parsed);
                const daysAgo = Math.max(0, Math.floor((now.getTime() - date.getTime()) / 86400000));
                return { date, daysAgo, raw };
            }
            return { date: null, daysAgo: null, raw };
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

        // Facebook 搜索结果会按账号、实验版本渲染成两种结构：常规的 role=article，
        // 或只有 role=feed 下的消息容器。不能只依赖前者，否则页面上明明有帖子却采集为 0。
        const getPostCards = () => {
            const articleCards = Array.from(document.querySelectorAll('[role=""article""]'));
            const feedCards = [];
            const pageletCards = Array.from(document.querySelectorAll('[data-pagelet*=""FeedUnit_""], [data-pagelet*=""feed_unit""]'));
            document.querySelectorAll('[data-ad-comet-preview=""message""], [data-testid=""post_message""], [data-ad-rendering-role=""story_message""]').forEach(message => {
                let card = message;
                while (card.parentElement
                    && card.parentElement.getAttribute('role') !== 'feed'
                    && !/FeedUnit_|feed_unit/i.test(card.parentElement.getAttribute('data-pagelet') || '')) {
                    card = card.parentElement;
                }
                if (card.parentElement && (card.parentElement.getAttribute('role') === 'feed'
                    || /FeedUnit_|feed_unit/i.test(card.parentElement.getAttribute('data-pagelet') || ''))) {
                    feedCards.push(card);
                }
            });
            return Array.from(new Set([...articleCards, ...feedCards, ...pageletCards]));
        };

        const getPostCardsSignature = (cards) => cards.map(card => {
            const text = cleanText(card.innerText || card.textContent).slice(0, 180);
            const href = card.querySelector('a[href]')?.href || '';
            return text + '|' + href;
        }).join('||');

        const isCardDescendant = (node, card) => {
            const article = node.closest('[role=""article""]');
            return article ? article === card : card.contains(node);
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
            const storyMessage = card.querySelector('[data-ad-rendering-role=""story_message""]');
            if (storyMessage && cleanText(storyMessage.innerText || storyMessage.textContent)) {
                return cleanText(storyMessage.innerText || storyMessage.textContent);
            }
            // Facebook frequently omits the stable message attributes. Only inspect
            // text nodes belonging directly to this post; article.innerText also contains
            // the author, time, reactions and comments.
            // Do not reject bare numbers: a post can legitimately contain text such as ""777"".
            // Interaction counters are filtered separately because they are span/button nodes.
            const ignoredText = /^(reply|share|like|comment|see \d+ replies?|view \d+ replies?|write a public comment|admin|wa|\d+\s*(?:[smhdwy]))$/i;
            const candidates = Array.from(card.querySelectorAll('[dir=""auto""], [dir=""ltr""]'))
                .filter(node => isCardDescendant(node, card))
                .filter(node => !node.closest('a, button, [role=""button""]'))
                .map(node => cleanText(node.innerText || node.textContent))
                .filter(text => text && text.length <= 500 && !ignoredText.test(text));
            const structuralCandidates = Array.from(card.querySelectorAll('div[dir=""auto""], div[dir=""ltr""], p[dir=""auto""], p[dir=""ltr""]'))
                .filter(node => isCardDescendant(node, card))
                .filter(node => !node.closest('a, button, [role=""button""]'))
                .map(node => cleanText(node.innerText || node.textContent))
                .filter(text => text && text.length <= 500 && !ignoredText.test(text));
            return (structuralCandidates.length ? structuralCandidates : candidates)
                .sort((a, b) => b.length - a.length)[0] || '';
        };

        // Facebook 的搜索实验有时会把时间锚点改写成当前 search URL，卡片 DOM 中不再保留帖子链接。
        // 仅从 Facebook 已下发的 hydration 脚本读取真实 URL；没有真实 URL 时跳过，绝不按群组或正文拼接。
        const hydratedPostUrlCache = new Map();
        const decodeHydratedUrl = (value) => cleanText(value)
            .replace(/\\u002F/gi, '/')
            .replace(/\\u003A/gi, ':')
            .replace(/\\\//g, '/')
            .replace(/&amp;/g, '&');
        const normalizeHydratedSource = (value) => (value || '')
            .replace(/\\u002F/gi, '/')
            .replace(/\\u003A/gi, ':')
            .replace(/\\u0026/gi, '&')
            .replace(/\\u003F/gi, '?')
            .replace(/\\u003D/gi, '=')
            .replace(/\\\//g, '/')
            .replace(/\\(['""])/g, '$1')
            .replace(/\s+/g, ' ');
        const findHydratedPostUrl = (card, postContent) => {
            const contentKey = cleanText(postContent).slice(0, 160);
            if (!contentKey) return null;
            const groupLink = Array.from(card.querySelectorAll('a[href*=""/groups/""]'))
                .find(link => !/\/user\/|\/posts\/|\/permalink\//.test(link.pathname || ''));
            const groupMatch = groupLink?.pathname.match(/\/groups\/([^/]+)/);
            const groupId = groupMatch ? groupMatch[1] : '';
            const cacheKey = groupId + '|' + contentKey;
            if (hydratedPostUrlCache.has(cacheKey)) return hydratedPostUrlCache.get(cacheKey);

            const prefixes = [contentKey, contentKey.slice(0, 100), contentKey.slice(0, 60)]
                .filter((value, index, values) => value.length >= 24 && values.indexOf(value) === index);
            let result = null;
            for (const script of Array.from(document.scripts)) {
                const source = script.textContent || '';
                if (!source || source.length < 100) continue;
                const normalizedSource = normalizeHydratedSource(source);
                for (const prefix of prefixes) {
                    const rawIndex = source.indexOf(prefix);
                    const normalizedIndex = normalizedSource.indexOf(prefix);
                    if (rawIndex < 0 && normalizedIndex < 0) continue;
                    // Restrict matching to this story's hydration object instead of scanning all loaded search results.
                    const sourceForSearch = rawIndex >= 0 ? source : normalizedSource;
                    const index = rawIndex >= 0 ? rawIndex : normalizedIndex;
                    const scope = sourceForSearch.slice(Math.max(0, index - 16000), Math.min(sourceForSearch.length, index + 32000));
                    if (groupId && !scope.includes(groupId)) continue;
                    const urlCandidates = scope.match(/https?:\/\/[^""'\s<>]+/gi) || [];
                    result = urlCandidates
                        .map(decodeHydratedUrl)
                        .map(canonicalPostUrl)
                        .find(Boolean) || null;
                    if (result) break;
                }
                if (result) break;
            }
            hydratedPostUrlCache.set(cacheKey, result);
            return result;
        };

        const extractPostData = (card) => {
            try {
                const postContent = getPostContent(card);
                // Recent posts 的图片、视频和 Reel 也都是帖子，不能按卡片外观过滤。
                // 只要 Facebook 提供了真实帖子链接（DOM 或同一条 hydration 数据）就允许采集。
                const postLinkEl = findPostTimeLink(card) || Array.from(card.querySelectorAll('a[href]'))
                    .find(link => canonicalPostUrl(link.href));
                const url = postLinkEl
                    ? canonicalPostUrl(postLinkEl.href)
                    : findHydratedPostUrl(card, postContent);
                const itemId = getPostItemId(url);
                // Facebook 同一帖子可能因 cft/tn 参数不同产生多个 URL，优先按真实帖子 ID 临时去重。
                const postKey = itemId || url;
                if (!url || seenPostKeys.has(postKey)) return null;
                const parsedTime = postLinkEl
                    ? parsePostTime(postLinkEl.textContent || postLinkEl.getAttribute('aria-label'))
                    : { date: null, daysAgo: null, raw: '' };
                if (isAiGroupPostCollect && recentDays > 0 && parsedTime.daysAgo !== null && parsedTime.daysAgo > recentDays) {
                    console.log('[AI群帖采集] 遇到超过最近天数的帖子，停止当前群:', parsedTime.raw, recentDays);
                    stopCurrentGroup = true;
                    return null;
                }

                const authorLink = findAuthorLink(card);
                const authorInfo = parseAuthorFromLink(authorLink);
                const postUser = getAuthorName(card, postLinkEl);
                const groupName = getGroupName(card);
                const isGroupPost = !!groupName || url.includes('/groups/') || location.pathname.includes('/groups/');
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

                seenPostKeys.add(postKey);
                return {
                    itemId, postUser, postAuthorId: authorInfo.postAuthorId, postAuthorUrl: authorInfo.postAuthorUrl,
                    url, fromResource: isAiPostLeadCollect ? 'ai_post_lead' : (isAiGroupPostCollect ? (aiGroupPostConfig.source === 'ai_competitor_post' ? 'ai_competitor_post' : (aiGroupPostConfig.source === 'ai_group_comment_post' ? 'ai_group_comment_post' : 'ai_group_post')) : (isGroupPost ? 'group' : 'page')),
                    groupName, reshareCount, commentCount, reactionCount,
                    usedCount: 0, postContent, fbAccount: '',
                    postCreateTime: parsedTime.date ? parsedTime.date.toISOString() : null
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
                if (stopCurrentGroup) {
                    isCompleted = true;
                    resolve(JSON.stringify(results));
                    return;
                }
                const cards = getPostCards();
                const currentCardsSignature = getPostCardsSignature(cards);
                const cardsChanged = !!lastCardsSignature && currentCardsSignature !== lastCardsSignature;
                lastCardsSignature = currentCardsSignature;

                let newItemsFound = 0;
                for (let i = 0; i < cards.length && results.length < targetCount; i++) {
                    const data = extractPostData(cards[i]);
                    if (stopCurrentGroup) {
                        isCompleted = true;
                        resolve(JSON.stringify(results));
                        return;
                    }
                    if (data) {
                        results.push(data);
                        newItemsFound++;
                    }
                }

                const currentScrollHeight = document.documentElement.scrollHeight || 0;
                const pageExpanded = currentScrollHeight > lastScrollHeight + 80;
                lastScrollHeight = Math.max(lastScrollHeight, currentScrollHeight);
                consecutiveNoNewItems = (newItemsFound > 0 || pageExpanded || cardsChanged)
                    ? 0
                    : consecutiveNoNewItems + 1;
                console.log(`[帖子采集] ${results.length}/${targetCount}, 本轮新增 ${newItemsFound}, 页面扩展 ${pageExpanded}, 无新增 ${consecutiveNoNewItems}/${maxConsecutiveNoNew}`);

                if (results.length >= targetCount) {
                    isCompleted = true;
                    resolve(JSON.stringify(results.slice(0, targetCount)));
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

                // Facebook 搜索的下一批不是固定延迟。滚动后最多观察 15 秒，等卡片或页面高度
                // 实际变化后再进入下一轮；连续无新增达到阈值才结束，避免慢加载结果页提前停止。
                let cardsAfterScroll = cards.length;
                let heightAfterScroll = currentScrollHeight;
                let signatureAfterScroll = currentCardsSignature;
                const loadDeadline = Date.now() + 15000;
                while (Date.now() < loadDeadline) {
                    await new Promise(resolve => setTimeout(resolve, 500));
                    cardsAfterScroll = getPostCards().length;
                    heightAfterScroll = document.documentElement.scrollHeight || 0;
                    signatureAfterScroll = getPostCardsSignature(getPostCards());
                    if (cardsAfterScroll > cards.length || heightAfterScroll > currentScrollHeight + 80 || signatureAfterScroll !== currentCardsSignature) break;
                }

                scrollCount++;
                if (cardsAfterScroll > cards.length || heightAfterScroll > currentScrollHeight + 80 || signatureAfterScroll !== currentCardsSignature) {
                    consecutiveNoNewItems = 0;
                    lastScrollHeight = Math.max(lastScrollHeight, heightAfterScroll);
                    lastCardsSignature = signatureAfterScroll;
                    console.log(`[帖子采集] 滚动后检测到页面追加，继续等待下一轮解析: cards=${cardsAfterScroll}`);
                }

                if (consecutiveNoNewItems >= maxConsecutiveNoNew || scrollCount >= maxScrolls) {
                    isCompleted = true;
                    resolve(JSON.stringify(results));
                    return;
                }
                setTimeout(() => doScroll(), randomDelay(2000, 3500));
            } catch (e) {
                console.error('[采集错误]', e);
                setTimeout(() => doScroll(), 3000);
            }
        };

        doScroll();
");

            // 大任务按批次已经持续落库，允许长时间滚动；没有任何数据时仍由前端无响应保护兜底。
            js.AppendLine("        setTimeout(() => {");
            js.AppendLine("            if (results.length > 0) resolve(JSON.stringify(results));");
            js.AppendLine("            else reject(new Error('Collection timeout with no data'));");
            js.AppendLine($"        }}, {Math.Min(Math.Max(expectedCount * 1000, 300000), 1800000)});");

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

                // 群组卡片中第一个 /groups/ 链接通常是头像，aria-label 会带有
                // ""Profile photo of"" 前缀；优先取卡片内真正有文本的群组标题链接。
                const titleLinkEl = Array.from(card.querySelectorAll('a[href*=""/groups/""]'))
                    .find(el => (el.textContent || '').trim() &&
                        !(el.getAttribute('aria-label') || '').toLowerCase().startsWith('profile photo of'));
                const rawGroupName = titleLinkEl ? titleLinkEl.textContent.trim() :
                    (groupLinkEl.getAttribute('aria-label') || groupLinkEl.textContent.trim());
                const groupName = rawGroupName.replace(/^profile photo of\s+/i, '').trim();
                if (!groupName) return null;

                let type = 'Public';
                const typeEl = card.querySelector('[aria-label*=""Public""], [aria-label*=""Private""], [aria-label*=""Closed""]');
                if (typeEl) {
                    const ariaLabel = typeEl.getAttribute('aria-label') || '';
                    if (ariaLabel.includes('Private') || ariaLabel.includes('Closed')) type = 'Private';
                }

                let memberQuantity = null, activeQuantity = '';
                // 统计信息有时不在 span[dir=auto] 中，而是在组合文本或 div 中。
                // 同时读取卡片完整可见文本，避免只拿到群组名称却漏掉活跃度。
                const statTexts = [card.innerText || '', ...Array.from(card.querySelectorAll('span[dir=""auto""]'))
                    .map(span => span.textContent || '')];
                for (const rawText of statTexts) {
                    const text = String(rawText).replace(/\s+/g, ' ').trim();
                    if (!text) continue;

                    const memberMatch = text.match(/([\d]+[\.,]?\d*)\s*(K|M|B)?\s*members?/i);
                    if (memberMatch && !memberQuantity) {
                        const rawNumber = Number(memberMatch[1].replace(/,/g, ''));
                        const unit = (memberMatch[2] || '').toUpperCase();
                        const multiplier = unit === 'K' ? 1000 : unit === 'M' ? 1000000 : unit === 'B' ? 1000000000 : 1;
                        if (Number.isFinite(rawNumber)) memberQuantity = Math.round(rawNumber * multiplier);

                        // 统计栏格式通常为：Public · 19K members · 40+ posts a day。
                        // 成员数后面的最后一段就是活跃度，直接按中点拆分，避免依赖具体文案。
                        const memberEnd = (memberMatch.index || 0) + memberMatch[0].length;
                        const afterMembers = text.slice(memberEnd);
                        const statParts = afterMembers.split(/[·•]/).map(part => part.trim()).filter(Boolean);
                        if (statParts.length > 0 && !activeQuantity) {
                            activeQuantity = statParts[statParts.length - 1];
                        }
                        continue;
                    }

                    const activeMatch = text.match(/([\d][\d,.]*\s*\+?\s*posts?\s+a\s+(?:day|week|month))/i);
                    if (activeMatch && !activeQuantity) activeQuantity = activeMatch[1].trim();
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
                
                // ✅ 获取并归一化用户主页链接。
                // Facebook 关系页可能返回 profile.php、用户名主页，或群组内 /user/ 链接，
                // 不能直接把关系页链接当成用户主页保存。
                var href = nameLink.href;
                if (!href || !href.includes('facebook.com')) {
                    console.log('Invalid URL');
                    return null;
                }

                var linkUrl = new URL(href, window.location.href);
                var pathname = linkUrl.pathname || '';
                var profileId = linkUrl.searchParams.get('id') || '';
                var groupUserMatch = pathname.match(/\/user\/(\d+)/);
                profileId = profileId || (groupUserMatch ? groupUserMatch[1] : '');

                // ✅ 获取用户名
                var userName = nameLink.textContent.trim();
                if (!userName || userName.length < 2 || userName.length > 150) {
                    console.log('Invalid username');
                    return null;
                }
                
                // ✅ profile.php 使用 id；普通主页使用路径名称。
                var fbUserId = profileId;
                var normalizedUrl = '';
                if (fbUserId) {
                    normalizedUrl = 'https://www.facebook.com/profile.php?id=' + encodeURIComponent(fbUserId);
                } else {
                    var pathParts = pathname.split('/').filter(Boolean);
                    var username = pathParts[0] || '';
                    if (!username || ['profile.php', 'groups', 'pages', 'people'].includes(username.toLowerCase())) {
                        console.log('No valid profile name:', href);
                        return null;
                    }
                    fbUserId = username;
                    normalizedUrl = 'https://www.facebook.com/' + username;
                }
                
                if (!fbUserId) {
                    console.log('No user ID');
                    return null;
                }
                if (seenUserIds.has(fbUserId)) {
                    console.log('Skip duplicate user:', fbUserId);
                    return null;
                }

                // ✅ 获取头像
                var avatar = '';
                var imgEl = container.querySelector('img');
                if (imgEl) {
                    avatar = imgEl.src || '';
                }

                var fromResource = 'peer_follower';
                var relation = new URL(window.location.href).searchParams.get('sk');
                if (relation === 'following') fromResource = 'peer_following';
                else if (relation === 'friends') fromResource = 'peer_friend';

                console.log('Found:', userName, fbUserId);
                seenUserIds.add(fbUserId);
                return { fbUserId: fbUserId, userName: userName, url: normalizedUrl, avatar: avatar, dataType: 1, fromResource: fromResource, syncTime: new Date().toISOString() };
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
            return _browsers.Values.Count(browser => !browser.IsDisposed);
        }

        /// <summary>
        /// 为指定账号切换 Facebook 语言设置。账号级任务严格串行，临时创建的 Tab 完成后释放。
        /// </summary>
        public async Task SwitchLanguageForAccount(string accountId, string cookie, string languageCode,
            string nativeName, string englishName, bool closeAfterTask = false)
        {
            var detailId = $"language-{accountId}-{DateTime.Now.Ticks}";
            if (!_activeAccountTasks.TryAdd(accountId, detailId))
            {
                throw new InvalidOperationException($"账号 {accountId} 正在执行其它任务，语言切换已跳过");
            }

            var createdForLanguageTask = closeAfterTask;
            string? restoreUrl = null;
            TaskCompletionSource<FacebookPageState>? readySignal = null;
            try
            {
                if (!_browsers.TryGetValue(accountId, out var browser) || browser.IsDisposed)
                {
                    // 必须先注册初始化完成信号，再创建浏览器，避免首页加载完成事件先于等待逻辑触发。
                    readySignal = new TaskCompletionSource<FacebookPageState>(
                        TaskCreationOptions.RunContinuationsAsynchronously);
                    _browserReadySignals[accountId] = readySignal;
                    CreateBrowser(accountId, "https://www.facebook.com", cookie, null, 0,
                        deviceId: null, taskType: 99, config: null, detailId: null, isOperation: false);
                    createdForLanguageTask = true;
                    var waitStart = DateTime.UtcNow;
                    while ((!_browsers.TryGetValue(accountId, out browser) || browser.IsDisposed)
                           && (DateTime.UtcNow - waitStart).TotalSeconds < 15)
                    {
                        await Task.Delay(200);
                    }
                    if (browser == null || browser.IsDisposed)
                    {
                        throw new TimeoutException($"账号 {accountId} 浏览器创建超时");
                    }

                    // 新建浏览器必须等待与采集相同的初始化完成事件，再进入语言设置。
                    System.Diagnostics.Debug.WriteLine($"⏳ 等待账号 {accountId} Facebook 首页初始化完成...");
                    var readyTask = readySignal.Task;
                    var completed = await Task.WhenAny(readyTask, Task.Delay(30000));
                    if (completed != readyTask)
                    {
                        throw new TimeoutException($"账号 {accountId} Facebook 首页初始化超时");
                    }
                    var homeState = await readyTask;
                    if (homeState != FacebookPageState.Authenticated)
                    {
                        throw new InvalidOperationException(
                            $"Facebook 首页未完成登录态确认: {GetPageStateMessage(homeState)}");
                    }
                    System.Diagnostics.Debug.WriteLine($"✅ 账号 {accountId} Facebook 首页已加载并确认登录");
                    _browserReadySignals.Remove(accountId);
                }
                else
                {
                    // 已存在 Tab 也要等当前导航完成，避免在加载中的页面上发起设置页跳转。
                    await WaitForPageLoad(browser, 30000);
                }

                restoreUrl = browser.Address;
                string languageUrl = "https://www.facebook.com/settings/?tab=language_and_region";
                Application.Current.Dispatcher.Invoke(() =>
                {
                    browser.Load(languageUrl);
                });

                System.Diagnostics.Debug.WriteLine($"📌 导航到语言设置页面: {languageUrl}");

                await WaitForFacebookNavigationAsync(browser, languageUrl, 30000);
                System.Diagnostics.Debug.WriteLine($"📌 语言设置页已就绪，当前地址: {browser.Address}");

                var switchScript = GenerateLanguageSwitchScript(languageCode, nativeName, englishName);
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
            finally
            {
                _browserReadySignals.Remove(accountId);
                if (createdForLanguageTask && !KeepBrowserAfterTask)
                {
                    CloseBrowser(accountId);
                }
                else if (!string.IsNullOrWhiteSpace(restoreUrl)
                         && !restoreUrl.Contains("/settings/", StringComparison.OrdinalIgnoreCase)
                         && _browsers.TryGetValue(accountId, out var existingBrowser)
                         && !existingBrowser.IsDisposed)
                {
                    Application.Current.Dispatcher.Invoke(() => existingBrowser.Load(restoreUrl));
                    try
                    {
                        await WaitForPageLoad(existingBrowser, 30000);
                        System.Diagnostics.Debug.WriteLine($"✅ 账号 {accountId} 已恢复原页面: {restoreUrl}");
                    }
                    catch (Exception restoreException)
                    {
                        System.Diagnostics.Debug.WriteLine(
                            $"⚠️ 账号 {accountId} 恢复原页面超时: {restoreException.Message}");
                    }
                }
                ReleaseAccountTask(accountId, detailId);
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
                        if (string.Equals(segments[i], "t", StringComparison.OrdinalIgnoreCase) && i + 1 < segments.Length)
                        {
                            var userId = Uri.UnescapeDataString(segments[i + 1]);
                            var decodedCurrentUrl = Uri.UnescapeDataString(currentUrl);
                            if (decodedCurrentUrl.Contains(userId, StringComparison.OrdinalIgnoreCase))
                                return true;
                        }
                    }
                    return false;
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

        private static Task<T> RunOnBrowserUiThreadAsync<T>(ChromiumWebBrowser browser, Func<Task<T>> action)
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
        /// 等待 Facebook 导航真正完成。不能只看 IsLoading，因为调用 Load 后该属性可能尚未切换。
        /// </summary>
        private async Task WaitForFacebookNavigationAsync(ChromiumWebBrowser browser, string expectedUrl, int timeoutMs)
        {
            var startTime = DateTime.UtcNow;
            while ((DateTime.UtcNow - startTime).TotalMilliseconds < timeoutMs)
            {
                if (browser.IsDisposed)
                {
                    throw new InvalidOperationException("语言设置页浏览器已关闭");
                }

                string address = "";
                bool isLoading = true;
                bool canExecute = false;
                Application.Current.Dispatcher.Invoke(() =>
                {
                    address = browser.Address ?? "";
                    isLoading = browser.IsLoading;
                    canExecute = browser.CanExecuteJavascriptInMainFrame;
                });

                var expectedPath = new Uri(expectedUrl).AbsolutePath;
                var reachedTarget = address.Contains(expectedPath, StringComparison.OrdinalIgnoreCase);
                if (reachedTarget && !isLoading && canExecute)
                {
                    var readyResult = await browser.EvaluateScriptAsync(
                        "document.readyState === 'complete' && !!document.body");
                    if (readyResult.Success && readyResult.Result is bool ready && ready)
                    {
                        await Task.Delay(1500);
                        return;
                    }
                }

                await Task.Delay(300);
            }

            throw new TimeoutException($"语言设置页加载超时，当前地址: {browser.Address}");
        }

        /// <summary>
        /// 生成语言切换JavaScript脚本
        /// </summary>
        private string GenerateLanguageSwitchScript(string languageCode, string selectedNativeName, string selectedEnglishName)
        {
            var js = new System.Text.StringBuilder();

            js.AppendLine("(async function() {");
            js.AppendLine("    try {");
            js.AppendLine($"        const nativeName = {JsonConvert.SerializeObject(selectedNativeName)};");
            js.AppendLine($"        const englishName = {JsonConvert.SerializeObject(selectedEnglishName)};");
            js.AppendLine("        const sleep = ms => new Promise(resolve => setTimeout(resolve, ms));");
            js.AppendLine("        const norm = value => (value || '').replace(/\\s+/g, ' ').trim().toLowerCase();");
            js.AppendLine("        const visible = el => { const r = el?.getBoundingClientRect(); return !!el && r.width > 0 && r.height > 0; };");
            js.AppendLine("        const text = el => norm((el?.innerText || '') + ' ' + (el?.getAttribute('aria-label') || ''));");
            js.AppendLine("        const waitFor = async (finder, label, timeout = 15000) => { const end = Date.now() + timeout; while (Date.now() < end) { const value = finder(); if (value) return value; await sleep(300); } throw new Error('等待' + label + '超时'); };");
            js.AppendLine("        const accountLanguage = await waitFor(() => [...document.querySelectorAll('[role=button],button')].find(el => visible(el) && (text(el).includes('account language') || /语言|language|idioma|langue|lingua/i.test(text(el)))), 'Account language 设置入口');");
            js.AppendLine("        accountLanguage.click();");
            js.AppendLine("        const getDialog = () => [...document.querySelectorAll('[role=dialog]')].filter(visible).pop();");
            js.AppendLine("        await waitFor(getDialog, '语言选择弹框');");
            js.AppendLine("        const searchInput = await waitFor(() => { const dialog = getDialog(); return dialog && [...dialog.querySelectorAll('input')].find(el => visible(el) && (el.type === 'text' || el.type === 'search') && !/facebook/i.test(el.getAttribute('aria-label') || '')); }, '语言搜索输入框');");
            js.AppendLine("        searchInput.focus(); const setter = Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'value').set; setter.call(searchInput, ''); searchInput.dispatchEvent(new Event('input', { bubbles: true })); for (const char of englishName || nativeName) { await sleep(45); setter.call(searchInput, searchInput.value + char); searchInput.dispatchEvent(new Event('input', { bubbles: true })); } await sleep(500);");
            js.AppendLine("        const target = await waitFor(() => { const dialog = getDialog(); return dialog && [...dialog.querySelectorAll('[role=radio]')].find(el => { const value = text(el); return value.includes(norm(nativeName)) || value.includes(norm(englishName)); }); }, '目标语言选项');");
            js.AppendLine("        if (target.getAttribute('aria-checked') !== 'true') { target.click(); await waitFor(() => target.getAttribute('aria-checked') === 'true', '语言选项生效', 5000); }");
            js.AppendLine("        await sleep(1200);");
            js.AppendLine("        return JSON.stringify({ success: true, message: nativeName });");
            js.AppendLine("    } catch (e) {");
            js.AppendLine("        return JSON.stringify({ success: false, message: e?.message || String(e) });");
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
            return _browsers.TryGetValue(accountId, out var browser) && !browser.IsDisposed;
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
            string source = "";
            string sourcePostId = "";
            string sourcePostUrl = "";

            if (!string.IsNullOrEmpty(configJson))
            {
                try
                {
                    var config = Newtonsoft.Json.Linq.JObject.Parse(configJson);
                    collectComment = config.ContainsKey("collectComment") ? config.Value<bool>("collectComment") : true;
                    collectLike = config.ContainsKey("collectLike") ? config.Value<bool>("collectLike") : true;
                    commentExpectedCount = config.ContainsKey("commentExpectedCount") ? config.Value<int>("commentExpectedCount") : expectedCount;
                    likeExpectedCount = config.ContainsKey("likeExpectedCount") ? config.Value<int>("likeExpectedCount") : expectedCount;
                    source = config.ContainsKey("source") ? (config.Value<string>("source") ?? "") : "";
                    sourcePostId = config.ContainsKey("sourcePostId") ? (config.Value<string>("sourcePostId") ?? "") : "";
                    sourcePostUrl = config.ContainsKey("sourcePostUrl") ? (config.Value<string>("sourcePostUrl") ?? "") : "";
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
            js.AppendLine($"        const COLLECT_SOURCE = {Newtonsoft.Json.JsonConvert.SerializeObject(source)};");
            js.AppendLine($"        const SOURCE_POST_ID = {Newtonsoft.Json.JsonConvert.SerializeObject(sourcePostId)};");
            js.AppendLine($"        const SOURCE_POST_URL = {Newtonsoft.Json.JsonConvert.SerializeObject(sourcePostUrl)};");

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
            js.AppendLine("                // Convert group-scoped user links to a stable Facebook profile URL.");
            js.AppendLine("                let url = originalUrl;");
            js.AppendLine("                try {");
            js.AppendLine("                    const profileUrl = new URL(originalUrl);");
            js.AppendLine("                    const groupUserMatch = profileUrl.pathname.match(/^\\/groups\\/[^/]+\\/user\\/([^/]+)/i);");
            js.AppendLine("                    if (groupUserMatch) {");
            js.AppendLine("                        const profileKey = decodeURIComponent(groupUserMatch[1]);");
            js.AppendLine("                        url = /^\\d+$/.test(profileKey) ? 'https://www.facebook.com/profile.php?id=' + profileKey : 'https://www.facebook.com/' + encodeURIComponent(profileKey);");
            js.AppendLine("                    } else if (profileUrl.searchParams.get('id')) {");
            js.AppendLine("                        url = 'https://www.facebook.com/profile.php?id=' + profileUrl.searchParams.get('id');");
            js.AppendLine("                    } else {");
            js.AppendLine("                        url = 'https://www.facebook.com' + profileUrl.pathname.replace(/\\/+$/, '');");
            js.AppendLine("                    }");
            js.AppendLine("                } catch (e) {");
            js.AppendLine("                    url = originalUrl.split('?')[0].split('&')[0];");
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
            js.AppendLine("                // 提取评论正文，AI群帖评论截流需要根据评论内容判断询盘意向");
            js.AppendLine("                let commentContent = '';");
            js.AppendLine("                let messageNode = commentElement.querySelector('[data-ad-comet-preview=\"message\"]');");
            js.AppendLine("                if (!messageNode) {");
            js.AppendLine("                    const textNodes = Array.from(commentElement.querySelectorAll('[dir=\"auto\"], [dir=\"ltr\"]'))");
            js.AppendLine("                        .filter(node => node.closest('[aria-label^=\"Comment by\"]') === commentElement)");
            js.AppendLine("                        .filter(node => !node.closest('a, button, [role=\"button\"]'));");
            js.AppendLine("                    messageNode = textNodes");
            js.AppendLine("                        .map(n => ({ node: n, text: (n.innerText || n.textContent || '').replace(/\\s+/g, ' ').trim() }))");
            js.AppendLine("                        .filter(item => item.text && item.text !== userName && item.text.length > 1)");
            js.AppendLine("                        .sort((a, b) => b.text.length - a.text.length)[0]?.node || null;");
            js.AppendLine("                }");
            js.AppendLine("                if (messageNode) {");
            js.AppendLine("                    commentContent = (messageNode.innerText || messageNode.textContent || '').replace(/\\s+/g, ' ').trim();");
            js.AppendLine("                }");
            js.AppendLine("                // Do not use the whole comment article as a fallback: it contains the author, timestamp and controls.");
            js.AppendLine("                if (!commentContent) {");
            js.AppendLine("                    const contentCandidates = Array.from(commentElement.querySelectorAll('[dir=\"auto\"], [dir=\"ltr\"]'))");
            js.AppendLine("                        .filter(node => node.closest('[aria-label^=\"Comment by\"]') === commentElement)");
            js.AppendLine("                        .filter(node => !node.closest('a, button, [role=\"button\"]'))");
            js.AppendLine("                        .map(node => (node.innerText || node.textContent || '').replace(/\\s+/g, ' ').trim())");
            js.AppendLine("                        .filter(text => text && text !== userName && !/^(reply|share|like|admin|\\d+[smhdwy]?)$/i.test(text));");
            js.AppendLine("                    commentContent = contentCandidates.sort((a, b) => b.length - a.length)[0] || '';");
            js.AppendLine("                }");
            js.AppendLine("");
            js.AppendLine("                // 提取用户ID");
            js.AppendLine("                let fbUserId = '';");
            js.AppendLine("                const idMatch = url.match(/[?&]id=(\\d+)/) || originalUrl.match(/[?&]id=(\\d+)/);");
            js.AppendLine("                if (idMatch) {");
            js.AppendLine("                    fbUserId = idMatch[1];");
            js.AppendLine("                } else {");
            js.AppendLine("                    const cleanUrl = url.replace(/https:\\/\\/www\\.facebook\\.com\\//i, '');");
            js.AppendLine("                    const nameMatch = cleanUrl.match(/^([^\\/?]+)/);");
            js.AppendLine("                    if (nameMatch && nameMatch[1] && nameMatch[1].toLowerCase() !== 'profile.php') {");
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
            js.AppendLine("                    commentContent: commentContent,");
            js.AppendLine("                    sourcePostId: SOURCE_POST_ID || null,");
            js.AppendLine("                    sourcePostUrl: SOURCE_POST_URL || window.location.href,");
            js.AppendLine("                    leadType: COLLECT_SOURCE === 'ai_competitor_comment' ? 'competitor_comment_lead' : (COLLECT_SOURCE === 'ai_group_comment' ? 'comment_lead' : ''),");
            js.AppendLine("                    fromResource: COLLECT_SOURCE === 'ai_competitor_comment' ? 'ai_competitor_comment' : (COLLECT_SOURCE === 'ai_group_comment' ? 'ai_group_comment' : '帖子评论采集')");
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
            js.AppendLine("        const expandCommentThreads = async () => {");
            js.AppendLine("            const candidates = Array.from(document.querySelectorAll('[role=\"button\"], a, span'))");
            js.AppendLine("                .filter(node => {");
            js.AppendLine("                    const text = (node.innerText || node.textContent || '').replace(/\\s+/g, ' ').trim();");
            js.AppendLine("                    return /^(view\\s+\\d+\\s+replies?|\\d+\\s+repl(?:y|ies)|view more comments?|more comments?)$/i.test(text);");
            js.AppendLine("                })");
            js.AppendLine("                .map(node => node.closest('[role=\"button\"], a') || node)");
            js.AppendLine("                .filter((node, index, list) => list.indexOf(node) === index)");
            js.AppendLine("                .slice(0, 20);");
            js.AppendLine("            for (const button of candidates) {");
            js.AppendLine("                try { button.scrollIntoView({ behavior: 'auto', block: 'center' }); await humanClick(button); await new Promise(resolve => setTimeout(resolve, randomDelay(800, 1500))); } catch (e) { console.warn('展开评论失败:', e); }");
            js.AppendLine("            }");
            js.AppendLine("            return candidates.length;");
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
            js.AppendLine("                    await expandCommentThreads();");
            js.AppendLine("                    while (results.length < targetCount && scrollCount < maxScrolls) {");
            js.AppendLine("                        await expandCommentThreads();");
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
