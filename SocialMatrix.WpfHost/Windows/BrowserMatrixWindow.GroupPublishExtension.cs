using System;
using System.Collections.Generic;
using System.IO;
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
                var randomizeImagesAndAppendEmoji = config["randomizeImagesAndAppendEmoji"]?.Value<bool>() ?? true;
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
                    : "";

                for (int i = 0; i < targetGroups.Count; i++)
                {
                    var group = targetGroups[i];
                    System.Diagnostics.Debug.WriteLine($"[发群帖] 正在发布到群组 {i + 1}/{targetGroups.Count}: {group.Name} ({group.Url})");

                    var success = false;
                    var failReason = "";
                    var publishedContent = randomizeImagesAndAppendEmoji && !string.IsNullOrWhiteSpace(postContent)
                        ? PostMediaRandomizer.AppendRandomEmoji(postContent)
                        : postContent;
                    try
                    {
                        await NavigateToGroupPage(browser, group.Url);
                        await OpenGroupComposer(browser);
                        if (mediaUrls.Length > 0)
                        {
                            // Facebook may replace the composer DOM after text input.
                            // Upload first so the media input belongs to the initial
                            // composer, then enter text into that same composer.
                            await UploadGroupMediaFiles(browser, mediaUrls, "", randomizeImagesAndAppendEmoji);
                        }
                        if (!string.IsNullOrWhiteSpace(postContent))
                        {
                            await InputGroupPostContent(browser, publishedContent);
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
                        postContent = publishedContent
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
                        // 页面就绪只依赖稳定的 DOM 结构，不依赖 Facebook 当前语言
                        // 或按钮文案；具体 composer 由后续结构化查找完成。
                        if (main && main.querySelectorAll('[role=""button""], [role=""textbox""]').length > 0) return true;
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
                        return !aria && !btn.hasAttribute('aria-expanded') && btn.tabIndex >= 0
                            && btn.querySelector('span') && text.length > 0 && text.length < 120;
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

                    const existingComposer = () => [...document.querySelectorAll('[role=""dialog""]')]
                        .filter(d => isVisible(d) && d.querySelector('[role=""textbox""]') && d.querySelector('input[type=""file""][multiple]'))
                        .at(-1);
                    const hasDraftContent = (composer) => {
                        const textbox = composer?.querySelector('[role=""textbox""]');
                        const text = (textbox?.innerText || textbox?.textContent || '').trim();
                        return !!text || !!composer?.querySelector('img[src^=""blob:""], video, [aria-label*=""Remove post attachment""], [aria-label*=""移除帖子附件""]');
                    };

                    let composer = existingComposer();
                    if (composer) {
                        if (hasDraftContent(composer)) throw new Error('检测到已有未发布的群帖草稿，请先手动关闭或发布后再执行任务');
                    } else {
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
                    }

                    const composerStart = Date.now();
                    while (Date.now() - composerStart < 35000) {
                        composer = [...document.querySelectorAll('[role=""dialog""]')].reverse()
                            .find(d => isVisible(d) && d.querySelector('[role=""textbox""]') && d.querySelector('input[type=""file""][multiple]'));
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
                    const dialogs = [...document.querySelectorAll('[role=""dialog""]')].reverse();
                    const composer = dialogs.find(d => d.querySelector('[role=""textbox""]') && d.querySelector('input[type=""file""][multiple]'))
                        || dialogs.find(d => d.querySelector('[role=""textbox""]'));
                    const textbox = composer?.querySelector('[role=""textbox""]');
                    if (!textbox) throw new Error('未找到帖子输入框');
                    textbox.focus();
                    await new Promise(r => setTimeout(r, 400));
                    document.execCommand('selectAll', false, null);
                    document.execCommand('delete', false, null);
                    await new Promise(r => setTimeout(r, 200));
                    // 与私信发送保持一致：逐字输入并带不规则停顿，避免整段粘贴。
                    for (const ch of content) {{
                        document.execCommand('insertText', false, ch);
                        const pause = 45 + Math.random() * 125 + (Math.random() > 0.92 ? 250 + Math.random() * 500 : 0);
                        await new Promise(r => setTimeout(r, pause));
                    }}
                    await new Promise(r => setTimeout(r, 500));
                    const actual = (textbox.innerText || textbox.textContent || '').replace(/\\r\\n/g, '\\n');
                    if (actual === content + content) {{
                        document.execCommand('selectAll', false, null);
                        document.execCommand('delete', false, null);
                        for (const ch of content) {{
                            document.execCommand('insertText', false, ch);
                            await new Promise(r => setTimeout(r, 35 + Math.random() * 85));
                        }}
                        await new Promise(r => setTimeout(r, 300));
                    }}
                    textbox.blur();
                    return true;
                }})();
            ";
            await RunGroupScript(browser, script, "输入帖子内容");
            System.Diagnostics.Debug.WriteLine("[发群帖] 内容已输入");
        }

        private async Task UploadGroupMediaFiles(ChromiumWebBrowser browser, string[] mediaUrls, string postContent, bool addImageNoise)
        {
            System.Diagnostics.Debug.WriteLine($"[发群帖] 准备上传 {mediaUrls.Length} 个文件");

            var invalidPaths = Array.FindAll(mediaUrls, path => string.IsNullOrWhiteSpace(path) || !System.IO.File.Exists(path));
            if (invalidPaths.Length > 0)
            {
                throw new Exception($"媒体文件不存在或路径无效: {string.Join(", ", invalidPaths)}");
            }

            var inputNodeId = await FindGroupFileInputNodeAsync(browser);
            if (inputNodeId <= 0) throw new Exception("未找到已打开群帖 composer 的媒体输入框");
            var temporaryFiles = new List<string>();
            var uploadPaths = addImageNoise
                ? PostMediaRandomizer.CreateNoisyImageCopies(mediaUrls, out temporaryFiles)
                : mediaUrls;
            try
            {
                await ExecuteDevToolsAsync(browser, "DOM.setFileInputFiles", new Dictionary<string, object>
                {
                    ["nodeId"] = inputNodeId,
                    ["files"] = uploadPaths
                });
                System.Diagnostics.Debug.WriteLine("[发群帖] 已通过 CDP 注入媒体文件");

            // Facebook may accept the file dialog immediately but continue uploading
            // asynchronously. Do not click Post after a fixed short delay: that can
            // publish the text while silently dropping the selected media.
            var expectedContentJson = JsonConvert.SerializeObject(postContent ?? "");
            var waitScript = @"
                (async function() {
                    const expectedContent = __POST_CONTENT__;
                    const visible = (el) => {
                        if (!el) return false;
                        const r = el.getBoundingClientRect();
                        const s = getComputedStyle(el);
                        return r.width > 0 && r.height > 0 && s.display !== 'none' && s.visibility !== 'hidden';
                    };
                    const findComposer = () => {
                        const composers = [...document.querySelectorAll('[role=""dialog""]')]
                            .filter(d => visible(d) && d.querySelector('[role=""textbox""]'));
                        const matching = expectedContent ? composers.filter(d => {
                            const t = d.querySelector('[role=""textbox""]');
                            return (t?.innerText || t?.textContent || '').includes(expectedContent);
                        }) : [];
                        return (matching.length ? matching : composers).at(-1);
                    };
                    const hasMedia = (root) => {
                        if (!root) return false;
                        if ([...root.querySelectorAll('input[type=""file""]')].some(i => i.files && i.files.length)) return true;
                        return [...root.querySelectorAll('img,video,[role=""img""],[style*=""background-image""],[data-visualcompletion*=""media""]')].some(el => {
                            const src = (el.getAttribute('src') || '').toLowerCase();
                            return visible(el) && (src.startsWith('blob:') || src.startsWith('data:') || el.tagName === 'VIDEO');
                        }) || [...root.querySelectorAll('[aria-label]')].some(el => {
                            const label = (el.getAttribute('aria-label') || '').toLowerCase();
                            return visible(el) && (label.includes('remove') || label.includes('移除') || label.includes('delete'));
                        });
                    };
                    const start = Date.now();
                    let stable = 0;
                    while (Date.now() - start < 60000) {
                        const composer = findComposer();
                        if (hasMedia(composer || document)) {
                            stable += 1;
                            if (stable >= 3) return true;
                        } else {
                            stable = 0;
                        }
                        await new Promise(r => setTimeout(r, 1000));
                    }
                    throw new Error('媒体上传未完成或未出现在发帖框');
                })();
            ".Replace("__POST_CONTENT__", expectedContentJson);
                await Task.Delay(4000);
                await RunGroupScript(browser, waitScript, "等待媒体上传");
                System.Diagnostics.Debug.WriteLine("[发群帖] 媒体已进入发帖框并完成稳定检查");
            }
            finally
            {
                if (addImageNoise) PostMediaRandomizer.DeleteTemporaryFiles(temporaryFiles);
            }
        }

        private async Task<int> FindGroupFileInputNodeAsync(ChromiumWebBrowser browser)
        {
            await ExecuteDevToolsAsync(browser, "DOM.enable", new Dictionary<string, object>());
            for (var attempt = 1; attempt <= 15; attempt++)
            {
                var document = await ExecuteDevToolsAsync(browser, "DOM.getDocument", new Dictionary<string, object>
                {
                    ["depth"] = 0
                });
                var rootNodeId = document.SelectToken("result.root.nodeId")?.Value<int>()
                    ?? document.SelectToken("root.nodeId")?.Value<int>()
                    ?? 0;
                if (rootNodeId > 0)
                {
                    var query = await ExecuteDevToolsAsync(browser, "DOM.querySelectorAll", new Dictionary<string, object>
                    {
                        ["nodeId"] = rootNodeId,
                        ["selector"] = "[role=dialog] input[type=file][multiple]"
                    });
                    var nodeIds = query.SelectToken("result.nodeIds") as JArray
                        ?? query["nodeIds"] as JArray;
                    var nodeId = nodeIds != null && nodeIds.Count > 0
                        ? nodeIds[nodeIds.Count - 1].Value<int>()
                        : 0;
                    if (nodeId > 0) return nodeId;
                }

                System.Diagnostics.Debug.WriteLine($"[发群帖] 等待群帖媒体输入框: {attempt}/15");
                await Task.Delay(1000);
            }

            return 0;
        }

        private async Task<JObject> ExecuteDevToolsAsync(ChromiumWebBrowser browser, string method, IDictionary<string, object> parameters)
        {
            var tcs = new TaskCompletionSource<JObject>(TaskCreationOptions.RunContinuationsAsynchronously);
            DevToolsResultObserver observer = null;
            IRegistration registration = null;
            var host = browser.GetBrowser().GetHost();

            // ExecuteDevToolsMethod is restricted to CEF's UI thread, which is
            // distinct from both the WPF dispatcher and the async continuation.
            await Cef.UIThreadTaskFactory.StartNew(() =>
            {
                var id = host.GetNextDevToolsMessageId();
                observer = new DevToolsResultObserver(id, tcs);
                registration = host.AddDevToolsMessageObserver(observer);
                host.ExecuteDevToolsMethod(id, method, parameters);
            });

            try
            {
                return await tcs.Task.WaitAsync(TimeSpan.FromSeconds(15));
            }
            finally
            {
                await Cef.UIThreadTaskFactory.StartNew(() =>
                {
                    registration?.Dispose();
                    observer?.Dispose();
                });
            }
        }

        private sealed class DevToolsResultObserver : CefSharp.Callback.IDevToolsMessageObserver
        {
            private readonly int _id;
            private readonly TaskCompletionSource<JObject> _tcs;
            public DevToolsResultObserver(int id, TaskCompletionSource<JObject> tcs) { _id = id; _tcs = tcs; }
            public bool OnDevToolsMessage(IBrowser browser, Stream message) => false;
            public void OnDevToolsMethodResult(IBrowser browser, int messageId, bool success, Stream result)
            {
                if (messageId != _id) return;
                using var reader = new StreamReader(result);
                var json = reader.ReadToEnd();
                if (!success) _tcs.TrySetException(new Exception(json));
                else _tcs.TrySetResult(JsonConvert.DeserializeObject<JObject>(json) ?? new JObject());
            }
            public void OnDevToolsEvent(IBrowser browser, string method, Stream parameters) { }
            public void OnDevToolsAgentAttached(IBrowser browser) { }
            public void OnDevToolsAgentDetached(IBrowser browser) { }
            public void Dispose() { }
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
