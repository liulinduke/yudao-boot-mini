using System;
using System.Threading.Tasks;
using CefSharp;
using CefSharp.Wpf;
using SocialMatrix.WpfHost.Services;

namespace SocialMatrix.WpfHost.Windows
{
    /// <summary>
    /// BrowserMatrixWindow 的私信发送功能扩展
    /// </summary>
    public partial class BrowserMatrixWindow
    {
        private string GenerateDmSendScript(string fbUserId, string messageText)
        {
            var builder = new DmScriptBuilder(fbUserId, messageText);
            return builder.Build();
        }

        private async Task<JavascriptResponse> EvaluateScriptWithTimeout(
            ChromiumWebBrowser browser, string script, int timeoutMs)
        {
            var evalTask = browser.EvaluateScriptAsync(script);
            var completed = await Task.WhenAny(evalTask, Task.Delay(timeoutMs));
            if (completed != evalTask)
            {
                throw new TimeoutException($"JS 执行超时 ({timeoutMs}ms)");
            }
            return await evalTask;
        }

        private async Task<bool> WaitForDmEditor(ChromiumWebBrowser browser, int timeoutMs = 20000)
        {
            var start = DateTime.Now;
            while ((DateTime.Now - start).TotalMilliseconds < timeoutMs)
            {
                var result = await browser.EvaluateScriptAsync(DmScriptBuilder.BuildEditorReadyCheckScript());
                if (result.Success && result.Result is bool ready && ready)
                {
                    return true;
                }

                await browser.EvaluateScriptAsync(DmScriptBuilder.BuildClickContinueScript());
                await Task.Delay(800);
            }
            return false;
        }

        public async Task SendDirectMessage(string accountId, string fbUserId, string messageText)
        {
            if (!_browsers.TryGetValue(accountId, out var browser))
            {
                System.Diagnostics.Debug.WriteLine($"❌ 账号 {accountId} 的浏览器不存在");
                OnCollectionError?.Invoke(accountId, "浏览器不存在");
                return;
            }

            try
            {
                System.Diagnostics.Debug.WriteLine($"📨 开始发送私信: 账号={accountId}, 目标={fbUserId}");

                // 1. 确保已进入私信页面（兜底：防止运营任务未导航成功）
                var dmUrl = $"https://www.facebook.com/messages/t/{fbUserId}/";
                string currentUrl = "";
                RunOnBrowserUiThread(browser, () => currentUrl = browser.Address ?? "");
                System.Diagnostics.Debug.WriteLine($"🔍 私信前当前 URL: {currentUrl}");

                if (!IsOnTargetUrl(currentUrl, dmUrl))
                {
                    System.Diagnostics.Debug.WriteLine($"🔗 不在私信页，导航到: {dmUrl}");
                    await NavigateBrowserToUrlAsync(browser, accountId, dmUrl);
                    RunOnBrowserUiThread(browser, () => currentUrl = browser.Address ?? "");
                    System.Diagnostics.Debug.WriteLine($"🔍 导航后 URL: {currentUrl}");
                }
                else
                {
                    await WaitForPageLoad(browser, timeoutMs: 15000);
                }
                System.Diagnostics.Debug.WriteLine($"✅ 私信页面加载完成");

                // 2. 点击 Continue 并等待编辑器出现（分步执行，避免页面跳转导致 JS 上下文销毁）
                System.Diagnostics.Debug.WriteLine($"📌 处理 Continue 按钮...");
                for (int i = 0; i < 10; i++)
                {
                    var clickResult = await browser.EvaluateScriptAsync(DmScriptBuilder.BuildClickContinueScript());
                    System.Diagnostics.Debug.WriteLine($"📌 Continue 点击结果[{i}]: {clickResult.Result}");

                    if (await WaitForDmEditor(browser, timeoutMs: 3000))
                    {
                        System.Diagnostics.Debug.WriteLine($"✅ 私信编辑器已就绪");
                        break;
                    }

                    if (i == 9)
                    {
                        System.Diagnostics.Debug.WriteLine($"❌ 等待私信编辑器超时");
                        OnCollectionError?.Invoke(accountId, "等待私信编辑器超时，请确认已点击 Continue");
                        return;
                    }
                }

                // 3. 输入消息并发送
                var script = GenerateDmSendScript(fbUserId, messageText);
                System.Diagnostics.Debug.WriteLine($"📜 私信输入发送脚本已生成: Length={script.Length}");
                var result = await EvaluateScriptWithTimeout(browser, script, timeoutMs: 60000);
                System.Diagnostics.Debug.WriteLine($"📜 私信JS执行返回: Success={result.Success}, Message={result.Message}, Result={result.Result}");

                if (result.Success && result.Result != null)
                {
                    var resultStr = result.Result.ToString();
                    System.Diagnostics.Debug.WriteLine($"✅ 私信发送结果: {resultStr}");

                    var resultObj = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(resultStr);
                    if (resultObj != null && resultObj.success == true)
                    {
                        System.Diagnostics.Debug.WriteLine($"✅ 私信发送成功");
                    }
                    else
                    {
                        var errorMsg = resultObj?.message?.ToString() ?? "未知错误";
                        System.Diagnostics.Debug.WriteLine($"❌ 私信发送失败: {errorMsg}");
                        OnCollectionError?.Invoke(accountId, $"私信发送失败: {errorMsg}");
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"❌ JS执行失败: {result.Message}");
                    OnCollectionError?.Invoke(accountId, $"JS执行失败: {result.Message}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ 私信发送异常: {ex.Message}");
                OnCollectionError?.Invoke(accountId, $"私信发送异常: {ex.Message}");
            }
        }
    }
}
