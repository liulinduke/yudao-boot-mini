using System;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SocialMatrix.WpfHost.Services;

namespace SocialMatrix.WpfHost.Services
{
    /// <summary>
    /// 转帖脚本：点赞 / 转发到动态消息 / 好友 / 群组（各带附言）
    /// </summary>
    public class RepostScriptBuilder : FacebookScriptBuilder
    {
        private readonly string _postUrl;
        private readonly string _actionConfigJson;

        public RepostScriptBuilder(string postUrl, string actionConfigJson)
        {
            _postUrl = postUrl ?? "";
            _actionConfigJson = actionConfigJson ?? "{}";
        }

        public override string Build()
        {
            var config = JObject.Parse(_actionConfigJson);
            var actions = config["actions"]?.ToObject<int[]>() ?? Array.Empty<int>();
            var shareToFriendCount = config["shareToFriendCount"]?.Value<int>() ?? 10;
            var selectedGroups = config["selectedGroups"]?.ToObject<JArray>() ?? new JArray();
            var feedMessage = PickMessage(config["feedScripts"] as JArray, config["feedAppendRandomEmoji"]?.Value<bool>() ?? false);
            var friendMessage = PickMessage(config["friendScripts"] as JArray, config["friendAppendRandomEmoji"]?.Value<bool>() ?? false);
            var groupMessage = PickMessage(config["groupScripts"] as JArray, config["groupAppendRandomEmoji"]?.Value<bool>() ?? false);
            var commentMessage = config.Value<string>("finalCommentText")
                                 ?? config.Value<string>("commentScript")
                                 ?? PickMessage(config["commentScripts"] as JArray, config["commentAppendRandomEmoji"]?.Value<bool>() ?? false);

            BeginScript();
            AddRepostHelpers();
            _js.AppendLine($"            const feedMessage = {JsonConvert.SerializeObject(feedMessage)};");
            _js.AppendLine($"            const friendMessage = {JsonConvert.SerializeObject(friendMessage)};");
            _js.AppendLine($"            const groupMessage = {JsonConvert.SerializeObject(groupMessage)};");
            _js.AppendLine($"            const commentMessage = {JsonConvert.SerializeObject(commentMessage)};");
            _js.AppendLine($"            const TARGET_POST_URL = {JsonConvert.SerializeObject(_postUrl)};");
            _js.AppendLine("            const results = [];");
            _js.AppendLine("            console.log('[转帖] 开始执行, 帖子:', TARGET_POST_URL);");
            _js.AppendLine("            if (!(await waitForPostDialog(25000))) {");
            _js.AppendLine("                console.error('[转帖] 目标帖子未加载完成');");
            _js.AppendLine("                reject(JSON.stringify({ success: false, message: '目标帖子未加载完成，请确认链接有效或稍后重试', results }));");
            _js.AppendLine("                return;");
            _js.AppendLine("            }");
            _js.AppendLine("            await randomDelay(1500, 2500);");
            _js.AppendLine("");

            ExecuteActions(actions, shareToFriendCount, selectedGroups);

            _js.AppendLine("                console.log('[转帖] 所有操作完成, 结果数:', results.length);");
            _js.AppendLine("                resolve(JSON.stringify(results));");
            _js.AppendLine("");
            _js.AppendLine("            } catch (e) {");
            _js.AppendLine("                console.error('[转帖] 错误:', e);");
            _js.AppendLine("                reject(JSON.stringify({ success: false, message: e.message, results }));");
            _js.AppendLine("            }");
            _js.AppendLine("        })();");
            _js.AppendLine("    });");

            return _js.ToString();
        }

        private static string PickMessage(JArray? scripts, bool appendRandomEmoji)
        {
            if (scripts == null || scripts.Count == 0)
            {
                return string.Empty;
            }

            var candidates = scripts
                .Select(token => token?.ToString()?.Trim())
                .Where(text => !string.IsNullOrWhiteSpace(text))
                .ToList();

            if (candidates.Count == 0)
            {
                return string.Empty;
            }

            var random = new Random();
            var selected = candidates[random.Next(candidates.Count)];
            if (!appendRandomEmoji)
            {
                return selected;
            }

            string[] emojis =
            {
                "\U0001F600",
                "\U0001F604",
                "\U0001F60A",
                "\U0001F609",
                "\U0001F44D",
                "\U0001F525",
                "\U0001F44F",
                "\U0001F389",
                "\U0001F970",
                "\u2764\uFE0F"
            };
            var emojiCount = random.Next(1, 3);
            var suffix = string.Concat(Enumerable.Range(0, emojiCount).Select(_ => emojis[random.Next(emojis.Length)]));
            return $"{selected} {suffix}".Trim();
        }

        private void AddRepostHelpers()
        {
            _js.AppendLine("            let postRoot = null;");
            _js.AppendLine("            const normalizeText = (text) => (text || '').replace(/\\s+/g, ' ').trim().toLowerCase();");
            _js.AppendLine("            const isVisibleElement = (el) => {");
            _js.AppendLine("                if (!el) return false;");
            _js.AppendLine("                const rect = el.getBoundingClientRect();");
            _js.AppendLine("                const style = window.getComputedStyle(el);");
            _js.AppendLine("                return rect.width > 0 && rect.height > 0 && style.display !== 'none' && style.visibility !== 'hidden';");
            _js.AppendLine("            };");
            _js.AppendLine("            const normalizeGroupSearchKey = (raw) => {");
            _js.AppendLine("                let key = normalizeText(String(raw || ''));");
            _js.AppendLine("                key = key.replace(/^profile photo of\\s+/, '');");
            _js.AppendLine("                return key;");
            _js.AppendLine("            };");
            _js.AppendLine("            const SKIP_URL_SEGMENTS = new Set(['facebook', 'com', 'share', 'posts', 'groups', 'photo', 'videos', 'watch', 'permalink', 'permalink.php']);");
            _js.AppendLine("            const SKIP_QUERY_PARAMS = new Set(['id', 'rdid', 'ref', 'mibextid']);");
            _js.AppendLine("            const addPostKey = (keys, p) => {");
            _js.AppendLine("                const k = (p || '').toLowerCase().trim();");
            _js.AppendLine("                if (!k) return;");
            _js.AppendLine("                if (k.startsWith('pfbid') && k.length > 12) { keys.add(k); return; }");
            _js.AppendLine("                if (k.length >= 6 && !SKIP_URL_SEGMENTS.has(k)) keys.add(k);");
            _js.AppendLine("            };");
            _js.AppendLine("            const extractPostKeysFromUrl = (url) => {");
            _js.AppendLine("                const keys = new Set();");
            _js.AppendLine("                const raw = String(url || '').trim();");
            _js.AppendLine("                if (!raw) return [];");
            _js.AppendLine("                try {");
            _js.AppendLine("                    const u = new URL(raw, location.href);");
            _js.AppendLine("                    u.pathname.split('/').filter(Boolean).forEach(p => addPostKey(keys, p));");
            _js.AppendLine("                    const storyFbid = u.searchParams.get('story_fbid');");
            _js.AppendLine("                    if (storyFbid) addPostKey(keys, storyFbid);");
            _js.AppendLine("                    const shareUrl = u.searchParams.get('share_url');");
            _js.AppendLine("                    if (shareUrl) extractPostKeysFromUrl(shareUrl).forEach(k => keys.add(k));");
            _js.AppendLine("                    u.searchParams.forEach((v, name) => {");
            _js.AppendLine("                        if (!SKIP_QUERY_PARAMS.has(name.toLowerCase())) addPostKey(keys, v);");
            _js.AppendLine("                    });");
            _js.AppendLine("                } catch (e) {");
            _js.AppendLine("                    raw.split(/[/?#&=]/).filter(Boolean).forEach(p => addPostKey(keys, p));");
            _js.AppendLine("                }");
            _js.AppendLine("                return [...keys];");
            _js.AppendLine("            };");
            _js.AppendLine("            const collectPostKeys = () => {");
            _js.AppendLine("                const keys = new Set();");
            _js.AppendLine("                extractPostKeysFromUrl(TARGET_POST_URL).forEach(k => keys.add(k));");
            _js.AppendLine("                extractPostKeysFromUrl(location.href).forEach(k => keys.add(k));");
            _js.AppendLine("                return [...keys];");
            _js.AppendLine("            };");
            _js.AppendLine("            const isDedicatedPostPage = () => {");
            _js.AppendLine("                const pageUrl = normalizeText(location.href);");
            _js.AppendLine("                if (pageUrl.includes('/share/p/') || pageUrl.includes('permalink.php') || pageUrl.includes('/posts/') || pageUrl.includes('story_fbid=') || pageUrl.includes('pfbid')) return true;");
            _js.AppendLine("                const keys = collectPostKeys();");
            _js.AppendLine("                return keys.some(k => pageUrl.includes(k));");
            _js.AppendLine("            };");
            _js.AppendLine("            const hasPostUrlRedirected = () => {");
            _js.AppendLine("                const pageUrl = normalizeText(location.href);");
            _js.AppendLine("                if (pageUrl.includes('permalink.php') && pageUrl.includes('story_fbid=')) return true;");
            _js.AppendLine("                const originKeys = extractPostKeysFromUrl(TARGET_POST_URL);");
            _js.AppendLine("                return originKeys.some(k => pageUrl.includes(k));");
            _js.AppendLine("            };");
            _js.AppendLine("            const getMainArticles = () => {");
            _js.AppendLine("                const main = document.querySelector('[role=\"main\"]') || document.body;");
            _js.AppendLine("                return [...main.querySelectorAll('div[role=\"article\"]')].filter(isVisibleElement);");
            _js.AppendLine("            };");
            _js.AppendLine("            const articleMatchesPost = (article, keys) => {");
            _js.AppendLine("                if (!article || keys.length === 0) return false;");
            _js.AppendLine("                const blob = normalizeText((article.innerText || '') + ' ' + [...article.querySelectorAll('a[href]')].map(a => a.href || '').join(' '));");
            _js.AppendLine("                return keys.some(k => blob.includes(k));");
            _js.AppendLine("            };");
            _js.AppendLine("            const findTargetPostArticle = () => {");
            _js.AppendLine("                const keys = collectPostKeys();");
            _js.AppendLine("                const dedicated = isDedicatedPostPage();");
            _js.AppendLine("                const articles = [...document.querySelectorAll('div[role=\"article\"]')].filter(isVisibleElement);");
            _js.AppendLine("                const mainArticles = getMainArticles();");
            _js.AppendLine("                if (keys.length > 0) {");
            _js.AppendLine("                    const matched = articles.filter(a => articleMatchesPost(a, keys));");
            _js.AppendLine("                    if (matched.length > 0) {");
            _js.AppendLine("                        matched.sort((a, b) => a.getBoundingClientRect().top - b.getBoundingClientRect().top);");
            _js.AppendLine("                        return matched[0];");
            _js.AppendLine("                    }");
            _js.AppendLine("                }");
            _js.AppendLine("                if (dedicated) {");
            _js.AppendLine("                    for (const a of mainArticles) {");
            _js.AppendLine("                        if (findShareButtonInRoot(a)) return a;");
            _js.AppendLine("                    }");
            _js.AppendLine("                    if (mainArticles.length > 0) return mainArticles[0];");
            _js.AppendLine("                }");
            _js.AppendLine("                if (articles.length === 1) return articles[0];");
            _js.AppendLine("                if (dedicated && articles.length > 0) {");
            _js.AppendLine("                    for (const a of articles) {");
            _js.AppendLine("                        if (findShareButtonInRoot(a)) return a;");
            _js.AppendLine("                    }");
            _js.AppendLine("                }");
            _js.AppendLine("                return null;");
            _js.AppendLine("            };");
            _js.AppendLine("            const findShareButtonInRoot = (root) => {");
            _js.AppendLine("                if (!root) return null;");
            _js.AppendLine("                const labels = ['send this to friends or post it on your profile', '发送给好友或发布到你的个人主页', '分享给好友或发布到你的主页'];");
            _js.AppendLine("                for (const btn of root.querySelectorAll('[role=\"button\"]')) {");
            _js.AppendLine("                    if (!isVisibleElement(btn)) continue;");
            _js.AppendLine("                    const aria = normalizeText(btn.getAttribute('aria-label'));");
            _js.AppendLine("                    if (labels.some(l => aria.includes(l))) return btn;");
            _js.AppendLine("                }");
            _js.AppendLine("                return null;");
            _js.AppendLine("            };");
            _js.AppendLine("            const getVisibleDialogs = () => [...document.querySelectorAll('[role=\"dialog\"]')].filter(isVisibleElement);");
            _js.AppendLine("            const isMessengerOrGroupPickerOnly = (dialog) => {");
            _js.AppendLine("                if (!dialog) return false;");
            _js.AppendLine("                const t = normalizeText(dialog.textContent);");
            _js.AppendLine("                const hasGroupSearch = t.includes('search for groups') || t.includes('搜索群组');");
            _js.AppendLine("                const hasPostShareBtn = !!findShareButtonInRoot(dialog);");
            _js.AppendLine("                return hasGroupSearch && !hasPostShareBtn;");
            _js.AppendLine("            };");
            _js.AppendLine("            const getPostPreviewDialog = () => {");
            _js.AppendLine("                const dialogs = getVisibleDialogs();");
            _js.AppendLine("                for (let i = dialogs.length - 1; i >= 0; i--) {");
            _js.AppendLine("                    const dlg = dialogs[i];");
            _js.AppendLine("                    if (isMessengerOrGroupPickerOnly(dlg)) continue;");
            _js.AppendLine("                    if (findShareButtonInRoot(dlg)) return dlg;");
            _js.AppendLine("                }");
            _js.AppendLine("                return null;");
            _js.AppendLine("            };");
            _js.AppendLine("            const waitForPostDialog = async (timeoutMs = 25000) => {");
            _js.AppendLine("                const start = Date.now();");
            _js.AppendLine("                let attempt = 0;");
            _js.AppendLine("                while (Date.now() - start < timeoutMs) {");
            _js.AppendLine("                    attempt++;");
            _js.AppendLine("                    const previewDialog = getPostPreviewDialog();");
            _js.AppendLine("                    const shareBtn = previewDialog ? findShareButtonInRoot(previewDialog) : null;");
            _js.AppendLine("                    if (attempt === 1 || attempt % 4 === 0) {");
            _js.AppendLine("                        console.log('[转帖] 等待帖子预览弹窗', attempt, 'url=', (location.href || '').slice(0, 120),");
            _js.AppendLine("                            'keys=', collectPostKeys().slice(0, 3).join(','), 'redirected=', hasPostUrlRedirected(),");
            _js.AppendLine("                            'dialogs=', getVisibleDialogs().length, 'preview=', !!previewDialog, 'shareBtn=', !!shareBtn);");
            _js.AppendLine("                    }");
            _js.AppendLine("                    if (previewDialog && shareBtn) {");
            _js.AppendLine("                        previewDialog.scrollIntoView({ block: 'center', inline: 'nearest' });");
            _js.AppendLine("                        await randomDelay(800, 1200);");
            _js.AppendLine("                        postRoot = previewDialog;");
            _js.AppendLine("                        console.log('[转帖] 帖子预览弹窗已就绪');");
            _js.AppendLine("                        return true;");
            _js.AppendLine("                    }");
            _js.AppendLine("                    if (isDedicatedPostPage()) {");
            _js.AppendLine("                        const article = findTargetPostArticle();");
            _js.AppendLine("                        const articleShareBtn = article ? findShareButtonInRoot(article) : null;");
            _js.AppendLine("                        if (article && articleShareBtn) {");
            _js.AppendLine("                            article.scrollIntoView({ block: 'center', inline: 'nearest' });");
            _js.AppendLine("                            await randomDelay(800, 1200);");
            _js.AppendLine("                            postRoot = article;");
            _js.AppendLine("                            console.log('[转帖] 专用帖子页已就绪(无预览弹窗)');");
            _js.AppendLine("                            return true;");
            _js.AppendLine("                        }");
            _js.AppendLine("                    }");
            _js.AppendLine("                    await randomDelay(600, 1000);");
            _js.AppendLine("                }");
            _js.AppendLine("                console.warn('[转帖] 等待帖子预览弹窗超时');");
            _js.AppendLine("                return false;");
            _js.AppendLine("            };");
            _js.AppendLine("            const detectRetryTooEarly = () => {");
            _js.AppendLine("                const keywords = ['Retried Too Early', 'try again later', '请稍后再试', '操作过快', 'slow down'];");
            _js.AppendLine("                const text = normalizeText(document.body?.innerText || '');");
            _js.AppendLine("                if (keywords.some(k => text.includes(k))) return true;");
            _js.AppendLine("                for (const dlg of getVisibleDialogs()) {");
            _js.AppendLine("                    const dt = normalizeText(dlg.textContent || '');");
            _js.AppendLine("                    if (keywords.some(k => dt.includes(k))) return true;");
            _js.AppendLine("                }");
            _js.AppendLine("                return false;");
            _js.AppendLine("            };");
            _js.AppendLine("            const waitThroughRetryTooEarly = async (maxWaitMs = 45000) => {");
            _js.AppendLine("                if (!detectRetryTooEarly()) return true;");
            _js.AppendLine("                console.warn('[转帖] 检测到 Retried Too Early，等待冷却...');");
            _js.AppendLine("                const cooldownStart = Date.now();");
            _js.AppendLine("                while (Date.now() - cooldownStart < maxWaitMs) {");
            _js.AppendLine("                    await randomDelay(4000, 6000);");
            _js.AppendLine("                    if (!detectRetryTooEarly()) {");
            _js.AppendLine("                        console.log('[转帖] 限流提示已消失');");
            _js.AppendLine("                        await randomDelay(2000, 3500);");
            _js.AppendLine("                        return true;");
            _js.AppendLine("                    }");
            _js.AppendLine("                }");
            _js.AppendLine("                return false;");
            _js.AppendLine("            };");
            _js.AppendLine("            const matchesShareNow = (aria, text) => {");
            _js.AppendLine("                const a = normalizeText(aria);");
            _js.AppendLine("                const t = normalizeText(text);");
            _js.AppendLine("                return a === 'share now' || t === 'share now' || a.includes('share now') || t.includes('share now') || a === '立即分享' || t === '立即分享';");
            _js.AppendLine("            };");
            _js.AppendLine("            const matchesMessengerSend = (aria, text) => {");
            _js.AppendLine("                const a = normalizeText(aria);");
            _js.AppendLine("                const t = normalizeText(text);");
            _js.AppendLine("                return a === 'send' || t === 'send' || a === '发送' || t === '发送';");
            _js.AppendLine("            };");
            _js.AppendLine("            const isShareComposerDialog = (dialog) => {");
            _js.AppendLine("                if (!dialog || !isVisibleElement(dialog)) return false;");
            _js.AppendLine("                if (postRoot && dialog === postRoot) return false;");
            _js.AppendLine("                const t = normalizeText(dialog.textContent);");
            _js.AppendLine("                if (t.includes('share now') || t.includes('share to a group')) return true;");
            _js.AppendLine("                if (t.includes('send to') && t.includes('messenger')) return true;");
            _js.AppendLine("                if (t.includes('search for groups')) return true;");
            _js.AppendLine("                for (const btn of dialog.querySelectorAll('[role=\"button\"]')) {");
            _js.AppendLine("                    if (!isVisibleElement(btn)) continue;");
            _js.AppendLine("                    const aria = btn.getAttribute('aria-label') || '';");
            _js.AppendLine("                    const text = (btn.textContent || '').trim();");
            _js.AppendLine("                    if (matchesShareNow(aria, text)) return true;");
            _js.AppendLine("                    const a = normalizeText(aria);");
            _js.AppendLine("                    if (a === 'share to a group' || normalizeText(text) === 'group') return true;");
            _js.AppendLine("                    if (a.includes('send to') && a.includes('via messenger')) return true;");
            _js.AppendLine("                }");
            _js.AppendLine("                return false;");
            _js.AppendLine("            };");
            _js.AppendLine("            const getShareComposerDialog = () => {");
            _js.AppendLine("                const dialogs = getVisibleDialogs();");
            _js.AppendLine("                for (let i = dialogs.length - 1; i >= 0; i--) {");
            _js.AppendLine("                    const dlg = dialogs[i];");
            _js.AppendLine("                    if (postRoot && dlg === postRoot) continue;");
            _js.AppendLine("                    if (isShareComposerDialog(dlg)) return dlg;");
            _js.AppendLine("                }");
            _js.AppendLine("                return null;");
            _js.AppendLine("            };");
            _js.AppendLine("            const closeShareComposer = async () => {");
            _js.AppendLine("                for (let i = 0; i < 3; i++) {");
            _js.AppendLine("                    const composer = getShareComposerDialog();");
            _js.AppendLine("                    if (!composer) return;");
            _js.AppendLine("                    const closeBtn = composer.querySelector('[aria-label=\"Close\"], [aria-label=\"关闭\"]');");
            _js.AppendLine("                    if (closeBtn && isVisibleElement(closeBtn)) {");
            _js.AppendLine("                        await humanClick(closeBtn);");
            _js.AppendLine("                        await randomDelay(500, 800);");
            _js.AppendLine("                    } else break;");
            _js.AppendLine("                }");
            _js.AppendLine("            };");
            _js.AppendLine("            const isClickableButton = (btn) => {");
            _js.AppendLine("                if (!isVisibleElement(btn)) return false;");
            _js.AppendLine("                if (btn.getAttribute('aria-disabled') === 'true') return false;");
            _js.AppendLine("                if (btn.hasAttribute('disabled')) return false;");
            _js.AppendLine("                return true;");
            _js.AppendLine("            };");
            _js.AppendLine("            const findMainLikeButton = () => {");
            _js.AppendLine("                if (!postRoot) return null;");
            _js.AppendLine("                for (const btn of postRoot.querySelectorAll('[role=\"button\"][aria-label=\"Like\" i], [role=\"button\"][aria-label=\"赞\" i]')) {");
            _js.AppendLine("                    if (isVisibleElement(btn)) return btn;");
            _js.AppendLine("                }");
            _js.AppendLine("                return null;");
            _js.AppendLine("            };");
            _js.AppendLine("            const getVisibleTextboxes = (root) => [...(root?.querySelectorAll('[role=\"textbox\"], div[contenteditable=\"true\"], textarea') || [])].filter(isVisibleElement);");
            _js.AppendLine("            const isCommentTextbox = (el) => {");
            _js.AppendLine("                if (!el || !isVisibleElement(el)) return false;");
            _js.AppendLine("                const aria = normalizeText(el.getAttribute('aria-label'));");
            _js.AppendLine("                const ariaPlaceholder = normalizeText(el.getAttribute('aria-placeholder'));");
            _js.AppendLine("                const placeholder = normalizeText(el.getAttribute('placeholder'));");
            _js.AppendLine("                const text = normalizeText(el.textContent || '');");
            _js.AppendLine("                const blob = `${aria} ${ariaPlaceholder} ${placeholder} ${text}`;");
            _js.AppendLine("                return blob.includes('comment') || blob.includes('leave a comment') || blob.includes('write a comment') || blob.includes('发表评论') || blob.includes('评论');");
            _js.AppendLine("            };");
            _js.AppendLine("            const findCommentTextbox = () => {");
            _js.AppendLine("                const roots = [postRoot, ...getVisibleDialogs(), document.querySelector('[role=\"main\"]'), document.body].filter(Boolean);");
            _js.AppendLine("                for (const root of roots) {");
            _js.AppendLine("                    const boxes = getVisibleTextboxes(root);");
            _js.AppendLine("                    for (const box of boxes) {");
            _js.AppendLine("                        if (isCommentTextbox(box)) return box;");
            _js.AppendLine("                    }");
            _js.AppendLine("                }");
            _js.AppendLine("                return null;");
            _js.AppendLine("            };");
            _js.AppendLine("            const findCommentButton = () => {");
            _js.AppendLine("                const roots = [postRoot, ...getVisibleDialogs(), document.querySelector('[role=\"main\"]'), document.body].filter(Boolean);");
            _js.AppendLine("                for (const root of roots) {");
            _js.AppendLine("                    const candidates = [...root.querySelectorAll('[role=\"button\"], a[href], div[aria-label], span[aria-label]')];");
            _js.AppendLine("                    for (const el of candidates) {");
            _js.AppendLine("                        if (!isVisibleElement(el)) continue;");
            _js.AppendLine("                        const aria = normalizeText(el.getAttribute('aria-label'));");
            _js.AppendLine("                        const text = normalizeText(el.textContent || '');");
            _js.AppendLine("                        const blob = `${aria} ${text}`;");
            _js.AppendLine("                        if (blob.includes('post comment') || blob.includes('comment') || blob.includes('leave a comment') || blob.includes('write a comment') || blob.includes('评论')) return el;");
            _js.AppendLine("                    }");
            _js.AppendLine("                }");
            _js.AppendLine("                return null;");
            _js.AppendLine("            };");
            _js.AppendLine("            const getCommentSurfaceRoot = () => postRoot || [...getVisibleDialogs()].pop() || document.querySelector('[role=\"main\"]') || document.body;");
            _js.AppendLine("            const getCommentSurfaceText = () => {");
            _js.AppendLine("                const root = getCommentSurfaceRoot();");
            _js.AppendLine("                if (!root) return '';");
            _js.AppendLine("                try {");
            _js.AppendLine("                    const clone = root.cloneNode(true);");
            _js.AppendLine("                    clone.querySelectorAll('[role=\"textbox\"], [contenteditable=\"true\"], textarea').forEach(el => el.remove());");
            _js.AppendLine("                    return normalizeText(clone.innerText || clone.textContent || '');");
            _js.AppendLine("                } catch (e) {");
            _js.AppendLine("                    return normalizeText(root.innerText || root.textContent || '');");
            _js.AppendLine("                }");
            _js.AppendLine("            };");
            _js.AppendLine("            const ensureCommentEditor = async () => {");
            _js.AppendLine("                let editor = findCommentTextbox();");
            _js.AppendLine("                if (editor) return editor;");
            _js.AppendLine("                const commentBtn = findCommentButton();");
            _js.AppendLine("                if (!commentBtn) {");
            _js.AppendLine("                    console.warn('[转帖评论] 未找到评论入口');");
            _js.AppendLine("                    return null;");
            _js.AppendLine("                }");
            _js.AppendLine("                await humanClick(commentBtn);");
            _js.AppendLine("                await randomDelay(1200, 2200);");
            _js.AppendLine("                const start = Date.now();");
            _js.AppendLine("                while (Date.now() - start < 10000) {");
            _js.AppendLine("                    editor = findCommentTextbox();");
            _js.AppendLine("                    if (editor) return editor;");
            _js.AppendLine("                    await randomDelay(400, 700);");
            _js.AppendLine("                }");
            _js.AppendLine("                return null;");
            _js.AppendLine("            };");
            _js.AppendLine("            const commentTextAppears = (text, beforeSurfaceText = '') => {");
            _js.AppendLine("                const normalized = normalizeText(text);");
            _js.AppendLine("                if (!normalized) return false;");
            _js.AppendLine("                const surfaceText = getCommentSurfaceText();");
            _js.AppendLine("                if (!surfaceText.includes(normalized)) return false;");
            _js.AppendLine("                return !beforeSurfaceText || !beforeSurfaceText.includes(normalized) || surfaceText.length > beforeSurfaceText.length;");
            _js.AppendLine("            };");
            _js.AppendLine("            const submitCommentByKeyboard = async (editor) => {");
            _js.AppendLine("                if (!editor) return false;");
            _js.AppendLine("                editor.focus();");
            _js.AppendLine("                await randomDelay(200, 400);");
            _js.AppendLine("                editor.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', code: 'Enter', which: 13, keyCode: 13, bubbles: true }));");
            _js.AppendLine("                editor.dispatchEvent(new KeyboardEvent('keypress', { key: 'Enter', code: 'Enter', which: 13, keyCode: 13, bubbles: true }));");
            _js.AppendLine("                editor.dispatchEvent(new KeyboardEvent('keyup', { key: 'Enter', code: 'Enter', which: 13, keyCode: 13, bubbles: true }));");
            _js.AppendLine("                await randomDelay(1500, 2500);");
            _js.AppendLine("                return true;");
            _js.AppendLine("            };");
            _js.AppendLine("            const clickCommentSubmit = async (editor) => {");
            _js.AppendLine("                const roots = [];");
            _js.AppendLine("                if (editor) {");
            _js.AppendLine("                    let current = editor.parentElement;");
            _js.AppendLine("                    for (let i = 0; i < 6 && current; i++, current = current.parentElement) roots.push(current);");
            _js.AppendLine("                }");
            _js.AppendLine("                roots.push(postRoot, ...getVisibleDialogs(), document.body);");
            _js.AppendLine("                for (const root of roots) {");
            _js.AppendLine("                    if (!root) continue;");
            _js.AppendLine("                    for (const btn of root.querySelectorAll('[role=\"button\"]')) {");
            _js.AppendLine("                        if (!isClickableButton(btn)) continue;");
            _js.AppendLine("                        const aria = normalizeText(btn.getAttribute('aria-label'));");
            _js.AppendLine("                        const text = normalizeText(btn.textContent || '');");
            _js.AppendLine("                        const blob = `${aria} ${text}`;");
            _js.AppendLine("                        if (blob.includes('leave a comment') || blob.includes('write a comment') || blob.includes('comment with') || blob.includes('insert an emoji') || blob.includes('attach a photo or video') || blob.includes('comment with a gif') || blob.includes('comment with a sticker')) continue;");
            _js.AppendLine("                        if (blob === 'comment' || blob.includes('post comment') || blob.includes('发表评论') || blob.includes('发布评论') || blob.includes('send comment')) {");
            _js.AppendLine("                            await humanClick(btn);");
            _js.AppendLine("                            await randomDelay(1500, 2500);");
            _js.AppendLine("                            return true;");
            _js.AppendLine("                        }");
            _js.AppendLine("                    }");
            _js.AppendLine("                }");
            _js.AppendLine("                return false;");
            _js.AppendLine("            };");
            _js.AppendLine("            const clickButtonInRoot = async (root, matcher) => {");
            _js.AppendLine("                if (!root) return false;");
            _js.AppendLine("                for (const btn of root.querySelectorAll('[role=\"button\"]')) {");
            _js.AppendLine("                    if (!isClickableButton(btn)) continue;");
            _js.AppendLine("                    const aria = btn.getAttribute('aria-label') || '';");
            _js.AppendLine("                    const text = (btn.textContent || '').trim();");
            _js.AppendLine("                    if (matcher(aria, text, btn)) {");
            _js.AppendLine("                        await humanClick(btn);");
            _js.AppendLine("                        await randomDelay(1200, 2000);");
            _js.AppendLine("                        return true;");
            _js.AppendLine("                    }");
            _js.AppendLine("                }");
            _js.AppendLine("                return false;");
            _js.AppendLine("            };");
            _js.AppendLine("            const clickButtonInComposer = async (matcher) => {");
            _js.AppendLine("                const composer = getShareComposerDialog();");
            _js.AppendLine("                if (composer && await clickButtonInRoot(composer, matcher)) return true;");
            _js.AppendLine("                return clickButtonInRoot(getShareComposerDialog(), matcher);");
            _js.AppendLine("            };");
            _js.AppendLine("            const clickDialogButton = async (matcher) => clickButtonInComposer(matcher);");
            _js.AppendLine("            const openShareDialog = async () => {");
            _js.AppendLine("                await closeShareComposer();");
            _js.AppendLine("                if (!postRoot) {");
            _js.AppendLine("                    console.warn('[转帖] postRoot 未就绪');");
            _js.AppendLine("                    return false;");
            _js.AppendLine("                }");
            _js.AppendLine("                postRoot.scrollIntoView({ block: 'center', inline: 'nearest' });");
            _js.AppendLine("                await randomDelay(800, 1500);");
            _js.AppendLine("                const btn = findShareButtonInRoot(postRoot);");
            _js.AppendLine("                if (!btn) {");
            _js.AppendLine("                    console.warn('[转帖] postRoot 内未找到分享按钮');");
            _js.AppendLine("                    return false;");
            _js.AppendLine("                }");
            _js.AppendLine("                await humanClick(btn);");
            _js.AppendLine("                await randomDelay(1500, 2500);");
            _js.AppendLine("                const waitStart = Date.now();");
            _js.AppendLine("                while (Date.now() - waitStart < 6000) {");
            _js.AppendLine("                    if (getShareComposerDialog()) {");
            _js.AppendLine("                        console.log('[转帖] 分享面板已打开');");
            _js.AppendLine("                        return true;");
            _js.AppendLine("                    }");
            _js.AppendLine("                    await randomDelay(400, 700);");
            _js.AppendLine("                }");
            _js.AppendLine("                console.warn('[转帖] 分享面板未出现');");
            _js.AppendLine("                return false;");
            _js.AppendLine("            };");
            _js.AppendLine("            const getEditorText = (editor) => (editor?.innerText || editor?.textContent || '').trim();");
            _js.AppendLine("            const findVisibleShareEditor = (root) => {");
            _js.AppendLine("                if (!root) return null;");
            _js.AppendLine("                const editors = [...root.querySelectorAll('div[role=\"textbox\"][contenteditable=\"true\"]')].filter(isVisibleElement);");
            _js.AppendLine("                return editors.length > 0 ? editors[editors.length - 1] : null;");
            _js.AppendLine("            };");
            _js.AppendLine("            const blurActiveEditor = async () => {");
            _js.AppendLine("                const active = document.activeElement;");
            _js.AppendLine("                if (active && active !== document.body) active.blur();");
            _js.AppendLine("                await randomDelay(300, 600);");
            _js.AppendLine("            };");
            _js.AppendLine("            const matchesGroupPost = (aria, text) => {");
            _js.AppendLine("                const a = normalizeText(aria);");
            _js.AppendLine("                const t = normalizeText(text);");
            _js.AppendLine("                if (matchesShareNow(aria, text)) return false;");
            _js.AppendLine("                return a === 'post' || t === 'post' || a === '发帖' || t === '发帖' || a === '发布' || t === '发布';");
            _js.AppendLine("            };");
            _js.AppendLine("            const findGroupPostButton = () => {");
            _js.AppendLine("                const dialogs = getVisibleDialogs();");
            _js.AppendLine("                for (let i = dialogs.length - 1; i >= 0; i--) {");
            _js.AppendLine("                    if (postRoot && dialogs[i] === postRoot) continue;");
            _js.AppendLine("                    for (const btn of dialogs[i].querySelectorAll('[role=\"button\"]')) {");
            _js.AppendLine("                        if (!isClickableButton(btn)) continue;");
            _js.AppendLine("                        const aria = btn.getAttribute('aria-label') || '';");
            _js.AppendLine("                        const text = (btn.textContent || '').trim();");
            _js.AppendLine("                        if (matchesGroupPost(aria, text)) return btn;");
            _js.AppendLine("                    }");
            _js.AppendLine("                }");
            _js.AppendLine("                return null;");
            _js.AppendLine("            };");
            _js.AppendLine("            const clickShareNow = async () => {");
            _js.AppendLine("                await blurActiveEditor();");
            _js.AppendLine("                const clicked = await clickButtonInComposer((aria, text) => matchesShareNow(aria, text));");
            _js.AppendLine("                if (!clicked) console.warn('[转帖] 未找到 Share now 按钮');");
            _js.AppendLine("                return clicked;");
            _js.AppendLine("            };");
            _js.AppendLine("            const clickGroupPost = async () => {");
            _js.AppendLine("                await blurActiveEditor();");
            _js.AppendLine("                const btn = findGroupPostButton();");
            _js.AppendLine("                if (!btn) {");
            _js.AppendLine("                    console.warn('[转帖] 未找到 Post 按钮');");
            _js.AppendLine("                    return false;");
            _js.AppendLine("                }");
            _js.AppendLine("                await humanClick(btn);");
            _js.AppendLine("                await randomDelay(1500, 2500);");
            _js.AppendLine("                return true;");
            _js.AppendLine("            };");
            _js.AppendLine("            const waitForGroupPostButton = async (timeoutMs = 8000) => {");
            _js.AppendLine("                const start = Date.now();");
            _js.AppendLine("                while (Date.now() - start < timeoutMs) {");
            _js.AppendLine("                    if (findGroupPostButton()) return true;");
            _js.AppendLine("                    await randomDelay(400, 700);");
            _js.AppendLine("                }");
            _js.AppendLine("                return false;");
            _js.AppendLine("            };");
            _js.AppendLine("            const clickMessengerSend = async () => {");
            _js.AppendLine("                await blurActiveEditor();");
            _js.AppendLine("                const clicked = await clickButtonInComposer((aria, text) => matchesMessengerSend(aria, text));");
            _js.AppendLine("                if (!clicked) console.warn('[转帖] 未找到 Messenger Send 按钮');");
            _js.AppendLine("                return clicked;");
            _js.AppendLine("            };");
            _js.AppendLine("            const inputShareText = async (editor, messageText) => {");
            _js.AppendLine("                if (!messageText || !String(messageText).trim()) return true;");
            _js.AppendLine("                if (!editor) return false;");
            _js.AppendLine("                const expectedText = String(messageText).trim();");
            _js.AppendLine("                const clearEditor = async () => {");
            _js.AppendLine("                    editor.focus();");
            _js.AppendLine("                    await randomDelay(200, 400);");
            _js.AppendLine("                    try {");
            _js.AppendLine("                        if (typeof editor.value === 'string') {");
            _js.AppendLine("                            editor.value = '';");
            _js.AppendLine("                            editor.dispatchEvent(new Event('input', { bubbles: true }));");
            _js.AppendLine("                            return;");
            _js.AppendLine("                        }");
            _js.AppendLine("                        if (editor.isContentEditable) {");
            _js.AppendLine("                            const sel = window.getSelection();");
            _js.AppendLine("                            const range = document.createRange();");
            _js.AppendLine("                            range.selectNodeContents(editor);");
            _js.AppendLine("                            sel.removeAllRanges();");
            _js.AppendLine("                            sel.addRange(range);");
            _js.AppendLine("                            document.execCommand('delete', false);");
            _js.AppendLine("                            editor.innerHTML = '';");
            _js.AppendLine("                            editor.textContent = '';");
            _js.AppendLine("                        }");
            _js.AppendLine("                    } catch (e) { console.warn('[转帖] 清空输入框失败', e); }");
            _js.AppendLine("                };");
            _js.AppendLine("                const readTyped = () => getEditorText(editor).trim();");
            _js.AppendLine("                await clearEditor();");
            _js.AppendLine("                await randomDelay(250, 450);");
            _js.AppendLine("                let typed = '';");
            _js.AppendLine("                try {");
            _js.AppendLine("                    if (typeof editor.value === 'string') {");
            _js.AppendLine("                        editor.value = messageText;");
            _js.AppendLine("                        editor.dispatchEvent(new Event('input', { bubbles: true }));");
            _js.AppendLine("                    } else {");
            _js.AppendLine("                        document.execCommand('insertText', false, messageText);");
            _js.AppendLine("                    }");
            _js.AppendLine("                    await randomDelay(250, 450);");
            _js.AppendLine("                    typed = readTyped();");
            _js.AppendLine("                } catch (e) { console.warn('[转帖] insertText 整段失败', e); }");
            _js.AppendLine("                if (typed !== expectedText) {");
            _js.AppendLine("                    await clearEditor();");
            _js.AppendLine("                    await randomDelay(200, 350);");
            _js.AppendLine("                    for (const ch of messageText) {");
            _js.AppendLine("                        if (typeof editor.value === 'string') {");
            _js.AppendLine("                            editor.value += ch;");
            _js.AppendLine("                            editor.dispatchEvent(new Event('input', { bubbles: true }));");
            _js.AppendLine("                        } else {");
            _js.AppendLine("                            document.execCommand('insertText', false, ch);");
            _js.AppendLine("                        }");
            _js.AppendLine("                        await randomDelay(30, 80);");
            _js.AppendLine("                    }");
            _js.AppendLine("                    await randomDelay(250, 450);");
            _js.AppendLine("                    typed = readTyped();");
            _js.AppendLine("                }");
            _js.AppendLine("                console.log('[转帖] 附言输入结果:', typed);");
            _js.AppendLine("                return typed === expectedText;");
            _js.AppendLine("            };");
            _js.AppendLine("            const submitComment = async () => {");
            _js.AppendLine("                if (!commentMessage || !String(commentMessage).trim()) return shareFail('评论话术为空');");
            _js.AppendLine("                const editor = await ensureCommentEditor();");
            _js.AppendLine("                if (!editor) return shareFail('未找到评论输入框');");
            _js.AppendLine("                const beforeSubmitSurface = getCommentSurfaceText();");
            _js.AppendLine("                if (!(await inputShareText(editor, commentMessage))) return shareFail('评论内容输入失败');");
            _js.AppendLine("                const beforeSubmitVisible = getEditorText(editor);");
            _js.AppendLine("                const clicked = await clickCommentSubmit(editor);");
            _js.AppendLine("                if (!clicked) {");
            _js.AppendLine("                    await submitCommentByKeyboard(editor);");
            _js.AppendLine("                }");
            _js.AppendLine("                const start = Date.now();");
            _js.AppendLine("                while (Date.now() - start < 12000) {");
            _js.AppendLine("                    const currentText = getEditorText(editor);");
            _js.AppendLine("                    if (!currentText || currentText.length < beforeSubmitVisible.length / 2) {");
            _js.AppendLine("                        return shareOk(1, commentMessage);");
            _js.AppendLine("                    }");
            _js.AppendLine("                    if (commentTextAppears(commentMessage, beforeSubmitSurface)) {");
            _js.AppendLine("                        return shareOk(1, commentMessage);");
            _js.AppendLine("                    }");
            _js.AppendLine("                    if (detectRetryTooEarly()) return shareFail('评论限流(Retried Too Early)');");
            _js.AppendLine("                    await randomDelay(500, 900);");
            _js.AppendLine("                }");
            _js.AppendLine("                return shareFail('评论提交后未确认成功');");
            _js.AppendLine("            };");
            _js.AppendLine("            const submitGroupPost = async () => {");
            _js.AppendLine("                await randomDelay(1500, 2500);");
            _js.AppendLine("                if (detectRetryTooEarly() && !(await waitThroughRetryTooEarly())) {");
            _js.AppendLine("                    return shareFail('群组发帖限流(Retried Too Early)');");
            _js.AppendLine("                }");
            _js.AppendLine("                if (!(await clickGroupPost())) return shareFail('未找到 Post');");
            _js.AppendLine("                await randomDelay(2000, 3000);");
            _js.AppendLine("                if (detectRetryTooEarly()) {");
            _js.AppendLine("                    console.warn('[转帖] 群组 Post 触发限流，冷却后重试一次');");
            _js.AppendLine("                    if (!(await waitThroughRetryTooEarly())) return shareFail('群组发帖限流(Retried Too Early)');");
            _js.AppendLine("                    await randomDelay(1500, 2500);");
            _js.AppendLine("                    if (!(await clickGroupPost())) return shareFail('群组 Post 重试失败');");
            _js.AppendLine("                    await randomDelay(2000, 3000);");
            _js.AppendLine("                    if (detectRetryTooEarly()) return shareFail('群组发帖限流(Retried Too Early)');");
            _js.AppendLine("                }");
            _js.AppendLine("                if (await detectGroupPostPendingApproval()) {");
            _js.AppendLine("                    console.log('[转帖] 群组发帖已提交，待管理员审核');");
            _js.AppendLine("                    return shareOk(3, '已提交，待群管理员审核');");
            _js.AppendLine("                }");
            _js.AppendLine("                return shareOk(1);");
            _js.AppendLine("            };");
            _js.AppendLine("            const performShare = async (target, arg) => {");
            _js.AppendLine("                try {");
            _js.AppendLine("                    if (!await openShareDialog()) return shareFail('打开分享面板失败');");
            _js.AppendLine("                    const composer = getShareComposerDialog();");
            _js.AppendLine("                    if (!composer) return shareFail('分享面板未就绪');");
            _js.AppendLine("                    if (target === 'feed') {");
            _js.AppendLine("                        if (feedMessage && !(await fillShareMessage(feedMessage))) return shareFail('动态附言输入失败');");
            _js.AppendLine("                        return (await clickShareNow()) ? shareOk(1) : shareFail('未找到 Share now');");
            _js.AppendLine("                    }");
            _js.AppendLine("                    if (target === 'friend') {");
            _js.AppendLine("                        if (friendMessage && !(await fillShareMessage(friendMessage))) return shareFail('好友附言输入失败');");
            _js.AppendLine("                        const idx = typeof arg === 'number' ? arg : 0;");
            _js.AppendLine("                        const findFriendBtns = (root) => [...(root?.querySelectorAll('[role=\"button\"]') || [])].filter(btn => {");
            _js.AppendLine("                            if (!isClickableButton(btn)) return false;");
            _js.AppendLine("                            const aria = btn.getAttribute('aria-label') || '';");
            _js.AppendLine("                            return aria.includes('Send to') && aria.includes('via Messenger');");
            _js.AppendLine("                        });");
            _js.AppendLine("                        let pick = findFriendBtns(composer)[idx];");
            _js.AppendLine("                        if (!pick) {");
            _js.AppendLine("                            await clickDialogButton((aria, text) => aria.includes('More Messenger contacts') || text === 'More');");
            _js.AppendLine("                            await randomDelay(1500, 2500);");
            _js.AppendLine("                            const composer2 = getShareComposerDialog();");
            _js.AppendLine("                            const moreFriends = findFriendBtns(composer2);");
            _js.AppendLine("                            pick = moreFriends[idx] || moreFriends[0];");
            _js.AppendLine("                        }");
            _js.AppendLine("                        if (!pick) return shareFail('未找到好友 Send');");
            _js.AppendLine("                        await humanClick(pick);");
            _js.AppendLine("                        await randomDelay(1800, 2800);");
            _js.AppendLine("                        if (friendMessage && !(await fillShareMessage(friendMessage))) return shareFail('好友附言输入失败');");
            _js.AppendLine("                        return (await clickMessengerSend()) ? shareOk(1) : shareFail('未找到好友 Send 确认按钮');");
            _js.AppendLine("                    }");
            _js.AppendLine("                    if (target === 'group') {");
            _js.AppendLine("                        if (!await clickDialogButton((aria, text) => aria === 'Share to a group' || text === 'Group')) return shareFail('未找到 Share to a group');");
            _js.AppendLine("                        await randomDelay(2000, 3000);");
            _js.AppendLine("                        const rawKey = String(arg || '');");
            _js.AppendLine("                        const key = normalizeGroupSearchKey(rawKey);");
            _js.AppendLine("                        const searchKey = key.includes('community') ? key.split('community')[0].trim() : (key.split(',')[0] || key);");
            _js.AppendLine("                        const groupDialog = getShareComposerDialog();");
            _js.AppendLine("                        if (!groupDialog) return shareFail('群组选择面板未就绪');");
            _js.AppendLine("                        const search = groupDialog.querySelector('input[aria-label=\"Search for groups\"], input[placeholder*=\"Search for groups\" i], input[type=\"search\"]');");
            _js.AppendLine("                        if (search && searchKey) {");
            _js.AppendLine("                            search.focus();");
            _js.AppendLine("                            search.value = searchKey;");
            _js.AppendLine("                            search.dispatchEvent(new Event('input', { bubbles: true }));");
            _js.AppendLine("                            await randomDelay(3500, 5000);");
            _js.AppendLine("                        }");
            _js.AppendLine("                        const pickerDialog = getShareComposerDialog();");
            _js.AppendLine("                        if (!pickerDialog) return shareFail('群组选择面板未就绪');");
            _js.AppendLine("                        const isGroupPickButton = (btn, text) => {");
            _js.AppendLine("                            if (!isClickableButton(btn)) return false;");
            _js.AppendLine("                            const aria = normalizeText(btn.getAttribute('aria-label'));");
            _js.AppendLine("                            if (aria === 'share to a group' || aria === 'group') return false;");
            _js.AppendLine("                            if (aria.startsWith('profile photo of') && !text.includes('group')) return false;");
            _js.AppendLine("                            return text.includes('group') || text.includes('private') || text.includes('public') || text.length >= 12;");
            _js.AppendLine("                        };");
            _js.AppendLine("                        let groupSelected = false;");
            _js.AppendLine("                        const candidates = [...pickerDialog.querySelectorAll('[role=\"button\"], [role=\"row\"], [role=\"listitem\"]')];");
            _js.AppendLine("                        for (const el of candidates) {");
            _js.AppendLine("                            const text = normalizeText(el.textContent);");
            _js.AppendLine("                            if (!text) continue;");
            _js.AppendLine("                            const btn = el.matches('[role=\"button\"]') ? el : el.querySelector('[role=\"button\"]') || el;");
            _js.AppendLine("                            if (!isGroupPickButton(btn, text)) continue;");
            _js.AppendLine("                            if (key && text.includes(key)) {");
            _js.AppendLine("                                await humanClick(btn);");
            _js.AppendLine("                                groupSelected = true;");
            _js.AppendLine("                                break;");
            _js.AppendLine("                            }");
            _js.AppendLine("                            if (searchKey && text.includes(normalizeText(searchKey))) {");
            _js.AppendLine("                                await humanClick(btn);");
            _js.AppendLine("                                groupSelected = true;");
            _js.AppendLine("                                break;");
            _js.AppendLine("                            }");
            _js.AppendLine("                        }");
            _js.AppendLine("                        if (!groupSelected) return shareFail('未找到群组');");
            _js.AppendLine("                        await randomDelay(4000, 6000);");
            _js.AppendLine("                        if (!(await waitForGroupPostButton())) return shareFail('群组发帖面板未就绪');");
            _js.AppendLine("                        if (groupMessage) {");
            _js.AppendLine("                            if (!(await fillShareMessage(groupMessage))) {");
            _js.AppendLine("                                console.warn('[转帖] 群组附言输入失败，仍尝试 Post');");
            _js.AppendLine("                            }");
            _js.AppendLine("                            await randomDelay(1000, 2000);");
            _js.AppendLine("                        }");
            _js.AppendLine("                        return await submitGroupPost();");
            _js.AppendLine("                    }");
            _js.AppendLine("                    return shareFail('未知分享类型');");
            _js.AppendLine("                } catch (e) {");
            _js.AppendLine("                    console.warn('[转帖] performShare失败:', target, e);");
            _js.AppendLine("                    return shareFail('执行异常: ' + (e?.message || e));");
            _js.AppendLine("                }");
            _js.AppendLine("            };");
            _js.AppendLine("");
        }

        private void ExecuteActions(int[] actions, int shareToFriendCount, JArray selectedGroups)
        {
            if (Array.Exists(actions, a => a == 1))
            {
                _js.AppendLine("            // 点赞");
                _js.AppendLine("            const likeButton = findMainLikeButton();");
                _js.AppendLine("            const alreadyLiked = document.querySelector('[aria-label=\"Remove Like\" i], [aria-label=\"Unlike\" i]');");
                _js.AppendLine("            if (alreadyLiked && isVisibleElement(alreadyLiked)) {");
                _js.AppendLine("                results.push({ actionType: 1, status: 1, remark: '已点赞' });");
                _js.AppendLine("            } else if (likeButton) {");
                _js.AppendLine("                await humanClick(likeButton);");
                _js.AppendLine("                results.push({ actionType: 1, status: 1 });");
                _js.AppendLine("            } else {");
                _js.AppendLine("                results.push({ actionType: 1, status: 2, failReason: '未找到点赞按钮' });");
                _js.AppendLine("            }");
                _js.AppendLine("            await randomDelay(1500, 2500);");
                _js.AppendLine("");
            }

            if (Array.Exists(actions, a => a == 2))
            {
                _js.AppendLine("            // 转发到动态消息");
                _js.AppendLine("            {");
                _js.AppendLine("                const feedResult = await performShare('feed');");
                _js.AppendLine("                if (feedResult?.ok) {");
                _js.AppendLine("                    results.push({ actionType: 2, status: feedResult.status || 1, targetName: '转发到动态消息', remark: feedResult.remark || '' });");
                _js.AppendLine("                } else {");
                _js.AppendLine("                    const failReason = detectRetryTooEarly() ? '操作过快(Retried Too Early)，请稍后重试' : (feedResult?.reason || '转发到动态消息失败');");
                _js.AppendLine("                    results.push({ actionType: 2, status: 2, failReason });");
                _js.AppendLine("                }");
                _js.AppendLine("            }");
                _js.AppendLine("            await closeShareComposer();");
                _js.AppendLine("            await randomDelay(2500, 4000);");
                _js.AppendLine("");
            }

            if (Array.Exists(actions, a => a == 6))
            {
                _js.AppendLine("            // 评论");
                _js.AppendLine("            {");
                _js.AppendLine("                const commentResult = await submitComment();");
                _js.AppendLine("                if (commentResult?.ok) {");
                _js.AppendLine("                    results.push({ actionType: 6, status: commentResult.status || 1, targetName: '评论', remark: commentResult.remark || '' });");
                _js.AppendLine("                } else {");
                _js.AppendLine("                    const failReason = detectRetryTooEarly() ? '操作过快(Retried Too Early)，请稍后重试' : (commentResult?.reason || '评论失败');");
                _js.AppendLine("                    results.push({ actionType: 6, status: 2, failReason });");
                _js.AppendLine("                }");
                _js.AppendLine("            }");
                _js.AppendLine("            await randomDelay(2000, 3500);");
                _js.AppendLine("");
            }

            if (Array.Exists(actions, a => a == 4))
            {
                _js.AppendLine($"            // 转贴到好友 x{shareToFriendCount}");
                _js.AppendLine($"            for (let i = 0; i < {shareToFriendCount}; i++) {{");
                _js.AppendLine("                const friendResult = await performShare('friend', i);");
                _js.AppendLine("                if (friendResult?.ok) {");
                _js.AppendLine("                    results.push({ actionType: 4, status: friendResult.status || 1, targetType: 'friend', remark: friendResult.remark || '' });");
                _js.AppendLine("                } else {");
                _js.AppendLine("                    const failReason = detectRetryTooEarly() ? '操作过快(Retried Too Early)，请稍后重试' : (friendResult?.reason || '转贴到好友失败');");
                _js.AppendLine("                    results.push({ actionType: 4, status: 2, failReason });");
                _js.AppendLine("                }");
                _js.AppendLine("                await closeShareComposer();");
                _js.AppendLine("                await randomDelay(2500, 4000);");
                _js.AppendLine("            }");
                _js.AppendLine("");
            }

            if (Array.Exists(actions, a => a == 5) && selectedGroups.Count > 0)
            {
                _js.AppendLine($"            // 转发到群组 ({selectedGroups.Count}个)");
                foreach (var group in selectedGroups)
                {
                    var groupId = group["groupId"]?.ToString() ?? "";
                    var groupName = group["groupName"]?.ToString() ?? "";
                    if (groupName.StartsWith("Profile photo of ", StringComparison.OrdinalIgnoreCase))
                        groupName = groupName.Substring("Profile photo of ".Length).Trim();
                    var searchKey = !string.IsNullOrEmpty(groupName) ? groupName : groupId;

                    if (!string.IsNullOrEmpty(groupId))
                    {
                        _js.AppendLine($"            {{");
                        _js.AppendLine($"                const groupResult = await performShare('group', {JsonConvert.SerializeObject(searchKey)});");
                        _js.AppendLine($"                if (groupResult?.ok) {{");
                        _js.AppendLine($"                    results.push({{ actionType: 5, status: groupResult.status || 1, targetType: 'group', targetId: '{groupId}', targetName: {JsonConvert.SerializeObject(groupName)}, remark: groupResult.remark || '' }});");
                        _js.AppendLine("                } else {");
                        _js.AppendLine($"                    const failReason = detectRetryTooEarly() ? '操作过快(Retried Too Early)，请稍后重试' : (groupResult?.reason || '转发到群组失败');");
                        _js.AppendLine($"                    results.push({{ actionType: 5, status: 2, targetType: 'group', targetId: '{groupId}', targetName: {JsonConvert.SerializeObject(groupName)}, failReason }});");
                        _js.AppendLine("                }");
                        _js.AppendLine("            }");
                        _js.AppendLine("            await closeShareComposer();");
                        _js.AppendLine("            await randomDelay(2500, 4000);");
                        _js.AppendLine("");
                    }
                }
            }
        }
    }
}
