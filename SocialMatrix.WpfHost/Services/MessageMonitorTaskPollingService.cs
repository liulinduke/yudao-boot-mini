using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using SocialMatrix.WpfHost.Windows;

namespace SocialMatrix.WpfHost.Services
{
    /// <summary>
    /// 领取到期的消息接收任务，并交给统一 BrowserMatrixWindow 执行。
    /// 不创建 MessageManagerWindow，也不负责展示消息管理界面。
    /// </summary>
    public sealed class MessageMonitorTaskPollingService : IDisposable
    {
        private const string ClaimUrl = "http://localhost:48080/admin-api/facebook/message/monitor/claim";
        private readonly MainWindow _mainWindow;
        private readonly HttpClient _httpClient = new();
        private readonly HashSet<string> _launching = new(StringComparer.Ordinal);
        private bool _running;
        private bool _started;

        public MessageMonitorTaskPollingService(MainWindow mainWindow)
        {
            _mainWindow = mainWindow;
        }

        public void Start()
        {
            if (_started) return;
            _started = true;
        }

        public void TriggerNow() => _ = PollAsync();

        private async Task PollAsync()
        {
            if (_running) return;
            _running = true;
            try
            {
                var token = TokenManager.Get();
                var slots = Math.Max(0, BrowserMatrixWindow.MaxConcurrentBrowsers - _mainWindow.GetBrowserWindowCount());
                if (string.IsNullOrWhiteSpace(token) || slots <= 0) return;
                using var request = new HttpRequestMessage(HttpMethod.Post, ClaimUrl)
                {
                    Content = new StringContent($"{{\"limit\":{slots},\"accountIds\":[],\"excludeAccounts\":[],\"manual\":false}}", Encoding.UTF8, "application/json")
                };
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                using var response = await _httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode) return;
                var root = JObject.Parse(await response.Content.ReadAsStringAsync());
                foreach (var item in root["data"] as JArray ?? new JArray())
                {
                    var monitorId = item.Value<string>("monitorId") ?? item.Value<long?>("monitorId")?.ToString();
                    var accountId = item.Value<string>("accountId") ?? item.Value<long?>("accountId")?.ToString();
                    if (string.IsNullOrWhiteSpace(monitorId) || string.IsNullOrWhiteSpace(accountId) || !_launching.Add(monitorId)) continue;
                    var detailId = $"message-monitor-{monitorId}";
                    Application.Current.Dispatcher.Invoke(() => _mainWindow.StartMessageMonitorTask(
                        monitorId, accountId, item.Value<string>("cookie") ?? "",
                        item.Value<string>("deviceId") ?? item.Value<long?>("deviceId")?.ToString() ?? "",
                        item.Value<string>("mode") ?? "scheduled", detailId));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"消息监控任务领取失败: {ex.Message}");
            }
            finally { _running = false; }
        }

        public void MarkFinished(string monitorId)
        {
            if (!string.IsNullOrWhiteSpace(monitorId)) _launching.Remove(monitorId);
        }

        public async Task ReportAsync(string monitorId, bool success, string? errorMessage = null,
            string? accountId = null, int messengerUnreadCount = 0, int notificationUnreadCount = 0)
        {
            if (string.IsNullOrWhiteSpace(monitorId)) return;
            try
            {
                var token = TokenManager.Get();
                if (string.IsNullOrWhiteSpace(token)) return;
                var query = $"monitorId={Uri.EscapeDataString(monitorId)}&success={success.ToString().ToLowerInvariant()}";
                if (!string.IsNullOrWhiteSpace(errorMessage)) query += $"&errorMessage={Uri.EscapeDataString(errorMessage)}";
                using var request = new HttpRequestMessage(HttpMethod.Post,
                    $"http://localhost:48080/admin-api/facebook/message/monitor/report?{query}");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                await _httpClient.SendAsync(request);
                if (success && long.TryParse(accountId, out var parsedAccountId))
                {
                    using var badgeRequest = new HttpRequestMessage(HttpMethod.Post,
                        "http://localhost:48080/admin-api/facebook/message/monitor/badge-report")
                    {
                        Content = new StringContent($"{{\"accountId\":{parsedAccountId},\"messengerUnreadCount\":{messengerUnreadCount},\"notificationUnreadCount\":{notificationUnreadCount},\"loggedIn\":true}}", Encoding.UTF8, "application/json")
                    };
                    badgeRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                    await _httpClient.SendAsync(badgeRequest);
                }
            }
            finally { MarkFinished(monitorId); }
        }

        public void Dispose()
        {
            _httpClient.Dispose();
        }
    }
}
