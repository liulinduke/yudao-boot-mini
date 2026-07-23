using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Newtonsoft.Json;
using SocialMatrix.WpfHost.Windows;

namespace SocialMatrix.WpfHost
{
    public partial class MainWindow
    {
        private readonly Queue<BrowserMatrixWindow.AccountLoginRequest> _accountLoginWindowQueue = new();
        private record AccountLoginWindowResult(
            object? accountDbId,
            object? accountId,
            string status,
            object? loginMode,
            object? errorReason,
            object? cookieSaved,
            object? windowClosed);

        private readonly List<AccountLoginWindowResult> _accountLoginWindowResults = new();
        private int _accountLoginWindowRunningCount = 0;
        private int _accountLoginWindowTotalCount = 0;
        private bool _accountLoginWindowAutoClose = false;
        private bool _accountLoginWindowBatchActive = false;
        private readonly object _accountLoginWindowLock = new();

        private void RegisterAccountLoginWindowEvents(BrowserMatrixWindow browserMatrixWindow)
        {
            browserMatrixWindow.OnAccountLoginProgress -= BrowserMatrixWindow_OnAccountLoginProgress;
            browserMatrixWindow.OnAccountLoginProgress += BrowserMatrixWindow_OnAccountLoginProgress;
        }

        private void BrowserMatrixWindow_OnAccountLoginProgress(string jsonData)
        {
            Dispatcher.Invoke(() => ReturnAccountLoginProgressToVue(jsonData));
        }

        private void BrowserMatrixWindow_OnAccountLoginBatchComplete(string jsonData)
        {
            Dispatcher.Invoke(() => ReturnAccountLoginCompleteToVue(jsonData));
        }

        private void ReturnAccountLoginProgressToVue(string jsonData)
        {
            try
            {
                if (VueWebView.CoreWebView2 == null)
                {
                    return;
                }

                var script = $@"
                    setTimeout(() => {{
                        window.dispatchEvent(new CustomEvent('fb:account-login:progress', {{
                            detail: {jsonData}
                        }}));
                    }}, 50);
                ";
                VueWebView.CoreWebView2.ExecuteScriptAsync(script);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"账号登录进度回传失败: {ex.Message}");
            }
        }

        private void ReturnAccountLoginCompleteToVue(string jsonData)
        {
            try
            {
                if (VueWebView.CoreWebView2 == null)
                {
                    return;
                }

                var script = $@"
                    setTimeout(() => {{
                        window.dispatchEvent(new CustomEvent('fb:account-login:complete', {{
                            detail: {jsonData}
                        }}));
                    }}, 50);
                ";
                VueWebView.CoreWebView2.ExecuteScriptAsync(script);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"账号登录完成回传失败: {ex.Message}");
            }
        }

        public void StartAccountLoginBatch(string accountsJson)
        {
            var accounts = JsonConvert.DeserializeObject<List<BrowserMatrixWindow.AccountLoginRequest>>(accountsJson);
            if (accounts == null || accounts.Count == 0)
            {
                return;
            }

            lock (_accountLoginWindowLock)
            {
                _accountLoginWindowQueue.Clear();
                _accountLoginWindowResults.Clear();
                foreach (var account in accounts)
                {
                    _accountLoginWindowQueue.Enqueue(account);
                }
                _accountLoginWindowRunningCount = 0;
                _accountLoginWindowTotalCount = accounts.Count;
                _accountLoginWindowAutoClose = accounts.Count > BrowserMatrixWindow.MaxConcurrentBrowsers;
                _accountLoginWindowBatchActive = true;
            }

            _ = PumpAccountLoginWindowQueueAsync();
            UpdateStatus($"Submitted {accounts.Count} accounts for per-window batch login");
        }

        private async System.Threading.Tasks.Task PumpAccountLoginWindowQueueAsync()
        {
            while (true)
            {
                BrowserMatrixWindow.AccountLoginRequest? nextAccount = null;
                bool shouldFinish = false;

                lock (_accountLoginWindowLock)
                {
                    if (_accountLoginWindowQueue.Count > 0 &&
                        _accountLoginWindowRunningCount < BrowserMatrixWindow.MaxConcurrentBrowsers)
                    {
                        nextAccount = _accountLoginWindowQueue.Dequeue();
                        _accountLoginWindowRunningCount++;
                    }
                    else if (_accountLoginWindowQueue.Count == 0 &&
                             _accountLoginWindowRunningCount == 0 &&
                             _accountLoginWindowBatchActive)
                    {
                        _accountLoginWindowBatchActive = false;
                        shouldFinish = true;
                    }
                }

                if (shouldFinish)
                {
                    var payload = JsonConvert.SerializeObject(new
                    {
                        summary = new
                        {
                            total = _accountLoginWindowTotalCount,
                            success = _accountLoginWindowResults.Count(x => x.status == "success"),
                            failed = _accountLoginWindowResults.Count(x => x.status == "failed"),
                            skipped = _accountLoginWindowResults.Count(x => x.status == "skipped")
                        },
                        results = _accountLoginWindowResults.Select(result => new
                        {
                            accountDbId = result.accountDbId?.ToString(),
                            accountId = result.accountId?.ToString(),
                            status = result.status,
                            loginMode = result.loginMode?.ToString(),
                            errorReason = result.errorReason?.ToString(),
                            cookieSaved = result.cookieSaved,
                            windowClosed = result.windowClosed
                        })
                    });
                    Dispatcher.Invoke(() => ReturnAccountLoginCompleteToVue(payload));
                    break;
                }

                if (nextAccount == null)
                {
                    await System.Threading.Tasks.Task.Delay(300);
                    continue;
                }

                Dispatcher.Invoke(() => StartSingleAccountLoginWindow(nextAccount));
            }
        }

        private void StartSingleAccountLoginWindow(BrowserMatrixWindow.AccountLoginRequest account)
        {
            var browserMatrixWindow = GetOrCreateBrowserMatrixWindow(account.AccountId);
            RegisterAccountLoginWindowEvents(browserMatrixWindow);

            void OnSingleComplete(string jsonData)
            {
                browserMatrixWindow.OnAccountLoginBatchComplete -= OnSingleComplete;

                try
                {
                    var payload = Newtonsoft.Json.Linq.JObject.Parse(jsonData);
                    var results = payload["results"] as Newtonsoft.Json.Linq.JArray;
                    if (results != null)
                    {
                        foreach (var item in results)
                        {
                            var status = item["status"]?.ToString() ?? item["Status"]?.ToString() ?? "";
                            _accountLoginWindowResults.Add(new AccountLoginWindowResult(
                                accountDbId: item["accountDbId"] ?? item["AccountDbId"],
                                accountId: item["accountId"] ?? item["AccountId"],
                                status,
                                loginMode: item["loginMode"] ?? item["LoginMode"],
                                errorReason: item["errorReason"] ?? item["ErrorReason"],
                                cookieSaved: item["cookieSaved"] ?? item["CookieSaved"],
                                windowClosed: item["windowClosed"] ?? item["WindowClosed"]));
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"单账号登录结果解析失败: {ex.Message}");
                }

                lock (_accountLoginWindowLock)
                {
                    _accountLoginWindowRunningCount = Math.Max(0, _accountLoginWindowRunningCount - 1);
                }
            }

            browserMatrixWindow.OnAccountLoginBatchComplete += OnSingleComplete;
            browserMatrixWindow.StartAccountLoginBatch(new List<BrowserMatrixWindow.AccountLoginRequest> { account }, _accountLoginWindowAutoClose);
        }
    }
}
