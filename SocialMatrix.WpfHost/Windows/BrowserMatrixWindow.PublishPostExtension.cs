using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CefSharp;
using CefSharp.Wpf;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SocialMatrix.WpfHost.Services;

namespace SocialMatrix.WpfHost.Windows
{
    /// <summary>
    /// BrowserMatrixWindow 的发个人帖功能扩展
    /// </summary>
    public partial class BrowserMatrixWindow
    {
        private string GeneratePublishPostScript(string actionConfigJson)
        {
            var builder = new PublishPostScriptBuilder(actionConfigJson);
            return builder.Build();
        }

        private async Task<bool> RunPublishPostScript(ChromiumWebBrowser browser, string script, string stepName)
        {
            var result = await browser.EvaluateScriptAsync(script);
            if (!result.Success)
            {
                throw new Exception($"{stepName}脚本执行失败: {result.Message}");
            }

            if (result.Result == null) return true;

            var json = result.Result.ToString();
            if (string.IsNullOrWhiteSpace(json)) return true;

            try
            {
                var resultObj = JsonConvert.DeserializeObject<dynamic>(json);
                if (resultObj?.success == false)
                {
                    throw new Exception(resultObj.message?.ToString() ?? $"{stepName}失败");
                }
            }
            catch (JsonException)
            {
                // CefSharp 有时直接返回字符串，非 JSON 则忽略
            }

            return true;
        }

        /// <summary>
        /// 执行发个人帖
        /// </summary>
        public async Task ExecutePublishPost(string accountId, string actionConfigJson)
        {
            try
            {
                var browser = GetBrowser(accountId);
                if (browser == null)
                {
                    throw new InvalidOperationException($"未找到账号 {accountId} 的浏览器");
                }

                JObject config = JObject.Parse(actionConfigJson);
                var postContent = config["postContent"]?.ToString() ?? "";
                var mediaUrls = config["mediaUrls"]?.ToObject<string[]>() ?? Array.Empty<string>();
                var privacySetting = config["privacySetting"]?.Value<int>() ?? 1;

                System.Diagnostics.Debug.WriteLine("[发个人帖] 开始执行...");

                await EnsureFacebookHome(browser);

                var builder = new PublishPostScriptBuilder(actionConfigJson);

                await RunPublishPostScript(browser, builder.BuildOpenComposerScript(), "打开发帖 composer");

                if (!string.IsNullOrWhiteSpace(postContent))
                {
                    await RunPublishPostScript(browser, builder.BuildInputContentScript(postContent), "输入帖子内容");
                }

                if (mediaUrls.Length > 0)
                {
                    await UploadPublishPostMedia(browser, mediaUrls);
                }

                await RunPublishPostScript(browser, builder.BuildSetPrivacyScript(privacySetting), "设置隐私");

                await RunPublishPostScript(browser, builder.BuildClickPostScript(), "发布帖子");

                System.Diagnostics.Debug.WriteLine("[发个人帖] 执行成功");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[发个人帖] 异常: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 等待指定账号浏览器页面就绪（供 JsBridge 调用）
        /// </summary>
        public async Task WaitForAccountPageReady(string accountId, int timeoutMs = 30000)
        {
            var browser = GetBrowser(accountId);
            if (browser == null)
            {
                await Task.Delay(2000);
                return;
            }
            await WaitForPageReady(browser, timeoutMs);
        }

        private async Task EnsureFacebookHome(ChromiumWebBrowser browser)
        {
            var checkScript = @"
                (function() {
                    const href = (location.href || '').toLowerCase();
                    return href.includes('facebook.com') && !href.includes('login');
                })();
            ";
            var check = await browser.EvaluateScriptAsync(checkScript);
            var onFacebook = check.Success && check.Result is bool b && b;

            if (!onFacebook)
            {
                System.Diagnostics.Debug.WriteLine("[发个人帖] 导航到 Facebook 首页");
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    browser.Load("https://www.facebook.com/");
                });
                await WaitForPageLoad(browser, 30000);
            }
            else
            {
                await WaitForPageReady(browser, 10000);
            }

            System.Diagnostics.Debug.WriteLine("[发个人帖] Facebook 首页已就绪");
        }

        private async Task UploadPublishPostMedia(ChromiumWebBrowser browser, string[] mediaUrls)
        {
            System.Diagnostics.Debug.WriteLine($"[发个人帖] 准备上传 {mediaUrls.Length} 个文件");

            var fileHandler = new FileUploadDialogHandler(new List<string>(mediaUrls));
            browser.DialogHandler = fileHandler;
            await Task.Delay(500);

            var triggerScript = @"
                (function() {
                    const composer = [...document.querySelectorAll('[role=""dialog""]')]
                        .reverse()
                        .find(d => d.querySelector('[role=""textbox""]'));
                    const root = composer || document;
                    const photoBtn = root.querySelector('[role=""button""][aria-label=""Photo/video""]:not([aria-disabled=""true""]), [role=""button""][aria-label=""照片/视频""]:not([aria-disabled=""true""])');
                    if (photoBtn) { photoBtn.click(); return true; }
                    const fileInput = root.querySelector('input[type=""file""]');
                    if (fileInput) { fileInput.click(); return true; }
                    return false;
                })();
            ";

            var triggerResult = await browser.EvaluateScriptAsync(triggerScript);
            if (triggerResult.Success && triggerResult.Result is bool ok && ok)
            {
                System.Diagnostics.Debug.WriteLine("[发个人帖] 已触发媒体上传");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("[发个人帖] ⚠️ 未找到 Photo/video 按钮");
            }

            await Task.Delay(3000);
        }
    }
}
