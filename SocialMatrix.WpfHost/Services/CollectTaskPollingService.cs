using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using SocialMatrix.WpfHost.Windows;

namespace SocialMatrix.WpfHost.Services
{
    /// <summary>
    /// WPF 常驻采集任务轮询器。后端定时任务只创建待执行明细，WPF 负责消费并启动指纹浏览器。
    /// </summary>
    public sealed class CollectTaskPollingService : IDisposable
    {
        private const string ClaimPendingUrl = "http://localhost:48080/admin-api/facebook/fb-collect-detail/claim-pending";
        private readonly MainWindow _mainWindow;
        private readonly HttpClient _httpClient = new();
        private readonly HashSet<string> _launchingDetails = new();
        private readonly Timer _timer;
        private bool _polling;

        public CollectTaskPollingService(MainWindow mainWindow)
        {
            _mainWindow = mainWindow;
            _timer = new Timer(async _ => await PollAsync(), null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10));
        }

        private async Task PollAsync()
        {
            if (_polling)
            {
                return;
            }
            _polling = true;
            try
            {
                var token = TokenManager.Get();
                if (string.IsNullOrWhiteSpace(token))
                {
                    return;
                }

                int availableSlots = Math.Max(BrowserMatrixWindow.MaxConcurrentBrowsers - _mainWindow.GetBrowserWindowCount(), 0);
                if (availableSlots <= 0)
                {
                    return;
                }

                using var request = new HttpRequestMessage(HttpMethod.Get, $"{ClaimPendingUrl}?limit={availableSlots}");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                var response = await _httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                {
                    System.Diagnostics.Debug.WriteLine($"⚠️ 轮询采集任务失败: {(int)response.StatusCode} {response.ReasonPhrase}");
                    return;
                }

                var body = await response.Content.ReadAsStringAsync();
                var details = ParseDetails(body);
                foreach (var detail in details)
                {
                    if (string.IsNullOrWhiteSpace(detail.DetailId) || !_launchingDetails.Add(detail.DetailId))
                    {
                        continue;
                    }
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        try
                        {
                            _mainWindow.CreateBrowserForAccount(
                                detail.DetailId,
                                detail.FbAccount ?? string.Empty,
                                string.IsNullOrWhiteSpace(detail.Cookie) ? null : detail.Cookie,
                                detail.SearchUrl,
                                detail.ExpectedCount,
                                detail.TaskType);
                            System.Diagnostics.Debug.WriteLine($"✅ WPF轮询启动采集: detailId={detail.DetailId}, account={detail.FbAccount}, taskType={detail.TaskType}");
                        }
                        catch (Exception ex)
                        {
                            _launchingDetails.Remove(detail.DetailId);
                            System.Diagnostics.Debug.WriteLine($"❌ WPF轮询启动采集失败: detailId={detail.DetailId}, error={ex.Message}");
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ 轮询采集任务异常: {ex.Message}");
            }
            finally
            {
                _polling = false;
            }
        }

        public void MarkDetailFinished(string detailId)
        {
            if (!string.IsNullOrWhiteSpace(detailId))
            {
                _launchingDetails.Remove(detailId);
            }
        }

        private static List<PendingCollectDetail> ParseDetails(string body)
        {
            var result = new List<PendingCollectDetail>();
            if (string.IsNullOrWhiteSpace(body))
            {
                return result;
            }
            var root = JObject.Parse(body);
            var data = root["data"] as JArray;
            if (data == null)
            {
                return result;
            }
            foreach (var item in data)
            {
                var detail = item.ToObject<PendingCollectDetail>();
                if (detail != null)
                {
                    result.Add(detail);
                }
            }
            return result;
        }

        public void Dispose()
        {
            _timer.Dispose();
            _httpClient.Dispose();
        }

        private sealed class PendingCollectDetail
        {
            [JsonProperty("detailId")]
            public string? DetailId { get; set; }

            [JsonProperty("fbAccount")]
            public string? FbAccount { get; set; }

            [JsonProperty("cookie")]
            public string? Cookie { get; set; }

            [JsonProperty("searchUrl")]
            public string? SearchUrl { get; set; }

            [JsonProperty("expectedCount")]
            public int ExpectedCount { get; set; }

            [JsonProperty("taskType")]
            public int TaskType { get; set; } = 1;
        }
    }
}
