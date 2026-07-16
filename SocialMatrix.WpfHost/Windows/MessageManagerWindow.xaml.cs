using CefSharp;
using CefSharp.Wpf;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SocialMatrix.WpfHost.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace SocialMatrix.WpfHost.Windows
{
    public partial class MessageManagerWindow : Window
    {
        private sealed class AccountRow : INotifyPropertyChanged
        {
            public long Id { get; set; }
            public string FbAccount { get; set; } = "";
            public string Cookie { get; set; } = "";
            public long? DeviceId { get; set; }
            public string DisplayName => string.IsNullOrWhiteSpace(FbAccount) ? Id.ToString() : FbAccount;
            public int ReceiveEnabled { get; set; }
            public int OnlineStatus { get; set; }
            private bool _selected;
            private bool _isCurrent;
            private bool _enabled;
            private string _mode = "disabled";
            private string _state = "未启用";
            public int MessengerUnreadCount { get; set; }
            public int CommentUnreadCount { get; set; }
            public int TotalUnreadCount => MessengerUnreadCount + CommentUnreadCount;
            public string LastCheckTime { get; set; } = "";
            public bool Enabled { get => _enabled; set { _enabled = value; OnChanged(); } }
            public bool IsSelected { get => _selected; set { _selected = value; OnChanged(); } }
            public bool IsCurrent { get => _isCurrent; set { _isCurrent = value; OnChanged(); } }
            public string Mode { get => _mode; set { _mode = value; OnChanged(); } }
            public string State { get => _state; set { _state = value; OnChanged(); } }
            public event PropertyChangedEventHandler? PropertyChanged;
            private void OnChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
            public void RefreshUnread()
            {
                OnChanged(nameof(MessengerUnreadCount));
                OnChanged(nameof(CommentUnreadCount));
                OnChanged(nameof(TotalUnreadCount));
            }
        }

        private sealed class ConversationRow
        {
            public long Id { get; set; }
            public long AccountId { get; set; }
            public string ConversationKey { get; set; } = "";
            public string TargetUserId { get; set; } = "";
            public string TargetName { get; set; } = "";
            public string ReplyTargetLanguage { get; set; } = "";
            public int UnreadCount { get; set; }
            public string LastMessagePreview { get; set; } = "";
            public string LastMessageTime { get; set; } = "";
            public string DisplayText => $"{(string.IsNullOrWhiteSpace(TargetName) ? TargetUserId : TargetName)}  {LastMessagePreview}";
        }

        private sealed class ScriptRow
        {
            public string Title { get; set; } = "";
            public string Content { get; set; } = "";
        }

        private sealed class BrowserSession
        {
            public required string AccountId { get; init; }
            public required ChromiumWebBrowser Browser { get; init; }
            public required IRequestContext RequestContext { get; init; }
            public string? MonitorId { get; set; }
            public string Mode { get; set; } = "scheduled";
            public string Kind { get; set; } = "messenger";
            public bool Completed { get; set; }
            public bool ManualView { get; set; }
            public bool MessengerOpened { get; set; }
            public int MonitorRounds { get; set; }
            public int? LastReportedMessengerUnreadCount { get; set; }
            public int? LastReportedNotificationUnreadCount { get; set; }
            public bool? LastReportedLoggedIn { get; set; }
            public bool BadgePersisted { get; set; }
            public Task InitializationTask { get; set; } = Task.CompletedTask;
            public HashSet<string> SeenKeys { get; } = new();
        }

        private readonly MainWindow _owner;
        private readonly ObservableCollection<AccountRow> _accounts = new();
        private readonly Dictionary<string, BrowserSession> _sessions = new();
        private readonly Dictionary<string, TaskCompletionSource<JObject>> _pending = new();
        private readonly DispatcherTimer _timer;
        private readonly DispatcherTimer _startupTimer;
        private readonly DispatcherTimer _badgeTimer;
        private readonly DispatcherTimer _selectionTimer;
        private AccountRow? _currentAccount;
        private bool _loading;
        private bool _claimingMonitors;
        private bool _checkingSelection;
        private bool _syncingAccountSelection;
        private int _requestSequence;
        private int _scriptPage = 1;
        private const int ScriptPageSize = 10;
        private int _scriptTotal;
        private string _lastSelectedText = "";

        public MessageManagerWindow(MainWindow owner)
        {
            _owner = owner;
            InitializeComponent();
            TargetLanguageBox.SelectedIndex = 0;
            RealtimeAccountList.ItemTemplate = BuildAccountTemplate();
            ScheduledAccountList.ItemTemplate = BuildAccountTemplate();
            ScriptList.ItemTemplate = BuildScriptTemplate();
            var listItemStyle = new Style(typeof(ListBoxItem));
            listItemStyle.Setters.Add(new Setter(Control.BackgroundProperty, System.Windows.Media.Brushes.Transparent));
            listItemStyle.Setters.Add(new Setter(Control.ForegroundProperty, System.Windows.Media.Brushes.Black));
            listItemStyle.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0)));
            listItemStyle.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch));
            listItemStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(0)));
            RealtimeAccountList.ItemContainerStyle = listItemStyle;
            ScheduledAccountList.ItemContainerStyle = listItemStyle;
            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
            _timer.Tick += async (_, _) => await RunMonitorRoundAsync();
            _startupTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _startupTimer.Tick += async (_, _) => await ClaimMonitorAccountsAsync(1);
            _badgeTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            _badgeTimer.Tick += async (_, _) => await PollBadgeCountsAsync();
            _selectionTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
            _selectionTimer.Tick += async (_, _) => await CheckSelectionTranslationAsync();
            Loaded += async (_, _) => await LoadDataAsync();
            Loaded += (_, _) => _selectionTimer.Start();
            Closed += (_, _) => CloseAllBrowsers();
        }

        private DataTemplate BuildAccountTemplate()
        {
            var template = new DataTemplate();
            var border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.PaddingProperty, new Thickness(10, 7, 8, 7));
            border.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Stretch);
            var rowStyle = new Style(typeof(Border));
            rowStyle.Setters.Add(new Setter(Border.BackgroundProperty, System.Windows.Media.Brushes.Transparent));
            rowStyle.Setters.Add(new Setter(Border.BorderBrushProperty, new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(240, 240, 240))));
            rowStyle.Setters.Add(new Setter(Border.BorderThicknessProperty, new Thickness(0, 0, 0, 1)));
            var currentRowTrigger = new DataTrigger
            {
                Binding = new System.Windows.Data.Binding(nameof(AccountRow.IsCurrent)),
                Value = true
            };
            currentRowTrigger.Setters.Add(new Setter(Border.BackgroundProperty, new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(198, 229, 255))));
            rowStyle.Triggers.Add(currentRowTrigger);
            border.SetValue(FrameworkElement.StyleProperty, rowStyle);
            var stack = new FrameworkElementFactory(typeof(StackPanel));
            var titleRow = new FrameworkElementFactory(typeof(StackPanel));
            titleRow.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
            var check = new FrameworkElementFactory(typeof(CheckBox));
            check.SetBinding(CheckBox.IsCheckedProperty, new System.Windows.Data.Binding(nameof(AccountRow.IsSelected)) { Mode = System.Windows.Data.BindingMode.TwoWay });
            check.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 0, 8, 0));
            check.AddHandler(CheckBox.CheckedEvent, new RoutedEventHandler(AccountSelectionChanged));
            check.AddHandler(CheckBox.UncheckedEvent, new RoutedEventHandler(AccountSelectionChanged));
            titleRow.AppendChild(check);
            var name = new FrameworkElementFactory(typeof(TextBlock));
            name.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding(nameof(AccountRow.DisplayName)));
            name.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);
            titleRow.AppendChild(name);
            stack.AppendChild(titleRow);
            var metaRow = new FrameworkElementFactory(typeof(StackPanel));
            metaRow.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
            var state = new FrameworkElementFactory(typeof(TextBlock));
            state.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding(nameof(AccountRow.State)));
            state.SetValue(TextBlock.FontSizeProperty, 12d);
            state.SetValue(TextBlock.MinWidthProperty, 52d);
            metaRow.AppendChild(state);
            var unreadRow = new FrameworkElementFactory(typeof(StackPanel));
            unreadRow.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
            var unread = new FrameworkElementFactory(typeof(TextBlock));
            unread.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding(nameof(AccountRow.MessengerUnreadCount)) { StringFormat = "消息 {0}" });
            unread.SetValue(TextBlock.ForegroundProperty, new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(64, 158, 255)));
            unread.SetValue(TextBlock.FontSizeProperty, 12d);
            unreadRow.AppendChild(unread);
            var notices = new FrameworkElementFactory(typeof(TextBlock));
            notices.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding(nameof(AccountRow.CommentUnreadCount)) { StringFormat = "通知 {0}" });
            notices.SetValue(TextBlock.ForegroundProperty, new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(230, 90, 70)));
            notices.SetValue(TextBlock.FontSizeProperty, 12d);
            notices.SetValue(FrameworkElement.MarginProperty, new Thickness(12, 0, 0, 0));
            unreadRow.AppendChild(notices);
            metaRow.AppendChild(unreadRow);
            stack.AppendChild(metaRow);
            border.AppendChild(stack);
            template.VisualTree = border;
            return template;
        }

        private DataTemplate BuildScriptTemplate()
        {
            var template = new DataTemplate();
            var panel = new FrameworkElementFactory(typeof(StackPanel));
            panel.SetValue(FrameworkElement.MarginProperty, new Thickness(8, 6, 8, 6));

            var title = new FrameworkElementFactory(typeof(TextBlock));
            title.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding(nameof(ScriptRow.Title)));
            title.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);
            panel.AppendChild(title);

            var content = new FrameworkElementFactory(typeof(TextBlock));
            content.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding(nameof(ScriptRow.Content)));
            content.SetValue(TextBlock.TextWrappingProperty, TextWrapping.Wrap);
            content.SetValue(TextBlock.MaxHeightProperty, 42d);
            content.SetValue(TextBlock.ForegroundProperty, new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(96, 98, 102)));
            panel.AppendChild(content);

            template.VisualTree = panel;
            return template;
        }

        private void AccountSelectionChanged(object sender, RoutedEventArgs e)
        {
            SelectedCountText.Text = $"已选 {_accounts.Count(x => x.IsSelected)} 个";
        }

        private async Task LoadScriptsAsync()
        {
            try
            {
                var result = await RelayAsync("scripts", new JObject
                {
                    ["pageNo"] = _scriptPage,
                    ["pageSize"] = ScriptPageSize,
                    ["scriptTitle"] = ScriptSearchBox.Text.Trim()
                });
                var list = result is JObject obj ? obj["list"] as JArray : result as JArray;
                _scriptTotal = result is JObject pageResult ? pageResult.Value<int?>("total") ?? 0 : list?.Count ?? 0;
                ScriptList.ItemsSource = list?.OfType<JObject>().Select(x => new ScriptRow
                {
                    Title = x.Value<string>("scriptTitle") ?? "未命名话术",
                    Content = x.Value<string>("scriptContent") ?? ""
                }).Where(x => !string.IsNullOrWhiteSpace(x.Content)).ToList() ?? new List<ScriptRow>();
                var pageCount = Math.Max(1, (int)Math.Ceiling(_scriptTotal / (double)ScriptPageSize));
                ScriptPageText.Text = $"第 {_scriptPage} / {pageCount} 页";
                ScriptPreviousButton.IsEnabled = _scriptPage > 1;
                ScriptNextButton.IsEnabled = _scriptPage < pageCount;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"话术库加载失败: {ex.Message}");
                ScriptList.ItemsSource = Array.Empty<ScriptRow>();
            }
        }

        private async void ScriptSearchButton_Click(object sender, RoutedEventArgs e)
        {
            _scriptPage = 1;
            await LoadScriptsAsync();
        }

        private async void ScriptPreviousButton_Click(object sender, RoutedEventArgs e)
        {
            if (_scriptPage <= 1) return;
            _scriptPage--;
            await LoadScriptsAsync();
        }

        private async void ScriptNextButton_Click(object sender, RoutedEventArgs e)
        {
            var pageCount = Math.Max(1, (int)Math.Ceiling(_scriptTotal / (double)ScriptPageSize));
            if (_scriptPage >= pageCount) return;
            _scriptPage++;
            await LoadScriptsAsync();
        }

        private void ScriptList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count == 0 || e.AddedItems[0] is not ScriptRow script) return;
            ReplyChineseBox.Text = script.Content;
            ReplyTranslatedBox.Text = script.Content;
            SendButton.IsEnabled = !string.IsNullOrWhiteSpace(script.Content);
        }

        public void HandleRelayMessage(JObject message)
        {
            if (!"fb:wpf:message-response".Equals(message.Value<string>("type"))) return;
            var requestId = message.Value<string>("requestId");
            if (requestId == null || !_pending.Remove(requestId, out var pending)) return;
            if (message.Value<bool?>("ok") == true) pending.TrySetResult(message["data"] as JObject ?? new JObject { ["value"] = message["data"] });
            else pending.TrySetException(new InvalidOperationException(message.Value<string>("error") ?? "消息管理中转请求失败"));
        }

        private async Task<JToken> RelayAsync(string action, JObject? payload = null)
        {
            var requestId = $"message-{DateTime.UtcNow.Ticks}-{++_requestSequence}";
            var pending = new TaskCompletionSource<JObject>(TaskCreationOptions.RunContinuationsAsynchronously);
            _pending[requestId] = pending;
            _owner.SendMessageRelayCommand(new JObject
            {
                ["requestId"] = requestId,
                ["action"] = action,
                ["payload"] = payload ?? new JObject()
            });
            var response = await pending.Task;
            return response["value"] ?? response;
        }

        private async Task LoadDataAsync()
        {
            try
            {
                _loading = true;
                var accounts = (JArray)await RelayAsync("accounts");
                var monitors = (JArray)await RelayAsync("monitors");
                var monitorMap = monitors.OfType<JObject>().ToDictionary(x => x.Value<long>("accountId"), x => x);
                _accounts.Clear();
                foreach (var token in accounts.OfType<JObject>())
                {
                    var account = new AccountRow
                    {
                        Id = token.Value<long>("id"),
                        FbAccount = token.Value<string>("fbAccount") ?? "",
                        Cookie = token.Value<string>("cookie") ?? "",
                        DeviceId = token.Value<long?>("deviceId")
                    };
                    if (monitorMap.TryGetValue(account.Id, out var monitor))
                    {
                        account.ReceiveEnabled = monitor.Value<int?>("receiveEnabled") ?? 0;
                        account.OnlineStatus = monitor.Value<int?>("onlineStatus") ?? 0;
                        account.Mode = monitor.Value<string>("mode") ?? "disabled";
                        account.Enabled = account.ReceiveEnabled == 1;
                        account.State = account.OnlineStatus == 1 ? "在线" : "离线";
                        account.MessengerUnreadCount = monitor.Value<int?>("messengerUnreadCount") ?? 0;
                        account.CommentUnreadCount = monitor.Value<int?>("notificationUnreadCount") ?? 0;
                        account.LastCheckTime = monitor.Value<string>("lastCheckTime") ?? "";
                    }
                    _accounts.Add(account);
                }
                foreach (var session in _sessions.Values.ToList())
                {
                    var row = _accounts.FirstOrDefault(x => x.Id.ToString() == session.AccountId);
                    if (row == null || row.ReceiveEnabled != 1) CloseMessageBrowserAccount(session.AccountId);
                }
                RefreshAccountGroups();
                await LoadScriptsAsync();
                await RunMonitorRoundAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"消息管理数据加载失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally { _loading = false; _timer.Start(); _startupTimer.Start(); _badgeTimer.Start(); }
        }

        private async Task RunMonitorRoundAsync()
        {
            try
            {
                await ClaimMonitorAccountsAsync(1);
                foreach (var session in _sessions.Values.ToList())
                {
                    if (_owner.GetBrowserMatrixWindowForAccount(session.AccountId)?.GetActiveBrowserCount() > 0)
                    {
                        if (session.MonitorId != null)
                            _ = RelayAsync("reportMonitor", new JObject
                            {
                                ["monitorId"] = session.MonitorId,
                                ["success"] = false,
                                ["errorMessage"] = "业务任务优先，消息监控已暂停"
                            });
                        CloseMessageBrowserAccount(session.AccountId);
                        continue;
                    }
                    // 红圈读取由 badgeTimer 每 5 秒执行，这里只处理业务任务占用。
                }
                foreach (var row in _accounts.Where(x => x.ReceiveEnabled == 1 && x.OnlineStatus == 1))
                {
                    if (!_sessions.ContainsKey(row.Id.ToString())) continue;
                    var monitor = (JArray)await RelayAsync("monitors");
                    var item = monitor.OfType<JObject>().FirstOrDefault(x => x.Value<long>("accountId") == row.Id);
                    if (item != null && item.Value<long?>("id") is long monitorId)
                        await RelayAsync("heartbeat", new JObject { ["monitorId"] = monitorId.ToString() });
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"消息监控轮询失败: {ex.Message}"); }
        }

        private async Task ClaimMonitorAccountsAsync(int batchSize, IEnumerable<string>? accountIds = null, bool manual = false)
        {
            if (_claimingMonitors) return;
            var availableSlots = Math.Max(0, FbFingerprintBrowserFactory.MaxConcurrentBrowsers - 5 - _owner.GetActiveBrowserWindowCount() - _sessions.Count);
            if (availableSlots == 0)
            {
                return;
            }
            _claimingMonitors = true;
            try
            {
                var excludedAccounts = new JArray(_sessions.Values
                    .Select(session => _accounts.FirstOrDefault(row => row.Id.ToString() == session.AccountId)?.FbAccount)
                    .Where(account => !string.IsNullOrWhiteSpace(account)));
                var claims = (JArray)await RelayAsync("claimMonitor", new JObject
                {
                    ["limit"] = Math.Min(batchSize, availableSlots),
                    ["excludeAccounts"] = excludedAccounts,
                    ["accountIds"] = accountIds == null ? new JArray() : new JArray(accountIds),
                    ["manual"] = manual
                });
                foreach (var token in claims.OfType<JObject>())
                {
                    var accountId = token.Value<long>("accountId").ToString();
                    var row = _accounts.FirstOrDefault(x => x.Id.ToString() == accountId);
                    if (row == null) continue;
                    if (manual) row.OnlineStatus = 1;
                    row.State = "正在启动";
                    StartMonitor(row, token);
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"消息监控账号领取失败: {ex.Message}"); }
            finally { _claimingMonitors = false; }
        }

        private void StartMonitor(AccountRow account, JObject claim)
        {
            // 监控阶段只读取当前 Facebook 页面上的两个未读角标，不打开 Messenger 或 notifications。
            var url = "https://www.facebook.com/";
            var mode = claim.Value<string>("mode") ?? account.Mode;
            account.State = "正在打开浏览器";
            var opened = OpenBrowser(account, claim.Value<string>("cookie") ?? account.Cookie, claim.Value<long?>("deviceId"), url, claim.Value<long>("monitorId"), mode);
            if (!opened)
            {
                _ = RelayAsync("reportMonitor", new JObject
                {
                    ["monitorId"] = claim.Value<string>("monitorId") ?? claim.Value<long>("monitorId").ToString(),
                    ["success"] = false,
                    ["errorMessage"] = "消息监控浏览器启动失败，已释放账号锁"
                });
            }
        }

        private bool OpenBrowser(AccountRow account, string cookie, long? deviceId, string url, long? monitorId = null, string mode = "realtime", bool show = false, bool manual = false)
        {
            var accountKey = account.Id.ToString();
            if (_owner.GetBrowserMatrixWindowForAccount(accountKey)?.GetActiveBrowserCount() > 0)
            {
                account.State = "等待账号任务完成";
                return false;
            }
            if (!_sessions.TryGetValue(accountKey, out var session))
            {
                var maxBrowsers = FbFingerprintBrowserFactory.MaxConcurrentBrowsers;
                var reservedForBusiness = 5;
                var messageLimit = "realtime".Equals(mode, StringComparison.OrdinalIgnoreCase)
                    ? Math.Max(0, maxBrowsers - reservedForBusiness)
                    : maxBrowsers;
                if (_owner.GetActiveBrowserWindowCount() + _sessions.Count >= messageLimit)
                {
                    account.State = "等待浏览器槽位";
                    return false;
                }
                var browser = FbFingerprintBrowserFactory.Create(accountKey, deviceId, out var context);
                session = new BrowserSession { AccountId = accountKey, Browser = browser, RequestContext = context };
                browser.FrameLoadEnd += (_, args) =>
                {
                    if (!args.Frame.IsMain) return;
                    Dispatcher.BeginInvoke(new Action(() => account.State = "浏览器已加载"));
                };
                BrowserHost.Children.Add(browser);
                _sessions[accountKey] = session;
                session.InitializationTask = InitializeBrowserAsync(session, cookie, deviceId, url);
            }
            session.MonitorId = monitorId?.ToString();
            session.Mode = account.OnlineStatus == 1 ? "realtime" : mode;
            session.ManualView = manual;
            session.Kind = url.Contains("notifications", StringComparison.OrdinalIgnoreCase) ? "comment" : "messenger";
            session.Completed = false;
            if (monitorId != null) session.MonitorRounds = 0;
            // 后台浏览器必须保持在可视树中，Hidden 不显示但允许 CefSharp 继续布局和加载页面。
            // Collapsed 会移出布局，部分 CefSharp 页面可能一直停留在 Cookie 注入阶段。
            foreach (var other in _sessions.Values.Where(x => x.AccountId != accountKey))
                other.Browser.Visibility = Visibility.Hidden;
            session.Browser.Visibility = show || _currentAccount?.Id.ToString() == accountKey ? Visibility.Visible : Visibility.Hidden;
            if (session.Browser.Visibility == Visibility.Visible) BrowserEmptyText.Visibility = Visibility.Collapsed;
            return true;
        }

        private async Task InitializeBrowserAsync(BrowserSession session, string cookie, long? deviceId, string url)
        {
            try { await FbFingerprintBrowserFactory.InitializeAsync(session.Browser, session.AccountId, cookie, deviceId, url); }
            catch (Exception ex)
            {
                SetAccountState(session.AccountId, $"浏览器异常：{ex.Message}");
                if (session.MonitorId != null)
                {
                    _ = RelayAsync("reportMonitor", new JObject
                    {
                        ["monitorId"] = session.MonitorId,
                        ["success"] = false,
                        ["errorMessage"] = "消息监控浏览器初始化失败，已释放账号锁"
                    });
                }
            }
        }

        private async void AccountList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_loading || _syncingAccountSelection || sender is not ListBox list
                || list.SelectedItem is not AccountRow row) return;

            // 实时和定时是两个 ListBox，切换时清掉另一个列表的选中项，
            // 否则切回原列表时不会再次触发 SelectionChanged。
            _syncingAccountSelection = true;
            try
            {
                if (ReferenceEquals(list, RealtimeAccountList))
                    ScheduledAccountList.SelectedItem = null;
                else if (ReferenceEquals(list, ScheduledAccountList))
                    RealtimeAccountList.SelectedItem = null;
            }
            finally
            {
                _syncingAccountSelection = false;
            }

            if ("disabled".Equals(row.Mode, StringComparison.OrdinalIgnoreCase)) return;

            BrowserSession session;
            if (!_sessions.TryGetValue(row.Id.ToString(), out session))
            {
                // 新账号立即创建并显示浏览器，Cookie/页面加载在后台继续。
                var opened = OpenBrowser(row, row.Cookie, row.DeviceId, "https://www.facebook.com/messages/", mode: row.Mode, show: true, manual: "scheduled".Equals(row.Mode, StringComparison.OrdinalIgnoreCase));
                if (!opened || !_sessions.TryGetValue(row.Id.ToString(), out session)) return;
            }
            else
            {
                // 已有浏览器只切换显示，不重复创建、不等待加载。
                session.Mode = row.OnlineStatus == 1 ? "realtime" : row.Mode;
                session.ManualView = row.OnlineStatus == 0 && "scheduled".Equals(row.Mode, StringComparison.OrdinalIgnoreCase);
                ShowBrowserSession(session);
            }

            if (_currentAccount != null && !ReferenceEquals(_currentAccount, row))
                _currentAccount.IsCurrent = false;
            row.IsCurrent = true;
            if (row.OnlineStatus != 1)
            {
                await RelayAsync("batchMonitorState", new JObject
                {
                    ["accountIds"] = new JArray(row.Id.ToString()),
                    ["state"] = "online"
                });
                row.OnlineStatus = 1;
                row.State = "在线";
                session.Mode = "realtime";
                session.ManualView = false;
                RefreshAccountGroups();
            }

            _currentAccount = row;
            if (!session.MessengerOpened) _ = OpenMessengerOnFirstViewAsync(session);
        }

        private void ShowBrowserSession(BrowserSession session)
        {
            foreach (var other in _sessions.Values.Where(x => x.AccountId != session.AccountId))
                other.Browser.Visibility = Visibility.Hidden;
            session.Browser.Visibility = Visibility.Visible;
            BrowserEmptyText.Visibility = Visibility.Collapsed;
        }

        private async Task OpenMessengerOnFirstViewAsync(BrowserSession session)
        {
            try
            {
                await session.InitializationTask;
                if (session.MessengerOpened) return;
                if (await IsFacebookLoginPageAsync(session))
                {
                    session.MessengerOpened = true;
                    SetAccountState(session.AccountId, "Cookie失效");
                    return;
                }
                var address = session.Browser.Address ?? "";
                if (!address.Contains("facebook.com/messages", StringComparison.OrdinalIgnoreCase))
                    session.Browser.Load("https://www.facebook.com/messages/");
                session.MessengerOpened = true;
            }
            catch (Exception ex)
            {
                SetAccountState(session.AccountId, $"Messenger打开失败：{ex.Message}");
            }
        }

        private async Task<bool> IsFacebookLoginPageAsync(BrowserSession session)
        {
            var address = session.Browser.Address ?? "";
            if (address.Contains("/login", StringComparison.OrdinalIgnoreCase)
                || address.Contains("checkpoint", StringComparison.OrdinalIgnoreCase)) return true;
            if (!session.Browser.CanExecuteJavascriptInMainFrame) return false;
            var result = await session.Browser.EvaluateScriptAsync(@"(function(){
                return !!document.querySelector('input[name=""email""], input[name=""pass""], form[action*=""login""]');
            })();");
            return result.Success && result.Result is bool isLogin && isLogin;
        }

        private List<AccountRow> GetSelectedPoolAccounts()
        {
            return _accounts.Where(x => x.ReceiveEnabled == 1 && x.IsSelected).ToList();
        }

        private async void BringOnline_Click(object sender, RoutedEventArgs e)
        {
            var selected = GetSelectedPoolAccounts()
                .Where(x => x.OnlineStatus != 1)
                .ToList();
            if (selected.Count == 0) { MessageBox.Show("请先勾选要上线的账号。", "提示", MessageBoxButton.OK, MessageBoxImage.Information); return; }
            var newCount = selected.Count(x => !_sessions.ContainsKey(x.Id.ToString()));
            var available = Math.Max(0, FbFingerprintBrowserFactory.MaxConcurrentBrowsers - 5 - _owner.GetActiveBrowserWindowCount() - _sessions.Count);
            if (newCount > available)
            {
                MessageBox.Show($"可用消息浏览器槽位不足，需要 {newCount} 个，当前只有 {available} 个。整批未执行。", "上线失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            await RelayAsync("batchMonitorState", new JObject
            {
                ["accountIds"] = new JArray(selected.Select(x => x.Id.ToString())),
                ["state"] = "online"
            });
            foreach (var row in selected)
            {
                row.OnlineStatus = 1;
                row.State = "等待上线";
                if (_sessions.TryGetValue(row.Id.ToString(), out var session)) { session.Mode = "realtime"; session.ManualView = false; }
            }
            await ClaimMonitorAccountsAsync(newCount, selected.Select(x => x.Id.ToString()), true);
            RefreshAccountGroups();
        }

        private async void SetScheduled_Click(object sender, RoutedEventArgs e)
        {
            var selected = GetSelectedPoolAccounts();
            if (selected.Count == 0) { MessageBox.Show("请先勾选要切换的账号。", "提示", MessageBoxButton.OK, MessageBoxImage.Information); return; }
            foreach (var row in selected)
            {
                if (_sessions.ContainsKey(row.Id.ToString())) CloseMessageBrowserAccount(row.Id.ToString());
                row.OnlineStatus = 0;
                row.Mode = "scheduled";
                row.State = "离线";
                row.IsSelected = false;
            }
            await RelayAsync("batchMonitorState", new JObject
            {
                ["accountIds"] = new JArray(selected.Select(x => x.Id.ToString())),
                ["state"] = "scheduled"
            });
            RefreshAccountGroups();
        }

        private async void ManagePool_Click(object sender, RoutedEventArgs e)
        {
            var window = new MessageReceivePoolWindow(
                async () => (JArray)await RelayAsync("accounts"),
                async () => (JArray)await RelayAsync("monitors"),
                async (ids, interval) => await RelayAsync("addMonitorPool", new JObject { ["accountIds"] = ids, ["checkIntervalMinutes"] = interval }),
                async (ids, interval) => await RelayAsync("updateMonitorIntervals", new JObject { ["accountIds"] = ids, ["checkIntervalMinutes"] = interval }),
                async ids => await RelayAsync("removeMonitorPool", new JObject { ["accountIds"] = ids })) { Owner = this };
            window.ShowDialog();
            await LoadDataAsync();
        }

        private void RefreshAccountGroups()
        {
            var keyword = AccountSearchBox?.Text?.Trim() ?? "";
            var rows = _accounts.Where(x => string.IsNullOrEmpty(keyword) || x.DisplayName.Contains(keyword, StringComparison.OrdinalIgnoreCase)).ToList();
            RealtimeAccountList.ItemsSource = rows.Where(x => x.ReceiveEnabled == 1 && x.OnlineStatus == 1)
                .OrderByDescending(x => x.TotalUnreadCount).ThenBy(x => x.DisplayName).ToList();
            ScheduledAccountList.ItemsSource = rows.Where(x => x.ReceiveEnabled == 1 && x.OnlineStatus == 0)
                .OrderByDescending(x => x.TotalUnreadCount).ThenBy(x => x.DisplayName).ToList();
        }

        private async void ReplyChineseBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (AutoTranslateCheckBox.IsChecked == true) await TranslateReplySafelyAsync();
        }

        private async void TargetLanguageBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (AutoTranslateCheckBox.IsChecked == true) await TranslateReplySafelyAsync();
        }
        private async void TranslateButton_Click(object sender, RoutedEventArgs e) => await TranslateReplySafelyAsync();

        private async Task TranslateReplySafelyAsync()
        {
            try
            {
                await TranslateReplyAsync();
            }
            catch (Exception ex)
            {
                ReplyTranslatedBox.Text = "";
                System.Diagnostics.Debug.WriteLine($"消息翻译失败: {ex.Message}");
                MessageBox.Show($"翻译失败，请稍后重试：{ex.Message}", "翻译提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private async Task TranslateReplyAsync()
        {
            if (TranslationProgress.Visibility == Visibility.Visible) return;
            var text = ReplyChineseBox.Text.Trim();
            if (text.Length == 0) { ReplyTranslatedBox.Text = ""; return; }
            var target = (TargetLanguageBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "英语";
            if (!text.Any(character => character >= '\u3400' && character <= '\u9FFF'))
            {
                ReplyTranslatedBox.Text = text;
                return;
            }
            TranslationProgress.Visibility = Visibility.Visible;
            TranslateButton.IsEnabled = false;
            SendButton.IsEnabled = false;
            try
            {
                var request = RelayAsync("translate", new JObject
                {
                    ["text"] = text, ["sourceLanguage"] = "zh", ["targetLanguage"] = target, ["context"] = "facebook_messenger_reply"
                });
                var completed = await Task.WhenAny(request, Task.Delay(TimeSpan.FromSeconds(10)));
                if (completed != request) throw new TimeoutException("翻译超过10秒未完成");
                var result = await request;
                ReplyTranslatedBox.Text = result.Value<string>("translation")
                    ?? result.SelectToken("data.translation")?.ToString()
                    ?? result.SelectToken("data.content")?.ToString()
                    ?? result.Value<string>("content")
                    ?? text;
            }
            finally
            {
                TranslationProgress.Visibility = Visibility.Collapsed;
                TranslateButton.IsEnabled = true;
                SendButton.IsEnabled = !string.IsNullOrWhiteSpace(ReplyTranslatedBox.Text);
            }
        }

        private async void SendButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentAccount == null)
            {
                MessageBox.Show("请先选择 Facebook 账号。", "发送提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (string.IsNullOrWhiteSpace(ReplyTranslatedBox.Text))
            {
                MessageBox.Show("请输入要发送的消息。", "发送提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            try
            {
                var text = ReplyTranslatedBox.Text.Trim();
                var account = _currentAccount;
                var conversation = await ResolveBrowserConversationAsync(account);
                if (conversation == null)
                {
                    MessageBox.Show("请先在中间 Messenger 打开一个会话。", "发送提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                var sent = await SendDirectMessageAsync(account, conversation, text);
                if (!sent) return;
                await RelayAsync("ingest", new JObject
                {
                    ["accountId"] = account.Id.ToString(),
                    ["conversationKey"] = conversation.ConversationKey,
                    ["targetUserId"] = conversation.TargetUserId,
                    ["targetName"] = conversation.TargetName,
                    ["targetUrl"] = $"https://www.facebook.com/{conversation.TargetUserId}",
                    ["originalText"] = text,
                    ["direction"] = "outbound",
                    ["sourceType"] = "messenger",
                    ["detectedLanguage"] = (TargetLanguageBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "en"
                });
                ReplyChineseBox.Text = "";
                ReplyTranslatedBox.Text = "";
                MessageBox.Show("消息已发送到 Messenger。", "发送成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"发送失败：{ex.Message}", "发送失败", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task<ConversationRow?> ResolveBrowserConversationAsync(AccountRow account)
        {
            if (!_sessions.TryGetValue(account.Id.ToString(), out var session)) return null;
            var address = session.Browser.Address ?? "";
            var match = Regex.Match(address, @"/messages/(?:e2ee/)?t/([^/?#]+)", RegexOptions.IgnoreCase);
            if (!match.Success) return null;

            var targetUserId = Uri.UnescapeDataString(match.Groups[1].Value);
            var targetName = targetUserId;
            if (session.Browser.CanExecuteJavascriptInMainFrame)
            {
                var result = await session.Browser.EvaluateScriptAsync(@"(function(){
                    const node = document.querySelector('[role=""main""] h1, [role=""main""] header h2, [role=""main""] header [dir=""auto""]');
                    return (node && (node.innerText || node.textContent) || '').trim();
                })();");
                if (result.Success && result.Result is string name && !string.IsNullOrWhiteSpace(name)) targetName = name;
            }

            return new ConversationRow
            {
                AccountId = account.Id,
                ConversationKey = targetUserId,
                TargetUserId = targetUserId,
                TargetName = targetName,
                ReplyTargetLanguage = (TargetLanguageBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "en"
            };
        }

        private async Task<bool> SendDirectMessageAsync(AccountRow account, ConversationRow conversation, string text)
        {
            var accountKey = account.Id.ToString();
            var url = $"https://www.facebook.com/messages/t/{conversation.TargetUserId}/";
            OpenBrowser(account, account.Cookie, account.DeviceId, url,
                mode: account.Mode, show: true, manual: "scheduled".Equals(account.Mode, StringComparison.OrdinalIgnoreCase));
            if (!_sessions.TryGetValue(accountKey, out var session))
            {
                MessageBox.Show("账号当前被其它任务占用，暂时无法发送。", "发送失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            var browser = session.Browser;
            await session.InitializationTask;
            if (!IsMessageUrl(browser.Address, conversation.TargetUserId))
            {
                browser.Load(url);
            }
            if (!await WaitForBrowserReadyAsync(browser, conversation.TargetUserId, 20000))
            {
                MessageBox.Show("Messenger 会话页面加载超时。", "发送失败", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            for (var attempt = 0; attempt < 10; attempt++)
            {
                if (!browser.CanExecuteJavascriptInMainFrame)
                {
                    await Task.Delay(500);
                    continue;
                }
                await browser.EvaluateScriptAsync(DmScriptBuilder.BuildClickContinueScript());
                var ready = await browser.EvaluateScriptAsync(DmScriptBuilder.BuildEditorReadyCheckScript());
                if (ready.Success && ready.Result is bool isReady && isReady) break;
                if (attempt == 9)
                {
                    MessageBox.Show("未找到 Messenger 输入框，请先完成页面加载。", "发送失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }
                await Task.Delay(800);
            }

            try
            {
                var responseTask = browser.EvaluateScriptAsync(BuildDirectMessengerSendScript(text));
                var completed = await Task.WhenAny(responseTask, Task.Delay(60000));
                if (completed != responseTask)
                {
                    MessageBox.Show("Messenger 发送超过 60 秒未完成。", "发送失败", MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }
                var response = await responseTask;
                var raw = response.Result?.ToString() ?? "";
                var resultJson = response.Result as JObject;
                if (resultJson == null && !string.IsNullOrWhiteSpace(raw))
                {
                    try { resultJson = JObject.Parse(raw); } catch { /* keep the raw result for the error message */ }
                }
                var sentSuccessfully = response.Success && (resultJson?.Value<bool?>("success") == true
                    || raw.Contains("\"success\":true", StringComparison.OrdinalIgnoreCase));
                if (!sentSuccessfully)
                {
                    MessageBox.Show($"Messenger 发送失败：{response.Message}\n{raw}", "发送失败", MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Messenger 发送异常：{ex.Message}", "发送失败", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        private static bool IsMessageUrl(string? address, string targetUserId)
        {
            return !string.IsNullOrWhiteSpace(address)
                   && address.Contains("/messages/", StringComparison.OrdinalIgnoreCase)
                   && address.Contains(targetUserId, StringComparison.OrdinalIgnoreCase);
        }

        private static string BuildDirectMessengerSendScript(string text)
        {
            var messageJson = JsonConvert.SerializeObject(text);
            return $@"(async function() {{
    const messageText = {messageJson};
    const sleep = ms => new Promise(resolve => setTimeout(resolve, ms));
    const visible = el => {{
        if (!el) return false;
        const rect = el.getBoundingClientRect();
        const style = getComputedStyle(el);
        return rect.width > 0 && rect.height > 0 && style.display !== 'none' && style.visibility !== 'hidden';
    }};
    const editor = [...document.querySelectorAll('[role=""textbox""][contenteditable=""true""], div[data-lexical-editor=""true""]')]
        .find(visible);
    if (!editor) return JSON.stringify({{ success: false, message: '未找到 Messenger 输入框' }});
    editor.focus();
    document.execCommand('selectAll', false, null);
    document.execCommand('delete', false, null);
    if (!document.execCommand('insertText', false, messageText)) {{
        const range = document.createRange();
        range.selectNodeContents(editor);
        range.deleteContents();
        range.insertNode(document.createTextNode(messageText));
    }}
    editor.dispatchEvent(new InputEvent('input', {{ bubbles: true, inputType: 'insertText', data: messageText }}));
    await sleep(350);
    if (!(editor.innerText || editor.textContent || '').trim())
        return JSON.stringify({{ success: false, message: 'Messenger 输入框未接收文本' }});
    const key = {{ key: 'Enter', code: 'Enter', keyCode: 13, which: 13, bubbles: true, cancelable: true }};
    for (const type of ['keydown', 'keypress', 'keyup']) editor.dispatchEvent(new KeyboardEvent(type, key));
    await sleep(1200);
    return JSON.stringify({{ success: true, message: '已执行 Messenger 发送' }});
}})();";
        }

        private static async Task<bool> WaitForBrowserReadyAsync(ChromiumWebBrowser browser, string targetUserId, int timeoutMs)
        {
            var start = DateTime.UtcNow;
            while ((DateTime.UtcNow - start).TotalMilliseconds < timeoutMs)
            {
                if (browser.CanExecuteJavascriptInMainFrame && !browser.IsLoading && IsMessageUrl(browser.Address, targetUserId)) return true;
                await Task.Delay(500);
            }
            return false;
        }

        private void AccountSearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            RefreshAccountGroups();
        }

        private void SetAccountState(string accountId, string state)
        {
            var row = _accounts.FirstOrDefault(x => x.Id.ToString() == accountId || x.FbAccount == accountId);
            if (row != null) row.State = state;
        }

        private void CloseMessageBrowserAccount(string accountId)
        {
            if (!_sessions.Remove(accountId, out var session)) return;
            _ = PersistBadgeAsync(session);
            BrowserHost.Children.Remove(session.Browser);
            var browser = session.Browser;
            var requestContext = session.RequestContext;
            try { browser.GetBrowser()?.CloseBrowser(true); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"关闭CEF浏览器失败: {ex.Message}"); }
            _ = Task.Run(() =>
            {
                try { browser.Dispose(); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"释放CEF浏览器失败: {ex.Message}"); }
                try { requestContext.Dispose(); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"释放账号请求上下文失败: {ex.Message}"); }
            });
            if (_currentAccount?.Id.ToString() == accountId) BrowserEmptyText.Visibility = Visibility.Visible;
        }

        private void CloseAllBrowsers()
        {
            foreach (var session in _sessions.Values.ToList())
            {
                _ = PersistBadgeAsync(session);
                if (session.MonitorId != null)
                    _ = RelayAsync("reportMonitor", new JObject
                    {
                        ["monitorId"] = session.MonitorId,
                        ["success"] = false,
                        ["errorMessage"] = "消息管理窗口已关闭"
                    });
                CloseMessageBrowserAccount(session.AccountId);
            }
            _timer.Stop();
            _startupTimer.Stop();
            _badgeTimer.Stop();
            _selectionTimer.Stop();
        }

        private async Task PollBadgeCountsAsync()
        {
            foreach (var session in _sessions.Values.ToList())
            {
                if (_owner.GetBrowserMatrixWindowForAccount(session.AccountId)?.GetActiveBrowserCount() > 0)
                    continue;
                try { await PollDomAsync(session); }
                catch (Exception ex) { SetAccountState(session.AccountId, $"红圈读取失败：{ex.Message}"); }
            }
        }

        private async Task PersistBadgeAsync(BrowserSession session)
        {
            if (session.BadgePersisted) return;
            var account = _accounts.FirstOrDefault(x => x.Id.ToString() == session.AccountId);
            if (account == null) return;
            session.BadgePersisted = true;
            try
            {
                await RelayAsync("reportUnreadBadges", new JObject
                {
                    ["accountId"] = account.Id.ToString(),
                    ["messengerUnreadCount"] = account.MessengerUnreadCount,
                    ["notificationUnreadCount"] = account.CommentUnreadCount,
                    ["loggedIn"] = session.LastReportedLoggedIn != false
                });
            }
            catch (Exception ex)
            {
                session.BadgePersisted = false;
                System.Diagnostics.Debug.WriteLine($"保存消息未读数失败: {ex.Message}");
            }
        }

        private async Task CheckSelectionTranslationAsync()
        {
            if (_checkingSelection) return;
            if (_currentAccount == null
                || !_sessions.TryGetValue(_currentAccount.Id.ToString(), out var session)
                || session.Browser.Visibility != Visibility.Visible
                || !session.Browser.CanExecuteJavascriptInMainFrame)
            {
                HideSelectionTranslation();
                return;
            }

            _checkingSelection = true;
            try
            {
                var result = await session.Browser.EvaluateScriptAsync(@"(function(){
const selected=(window.getSelection()?.toString()||'').trim();
if(!selected) return {text:''};
const range=window.getSelection()?.rangeCount?window.getSelection().getRangeAt(0):null;
const rect=range?.getBoundingClientRect();
return {text:selected,left:rect?.left||20,top:(rect?.bottom||20)+8};
})();");
                if (!result.Success || result.Result == null) { HideSelectionTranslation(); return; }
                var selection = JObject.Parse(JsonConvert.SerializeObject(result.Result));
                var text = selection.Value<string>("text")?.Trim() ?? "";
                if (text.Length == 0) { HideSelectionTranslation(); return; }
                if (text == _lastSelectedText && SelectionTranslationPopup.Visibility == Visibility.Visible) return;

                _lastSelectedText = text;
                SelectionTranslationText.Text = "";
                SelectionTranslationProgress.Visibility = Visibility.Visible;
                ShowSelectionTranslation(selection.Value<double?>("left") ?? 20, selection.Value<double?>("top") ?? 20);
                if (text.Any(character => character >= '\u3400' && character <= '\u9FFF'))
                {
                    SelectionTranslationProgress.Visibility = Visibility.Collapsed;
                    SelectionTranslationText.Text = text;
                    return;
                }
                var translated = (JObject)await RelayAsync("translate", new JObject
                {
                    ["text"] = text,
                    ["sourceLanguage"] = "auto",
                    ["targetLanguage"] = "zh",
                    ["context"] = "facebook_messenger_selection"
                });
                if (_lastSelectedText == text)
                {
                    SelectionTranslationProgress.Visibility = Visibility.Collapsed;
                    SelectionTranslationText.Text = translated.Value<string>("translation") ?? text;
                }
                else
                {
                    HideSelectionTranslation();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"划词翻译失败: {ex.Message}");
                HideSelectionTranslation();
            }
            finally { _checkingSelection = false; }
        }

        private void ShowSelectionTranslation(double left, double top)
        {
            SelectionTranslationPopup.Margin = new Thickness(Math.Max(8, Math.Min(left, Math.Max(8, BrowserHost.ActualWidth - 380))),
                Math.Max(8, Math.Min(top, Math.Max(8, BrowserHost.ActualHeight - 100))), 0, 0);
            SelectionTranslationPopup.Visibility = Visibility.Visible;
        }

        private void HideSelectionTranslation()
        {
            _lastSelectedText = "";
            SelectionTranslationProgress.Visibility = Visibility.Collapsed;
            SelectionTranslationPopup.Visibility = Visibility.Collapsed;
        }

        private string BuildMessengerCollectScript() => @"(function(){
const root=document.querySelector('[role=main]')||document.body; const path=location.pathname;
const match=path.match(/\/messages\/(?:e2ee\/)?t\/([^/]+)/); const key=match?match[1]:path;
return [...root.querySelectorAll('[role=article]')].map(article=>{const aria=article.getAttribute('aria-label')||'';const text=(article.innerText||'').trim();const values=[...article.querySelectorAll('[dir=auto]')].map(x=>(x.innerText||'').trim()).filter(x=>x&&x.length<2000&&!/^Enter, Message sent/i.test(x));const originalText=[...new Set(values)].sort((a,b)=>a.length-b.length)[0]||text.split('\n')[0]||'';return {externalMessageId:article.getAttribute('data-id')||key+'-'+btoa(unescape(encodeURIComponent(aria+'|'+originalText))).slice(0,32),conversationKey:key,targetUserId:match?match[1]:'',targetName:(aria.match(/\bby (.+?):/i)||[])[1]||'',originalText,messageTime:new Date().toISOString(),incoming:!/\bby You:/i.test(aria+' '+text)};}).filter(x=>x.incoming&&x.originalText);
})();";

        private string BuildNotificationCollectScript() => @"(function(){
const textOf=e=>(e?.getAttribute?.('aria-label')||e?.getAttribute?.('title')||e?.innerText||'').trim();
const countOf=e=>{const label=textOf(e);const named=label.match(/(\d+)\s*(?:unread|未读|new|新消息)/i);if(named)return Number(named[1]);const values=[...e.querySelectorAll?.('span,div,[role=img]')||[]].map(textOf).filter(x=>/^\d{1,4}$/.test(x));return values.length?Number(values[values.length-1]):0;};
const links=[...document.querySelectorAll('a[href], [role=link], [role=button]')];
const find=(terms)=>{const nodes=links.filter(e=>{const value=((e.getAttribute('href')||'')+' '+textOf(e)).toLowerCase();return terms.some(t=>value.includes(t));});return nodes.reduce((max,e)=>Math.max(max,countOf(e)),0);};
return {messengerUnreadCount:find(['/messages','messenger']),commentUnreadCount:find(['/notifications','notification','通知']),page:location.href};
})();";

        private string BuildUnreadBadgeScript() => @"(function(){
const loginPage=!!document.querySelector('input[name=""email""],input[name=""pass""]')||/\/login\.php/i.test(location.pathname);
if(loginPage)return {loggedIn:false,messengerUnreadCount:0,commentUnreadCount:0};
const labels=[...document.querySelectorAll('[aria-label]')].map(e=>e.getAttribute('aria-label')||'');
const count=(names)=>{const value=labels.find(label=>names.some(name=>new RegExp('^'+name+'[,，].*(\\d+)\\s*(?:unread|未读)','i').test(label)));const match=value?.match(/(\d+)\s*(?:unread|未读)/i);return match?Number(match[1]):0;};
const titleMatch=document.title.match(/^\((\d+)\)\s*Messenger/i);
const messenger=count(['Messenger','Messages','消息'])||(titleMatch?Number(titleMatch[1]):0);
return {loggedIn:true,messengerUnreadCount:messenger,commentUnreadCount:count(['Notifications','通知']),page:location.href};
})();";

        private async Task PollDomAsync(BrowserSession session)
        {
            if (!session.Browser.CanExecuteJavascriptInMainFrame) return;
            var result = await session.Browser.EvaluateScriptAsync(BuildUnreadBadgeScript());
            if (!result.Success || result.Result == null) return;
            var counts = JObject.Parse(JsonConvert.SerializeObject(result.Result));
            var account = _accounts.FirstOrDefault(x => x.Id.ToString() == session.AccountId);
            if (account != null)
            {
                var hadUnread = account.TotalUnreadCount > 0;
                account.MessengerUnreadCount = counts.Value<int?>("messengerUnreadCount") ?? 0;
                account.CommentUnreadCount = counts.Value<int?>("commentUnreadCount") ?? 0;
                var loggedIn = counts.Value<bool?>("loggedIn") != false;
                account.State = !loggedIn ? "Cookie失效" : account.OnlineStatus == 1 ? "在线" : "离线";
                account.RefreshUnread();
                session.LastReportedMessengerUnreadCount = account.MessengerUnreadCount;
                session.LastReportedNotificationUnreadCount = account.CommentUnreadCount;
                session.LastReportedLoggedIn = loggedIn;
                if (account.ReceiveEnabled == 1 && hadUnread != (account.TotalUnreadCount > 0)) RefreshAccountGroups();
            }
            // 用户手动点击的定时账号需要保留页面，只有后台领取的定时检查才自动关闭。
            if (session.Mode == "scheduled" && !session.ManualView && !session.Completed)
            {
                await PersistBadgeAsync(session);
                session.Completed = true;
                if (session.MonitorId != null) _ = RelayAsync("reportMonitor", new JObject { ["monitorId"] = session.MonitorId, ["success"] = true });
                Dispatcher.BeginInvoke(new Action(() => CloseMessageBrowserAccount(session.AccountId)));
            }
        }

        private async void TimerTick(object? sender, EventArgs e)
        {
            foreach (var session in _sessions.Values.ToList())
            {
                try { await PollDomAsync(session); }
                catch (Exception ex) { SetAccountState(session.AccountId, $"监控失败：{ex.Message}"); }
            }
        }
    }
}
