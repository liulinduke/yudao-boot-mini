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
        public async Task ExecuteGroupPublish(string accountId, string actionConfigJson, string detailIdOverride = "")
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

                var publishResults = new List<object>();
                var detailId = !string.IsNullOrWhiteSpace(detailIdOverride)
                    ? detailIdOverride
                    : _accountDetailIds.TryGetValue(accountId, out var mappedDetailId)
                    ? mappedDetailId
                    : CurrentDetailId ?? "";

                for (int i = 0; i < targetGroups.Count; i++)
                {
                    var group = targetGroups[i];
                    System.Diagnostics.Debug.WriteLine($"[发群帖] 正在发布到群组 {i + 1}/{targetGroups.Count}: {group.Name} ({group.Url})");

                    var success = false;
                    var failReason = "";
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

                        success = true;
                        System.Diagnostics.Debug.WriteLine($"[发群帖] ✅ 发布成功: {group.Name}");
                    }
                    catch (Exception ex)
                    {
                        failReason = ex.Message;
                        System.Diagnostics.Debug.WriteLine($"[发群帖] ❌ 发布到 {group.Name} 失败: {ex.Message}");
                    }

                    publishResults.Add(new
                    {
                        accountId,
                        targetUrl = group.Url,
                        groupId = group.Id,
                        groupName = group.Name,
                        groupUrl = group.Url,
                        joinStatus = success ? 1 : 2,
                        failReason = success ? "" : failReason,
                        joinTime = DateTime.Now.ToString("O"),
                        syncTime = DateTime.Now.ToString("O"),
                        postContent
                    });

                    if (i < targetGroups.Count - 1)
                    {
                        var random = new Random();
                        var intervalMs = random.Next(minIntervalSeconds * 1000, maxIntervalSeconds * 1000);
                        System.Diagnostics.Debug.WriteLine($"[发群帖] 等待 {intervalMs / 1000} 秒后继续...");
                        await Task.Delay(intervalMs);
                    }
                }

                System.Diagnostics.Debug.WriteLine("[发群帖] 所有操作完成");
                if (!string.IsNullOrWhiteSpace(detailId))
                {
                    OnCollectionComplete?.Invoke(detailId, accountId, JsonConvert.SerializeObject(publishResults), 13);
                }
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
            var currentUrl = await GetBrowserUrl(browser);
            var groupParts = groupUrl.TrimEnd('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
            var groupKey = groupParts.Length > 0 ? groupParts[groupParts.Length - 1] : "";
            var needsNavigate = string.IsNullOrEmpty(currentUrl)
                || !currentUrl.Contains("/groups/", StringComparison.OrdinalIgnoreCase)
                || (!string.IsNullOrEmpty(groupKey) && !currentUrl.Contains(groupKey, StringComparison.OrdinalIgnoreCase));
            if (needsNavigate)
            {
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    browser.Load(groupUrl);
                });
                await WaitForPageLoad(browser, 45000);
            }

            await WaitForPageReady(browser, 15000);
            await WaitForGroupFeedReady(browser);
            System.Diagnostics.Debug.WriteLine("[发群帖] 群组页面已就绪");
        }

        private async Task<string> GetBrowserUrl(ChromiumWebBrowser browser)
        {
            var result = await browser.EvaluateScriptAsync("(function(){ return location.href || ''; })();");
            return result.Success && result.Result != null ? result.Result.ToString() ?? "" : "";
        }

        private async Task WaitForGroupFeedReady(ChromiumWebBrowser browser)
        {
            const string script = @"
                (async function() {
                    const normalize = (t) => (t || '').replace(/\s+/g, ' ').trim().toLowerCase();
                    const hasPostBox = () => {
                        const main = document.querySelector('[role=""main""]') || document.body;
                        for (const btn of main.querySelectorAll('[role=""button""]')) {
                            const text = normalize(btn.textContent);
                            const aria = normalize(btn.getAttribute('aria-label'));
                            if (text.includes('write something') || text.includes('写点什么') || text.includes('在想什么')) return true;
                            if (aria.includes('write something') || aria.includes('写点什么')) return true;
                        }
                        return false;
                    };
                    const ensureDiscussion = () => {
                        for (const tab of document.querySelectorAll('[role=""tab""], a[role=""tab""]')) {
                            const label = normalize(tab.textContent);
                            if (label === 'discussion' || label === '讨论' || label === '动态') {
                                if (tab.getAttribute('aria-selected') !== 'true') tab.click();
                                return;
                            }
                        }
                    };
                    const start = Date.now();
                    while (Date.now() - start < 30000) {
                        if (!location.href.includes('/groups/')) {
                            await new Promise(r => setTimeout(r, 500));
                            continue;
                        }
                        ensureDiscussion();
                        const main = document.querySelector('[role=""main""]');
                        if (main) main.scrollIntoView({ block: 'start' });
                        window.scrollTo(0, 0);
                        if (hasPostBox()) return true;
                        await new Promise(r => setTimeout(r, 600));
                    }
                    throw new Error('群组发帖区未加载(未出现 Write something / 写点什么)');
                })();
            ";
            await RunGroupScript(browser, script, "等待群组发帖区");
        }

        private async Task RunGroupScript(ChromiumWebBrowser browser, string script, string stepName)
        {
            var result = await browser.EvaluateScriptAsync(script);
            if (!result.Success)
            {
                var detail = result.Message ?? "";
                if (result.Result != null) detail += " | " + result.Result;
                throw new Exception($"{stepName}失败: {detail}");
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
                    const normalizeLower = (t) => normalize(t).toLowerCase();
                    const humanClick = async (el) => {
                        if (!el) return false;
                        el.scrollIntoView({ block: 'center', inline: 'nearest' });
                        await new Promise(r => setTimeout(r, 400));
                        const rect = el.getBoundingClientRect();
                        const opts = { bubbles: true, cancelable: true, clientX: rect.left + rect.width / 2, clientY: rect.top + rect.height / 2, view: window };
                        el.dispatchEvent(new MouseEvent('mouseover', opts));
                        el.dispatchEvent(new MouseEvent('mousedown', opts));
                        await new Promise(r => setTimeout(r, 100));
                        el.dispatchEvent(new MouseEvent('mouseup', opts));
                        el.dispatchEvent(new MouseEvent('click', opts));
                        if (typeof el.click === 'function') el.click();
                        return true;
                    };
                    const isPostBoxButton = (btn) => {
                        const text = normalizeLower(btn.textContent);
                        const aria = normalizeLower(btn.getAttribute('aria-label'));
                        if (/anonymous|匿名/.test(text + ' ' + aria)) return false;
                        if (text.includes('write something') || text.includes('写点什么') || text.includes('在想什么')) return true;
                        if (text.includes('create a post') || text.includes('创建帖子')) return true;
                        if (aria.includes('write something') || aria.includes('写点什么')) return true;
                        return false;
                    };
                    const findPostBoxButton = () => {
                        const roots = [document.querySelector('[role=""main""]'), document.body].filter(Boolean);
                        for (const root of roots) {
                            for (const btn of root.querySelectorAll('[role=""button""]')) {
                                if (!isVisible(btn) || !isPostBoxButton(btn)) continue;
                                return btn;
                            }
                        }
                        try {
                            const cssBox = document.querySelector('span:has(>span a[href])+div[role=button]:has(>div+div[role=none][data-visualcompletion])');
                            if (isVisible(cssBox)) return cssBox;
                        } catch (e) { /* :has may be unsupported */ }
                        return null;
                    };
                    const collectCandidates = () => {
                        const samples = [];
                        const main = document.querySelector('[role=""main""]') || document.body;
                        for (const btn of main.querySelectorAll('[role=""button""]')) {
                            if (!isVisible(btn)) continue;
                            const text = normalize(btn.textContent);
                            if (!text || text.length > 60) continue;
                            samples.push(text);
                            if (samples.length >= 12) break;
                        }
                        return samples;
                    };

                    let postBox = null;
                    const start = Date.now();
                    while (Date.now() - start < 25000) {
                        postBox = findPostBoxButton();
                        if (postBox) break;
                        const main = document.querySelector('[role=""main""]');
                        if (main) main.scrollIntoView({ block: 'start' });
                        await new Promise(r => setTimeout(r, 600));
                    }
                    if (!postBox) {
                        const samples = collectCandidates();
                        throw new Error('未找到群组发帖框, url=' + (location.href || '').slice(0, 100) + ', candidates=' + samples.join(' | '));
                    }

                    await humanClick(postBox);
                    await new Promise(r => setTimeout(r, 2500));

                    const composerStart = Date.now();
                    while (Date.now() - composerStart < 15000) {
                        const composer = [...document.querySelectorAll('[role=""dialog""]')].reverse()
                            .find(d => isVisible(d) && d.querySelector('[role=""textbox""]'));
                        if (composer) return true;
                        await new Promise(r => setTimeout(r, 500));
                    }
                    throw new Error('群组发帖 composer 未打开');
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
                        const timeout = setTimeout(() => {
                            console.log('[发群帖检测] 发布超时，进行最终检查...');
                            const dialogs = [...document.querySelectorAll('[role=""dialog""]')];
                            console.log('[发群帖检测] 当前 dialog 数量:', dialogs.length);
                            dialogs.forEach((d, i) => {
                                const hasTextbox = !!d.querySelector('[role=""textbox""]');
                                const hasPostBtn = !!d.querySelector('[role=""button""][aria-label=""Post""], [role=""button""][aria-label=""发布""]');
                                const isVisible = d.offsetWidth > 0 && d.offsetHeight > 0;
                                console.log(`[发群帖检测] dialog ${i}: hasTextbox=${hasTextbox}, hasPostBtn=${hasPostBtn}, isVisible=${isVisible}`);
                            });
                            reject(new Error('发布超时'));
                        }, 45000);

                        const isVisible = (el) => {
                            if (!el) return false;
                            const rect = el.getBoundingClientRect();
                            const style = window.getComputedStyle(el);
                            return rect.width > 0 && rect.height > 0 && style.display !== 'none' && style.visibility !== 'hidden';
                        };
                        const normalize = (text) => (text || '').replace(/\s+/g, ' ').trim();
                        const isEnabled = (btn) => btn && btn.getAttribute('aria-disabled') !== 'true' && !btn.hasAttribute('disabled');
                        const isPublishButton = (btn) => {
                            const label = normalize(btn.getAttribute('aria-label'));
                            const text = normalize(btn.textContent);
                            if (/comment|评论/i.test(label + ' ' + text)) return false;
                            return /^(Post|发帖|发布|Submit|提交)$/i.test(label) || /^(Post|发帖|发布|Submit|提交)$/i.test(text);
                        };
                        const isComposerDialog = (dialog) => {
                            if (!dialog || dialog.offsetWidth === 0 || dialog.offsetHeight === 0) return false;
                            const textbox = [...dialog.querySelectorAll('[role=""textbox""]')].find(isVisible);
                            const postBtn = [...dialog.querySelectorAll('[role=""button""]')].find(btn => isVisible(btn) && isPublishButton(btn));
                            if (!textbox || !postBtn) return false;

                            const text = normalize(textbox.textContent);
                            const hasFileInput = !!dialog.querySelector('input[type=""file""]');
                            const hasMediaPreview = !!dialog.querySelector('img[src^=""blob:""], video, [aria-label*=""Remove""], [aria-label*=""移除""]');

                            // After Facebook accepts the post, it may leave/reopen an empty composer.
                            // Treat that as complete instead of waiting for every dialog to disappear.
                            return isEnabled(postBtn) || text.length > 0 || hasFileInput && hasMediaPreview;
                        };

                        let stableCompleteChecks = 0;
                        const checkInterval = setInterval(() => {
                            const dialogs = [...document.querySelectorAll('[role=""dialog""]')];
                            const activeComposers = dialogs.filter(isComposerDialog);

                            console.log('[发群帖检测] 检测中 - dialog总数:', dialogs.length, ', 活跃composer:', activeComposers.length);

                            if (activeComposers.length === 0) {
                                stableCompleteChecks += 1;
                                if (stableCompleteChecks >= 2) {
                                    console.log('[发群帖检测] ✅ 未找到活跃的发帖composer，判定发布完成');
                                    clearTimeout(timeout);
                                    clearInterval(checkInterval);
                                    resolve(true);
                                    return;
                                }
                            } else {
                                stableCompleteChecks = 0;
                            }

                            activeComposers.forEach((c, i) => {
                                const ariaLabel = c.querySelector('[role=""button""][aria-label]')?.getAttribute('aria-label');
                                console.log(`[发群帖检测] 活跃composer ${i}: aria-label=${ariaLabel || '无'}`);
                            });
                        }, 800);
                    });
                })();
            ";
            await RunGroupScript(browser, script, "等待发布完成");
            System.Diagnostics.Debug.WriteLine("[发群帖] 发布完成，composer 已关闭");
        }
    }
}
