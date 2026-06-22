using System;
using System.Collections.Generic;
using System.Windows;
using Newtonsoft.Json;
using SocialMatrix.WpfHost.Windows;

namespace SocialMatrix.WpfHost
{
    public partial class MainWindow
    {
        private void EnsureAccountLoginWindowEvents()
        {
            if (_browserMatrixWindow == null)
            {
                return;
            }

            _browserMatrixWindow.OnAccountLoginProgress -= BrowserMatrixWindow_OnAccountLoginProgress;
            _browserMatrixWindow.OnAccountLoginBatchComplete -= BrowserMatrixWindow_OnAccountLoginBatchComplete;
            _browserMatrixWindow.OnAccountLoginProgress += BrowserMatrixWindow_OnAccountLoginProgress;
            _browserMatrixWindow.OnAccountLoginBatchComplete += BrowserMatrixWindow_OnAccountLoginBatchComplete;
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
            if (_browserMatrixWindow == null || !_browserMatrixWindow.IsVisible)
            {
                _browserMatrixWindow = new BrowserMatrixWindow();
                EnsureAccountLoginWindowEvents();
                _browserMatrixWindow.Show();
            }
            else
            {
                EnsureAccountLoginWindowEvents();
            }

            var accounts = JsonConvert.DeserializeObject<List<BrowserMatrixWindow.AccountLoginRequest>>(accountsJson);
            if (accounts == null || accounts.Count == 0)
            {
                return;
            }

            _browserMatrixWindow.StartAccountLoginBatch(accounts);
            _browserMatrixWindow.Activate();
            UpdateStatus($"Submitted {accounts.Count} accounts for batch login");
        }
    }
}
