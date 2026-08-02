using Newtonsoft.Json.Linq;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace SocialMatrix.WpfHost.Windows
{
    public partial class MessageReceivePoolWindow : Window
    {
        private sealed class PoolRow : INotifyPropertyChanged
        {
            public string AccountId { get; set; } = "";
            public string DisplayName { get; set; } = "";
            public bool InPool { get; set; }
            private bool _selected;
            public bool IsSelected { get => _selected; set { _selected = value; OnChanged(); } }
            public event PropertyChangedEventHandler? PropertyChanged;
            private void OnChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        private readonly Func<Task<JArray>> _loadAccounts;
        private readonly Func<Task<JArray>> _loadMonitors;
        private readonly Func<JArray, string, Task<JToken>> _addPool;
        private readonly Func<JArray, string, Task<JToken>> _saveSchedule;
        private readonly Func<JArray, Task<JToken>> _removePool;
        private readonly ObservableCollection<PoolRow> _rows = new();
        private readonly ObservableCollection<string> _scheduleTimes = new() { "06:00" };

        public MessageReceivePoolWindow(
            Func<Task<JArray>> loadAccounts,
            Func<Task<JArray>> loadMonitors,
            Func<JArray, string, Task<JToken>> addPool,
            Func<JArray, string, Task<JToken>> saveSchedule,
            Func<JArray, Task<JToken>> removePool)
        {
            _loadAccounts = loadAccounts;
            _loadMonitors = loadMonitors;
            _addPool = addPool;
            _saveSchedule = saveSchedule;
            _removePool = removePool;
            InitializeComponent();
            AvailableList.ItemTemplate = BuildTemplate();
            PoolList.ItemTemplate = BuildTemplate();
            ScheduleTimesPanel.ItemsSource = _scheduleTimes;
            Loaded += async (_, _) => await LoadAsync();
        }

        private DataTemplate BuildTemplate()
        {
            var template = new DataTemplate();
            var panel = new FrameworkElementFactory(typeof(StackPanel));
            panel.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
            panel.SetValue(FrameworkElement.MarginProperty, new Thickness(2, 7, 2, 7));
            var check = new FrameworkElementFactory(typeof(CheckBox));
            check.SetBinding(CheckBox.IsCheckedProperty, new System.Windows.Data.Binding(nameof(PoolRow.IsSelected)) { Mode = System.Windows.Data.BindingMode.TwoWay });
            check.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 0, 8, 0));
            panel.AppendChild(check);
            var name = new FrameworkElementFactory(typeof(TextBlock));
            name.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding(nameof(PoolRow.DisplayName)));
            panel.AppendChild(name);
            template.VisualTree = panel;
            return template;
        }

        private async Task LoadAsync()
        {
            var accounts = await _loadAccounts();
            var monitors = await _loadMonitors();
            var monitorMap = monitors.OfType<JObject>().ToDictionary(x => x.Value<string>("accountId") ?? "", x => x);
            _rows.Clear();
            foreach (var token in accounts.OfType<JObject>())
            {
                var id = token.Value<string>("id") ?? "";
                var monitor = monitorMap.TryGetValue(id, out var found) ? found : null;
                _rows.Add(new PoolRow
                {
                    AccountId = id,
                    DisplayName = token.Value<string>("fbAccount") ?? id,
                    InPool = monitor?.Value<int?>("receiveEnabled") == 1
                });
            }
            var existingTimes = monitors.OfType<JObject>()
                .Select(x => x.Value<string>("scheduleTimes"))
                .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));
            if (!string.IsNullOrWhiteSpace(existingTimes))
            {
                _scheduleTimes.Clear();
                foreach (var time in existingTimes.Split(',', StringSplitOptions.RemoveEmptyEntries)
                             .Select(x => x.Trim()).Where(IsValidTime).Distinct().OrderBy(x => x))
                    _scheduleTimes.Add(time);
            }
            RefreshLists();
        }

        private void RefreshLists()
        {
            var keyword = SearchBox?.Text?.Trim() ?? "";
            AvailableList.ItemsSource = _rows.Where(x => !x.InPool && (keyword.Length == 0 || x.DisplayName.Contains(keyword, StringComparison.OrdinalIgnoreCase))).ToList();
            PoolList.ItemsSource = _rows.Where(x => x.InPool && (keyword.Length == 0 || x.DisplayName.Contains(keyword, StringComparison.OrdinalIgnoreCase))).ToList();
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => RefreshLists();

        private async void Add_Click(object sender, RoutedEventArgs e)
        {
            var rows = _rows.Where(x => x.IsSelected && !x.InPool).ToList();
            if (rows.Count == 0) return;
            await _addPool(new JArray(rows.Select(x => x.AccountId)), string.Join(",", _scheduleTimes));
            foreach (var row in rows) { row.InPool = true; row.IsSelected = false; }
            RefreshLists();
        }

        private async void SaveSchedule_Click(object sender, RoutedEventArgs e)
        {
            var rows = _rows.Where(x => x.InPool).ToList();
            if (rows.Count == 0)
            {
                MessageBox.Show("接收池中还没有账号。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (_scheduleTimes.Count == 0)
            {
                MessageBox.Show("请至少添加一个接收时间。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            await _saveSchedule(new JArray(rows.Select(x => x.AccountId)), string.Join(",", _scheduleTimes));
            Close();
        }

        private static bool IsValidTime(string value) =>
            TimeSpan.TryParseExact(value, @"hh\:mm", null, out var time)
            && time >= TimeSpan.Zero && time < TimeSpan.FromDays(1);

        private void AddTime_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new TimeInputWindow { Owner = this };
            if (dialog.ShowDialog() != true) return;
            var time = dialog.TimeValue.Trim();
            if (!IsValidTime(time))
            {
                MessageBox.Show("请输入有效时间，例如 06:00。", "时间格式不正确", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            time = TimeSpan.ParseExact(time, @"hh\:mm", null).ToString(@"hh\:mm");
            if (!_scheduleTimes.Contains(time))
            {
                _scheduleTimes.Add(time);
                var sorted = _scheduleTimes.OrderBy(x => x).ToList();
                _scheduleTimes.Clear();
                foreach (var item in sorted) _scheduleTimes.Add(item);
            }
        }

        private void RemoveTime_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string time) _scheduleTimes.Remove(time);
        }

        private async void Remove_Click(object sender, RoutedEventArgs e)
        {
            var rows = _rows.Where(x => x.IsSelected && x.InPool).ToList();
            if (rows.Count == 0) return;
            await _removePool(new JArray(rows.Select(x => x.AccountId)));
            foreach (var row in rows) { row.InPool = false; row.IsSelected = false; }
            RefreshLists();
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();

        private sealed class TimeInputWindow : Window
        {
            private readonly TextBox _input = new() { Width = 150, Height = 30, Margin = new Thickness(0, 8, 0, 14) };
            public string TimeValue => _input.Text;

            public TimeInputWindow()
            {
                Title = "添加接收时间"; Width = 280; Height = 170; ResizeMode = ResizeMode.NoResize;
                WindowStartupLocation = WindowStartupLocation.CenterOwner;
                var root = new StackPanel { Margin = new Thickness(18) };
                root.Children.Add(new TextBlock { Text = "每天几点接收消息？", FontSize = 14, FontWeight = FontWeights.SemiBold });
                _input.Text = "06:00"; _input.SelectAll(); root.Children.Add(_input);
                var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
                var cancel = new Button { Content = "取消", Width = 70, Margin = new Thickness(0, 0, 8, 0) };
                cancel.Click += (_, _) => DialogResult = false;
                var confirm = new Button { Content = "添加", Width = 70, IsDefault = true };
                confirm.Click += (_, _) => DialogResult = true;
                buttons.Children.Add(cancel); buttons.Children.Add(confirm); root.Children.Add(buttons); Content = root;
                Loaded += (_, _) => _input.Focus();
            }
        }
    }
}
