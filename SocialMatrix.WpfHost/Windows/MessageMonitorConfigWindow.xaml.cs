using Newtonsoft.Json.Linq;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SocialMatrix.WpfHost.Windows
{
    public partial class MessageMonitorConfigWindow : Window
    {
        private sealed class ConfigRow : INotifyPropertyChanged
        {
            public long Id { get; set; }
            public string DisplayName { get; set; } = "";
            private string _mode = "disabled";
            public string Mode
            {
                get => _mode;
                set
                {
                    if (_mode == value) return;
                    _mode = value;
                    OnChanged();
                    OnChanged(nameof(ModeLabel));
                }
            }
            public int Interval { get; set; } = 30; // 兼容历史配置，新的定时接收使用 ScheduleTimes
            public ObservableCollection<string> ScheduleTimeList { get; } = new();
            public string ScheduleTimes
            {
                get => string.Join(",", ScheduleTimeList);
                set
                {
                    ScheduleTimeList.Clear();
                    foreach (var item in (value ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries))
                    {
                        if (TryNormalizeTime(item, out var normalized) && !ScheduleTimeList.Contains(normalized))
                            ScheduleTimeList.Add(normalized);
                    }
                }
            }
            public string ModeLabel => Mode switch { "scheduled" => "定时接收", _ => "关闭" };
            public event PropertyChangedEventHandler? PropertyChanged;
            private void OnChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        private static bool TryNormalizeTime(string value, out string normalized)
        {
            normalized = "";
            if (!TimeSpan.TryParseExact(value.Trim(), @"hh\:mm", null, out var time)
                || time < TimeSpan.Zero || time >= TimeSpan.FromDays(1)) return false;
            normalized = time.ToString(@"hh\:mm");
            return true;
        }

        private readonly Func<Task<JArray>> _loadAccounts;
        private readonly Func<Task<JArray>> _loadMonitors;
        private readonly Func<JArray, Task<JToken>> _saveMonitors;
        private readonly Func<Task<JArray>> _loadConfigs;
        private readonly Func<JArray, Task<JToken>> _saveConfigs;
        private readonly ObservableCollection<ConfigRow> _rows = new();

        public MessageMonitorConfigWindow(
            Func<Task<JArray>> loadAccounts,
            Func<Task<JArray>> loadMonitors,
            Func<JArray, Task<JToken>> saveMonitors,
            Func<Task<JArray>> loadConfigs,
            Func<JArray, Task<JToken>> saveConfigs)
        {
            _loadAccounts = loadAccounts;
            _loadMonitors = loadMonitors;
            _saveMonitors = saveMonitors;
            _loadConfigs = loadConfigs;
            _saveConfigs = saveConfigs;
            InitializeComponent();
            AccountGrid.ItemsSource = _rows;
            Loaded += async (_, _) =>
            {
                FitIntoWorkArea();
                await LoadAsync();
            };
        }

        private void FitIntoWorkArea()
        {
            var area = SystemParameters.WorkArea;
            Width = Math.Min(Width, Math.Max(320, area.Width - 24));
            Height = Math.Min(Height, Math.Max(240, area.Height - 24));
            Left = area.Left + Math.Max(12, (area.Width - Width) / 2);
            Top = area.Top + Math.Max(12, (area.Height - Height) / 2);
        }

        private async Task LoadAsync()
        {
            var accounts = await _loadAccounts();
            var monitors = await _loadMonitors();
            foreach (var account in accounts.OfType<JObject>())
            {
                var id = account.Value<long>("id");
                var monitor = monitors.OfType<JObject>().FirstOrDefault(x => x.Value<long>("accountId") == id);
                var mode = monitor?.Value<string>("mode") == "disabled" ? "disabled" : "scheduled";
                _rows.Add(new ConfigRow
                {
                    Id = id,
                    DisplayName = account.Value<string>("fbAccount") ?? id.ToString(),
                    Mode = mode,
                    Interval = Math.Max(1, monitor?.Value<int?>("checkIntervalMinutes") ?? 30),
                    ScheduleTimes = string.IsNullOrWhiteSpace(monitor?.Value<string>("scheduleTimes"))
                        ? "06:00"
                        : monitor!.Value<string>("scheduleTimes")!
                });
            }
            await RefreshCapacityAsync();
        }

        private async Task RefreshCapacityAsync()
        {
            var configs = await _loadConfigs();
            var max = configs.OfType<JObject>().FirstOrDefault(x => x.Value<string>("configKey") == "browser_max_concurrent")?.Value<int?>("configValue") ?? 19;
            var reserved = configs.OfType<JObject>().FirstOrDefault(x => x.Value<string>("configKey") == "message_realtime_reserved_slots")?.Value<int?>("configValue") ?? 5;
            var available = Math.Max(0, max - reserved);
            var selected = _rows.Count(x => x.Mode == "scheduled");
            CapacityText.Text = $"最大窗口 {max}，预留业务窗口 {reserved}，定时接收账号 {selected}，可用监控名额 {available}";
        }

        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                AccountGrid.CommitEdit(System.Windows.Controls.DataGridEditingUnit.Cell, true);
                AccountGrid.CommitEdit(System.Windows.Controls.DataGridEditingUnit.Row, true);
                var configs = await _loadConfigs();
                var max = configs.OfType<JObject>().FirstOrDefault(x => x.Value<string>("configKey") == "browser_max_concurrent")?.Value<int?>("configValue") ?? 19;
                var reserved = configs.OfType<JObject>().FirstOrDefault(x => x.Value<string>("configKey") == "message_realtime_reserved_slots")?.Value<int?>("configValue") ?? 5;
                var scheduled = _rows.Count(x => x.Mode == "scheduled");
                if (scheduled > Math.Max(0, max - reserved))
                {
                    MessageBox.Show("定时接收账号超过可用浏览器名额，请减少账号或调整预留名额。", "保存失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                foreach (var row in _rows.Where(x => x.Mode == "scheduled"))
                {
                    if (row.ScheduleTimeList.Count == 0)
                    {
                        MessageBox.Show($"请为账号 {row.DisplayName} 设置至少一个定时接收时间。", "保存失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                }
                var items = new JArray(_rows.Select(x => new JObject
                {
                    ["accountId"] = x.Id.ToString(), ["mode"] = x.Mode,
                    ["checkIntervalMinutes"] = Math.Max(1, x.Interval),
                    ["scheduleTimes"] = x.ScheduleTimes?.Trim(), ["status"] = 1
                }));
                await _saveMonitors(items);
                MessageBox.Show("消息接收账号配置已保存。", "保存成功", MessageBoxButton.OK, MessageBoxImage.Information);
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"消息接收账号配置保存失败：{ex.Message}", "保存失败", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AddTime_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement element || element.DataContext is not ConfigRow row) return;
            var dialog = new TimeInputWindow { Owner = this };
            if (dialog.ShowDialog() != true || !TryNormalizeTime(dialog.TimeValue, out var time))
            {
                if (dialog.DialogResult == true)
                    MessageBox.Show("请输入有效时间，例如 06:00。", "时间格式不正确", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (!row.ScheduleTimeList.Contains(time))
            {
                row.ScheduleTimeList.Add(time);
                SortTimes(row.ScheduleTimeList);
            }
        }

        private void RemoveTime_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement element || element.DataContext is not string time) return;
            // The button is inside the row's ItemsControl; walk up to the row data context.
            var row = FindDataContext<ConfigRow>(element);
            row?.ScheduleTimeList.Remove(time);
        }

        private static T? FindDataContext<T>(DependencyObject source) where T : class
        {
            var current = source;
            while (current != null)
            {
                if (current is FrameworkElement element && element.DataContext is T value) return value;
                current = VisualTreeHelper.GetParent(current);
            }
            return null;
        }

        private static void SortTimes(ObservableCollection<string> times)
        {
            var sorted = times.OrderBy(x => x).ToList();
            times.Clear();
            foreach (var item in sorted) times.Add(item);
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) { DialogResult = false; Close(); }

        private sealed class TimeInputWindow : Window
        {
            private readonly TextBox _input = new() { Width = 150, Height = 30, Margin = new Thickness(0, 8, 0, 14) };
            public string TimeValue => _input.Text;

            public TimeInputWindow()
            {
                Title = "添加接收时间";
                Width = 280;
                Height = 170;
                ResizeMode = ResizeMode.NoResize;
                WindowStartupLocation = WindowStartupLocation.CenterOwner;
                var root = new StackPanel { Margin = new Thickness(18) };
                root.Children.Add(new TextBlock { Text = "每天几点接收消息？", FontSize = 14, FontWeight = FontWeights.SemiBold });
                _input.Text = "06:00";
                _input.SelectAll();
                root.Children.Add(_input);
                var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
                var cancel = new Button { Content = "取消", Width = 70, Margin = new Thickness(0, 0, 8, 0) };
                cancel.Click += (_, _) => { DialogResult = false; };
                var confirm = new Button { Content = "添加", Width = 70, IsDefault = true };
                confirm.Click += (_, _) => { DialogResult = true; };
                buttons.Children.Add(cancel);
                buttons.Children.Add(confirm);
                root.Children.Add(buttons);
                Content = root;
                Loaded += (_, _) => _input.Focus();
            }
        }
    }
}
