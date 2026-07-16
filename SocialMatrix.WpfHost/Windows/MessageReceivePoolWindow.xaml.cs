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
        private readonly Func<JArray, int, Task<JToken>> _addPool;
        private readonly Func<JArray, int, Task<JToken>> _saveSchedule;
        private readonly Func<JArray, Task<JToken>> _removePool;
        private readonly ObservableCollection<PoolRow> _rows = new();

        public MessageReceivePoolWindow(
            Func<Task<JArray>> loadAccounts,
            Func<Task<JArray>> loadMonitors,
            Func<JArray, int, Task<JToken>> addPool,
            Func<JArray, int, Task<JToken>> saveSchedule,
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
            await _addPool(new JArray(rows.Select(x => x.AccountId)), GetInterval());
            foreach (var row in rows) { row.InPool = true; row.IsSelected = false; }
            RefreshLists();
        }

        private int GetInterval() => int.TryParse(IntervalBox.Text, out var value) ? Math.Max(1, value) : 30;

        private async void SaveSchedule_Click(object sender, RoutedEventArgs e)
        {
            var rows = _rows.Where(x => x.InPool).ToList();
            if (rows.Count == 0)
            {
                MessageBox.Show("接收池中还没有账号。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            await _saveSchedule(new JArray(rows.Select(x => x.AccountId)), GetInterval());
            Close();
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
    }
}
