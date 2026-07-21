using CefSharp;
using CefSharp.Wpf;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SocialMatrix.WpfHost.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace SocialMatrix.WpfHost.Windows
{
    public partial class BrowserMatrixWindow
    {
        private static readonly HttpClient ProfileHttpClient = new() { Timeout = TimeSpan.FromSeconds(30) };

        private async Task<string> ExecuteProfileUpdateAsync(ChromiumWebBrowser browser, string accountId, string? configJson)
        {
            var config = string.IsNullOrWhiteSpace(configJson) ? new JObject() : JObject.Parse(configJson);
            var tempFiles = new List<string>();
            try
            {
                await WaitForPageReady(browser, 30000);
                if (await IsProfileLoginPageAsync(browser))
                    return ProfileResult(false, accountId, "Cookie已失效或账号需要重新登录");

                if (!await EvaluateProfileScriptAsync(browser, BuildOpenProfileEditorScript()))
                    return ProfileResult(false, accountId, "未找到 Facebook 个人资料编辑入口");
                await Task.Delay(1200);

                var avatarUrl = config.Value<string>("avatarUrl");
                if (!string.IsNullOrWhiteSpace(avatarUrl))
                {
                    var avatarPath = await DownloadProfileFileAsync(avatarUrl, "avatar");
                    tempFiles.Add(avatarPath);
                    await UploadProfileMediaAsync(browser, avatarPath, true);
                }

                var coverUrl = config.Value<string>("coverUrl");
                if (!string.IsNullOrWhiteSpace(coverUrl))
                {
                    var coverPath = await DownloadProfileFileAsync(coverUrl, "cover");
                    tempFiles.Add(coverPath);
                    await UploadProfileMediaAsync(browser, coverPath, false);
                }

                var nickname = config.Value<string>("nickname");
                var signature = config.Value<string>("signature");
                if (!string.IsNullOrWhiteSpace(nickname) || !string.IsNullOrWhiteSpace(signature))
                {
                    var textResult = await EvaluateProfileScriptAsync(browser, BuildSetProfileTextScript(nickname ?? "", signature ?? ""));
                    if (!textResult) return ProfileResult(false, accountId, "未找到昵称或个人签名编辑框");
                }

                if (!await EvaluateProfileScriptAsync(browser, BuildClickSaveScript()))
                    return ProfileResult(false, accountId, "未找到 Facebook 资料保存按钮");
                await Task.Delay(1800);
                return ProfileResult(true, accountId, "", avatarUrl, coverUrl, nickname, signature);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ 账号 {accountId} 资料上传失败: {ex.Message}");
                return ProfileResult(false, accountId, ex.Message);
            }
            finally
            {
                foreach (var path in tempFiles)
                {
                    try { File.Delete(path); } catch { }
                }
                browser.DialogHandler = null;
            }
        }

        private async Task UploadProfileMediaAsync(ChromiumWebBrowser browser, string localPath, bool avatar)
        {
            browser.DialogHandler = new FileUploadDialogHandler(new List<string> { localPath });
            var triggered = await EvaluateProfileScriptAsync(browser, BuildTriggerMediaScript(avatar));
            if (!triggered) throw new InvalidOperationException(avatar ? "未找到头像上传入口" : "未找到封面上传入口");
            await Task.Delay(4000);
            if (!await EvaluateProfileScriptAsync(browser, BuildClickSaveScript()))
                throw new InvalidOperationException(avatar ? "未找到头像保存按钮" : "未找到封面保存按钮");
            await Task.Delay(1200);
        }

        private async Task<string> DownloadProfileFileAsync(string url, string prefix)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
                throw new InvalidOperationException($"资料文件地址无效: {url}");
            var extension = Path.GetExtension(uri.AbsolutePath);
            if (string.IsNullOrWhiteSpace(extension) || extension.Length > 6) extension = ".jpg";
            var path = Path.Combine(Path.GetTempPath(), $"social-matrix-{prefix}-{Guid.NewGuid():N}{extension}");
            var bytes = await ProfileHttpClient.GetByteArrayAsync(uri);
            await File.WriteAllBytesAsync(path, bytes);
            return path;
        }

        private async Task<bool> EvaluateProfileScriptAsync(ChromiumWebBrowser browser, string script)
        {
            for (var attempt = 0; attempt < 20; attempt++)
            {
                if (browser.CanExecuteJavascriptInMainFrame && !browser.IsLoading)
                {
                    var result = await browser.EvaluateScriptAsync(script);
                    if (result.Success)
                    {
                        if (result.Result is bool value) return value;
                        if (bool.TryParse(result.Result?.ToString(), out var parsed)) return parsed;
                        return true;
                    }
                }
                await Task.Delay(500);
            }
            return false;
        }

        private async Task<bool> IsProfileLoginPageAsync(ChromiumWebBrowser browser)
        {
            var result = await browser.EvaluateScriptAsync("location.href.includes('/login') || location.href.includes('/checkpoint')");
            return result.Success && result.Result is bool && (bool)result.Result;
        }

        private static string ProfileResult(bool success, string accountId, string error,
            string? avatarUrl = null, string? coverUrl = null, string? nickname = null, string? signature = null)
        {
            return JsonConvert.SerializeObject(new
            {
                success,
                accountId,
                errorMessage = error,
                avatarUrl,
                coverUrl,
                nickname,
                signature
            });
        }

        private static string BuildOpenProfileEditorScript() => @"
(async function() {
  const sleep = ms => new Promise(r => setTimeout(r, ms));
  const visible = el => { const r = el?.getBoundingClientRect(); return !!el && r.width > 0 && r.height > 0; };
  const labels = ['edit profile','编辑个人主页','编辑主页','edit public details','编辑公开资料'];
  const text = el => ((el.getAttribute('aria-label') || '') + ' ' + (el.innerText || '')).trim().toLowerCase();
  const editorAlreadyOpen = [...document.querySelectorAll('input,textarea,[contenteditable=""true""]')].some(visible);
  if (editorAlreadyOpen) return true;
  const button = [...document.querySelectorAll('[role=""button""],button,a')].find(el => visible(el) && labels.some(x => text(el).includes(x)));
  if (!button) return false;
  button.click();
  await sleep(1200);
  return true;
})();";

        private static string BuildTriggerMediaScript(bool avatar) => $@"
(async function() {{
  const sleep = ms => new Promise(r => setTimeout(r, ms));
  const visible = el => {{ const r = el?.getBoundingClientRect(); return !!el && r.width > 0 && r.height > 0; }};
  const words = {JsonConvert.SerializeObject(avatar
      ? new[] { "profile picture", "头像", "profile photo", "添加头像", "编辑头像" }
      : new[] { "cover photo", "封面", "添加封面", "编辑封面" })};
  const text = el => ((el.getAttribute('aria-label') || '') + ' ' + (el.innerText || '')).trim().toLowerCase();
  let button = [...document.querySelectorAll('[role=""button""],button,a')].find(el => visible(el) && words.some(x => text(el).toLowerCase().includes(x.toLowerCase())));
  if (button) button.click();
  await sleep(700);
  const input = [...document.querySelectorAll('input[type=""file""]')].find(visible) || document.querySelector('input[type=""file""]');
  if (!input) return false;
  input.click();
  return true;
}})();";

        private static string BuildSetProfileTextScript(string nickname, string signature) => $@"
(function() {{
  const setValue = (el, value) => {{
    if (!el || !value) return false;
    el.focus();
    if (el.isContentEditable) {{ el.textContent = value; }}
    else {{ const setter = Object.getOwnPropertyDescriptor(el.__proto__, 'value')?.set; setter?.call(el, value); }}
    el.dispatchEvent(new Event('input', {{ bubbles: true }}));
    el.dispatchEvent(new Event('change', {{ bubbles: true }}));
    return true;
  }};
  const label = el => ((el.getAttribute('aria-label') || '') + ' ' + (el.getAttribute('name') || '') + ' ' + (el.getAttribute('placeholder') || '')).toLowerCase();
  const fields = [...document.querySelectorAll('input,textarea,[contenteditable=""true""]')].filter(el => {{ const r = el.getBoundingClientRect(); return r.width > 0 && r.height > 0; }});
  const nameField = fields.find(el => /name|昵称|姓名|名字/.test(label(el)));
  const bioField = fields.find(el => /bio|about|intro|简介|签名/.test(label(el)));
  const nameOk = setValue(nameField, {JsonConvert.SerializeObject(nickname)});
  const bioOk = setValue(bioField, {JsonConvert.SerializeObject(signature)});
  return nameOk || bioOk;
}})();";

        private static string BuildClickSaveScript() => @"
(function() {
  const visible = el => { const r = el?.getBoundingClientRect(); return !!el && r.width > 0 && r.height > 0; };
  const words = ['save','保存','done','完成','apply','应用','submit','提交'];
  const text = el => ((el.getAttribute('aria-label') || '') + ' ' + (el.innerText || '')).toLowerCase();
  const button = [...document.querySelectorAll('[role=""button""],button')].reverse().find(el => visible(el) && words.some(x => text(el).includes(x)));
  if (button) { button.click(); return true; }
  return false;
})();";
    }
}
