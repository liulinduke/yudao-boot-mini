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
    /// BrowserMatrixWindow 的发群帖功能扩展
    /// </summary>
    public partial class BrowserMatrixWindow
    {
        private string GenerateGroupPublishScript(string actionConfigJson)
        {
            var builder = new GroupPublishScriptBuilder(actionConfigJson);
            return builder.Build();
        }

        /// <summary>
        /// 执行发群帖（C# 分步控制）
        /// </summary>
        public async Task ExecuteGroupPublish(string accountId, string actionConfigJson)
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
                var anonymouslyPost = config["anonymouslyPost"]?.Value<bool>() ?? false;
                var groupType = config["groupType"]?.Value<int>() ?? 1;
                var selectedGroups = config["selectedGroups"]?.ToObject<JArray>() ?? new JArray();
                var selectedUnjoinedGroups = config["selectedUnjoinedGroups"]?.ToObject<JArray>() ?? new JArray();
                var minIntervalSeconds = config["minIntervalSeconds"]?.Value<int>() ?? 10;
                var maxIntervalSeconds = config["maxIntervalSeconds"]?.Value<int>() ?? 20;

                System.Diagnostics.Debug.WriteLine("[发群帖] 开始执行...");

                var targetGroups = BuildGroupPublishTargets(groupType, selectedGroups, selectedUnjoinedGroups);
                if (targetGroups.Count == 0)
                {
                    throw new Exception("请至少选择一个群组");
                }

                System.Diagnostics.Debug.WriteLine($"[发群帖] 共 {targetGroups.Count} 个目标群组");

                for (int i = 0; i < targetGroups.Count; i++)
                {
                    var group = targetGroups[i];
                    System.Diagnostics.Debug.WriteLine($"[发群帖] 正在发布到群组 {i + 1}/{targetGroups.Count}: {group.Name} ({group.Url})");

                    try
                    {
                        await NavigateToGroupPage(browser, group.Url);
                        await OpenGroupComposer(browser);
                        if (!string.IsNullOrWhiteSpace(postContent))
                        {
                            await InputGroupPostContent(browser, postContent);
                        }
                        if (mediaUrls.Length > 0)
                        {
                            await UploadGroupMediaFiles(browser, mediaUrls);
                        }
                        if (anonymouslyPost)
                        {
                            await SetAnonymousPost(browser);
                        }
                        await ClickGroupPublishButton(browser);
                        await WaitForGroupPublishComplete(browser);

                        System.Diagnostics.Debug.WriteLine($"[发群帖] ✅ 发布成功: {group.Name}");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[发群帖] ❌ 发布到 {group.Name} 失败: {ex.Message}");
                    }

                    if (i < targetGroups.Count - 1)
                    {
                        var random = new Random();
                        var intervalMs = random.Next(minIntervalSeconds * 1000, maxIntervalSeconds * 1000);
                        System.Diagnostics.Debug.WriteLine($"[发群帖] 等待 {intervalMs / 1000} 秒后继续...");
                        await Task.Delay(intervalMs);
                    }
                }

                System.Diagnostics.Debug.WriteLine("[发群帖] 所有操作完成");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[发群帖] 异常: {ex.Message}");
                throw;
            }
        }

        private sealed class GroupPublishTarget
        {
            public string Id { get; set; } = "";
            public string Name { get; set; } = "";
            public string Url { get; set; } = "";
        }

        private static List<GroupPublishTarget> BuildGroupPublishTargets(int groupType, JArray selectedGroups, JArray selectedUnjoinedGroups)
        {
            var targets = new List<GroupPublishTarget>();
            var source = groupType == 2 ? selectedUnjoinedGroups : selectedGroups;

            foreach (var group in source)
            {
                var groupId = group["groupId"]?.ToString() ?? "";
                var groupName = group["groupName"]?.ToString() ?? "";
                var groupUrl = group["groupUrl"]?.ToString() ?? "";

                if (string.IsNullOrEmpty(groupUrl) && !string.IsNullOrEmpty(groupId))
                {
                    groupUrl = $"https://www.facebook.com/groups/{groupId}";
                }

                if (!string.IsNullOrEmpty(groupUrl))
                {
                    targets.Add(new GroupPublishTarget { Id = groupId, Name = groupName, Url = groupUrl });
                }
            }

            return targets;
        }

        private async Task NavigateToGroupPage(ChromiumWebBrowser browser, string groupUrl)
        {
            System.Diagnostics.Debug.WriteLine($"[发群帖] 导航到群组: {groupUrl}");
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                browser.Load(groupUrl);
            });
            await WaitForPageLoad(browser, 30000);
            await WaitForPageReady(browser, 10000);
            System.Diagnostics.Debug.WriteLine("[发群帖] 群组页面已就绪");
        }

        private async Task RunGroupScript(ChromiumWebBrowser browser, string script, string stepName)
        {
            var result = await browser.EvaluateScriptAsync(script);
            if (!result.Success)
            {
                throw new Exception($"{stepName}失败: {result.Message}");
            }
        }

        private async Task OpenGroupComposer(ChromiumWebBrowser browser)
        {
            const string script = @"
                (async function() {
                    const isVisible = (el) => {
                        if (!el) return false;
                        const rect = el.getBoundingClientRect();
                        const style = window.getComputedStyle(el);
                        return rect.width > 0 && rect.height > 0 && style.display !== 'none' && style.visibility !== 'hidden';
                    };
                    const normalize = (t) => (t || '').replace(/\s+/g, ' ').trim();
                    const clickEl = (el) => {
                        if (!el) return false;
                        el.scrollIntoView({ block: 'center', inline: 'nearest' });
                        el.click();
                        return true;
                    };

                    let opened = false;
                    const cssBox = document.querySelector('span:has(>span a[href])+div[role=button]:has(>div+div[role=none][data-visualcompletion])');
                    if (isVisible(cssBox)) opened = clickEl(cssBox);

                    if (!opened) {
                        for (const btn of document.querySelectorAll('[role=""button""]')) {
                            if (!isVisible(btn)) continue;
                            const text = normalize(btn.textContent);
                            const aria = normalize(btn.getAttribute('aria-label'));
                            if (/write something|写点什么|在想什么|匿名发帖|anonymous post/i.test(text + ' ' + aria)) {
                                opened = clickEl(btn);
                                if (opened) break;
                            }
                        }
                    }

                    if (!opened) throw new Error('未找到群组发帖框');
                    await new Promise(r => setTimeout(r, 2000));

                    const composer = [...document.querySelectorAll('[role=""dialog""]')].reverse()
                        .find(d => isVisible(d) && d.querySelector('[role=""textbox""]'));
                    if (!composer) throw new Error('群组发帖 composer 未打开');
                    return true;
                })();
            ";
            await RunGroupScript(browser, script, "打开群组发帖框");
            System.Diagnostics.Debug.WriteLine("[发群帖] composer 已打开");
        }

        private async Task InputGroupPostContent(ChromiumWebBrowser browser, string postContent)
        {
            var contentJson = JsonConvert.SerializeObject(postContent);
            var script = $@"
                (async function() {{
                    const content = {contentJson};
                    const composer = [...document.querySelectorAll('[role=""dialog""]')].reverse()
                        .find(d => d.querySelector('[role=""textbox""]'));
                    const textbox = composer?.querySelector('[role=""textbox""]');
                    if (!textbox) throw new Error('未找到帖子输入框');
                    textbox.focus();
                    await new Promise(r => setTimeout(r, 400));
                    document.execCommand('insertText', false, content);
                    textbox.dispatchEvent(new InputEvent('input', {{ data: content, bubbles: true, inputType: 'insertText' }}));
                    await new Promise(r => setTimeout(r, 800));
                    textbox.blur();
                    return true;
                }})();
            ";
            await RunGroupScript(browser, script, "输入帖子内容");
            System.Diagnostics.Debug.WriteLine("[发群帖] 内容已输入");
        }

        private async Task UploadGroupMediaFiles(ChromiumWebBrowser browser, string[] mediaUrls)
        {
            System.Diagnostics.Debug.WriteLine($"[发群帖] 准备上传 {mediaUrls.Length} 个文件");

            var fileHandler = new FileUploadDialogHandler(new List<string>(mediaUrls));
            browser.DialogHandler = fileHandler;
            await Task.Delay(500);

            const string triggerScript = @"
                (function() {
                    const composer = [...document.querySelectorAll('[role=""dialog""]')].reverse()
                        .find(d => d.querySelector('[role=""textbox""]') || d.querySelector('input[type=""file""]'));
                    const root = composer || document;
                    const photoBtn = root.querySelector('[role=""button""][aria-label=""Photo/video""]:not([aria-disabled=""true""]), [role=""button""][aria-label=""照片/视频""]:not([aria-disabled=""true""])');
                    if (photoBtn) { photoBtn.click(); return 'photo'; }
                    const fileInput = root.querySelector('input[type=""file""]');
                    if (fileInput) { fileInput.click(); return 'file'; }
                    return '';
                })();
            ";

            var triggerResult = await browser.EvaluateScriptAsync(triggerScript);
            var mode = triggerResult.Result?.ToString() ?? "";
            if (string.IsNullOrEmpty(mode))
            {
                throw new Exception("未找到 Photo/video 按钮或文件输入框");
            }

            System.Diagnostics.Debug.WriteLine($"[发群帖] 已触发媒体上传: {mode}");
            await Task.Delay(4000);
        }

        private async Task SetAnonymousPost(ChromiumWebBrowser browser)
        {
            const string script = @"
                (async function() {
                    const composer = [...document.querySelectorAll('[role=""dialog""]')].reverse()
                        .find(d => d.querySelector('[role=""textbox""]') || d.querySelector('input[type=""checkbox""]'));
                    const checkbox = composer?.querySelector('input[type=""checkbox""]') || document.querySelector('div[role=""dialog""] input[type=""checkbox""]');
                    if (!checkbox) return false;
                    checkbox.click();
                    await new Promise(r => setTimeout(r, 800));
                    const gotIt = document.querySelector('div[role=""dialog""] [role=""button""][aria-label=""Got it""], div[role=""dialog""] [role=""button""][aria-label=""知道了""]');
                    if (gotIt) gotIt.click();
                    return true;
                })();
            ";
            await browser.EvaluateScriptAsync(script);
            System.Diagnostics.Debug.WriteLine("[发群帖] 已设置匿名发帖");
        }

        private async Task ClickGroupPublishButton(ChromiumWebBrowser browser)
        {
            const string script = @"
                (async function() {
                    const isEnabled = (btn) => btn && btn.getAttribute('aria-disabled') !== 'true' && !btn.hasAttribute('disabled');
                    const findPostBtn = () => {
                        const composer = [...document.querySelectorAll('[role=""dialog""]')].reverse()
                            .find(d => d.querySelector('[role=""textbox""]') || d.querySelector('[role=""button""][aria-label=""Post""]'));
                        const root = composer || document;
                        return root.querySelector('[role=""button""][aria-label=""Post""], [role=""button""][aria-label=""发帖""], [role=""button""][aria-label=""发布""], [role=""button""][aria-label=""Submit""], [role=""button""][aria-label=""提交""]');
                    };

                    const start = Date.now();
                    while (Date.now() - start < 20000) {
                        const nextBtn = document.querySelector('div[role=""dialog""] [role=""button""][aria-label=""Next""]:not([aria-disabled=""true""]), div[role=""dialog""] [role=""button""][aria-label=""继续""]:not([aria-disabled=""true""])');
                        if (nextBtn) { nextBtn.click(); await new Promise(r => setTimeout(r, 1500)); }

                        const btn = findPostBtn();
                        if (isEnabled(btn)) {
                            btn.click();
                            return true;
                        }
                        await new Promise(r => setTimeout(r, 500));
                    }
                    throw new Error('未找到可点击的 Post 按钮(可能内容为空或媒体未上传完成)');
                })();
            ";
            await RunGroupScript(browser, script, "点击发布按钮");
            System.Diagnostics.Debug.WriteLine("[发群帖] 已点击发布按钮");
        }

        private async Task WaitForGroupPublishComplete(ChromiumWebBrowser browser)
        {
            const string script = @"
                (function() {
                    return new Promise((resolve, reject) => {
                        const timeout = setTimeout(() => reject(new Error('发布超时')), 45000);
                        const hasComposer = () => [...document.querySelectorAll('[role=""dialog""]')]
                            .some(d => d.querySelector('[role=""textbox""]'));
                        const checkInterval = setInterval(() => {
                            if (!hasComposer()) {
                                clearTimeout(timeout);
                                clearInterval(checkInterval);
                                resolve(true);
                            }
                        }, 500);
                    });
                })();
            ";
            await RunGroupScript(browser, script, "等待发布完成");
            System.Diagnostics.Debug.WriteLine("[发群帖] 发布完成，composer 已关闭");
        }
    }
}
