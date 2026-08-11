using CefSharp;
using CefSharp.Wpf;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SocialMatrix.WpfHost.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
                if (!string.IsNullOrWhiteSpace(signature) &&
                    !await EvaluateProfileScriptAsync(browser, BuildSetBioScript(signature)))
                    return ProfileResult(false, accountId, "未找到个人签名编辑框或保存按钮");

                if (!string.IsNullOrWhiteSpace(nickname) &&
                    !await UpdateProfileNameAsync(browser, ResolveFacebookProfileId(browser, accountId), nickname))
                    return ProfileResult(false, accountId, "未找到 Facebook 姓名编辑框或保存按钮");

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
            if (!await EvaluateProfileScriptAsync(browser, BuildClickMediaSaveScript(avatar)))
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

        private async Task<bool> UpdateProfileNameAsync(ChromiumWebBrowser browser, string accountId, string nickname)
        {
            var hasNameField = await EvaluateProfileScriptAsync(browser, BuildHasNameFieldScript());
            if (!hasNameField)
            {
                var opened = await EvaluateProfileScriptAsync(browser, BuildOpenNameEditorScript(accountId));
                if (!opened) return false;
                await WaitForPageReady(browser, 30000);
                await Task.Delay(1800);
            }

            var updated = await EvaluateProfileScriptAsync(browser, BuildSetNicknameScript(nickname));
            if (!updated) return false;

            await Task.Delay(1600);
            await browser.LoadUrlAsync($"https://www.facebook.com/profile.php?id={Uri.EscapeDataString(accountId)}");
            await WaitForPageReady(browser, 30000);
            return true;
        }

        private static string ResolveFacebookProfileId(ChromiumWebBrowser browser, string fallbackAccountId)
        {
            if (Uri.TryCreate(browser.Address, UriKind.Absolute, out var uri))
            {
                var queryPart = uri.Query.TrimStart('?')
                    .Split('&', StringSplitOptions.RemoveEmptyEntries)
                    .FirstOrDefault(part => part.StartsWith("id=", StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrWhiteSpace(queryPart))
                {
                    var profileId = Uri.UnescapeDataString(queryPart[3..]);
                    if (!string.IsNullOrWhiteSpace(profileId)) return profileId;
                }
            }

            return fallbackAccountId;
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
  const labels = ['edit profile','edit your profile','edit profile details','编辑个人资料','编辑个人主页','编辑主页','edit public details','编辑公开资料'];
  const text = el => ((el.getAttribute('aria-label') || '') + ' ' + (el.getAttribute('title') || '') + ' ' + (el.innerText || '')).trim().toLowerCase();
  const editorAlreadyOpen = [...document.querySelectorAll('[role=""dialog""]')].some(el => visible(el) && /edit profile|编辑个人资料|编辑个人主页/.test(text(el)));
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
  const text = el => ((el.getAttribute('aria-label') || '') + ' ' + (el.getAttribute('title') || '') + ' ' + (el.innerText || '')).trim().toLowerCase();
  const dialogs = () => [...document.querySelectorAll('[role=""dialog""]')].filter(visible);
  const root = () => dialogs().pop() || document;
  const exactLabels = {JsonConvert.SerializeObject(avatar
      ? new[] { "add profile picture", "update profile picture", "添加头像", "编辑头像" }
      : new[] { "add cover photo", "edit cover photo", "change cover photo", "添加封面", "编辑封面" })};
  const candidates = () => [...root().querySelectorAll('[role=""button""],button,a,[tabindex=""0""]')].filter(visible);
  let button = candidates().find(el => exactLabels.some(label => (el.getAttribute('aria-label') || '').trim().toLowerCase() === label));
  if (!button) button = candidates().find(el => exactLabels.some(label => text(el).includes(label)));
  if (!button) return false;
  button.click();
  await sleep(900);
  let input = [...document.querySelectorAll('input[type=""file""]')].find(visible) || document.querySelector('input[type=""file""]');
  if (!input) {{
    const upload = candidates().find(el => /upload photo|upload|上传照片|上传/.test(text(el)));
    if (upload) upload.click();
    await sleep(900);
    input = [...document.querySelectorAll('input[type=""file""]')].find(visible) || document.querySelector('input[type=""file""]');
  }}
  if (!input) return false;
  input.click();
  return true;
}})();";

        private static string BuildSetBioScript(string signature) => $@"
(async function() {{
  const sleep = ms => new Promise(r => setTimeout(r, ms));
  const visible = el => {{ const r = el?.getBoundingClientRect(); return !!el && r.width > 0 && r.height > 0; }};
  const text = el => ((el.getAttribute('aria-label') || '') + ' ' + (el.innerText || '')).trim().toLowerCase();
  const dialog = () => [...document.querySelectorAll('[role=""dialog""]')].filter(visible).pop() || document;
  const root = dialog();
  let trigger = [...root.querySelectorAll('[role=""button""],button,a')].find(el => visible(el) && /add bio|edit bio|bio|简介|签名/.test(text(el)));
  if (trigger && !root.querySelector('textarea[placeholder=""Describe who you are""],textarea')) trigger.click();
  for (let i = 0; i < 20; i++) {{
    const bioFields = [...document.querySelectorAll('textarea[placeholder=""Describe who you are""]')].filter(visible);
    const field = bioFields[0] || [...document.querySelectorAll('[role=""dialog""] textarea, [role=""dialog""] [contenteditable=""true""]')].filter(visible)[0];
    if (field) {{
      field.focus();
      const value = {JsonConvert.SerializeObject(signature)};
      if (field.isContentEditable) field.textContent = value;
      else {{ const proto = Object.getPrototypeOf(field); const setter = Object.getOwnPropertyDescriptor(proto, 'value')?.set; if (setter) setter.call(field, value); else field.value = value; }}
      field.dispatchEvent(new InputEvent('input', {{ bubbles: true, inputType: 'insertText', data: value }}));
      field.dispatchEvent(new Event('change', {{ bubbles: true }}));
      await sleep(250);
      const save = [...document.querySelectorAll('[role=""dialog""] [role=""button""], [role=""dialog""] button, [role=""button""]')].filter(visible).find(el => /save profile bio|save|保存/.test(text(el)));
      if (save) {{ save.click(); await sleep(700); return true; }}
    }}
    await sleep(250);
  }}
  return false;
}})();";

        private static string BuildSetNicknameScript(string nickname) => $@"
(async function() {{
  const value = {JsonConvert.SerializeObject(nickname)};
  const visible = el => {{ const r = el?.getBoundingClientRect(); return !!el && r.width > 0 && r.height > 0; }};
  const label = el => [el.getAttribute('aria-label'), el.getAttribute('name'), el.getAttribute('placeholder'), el.getAttribute('title'), el.closest('[role=""dialog""],form,section')?.innerText].filter(Boolean).join(' ').toLowerCase();
  const fields = [...document.querySelectorAll('input,textarea,[contenteditable=""true""],[role=""textbox""]')].filter(visible);
  const setValue = (field, next) => {{
    field.focus();
    if (field.isContentEditable) field.textContent = next;
    else {{ const setter = Object.getOwnPropertyDescriptor(Object.getPrototypeOf(field), 'value')?.set; if (setter) setter.call(field, next); else field.value = next; }}
    field.dispatchEvent(new InputEvent('input', {{ bubbles: true, inputType: 'insertText', data: next }}));
    field.dispatchEvent(new Event('change', {{ bubbles: true }}));
  }};
  const first = fields.find(el => /first name|given name|名/.test(label(el)) && !/last|family|姓/.test(label(el)));
  const last = fields.find(el => /last name|family name|surname|姓/.test(label(el)));
  const full = fields.find(el => /full name|display name|nickname|名字和姓氏/.test(label(el)));
  if (full) setValue(full, value);
  else if (first) {{
    const parts = value.trim().split(/\s+/);
    setValue(first, parts.shift() || value);
    if (last && parts.length) setValue(last, parts.join(' '));
  }}
  else return false;
  const save = [...document.querySelectorAll('[role=""button""],button')].filter(visible).reverse().find(el => /save|保存|done|完成|continue|继续/.test((el.getAttribute('aria-label') || '') + ' ' + (el.innerText || '')).toLowerCase());
  if (save) {{ save.click(); return true; }}
  return false;
}})();";

        private static string BuildHasNameFieldScript() => @"
(function() {
  const visible = el => { const r = el?.getBoundingClientRect(); return !!el && r.width > 0 && r.height > 0; };
  const label = el => [el.getAttribute('aria-label'), el.getAttribute('name'), el.getAttribute('placeholder'), el.getAttribute('title'), el.closest('[role=""dialog""],form,section')?.innerText].filter(Boolean).join(' ').toLowerCase();
  return [...document.querySelectorAll('input,textarea,[contenteditable=""true""],[role=""textbox""]')].some(el => visible(el) && /first name|full name|display name|nickname|姓名|名字|名字和姓氏|name/.test(label(el)));
})();";

        private static string BuildOpenNameEditorScript(string accountId) => $@"
(function() {{
  const href = 'https://accountscenter.facebook.com/profiles/' + encodeURIComponent({JsonConvert.SerializeObject(accountId)}) + '/name';
  location.href = href;
  return true;
}})();";

        private static string BuildClickMediaSaveScript(bool avatar) => $@"
(function() {{
  const visible = el => {{ const r = el?.getBoundingClientRect(); return !!el && r.width > 0 && r.height > 0; }};
  const text = el => ((el.getAttribute('aria-label') || '') + ' ' + (el.innerText || '')).toLowerCase();
  const dialogs = [...document.querySelectorAll('[role=""dialog""]')].filter(visible);
  const root = dialogs[dialogs.length - 1] || document;
  const buttons = [...root.querySelectorAll('[role=""button""],button')].filter(visible);
  const save = buttons.find(el => /save|done|apply|confirm|保存|完成|应用|确定/.test(text(el)));
  if (save) {{ save.click(); return true; }}
  const next = buttons.find(el => /continue|next|下一步|继续/.test(text(el)));
  if (next) {{ next.click(); return true; }}
  return false;
}})();";

    }
}
