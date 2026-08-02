
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
        private CollectTaskPollingService? _collectTaskPollingService;
        private BrowserMatrixWindow? _browserMatrixWindow;
        private readonly Dictionary<string, BrowserMatrixWindow> _browserMatrixWindows = new();
        private MessageManagerWindow? _messageManagerWindow;
        private Services.MessageMonitorTaskPollingService? _messageMonitorTaskPollingService;
        private readonly Dictionary<string, string> _messageMonitorByAccount = new();
        private bool _isWorkAreaMaximized;
        private Rect _normalWindowBounds;
        private bool _titleBarMouseDown;
        private Point _titleBarMouseDownPoint;

        public MainWindow()
        {
            InitializeComponent();
            MaximizeToWorkArea();
            InitializeVueWebView();
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
                // 确保 WebView2 运行时已安装
                await VueWebView.EnsureCoreWebView2Async();

                VueWebView.CoreWebView2.NavigationCompleted += (_, _) =>
                {
                    // 消息监控作为统一浏览器任务运行，不创建后台消息管理窗口。
                    Dispatcher.BeginInvoke(new Action(StartMessageMonitorTaskPolling));
                };

                // 消息管理由独立 WPF 窗口承载，拦截旧前端或缓存前端发起的路由导航。
                VueWebView.CoreWebView2.NavigationStarting += (_, args) =>
                {
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
                UpdateStatus("WPF已启动，等待Vue领取AI采集任务");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"WebView2 初始化失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

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

        private void StartCollectTaskPolling()
        {
            _collectTaskPollingService ??= new CollectTaskPollingService(this);
            UpdateStatus("WPF采集任务通知监听已启动");
            // 处理 WPF/Vue 尚未建立 WebSocket 连接前已经入队的任务，仅启动时执行一次。
            _collectTaskPollingService.TriggerNow();
        }

        private void StartMessageMonitorTaskPolling()
        {
            _messageMonitorTaskPollingService ??= new Services.MessageMonitorTaskPollingService(this);
            _messageMonitorTaskPollingService.Start();
            UpdateStatus("Facebook消息定时接收任务监听已启动");
        }

        public void StartMessageMonitorTask(string monitorId, string accountId, string cookie,
            string deviceId, string mode, string detailId)
        {
            _messageMonitorByAccount[accountId] = monitorId;
            CreateBrowserForAccount(detailId, accountId, cookie, "https://www.facebook.com/", 0, 19,
                JsonConvert.SerializeObject(new { monitorId, mode }), true,
                long.TryParse(deviceId, out var parsedDeviceId) ? parsedDeviceId : null);
        }

        public void TriggerMessageMonitorTaskClaim()
        {
            _messageMonitorTaskPollingService ??= new Services.MessageMonitorTaskPollingService(this);
            _messageMonitorTaskPollingService.TriggerNow();
        }

        internal void MarkMessageMonitorReported(string accountId)
        {
            _messageMonitorByAccount.Remove(accountId);
        }

        /// <summary>
        /// Vue 收到后台任务通知后调用，WPF 负责领取任务并启动浏览器。
        /// </summary>
        public void TriggerCollectTaskClaim()
        {
            _collectTaskPollingService ??= new CollectTaskPollingService(this);
            _collectTaskPollingService.TriggerNow();
        }

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
            string? searchUrl = null, int expectedCount = 100, int taskType = 1, string? config = null, bool isOperation = false, long? deviceId = null)
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
                cookie, searchUrl, expectedCount, deviceId: deviceId, taskType: taskType, config: config, detailId: detailId, isOperation: isOperation);
            
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
                if (taskType == 19)
                {
                    var monitorId = _messageMonitorByAccount.TryGetValue(accId, out var id) ? id : "";
                    try
                    {
                        var monitorResult = JObject.Parse(jsonData);
                        var success = monitorResult.Value<bool?>("success") ?? true;
                        _ = _messageMonitorTaskPollingService?.ReportAsync(monitorId, success,
                            monitorResult.Value<string>("errorMessage"), accId,
                            monitorResult.Value<int?>("messengerUnreadCount") ?? 0,
                            monitorResult.Value<int?>("notificationUnreadCount") ?? 0);
                    }
                    catch
                    {
                        _ = _messageMonitorTaskPollingService?.ReportAsync(monitorId, false, "消息监控结果解析失败", accId);
                    }
                    _messageMonitorByAccount.Remove(accId);
                    return;
                }
                // 将数据回传给 Vue
                Dispatcher.Invoke(() =>
                {
                    ReturnCollectionDataToVue(dId, accId, jsonData, taskType);
                });
            };

            // 登录页/Cookie 失效由 Vue relay 持久化到账号登录状态，WPF 不直接改业务数据。
            browserMatrixWindow.OnCollectionError += (accId, errorMessage) =>
            {
                Dispatcher.Invoke(() =>
                {
                    if (_messageMonitorByAccount.TryGetValue(accId, out var monitorId))
                    {
                        _ = _messageMonitorTaskPollingService?.ReportAsync(monitorId, false, errorMessage);
                        _messageMonitorByAccount.Remove(accId);
                        return;
                    }
                    var detailId = browserMatrixWindow.GetActiveDetailId(accId);
                    ReturnCollectionErrorToVue(accId, detailId, errorMessage);
                    if (IsNetworkLoadError(errorMessage))
                    {
                        browserMatrixWindow.CloseBrowser(accId);
                    }
                });
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

        private void ReturnCollectionErrorToVue(string accountId, string? detailId, string errorMessage)
        {
            try
            {
                if (VueWebView.CoreWebView2 == null) return;
                var detail = JsonConvert.SerializeObject(new
                {
                    accountId,
                    detailId,
                    errorMessage,
                    timestamp = DateTime.UtcNow
                });
                var script = $"window.dispatchEvent(new CustomEvent('fb:collection:error', {{ detail: {detail} }}));";
                VueWebView.CoreWebView2.ExecuteScriptAsync(script);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ 回传账号异常状态失败: {ex.Message}");
            }
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
            if (_browserMatrixWindows.TryGetValue(accountId, out var browserMatrixWindow))
            {
                browserMatrixWindow.CloseBrowser(accountId);
                _browserMatrixWindows.Remove(accountId);
                UpdateStatus($"已关闭账号 {accountId} 的浏览器");
                
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
            _messageMonitorTaskPollingService?.Dispose();
            _collectTaskPollingService?.Dispose();
            _messageManagerWindow?.CloseForShutdown();
            base.OnClosed(e);
        }
    }
}
