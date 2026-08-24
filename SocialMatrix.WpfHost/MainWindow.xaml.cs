
using Microsoft.Web.WebView2.Core;
using CefSharp;
using CefSharp.Wpf;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using SocialMatrix.WpfHost.Services;
using SocialMatrix.WpfHost.Windows;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

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
        private MessageManagerWindow? _messageManagerWindow;
        private readonly Dictionary<string, string> _messageMonitorByAccount = new();
        private bool _isWorkAreaMaximized;
        private Rect _normalWindowBounds;
        private bool _titleBarMouseDown;
        private Point _titleBarMouseDownPoint;
        private readonly AppUpdateService _appUpdateService = new();
        private bool _updateCheckStarted;
        private bool _pendingUpdateApplyStarted;
        private bool _showStartupLoading;

        private const string ProductionVueUrl = "http://1.14.181.156";

        public MainWindow()
        {
            InitializeComponent();
            _showStartupLoading = !File.Exists(Path.Combine(AppContext.BaseDirectory, ".first-launch-complete"));
            if (!_showStartupLoading)
            {
                VueWebView.Visibility = Visibility.Visible;
                VueLoadingOverlay.Visibility = Visibility.Collapsed;
            }
            MaximizeToWorkArea();
            InitializeVueWebView();
            ContentRendered += MainWindow_ContentRendered;
        }

        private async void MainWindow_ContentRendered(object? sender, EventArgs e)
        {
            if (_updateCheckStarted)
            {
                return;
            }

            _updateCheckStarted = true;
            await Task.Delay(TimeSpan.FromSeconds(3));
            await _appUpdateService.CheckAndDownloadAsync();
        }

        /// <summary>
        /// 用户主动启动任务时应用已下载的更新。更新期间才会重启当前程序。
        /// </summary>
        public void ApplyPendingUpdateOnUserStart()
        {
            if (_pendingUpdateApplyStarted)
            {
                return;
            }

            _pendingUpdateApplyStarted = true;
            _appUpdateService.ApplyPendingUpdateOnStartup();
        }

        private void WindowHeader_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                _titleBarMouseDown = false;
                Mouse.Capture(null);
                ToggleWindowState();
                e.Handled = true;
                return;
            }

            _titleBarMouseDown = true;
            _titleBarMouseDownPoint = e.GetPosition(this);
            ((UIElement)sender).CaptureMouse();
            e.Handled = true;
        }

        private void WindowHeader_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_titleBarMouseDown || e.LeftButton != MouseButtonState.Pressed)
            {
                return;
            }

            var currentPoint = e.GetPosition(this);
            var movedEnough = Math.Abs(currentPoint.X - _titleBarMouseDownPoint.X) >= SystemParameters.MinimumHorizontalDragDistance
                || Math.Abs(currentPoint.Y - _titleBarMouseDownPoint.Y) >= SystemParameters.MinimumVerticalDragDistance;
            if (!movedEnough)
            {
                return;
            }

            _titleBarMouseDown = false;
            Mouse.Capture(null);

            if (_isWorkAreaMaximized)
            {
                var screenPoint = PointToScreen(_titleBarMouseDownPoint);
                var restoreBounds = _normalWindowBounds;
                var maximizedWidth = ActualWidth;

                RestoreFromWorkArea();
                Left = screenPoint.X - restoreBounds.Width * _titleBarMouseDownPoint.X / maximizedWidth;
                Top = screenPoint.Y - _titleBarMouseDownPoint.Y;
            }

            DragMove();
        }

        private void WindowHeader_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _titleBarMouseDown = false;
            Mouse.Capture(null);
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void MaximizeButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleWindowState();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void ToggleWindowState()
        {
            if (_isWorkAreaMaximized)
            {
                RestoreFromWorkArea();
            }
            else
            {
                MaximizeToWorkArea();
            }
        }

        private void MaximizeToWorkArea()
        {
            if (_isWorkAreaMaximized)
            {
                return;
            }

            _normalWindowBounds = new Rect(Left, Top, Width, Height);
            var workArea = SystemParameters.WorkArea;
            ResizeMode = ResizeMode.NoResize;
            Left = workArea.Left;
            Top = workArea.Top;
            Width = workArea.Width;
            Height = workArea.Height;
            _isWorkAreaMaximized = true;
            MainWindowChrome.CornerRadius = new CornerRadius(0);
            WindowHeaderBorder.CornerRadius = new CornerRadius(0);
            MaximizeButton.Content = "❐";
            MaximizeButton.ToolTip = "还原";
        }

        private void RestoreFromWorkArea()
        {
            if (!_isWorkAreaMaximized)
            {
                return;
            }

            Left = _normalWindowBounds.Left;
            Top = _normalWindowBounds.Top;
            Width = _normalWindowBounds.Width;
            Height = _normalWindowBounds.Height;
            ResizeMode = ResizeMode.CanResize;
            _isWorkAreaMaximized = false;
            MainWindowChrome.CornerRadius = new CornerRadius(12);
            WindowHeaderBorder.CornerRadius = new CornerRadius(12, 12, 0, 0);
            MaximizeButton.Content = "□";
            MaximizeButton.ToolTip = "最大化";
        }

        public void OpenMessageManagerWindow()
        {
            if (_messageManagerWindow != null)
            {
                _messageManagerWindow.ShowForUser();
                return;
            }

            // 消息管理与主窗口保持独立任务栏窗口，避免拥有窗口关系导致无法切回主界面。
            _messageManagerWindow = new MessageManagerWindow(this);
            _messageManagerWindow.Closed += (_, _) => _messageManagerWindow = null;
            _messageManagerWindow.ShowForUser();
        }

        /// <summary>
        /// 初始化 WebView2 加载 Vue 前端
        /// </summary>
        private async void InitializeVueWebView()
        {
            try
            {
                // Keep WebView2 localStorage/cookies outside the Velopack version
                // directory. Otherwise every update can create a new profile and
                // discard the system login token.
                var webViewUserDataFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "EyochSocial",
                    "WebView2");
                Directory.CreateDirectory(webViewUserDataFolder);
                var webViewEnvironment = await CoreWebView2Environment.CreateAsync(
                    null,
                    webViewUserDataFolder,
                    null);
                await VueWebView.EnsureCoreWebView2Async(webViewEnvironment);

                VueWebView.CoreWebView2.NavigationCompleted += (_, _) =>
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        VueWebView.Visibility = Visibility.Visible;
                        VueLoadingOverlay.Visibility = Visibility.Collapsed;
                        if (_showStartupLoading)
                        {
                            _showStartupLoading = false;
                            try
                            {
                                File.WriteAllText(
                                    Path.Combine(AppContext.BaseDirectory, ".first-launch-complete"),
                                    DateTimeOffset.UtcNow.ToString("O"));
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"首次启动标记写入失败: {ex.Message}");
                            }
                        }
                    }));
                };

                // 消息管理由独立 WPF 窗口承载，拦截旧前端或缓存前端发起的路由导航。
                VueWebView.CoreWebView2.NavigationStarting += (_, args) =>
                {
                    if (_showStartupLoading)
                    {
                        Dispatcher.BeginInvoke(new Action(() =>
                        {
                            VueWebView.Visibility = Visibility.Collapsed;
                            VueLoadingOverlay.Visibility = Visibility.Visible;
                        }));
                    }
                    try
                    {
                        var uri = new Uri(args.Uri);
                        var path = uri.AbsolutePath.TrimEnd('/');
                        if (string.Equals(path, "/facebook/message", StringComparison.OrdinalIgnoreCase)
                            && !uri.Query.Contains("detached=1", StringComparison.OrdinalIgnoreCase))
                        {
                            args.Cancel = true;
                            Dispatcher.BeginInvoke(new Action(OpenMessageManagerWindow));
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"消息管理路由拦截失败: {ex.Message}");
                    }
                };

                // 创建 JS 桥接服务
                _jsBridge = new JsBridgeService(this);
                VueWebView.CoreWebView2.AddHostObjectToScript("wpfBridge", _jsBridge);
                VueWebView.CoreWebView2.WebMessageReceived += (_, args) =>
                {
                    try
                    {
                        var payload = JObject.Parse(args.TryGetWebMessageAsString());
                        _messageManagerWindow?.HandleRelayMessage(payload);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"消息管理中转响应解析失败: {ex.Message}");
                    }
                };

                // 开发环境连接本机 Vite，生产环境直接连接服务器 Nginx。
#if DEBUG
                VueWebView.Source = new Uri("http://localhost:80");
#else
                VueWebView.Source = new Uri(ProductionVueUrl);
#endif

                System.Diagnostics.Debug.WriteLine("✅ WebView2 初始化成功");
                UpdateStatus("WPF已启动，等待Vue领取AI采集任务");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"WebView2 初始化失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private const string WpfFileProxyScript = @"
(() => {
  const filePrefix = 'http://';
  const filePath = '/admin-api/infra/file/';
  const transparentImage = 'data:image/gif;base64,R0lGODlhAQABAAD/ACwAAAAAAQABAAACADs=';
  const isFileUrl = (value) => typeof value === 'string'
    && value.startsWith(filePrefix) && value.includes(filePath);
  const originalSetAttribute = Element.prototype.setAttribute;
  const loadFile = (node, name, value) => {
    const bridge = window.chrome && window.chrome.webview && window.chrome.webview.hostObjects
      ? window.chrome.webview.hostObjects.wpfBridge : null;
    if (!bridge) return;
    originalSetAttribute.call(node, name, name === 'src' ? transparentImage : '#');
    Promise.resolve(bridge.GetFileDataUrl(value)).then((dataUrl) => {
      if (typeof dataUrl === 'string' && dataUrl.startsWith('data:')) {
        originalSetAttribute.call(node, name, dataUrl);
      }
    }).catch(() => {});
  };
  Element.prototype.setAttribute = function(name, value) {
    const lowerName = String(name).toLowerCase();
    if ((lowerName === 'src' || lowerName === 'href') && isFileUrl(value)) {
      loadFile(this, lowerName, value);
      return;
    }
    return originalSetAttribute.call(this, name, value);
  };
  const patchProperty = (prototype, property) => {
    const descriptor = Object.getOwnPropertyDescriptor(prototype, property);
    if (!descriptor || !descriptor.set || !descriptor.get) return;
    Object.defineProperty(prototype, property, {
      configurable: descriptor.configurable,
      enumerable: descriptor.enumerable,
      get: descriptor.get,
      set(value) {
        if (isFileUrl(value)) loadFile(this, 'src', value);
        else descriptor.set.call(this, value);
      }
    });
  };
  patchProperty(HTMLImageElement.prototype, 'src');
  if (typeof HTMLSourceElement !== 'undefined') patchProperty(HTMLSourceElement.prototype, 'src');
  const rewrite = (root) => {
    if (!root || !root.querySelectorAll) return;
    root.querySelectorAll('img[src],a[href],link[href],source[src]').forEach((node) => {
      const attr = node.hasAttribute('src') ? 'src' : 'href';
      const value = node.getAttribute(attr);
      if (isFileUrl(value)) loadFile(node, attr, value);
    });
  };
  new MutationObserver((records) => records.forEach((record) => {
    record.addedNodes.forEach((node) => {
      if (node.nodeType === 1) rewrite(node);
    });
  })).observe(document.documentElement, { childList: true, subtree: true });
  rewrite(document);
})();";

        public void SendMessageRelayCommand(JObject command)
        {
            if (VueWebView.CoreWebView2 == null) return;
            var json = command.ToString(Formatting.None).Replace("\\", "\\\\").Replace("'", "\\'");
            var script = $"window.dispatchEvent(new CustomEvent('fb:wpf:message-command',{{detail:JSON.parse('{json}')}}));";
            VueWebView.CoreWebView2.ExecuteScriptAsync(script);
        }

        public void NotifyFacebookUnreadChanged(string accountId, int messengerUnreadCount,
            int notificationUnreadCount)
        {
            if (VueWebView.CoreWebView2 == null) return;
            var detail = JsonConvert.SerializeObject(new
            {
                accountId,
                messengerUnreadCount,
                notificationUnreadCount,
                timestamp = DateTimeOffset.Now
            });
            VueWebView.CoreWebView2.ExecuteScriptAsync(
                $"window.dispatchEvent(new CustomEvent('fb:message:badge-changed',{{detail:{detail}}}));");
        }

        public void NotifyBrowserClosed(string accountId, string detailId, int taskType)
        {
            if (VueWebView.CoreWebView2 == null) return;
            var detail = JsonConvert.SerializeObject(new { accountId, detailId, taskType });
            VueWebView.CoreWebView2.ExecuteScriptAsync(
                $"window.dispatchEvent(new CustomEvent('fb:wpf:browser-closed',{{detail:{detail}}}));");
        }

        public void StartMessageMonitorTask(string monitorId, string accountId, string cookie,
            string deviceId, string mode, string detailId, string? proxyConfigJson = null)
        {
            _messageMonitorByAccount[accountId] = monitorId;
            CreateBrowserForAccount(detailId, accountId, cookie, "https://www.facebook.com/", 0, 19,
                JsonConvert.SerializeObject(new { monitorId, mode }), true,
                long.TryParse(deviceId, out var parsedDeviceId) ? parsedDeviceId : null,
                proxyConfigJson: proxyConfigJson);
        }

        /// <summary>
        /// Vue 收到后台任务通知后调用，WPF 负责领取任务并启动浏览器。
        /// </summary>
        public void StartDmTaskFromQueue(string taskId, string detailId, string accountId,
            string cookie, string targetUserId, string scriptContent)
        {
            _jsBridge?.StartDmTask(taskId, detailId, accountId, cookie, targetUserId, scriptContent);
        }

        public void StartGroupPublishTaskFromQueue(string taskId, string accountId,
            string cookie, string actionConfig, string detailId)
        {
            _jsBridge?.StartGroupPublishTask(taskId, accountId, cookie, actionConfig, detailId);
        }

        /// <summary>
        /// 为指定账号创建浏览器实例（供 Vue 调用）
        /// </summary>
        public void CreateBrowserForAccount(string detailId, string accountId, string? cookie = null,
            string? searchUrl = null, int expectedCount = 100, int taskType = 1, string? config = null, bool isOperation = false, long? deviceId = null,
            string? password = null, string? tfa = null, string? loginAccountId = null, string? proxyConfigJson = null)
        {
            // 记录配置信息
            if (!string.IsNullOrEmpty(config))
            {
                System.Diagnostics.Debug.WriteLine($"📋 MainWindow 收到配置: {config}");
            }

            if (!_browserMatrixWindows.ContainsKey(accountId) &&
                GetBrowserWindowCount() >= BrowserMatrixWindow.MaxConcurrentBrowsers)
            {
                UpdateStatus($"已达到最大并发窗口数 ({BrowserMatrixWindow.MaxConcurrentBrowsers})，无法为账号 {accountId} 创建窗口");
                return;
            }

            var browserMatrixWindow = GetOrCreateBrowserMatrixWindow(accountId);

            // 在统一矩阵窗口的账号 Tab 中创建浏览器并启动自动化任务
            browserMatrixWindow.CreateBrowser(accountId, "https://www.facebook.com",
                cookie, searchUrl, expectedCount, deviceId: deviceId, taskType: taskType, config: config, detailId: detailId, isOperation: isOperation,
                password: password, tfa: tfa, loginAccountId: loginAccountId, proxyConfigJson: proxyConfigJson);

            
            UpdateStatus($"已为账号 {accountId} 启动自动化采集 (明细ID: {detailId}, 类型: {taskType})");
        }

        public BrowserMatrixWindow? GetBrowserMatrixWindowForAccount(string accountId)
        {
            return _browserMatrixWindows.TryGetValue(accountId, out var window) && window.IsWindowAvailable
                ? window
                : null;
        }

        public int GetBrowserWindowCount()
        {
            return _browserMatrixWindow?.GetActiveBrowserCount() ?? 0;
        }

        public int GetActiveBrowserWindowCount()
        {
            return GetBrowserWindowCount();
        }

        /// <summary>
        /// 批量临时任务全部结束后，关闭没有账号 Tab 的统一浏览器窗口。
        /// 单个账号任务结束时不能在这里关闭，否则下一批会重新创建顶层窗口。
        /// </summary>
        public void CloseBrowserMatrixWindowIfEmpty()
        {
            if (_browserMatrixWindow != null && _browserMatrixWindow.GetActiveBrowserCount() == 0)
            {
                _browserMatrixWindow.Close();
            }
        }

        public BrowserMatrixWindow GetOrCreateBrowserMatrixWindow(string accountId)
        {
            if (_browserMatrixWindow != null && _browserMatrixWindow.IsWindowAvailable)
            {
                _browserMatrixWindows[accountId] = _browserMatrixWindow;
                System.Diagnostics.Debug.WriteLine($"⚠️ 复用统一 BrowserMatrixWindow，账号 {accountId} 使用独立 Tab");
                return _browserMatrixWindow;
            }

            var browserMatrixWindow = new BrowserMatrixWindow();
            _browserMatrixWindows[accountId] = browserMatrixWindow;
            _browserMatrixWindow = browserMatrixWindow;

            browserMatrixWindow.Closed += (_, _) =>
            {
                foreach (var key in _browserMatrixWindows.Where(pair => ReferenceEquals(pair.Value, browserMatrixWindow)).Select(pair => pair.Key).ToList())
                    _browserMatrixWindows.Remove(key);
                if (ReferenceEquals(_browserMatrixWindow, browserMatrixWindow))
                {
                    _browserMatrixWindow = null;
                }
            };

            // 监听采集完成事件
            browserMatrixWindow.OnCollectionComplete += (dId, accId, jsonData, taskType) =>
            {
                System.Diagnostics.Debug.WriteLine($"📨 MainWindow 收到采集完成事件: 明细ID={dId}, 账号={accId}, 数据长度={jsonData.Length}, 类型={taskType}");
                // 事件可能由 CEF/采集线程同步触发，不能在这里阻塞采集任务的 finally 清理。
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (taskType == 19)
                    {
                        var monitorId = _messageMonitorByAccount.TryGetValue(accId, out var id) ? id : "";
                        ReturnMessageMonitorResultToVue(monitorId, accId, jsonData);
                        _messageMonitorByAccount.Remove(accId);
                        return;
                    }
                    // 将数据回传给 Vue
                    ReturnCollectionDataToVue(dId, accId, jsonData, taskType);
                }));
            };

            browserMatrixWindow.OnCollectionBatch += (dId, accId, jsonData, taskType) =>
            {
                Dispatcher.BeginInvoke(new Action(() => ReturnCollectionBatchToVue(dId, accId, jsonData, taskType)));
            };

            // 登录页/Cookie 失效由 Vue relay 持久化到账号登录状态，WPF 不直接改业务数据。
            browserMatrixWindow.OnCollectionError += (accId, errorMessage) =>
            {
                // 错误通知可能紧接着关闭浏览器状态；必须在调度前固定明细 ID。
                var detailId = browserMatrixWindow.GetActiveDetailId(accId);
                var taskType = browserMatrixWindow.GetActiveTaskType(accId);
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (_messageMonitorByAccount.TryGetValue(accId, out var monitorId))
                    {
                        ReturnMessageMonitorResultToVue(monitorId, accId,
                            JsonConvert.SerializeObject(new { success = false, errorMessage }));
                        _messageMonitorByAccount.Remove(accId);
                        return;
                    }
                    ReturnCollectionErrorToVue(accId, detailId, errorMessage, taskType);
                    if (IsNetworkLoadError(errorMessage)
                        && !BrowserMatrixWindow.KeepBrowserAfterTaskForDebug)
                    {
                        browserMatrixWindow.CloseBrowser(accId);
                    }
                }));
            };

            RegisterAccountLoginWindowEvents(browserMatrixWindow);
            browserMatrixWindow.Show();
            browserMatrixWindow.Activate();
            System.Diagnostics.Debug.WriteLine($"✅ 已创建统一 BrowserMatrixWindow，账号 {accountId} 将使用独立 Tab");

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
                var eventName = taskType == 18 ? "fb:profile:update:complete" : "fb:collection:complete";
                var script = $@"
                    setTimeout(() => {{
                        window.dispatchEvent(new CustomEvent('{eventName}', {{
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

        private void ReturnCollectionBatchToVue(string detailId, string accountId, string jsonData, int taskType)
        {
            try
            {
                if (VueWebView.CoreWebView2 == null) return;
                var script = $@"window.dispatchEvent(new CustomEvent('fb:collection:batch', {{ detail: {{
                    detailId: '{detailId}', accountId: '{accountId}', data: {jsonData}, taskType: {taskType},
                    timestamp: new Date().toISOString() }} }}));";
                VueWebView.CoreWebView2.ExecuteScriptAsync(script);
                System.Diagnostics.Debug.WriteLine($"📤 已回传采集批次: 明细ID={detailId}, 数量={JArray.Parse(jsonData).Count}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"⚠️ 采集批次回传失败: {ex.Message}");
            }
        }

        private void ReturnCollectionErrorToVue(string accountId, string? detailId, string errorMessage, int taskType = 0)
        {
            try
            {
                if (VueWebView.CoreWebView2 == null) return;
                var detail = JsonConvert.SerializeObject(new
                {
                    accountId,
                    detailId,
                    success = false,
                    errorMessage,
                    taskType,
                    timestamp = DateTime.UtcNow
                });
                var eventName = taskType == 18 ? "fb:profile:update:complete" : "fb:collection:error";
                var script = $"window.dispatchEvent(new CustomEvent('{eventName}', {{ detail: {detail} }}));";
                VueWebView.CoreWebView2.ExecuteScriptAsync(script);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ 回传账号异常状态失败: {ex.Message}");
            }
        }

        private void ReturnMessageMonitorResultToVue(string monitorId, string accountId, string jsonData)
        {
            if (VueWebView.CoreWebView2 == null) return;
            var detail = JsonConvert.SerializeObject(new
            {
                monitorId,
                accountId,
                data = string.IsNullOrWhiteSpace(jsonData) ? "{}" : jsonData,
                timestamp = DateTimeOffset.Now
            });
            VueWebView.CoreWebView2.ExecuteScriptAsync(
                $"window.dispatchEvent(new CustomEvent('fb:message:monitor-complete',{{detail:{detail}}}));");
        }

        private static bool IsNetworkLoadError(string errorMessage)
        {
            return Regex.IsMatch(errorMessage ?? "", "ConnectionClosed|网络|网络连接|页面加载失败|空白页|This site can.?t be reached|ERR_|超时", RegexOptions.IgnoreCase);
        }

        /// <summary>
        /// 关闭指定账号的浏览器
        /// </summary>
        public void CloseBrowserForAccount(string accountId)
        {
            _browserMatrixWindows.TryGetValue(accountId, out var browserMatrixWindow);

            // 调试保留模式下，拦截 Vue/队列的自动关闭请求，避免旧前端构建或超时兜底绕过
            // BrowserMatrixWindow 任务 finally 的保留判断。Tab 头部的手动关闭仍直接调用
            // BrowserMatrixWindow.CloseBrowser，不经过这里，因此不影响用户主动排查。
            // 如果用户已经手动关闭了浏览器，必须继续清理失效映射，不能被调试模式拦截。
            if (BrowserMatrixWindow.KeepBrowserAfterTaskForDebug
                && browserMatrixWindow != null
                && browserMatrixWindow.HasActiveBrowser(accountId))
            {
                System.Diagnostics.Debug.WriteLine(
                    $"🧪 调试保留模式：忽略前端自动关闭账号 {accountId} 的浏览器");
                return;
            }

            if (browserMatrixWindow != null)
            {
                if (browserMatrixWindow.HasActiveBrowser(accountId))
                {
                    browserMatrixWindow.CloseBrowser(accountId);
                }
                _browserMatrixWindows.Remove(accountId);
                UpdateStatus($"已清理账号 {accountId} 的浏览器状态");
                
                // 如果没有活跃浏览器，关闭窗口
                if (browserMatrixWindow.GetActiveBrowserCount() == 0)
                {
                    browserMatrixWindow.Close();
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

        protected override void OnClosed(EventArgs e)
        {
            _messageManagerWindow?.CloseForShutdown();
            base.OnClosed(e);
        }
    }
}
