using System;
using System.Threading.Tasks;
using CefSharp;
using CefSharp.Wpf;
using Newtonsoft.Json;
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

        private void NotifyDmResult(string accountId, string taskId, string detailId, string fbUserId, bool success, string? message = null)
        {
            var payload = JsonConvert.SerializeObject(new
            {
                success,
                taskId,
                detailId,
                accountId,
                targetUserId = fbUserId,
                message = message ?? ""
            });
            OnCollectionComplete?.Invoke(detailId, accountId, payload, 14);
        }

        /// <summary>
        /// 在已有浏览器上执行单条私信明细（同账号后续明细复用浏览器）
        /// </summary>
        public async Task ExecuteDmDetailAsync(string taskId, string detailId, string accountId, string fbUserId, string messageText)
        {
            _accountDetailIds[accountId] = detailId;
            CurrentDetailId = detailId;
            _dmTaskIds[accountId] = taskId;
            await SendDirectMessage(accountId, fbUserId, messageText, taskId, detailId);
        }

        public async Task SendDirectMessage(string accountId, string fbUserId, string messageText, string? taskId = null, string? detailId = null)
        {
            taskId ??= _dmTaskIds.TryGetValue(accountId, out var tid) ? tid : "";
            detailId ??= _accountDetailIds.TryGetValue(accountId, out var did) ? did : (CurrentDetailId ?? "");

            if (!_browsers.TryGetValue(accountId, out var browser))
            {
                System.Diagnostics.Debug.WriteLine($"❌ 账号 {accountId} 的浏览器不存在");
                OnCollectionError?.Invoke(accountId, "浏览器不存在");
                NotifyDmResult(accountId, taskId, detailId, fbUserId, false, "浏览器不存在");
                return;
            }

            if (!_dmSendingAccounts.Add(accountId))
            {
                const string runningMessage = "账号正在发送上一条私信";
                System.Diagnostics.Debug.WriteLine($"⚠️ {runningMessage}: account={accountId}, detail={detailId}");
                return;
            }

            try
            {
                System.Diagnostics.Debug.WriteLine($"📨 开始发送私信: 任务={taskId}, 明细={detailId}, 账号={accountId}, 目标={fbUserId}");

                // 1. 确保已进入私信页面（公共主页也优先走 messages/t/{id}）。
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
                if (!IsOnTargetUrl(currentUrl, dmUrl))
                {
                    var err = $"未进入目标私信会话，当前={currentUrl}, 目标={dmUrl}";
                    System.Diagnostics.Debug.WriteLine($"❌ {err}");
                    NotifyDmResult(accountId, taskId, detailId, fbUserId, false, err);
                    return;
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
                        const string err = "等待私信编辑器超时，请确认已点击 Continue";
                        System.Diagnostics.Debug.WriteLine($"❌ {err}");
                        OnCollectionError?.Invoke(accountId, err);
                        NotifyDmResult(accountId, taskId, detailId, fbUserId, false, err);
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
                    var resultStr = result.Result.ToString() ?? "";
                    System.Diagnostics.Debug.WriteLine($"✅ 私信发送结果: {resultStr}");

                    var resultObj = JsonConvert.DeserializeObject<dynamic>(resultStr);
                    if (resultObj != null && resultObj.success == true)
                    {
                        System.Diagnostics.Debug.WriteLine($"✅ 私信发送成功，回传结果到 Vue");
                        NotifyDmResult(accountId, taskId, detailId, fbUserId, true);
                    }
                    else
                    {
                        var errorMsg = resultObj?.message?.ToString() ?? "未知错误";
                        System.Diagnostics.Debug.WriteLine($"❌ 私信发送失败: {errorMsg}");
                        OnCollectionError?.Invoke(accountId, $"私信发送失败: {errorMsg}");
                        NotifyDmResult(accountId, taskId, detailId, fbUserId, false, errorMsg);
                    }
                }
                else
                {
                    var err = $"JS执行失败: {result.Message}";
                    System.Diagnostics.Debug.WriteLine($"❌ {err}");
                    OnCollectionError?.Invoke(accountId, err);
                    NotifyDmResult(accountId, taskId, detailId, fbUserId, false, err);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ 私信发送异常: {ex.Message}");
                OnCollectionError?.Invoke(accountId, $"私信发送异常: {ex.Message}");
                NotifyDmResult(accountId, taskId, detailId, fbUserId, false, ex.Message);
            }
            finally
            {
                _dmSendingAccounts.Remove(accountId);
            }
        }
    }
}
