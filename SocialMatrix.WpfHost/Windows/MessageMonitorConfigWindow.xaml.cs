using Newtonsoft.Json.Linq;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;

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
            public int Interval { get; set; } = 30;
            public string ModeLabel => Mode switch { "realtime" => "实时在线", "scheduled" => "定时检查", _ => "不接收" };
            public event PropertyChangedEventHandler? PropertyChanged;
            private void OnChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
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
                var mode = monitor?.Value<string>("mode") ?? "disabled";
                _rows.Add(new ConfigRow
                {
                    Id = id,
                    DisplayName = account.Value<string>("fbAccount") ?? id.ToString(),
                    Mode = mode,
                    Interval = Math.Max(1, monitor?.Value<int?>("checkIntervalMinutes") ?? 30)
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
            var selected = _rows.Count(x => x.Mode == "realtime");
            CapacityText.Text = $"最大窗口 {max}，预留业务窗口 {reserved}，实时可用名额 {available}，当前选择 {selected}";
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
                var realtime = _rows.Count(x => x.Mode == "realtime");
                if (realtime > Math.Max(0, max - reserved))
                {
                    MessageBox.Show("实时在线账号超过可用浏览器名额，请减少实时账号或调整预留名额。", "保存失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                var items = new JArray(_rows.Select(x => new JObject
                {
                    ["accountId"] = x.Id, ["mode"] = x.Mode,
                    ["checkIntervalMinutes"] = Math.Max(1, x.Interval), ["status"] = 1
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

        private void Cancel_Click(object sender, RoutedEventArgs e) { DialogResult = false; Close(); }
    }
}
