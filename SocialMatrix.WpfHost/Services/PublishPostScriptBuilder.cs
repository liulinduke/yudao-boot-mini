using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SocialMatrix.WpfHost.Services;

namespace SocialMatrix.WpfHost.Services
{
    /// <summary>
    /// 发个人帖脚本生成器
    /// </summary>
    public class PublishPostScriptBuilder : FacebookScriptBuilder
    {
        private readonly string _actionConfigJson;

        public PublishPostScriptBuilder(string actionConfigJson)
        {
            _actionConfigJson = actionConfigJson ?? "{}";
        }

        public override string Build()
        {
            var config = JObject.Parse(_actionConfigJson);
            var postContent = config["postContent"]?.ToString() ?? "";
            var privacySetting = config["privacySetting"]?.Value<int>() ?? 1;

            BeginScript();
            AddPublishPostHelpers();
            AppendOpenComposerSteps();
            AppendSetPrivacySteps(privacySetting);
            if (!string.IsNullOrEmpty(postContent))
            {
                AppendInputContentSteps(postContent);
            }
            AddMediaUploadPlaceholder();
            return EndScript();
        }

        public string BuildOpenComposerScript()
        {
            BeginScript();
            AddPublishPostHelpers();
            AppendOpenComposerSteps();
            return EndScript();
        }

        public string BuildSetPrivacyScript(int privacySetting)
        {
            BeginScript();
            AddPublishPostHelpers();
            AppendSetPrivacySteps(privacySetting);
            return EndScript();
        }

        public string BuildInputContentScript(string postContent)
        {
            BeginScript();
            AddPublishPostHelpers();
            AppendInputContentSteps(postContent);
            return EndScript();
        }

        /// <summary>
        /// 文件上传后点击发布
        /// </summary>
        public string BuildContinueScript()
        {
            BeginScript();
            AddPublishPostHelpers();
            AppendClickPostSteps();
            AppendWaitForPublishCompleteSteps();
            return EndScript();
        }

        public string BuildClickPostScript()
        {
            BeginScript();
            AddPublishPostHelpers();
            AppendClickPostSteps();
            AppendWaitForPublishCompleteSteps();
            return EndScript();
        }

        private void AddPublishPostHelpers()
        {
            _js.AppendLine("            const normalizeText = (text) => (text || '').replace(/\\s+/g, ' ').trim();");
            _js.AppendLine("            const isVisibleElement = (el) => {");
            _js.AppendLine("                if (!el) return false;");
            _js.AppendLine("                const rect = el.getBoundingClientRect();");
            _js.AppendLine("                const style = window.getComputedStyle(el);");
            _js.AppendLine("                return rect.width > 0 && rect.height > 0 && style.display !== 'none' && style.visibility !== 'hidden';");
            _js.AppendLine("            };");
            _js.AppendLine("            const getComposerDialog = () => {");
            _js.AppendLine("                const dialogs = [...document.querySelectorAll('[role=\"dialog\"]')].filter(isVisibleElement);");
            _js.AppendLine("                for (let i = dialogs.length - 1; i >= 0; i--) {");
            _js.AppendLine("                    if (dialogs[i].querySelector('[role=\"textbox\"]')) return dialogs[i];");
            _js.AppendLine("                }");
            _js.AppendLine("                return null;");
            _js.AppendLine("            };");
            _js.AppendLine("            const blurActiveEditor = async () => {");
            _js.AppendLine("                const active = document.activeElement;");
            _js.AppendLine("                if (active && active !== document.body) active.blur();");
            _js.AppendLine("                await randomDelay(300, 600);");
            _js.AppendLine("            };");
            _js.AppendLine("            const waitForComposer = async (timeoutMs = 15000) => {");
            _js.AppendLine("                const start = Date.now();");
            _js.AppendLine("                while (Date.now() - start < timeoutMs) {");
            _js.AppendLine("                    if (getComposerDialog()) return true;");
            _js.AppendLine("                    await randomDelay(400, 700);");
            _js.AppendLine("                }");
            _js.AppendLine("                return false;");
            _js.AppendLine("            };");
            _js.AppendLine("");
        }

        private void AppendOpenComposerSteps()
        {
            _js.AppendLine("            console.log('[发个人帖] 打开发帖 composer');");
            _js.AppendLine("            let opened = false;");
            _js.AppendLine("            for (const btn of document.querySelectorAll('[role=\"button\"]')) {");
            _js.AppendLine("                if (!isVisibleElement(btn)) continue;");
            _js.AppendLine("                const text = normalizeText(btn.textContent);");
            _js.AppendLine("                if (/what.s on your mind|在想什么|创建帖子|create post/i.test(text)) {");
            _js.AppendLine("                    await humanClick(btn);");
            _js.AppendLine("                    opened = true;");
            _js.AppendLine("                    break;");
            _js.AppendLine("                }");
            _js.AppendLine("            }");
            _js.AppendLine("            if (!opened) {");
            _js.AppendLine("                const menu = document.querySelector('[aria-label=\"Facebook menu\"], [aria-label*=\"Facebook menu\" i], [aria-label*=\"菜单\" i][role=\"button\"]');");
            _js.AppendLine("                if (!menu) throw new Error('未找到发帖入口(Facebook menu / 在想什么)');");
            _js.AppendLine("                await humanClick(menu);");
            _js.AppendLine("                await randomDelay(800, 1500);");
            _js.AppendLine("                const postItem = Array.from(document.querySelectorAll('div[role=\"listitem\"] span[id], [role=\"menuitem\"] span, [role=\"menuitem\"]'))");
            _js.AppendLine("                    .find(x => ['Post', '帖子'].includes(normalizeText(x.textContent)));");
            _js.AppendLine("                if (!postItem) throw new Error('未找到帖子菜单项');");
            _js.AppendLine("                await humanClick(postItem);");
            _js.AppendLine("            }");
            _js.AppendLine("            await randomDelay(1500, 2500);");
            _js.AppendLine("            if (!(await waitForComposer())) throw new Error('发帖 composer 未打开');");
            _js.AppendLine("            console.log('[发个人帖] composer 已打开');");
            _js.AppendLine("");
        }

        private void AppendSetPrivacySteps(int privacySetting)
        {
            _js.AppendLine($"            console.log('[发个人帖] 设置隐私 privacySetting={privacySetting}');");
            _js.AppendLine("            await blurActiveEditor();");
            _js.AppendLine("            const composer = getComposerDialog();");
            _js.AppendLine("            if (!composer) throw new Error('发帖 composer 未就绪');");
            _js.AppendLine("            const privacyButton = Array.from(composer.querySelectorAll('[role=\"button\"][aria-label]'))");
            _js.AppendLine("                .find(x => {");
            _js.AppendLine("                    const label = x.getAttribute('aria-label') || '';");
            _js.AppendLine("                    return label.startsWith('Edit privacy') || label.startsWith('编辑隐私');");
            _js.AppendLine("                });");
            _js.AppendLine("            if (!privacyButton) {");
            _js.AppendLine("                console.warn('[发个人帖] 未找到隐私按钮，跳过');");
            _js.AppendLine("            } else {");
            _js.AppendLine("                await humanClick(privacyButton);");
            _js.AppendLine("                await randomDelay(1200, 2000);");
            _js.AppendLine($"                const privacyKeywords = {{ 1: ['Public', '公开'], 2: ['Friends', '好友'], 3: ['Only me', 'Only Me', '仅自己', '只有我'] }}[{privacySetting}] || ['Public', '公开'];");
            _js.AppendLine("                const pickPrivacyLeaf = () => {");
            _js.AppendLine("                    const candidates = [];");
            _js.AppendLine("                    document.querySelectorAll('li, [role=\"menuitem\"], [role=\"option\"], [role=\"radio\"], div[aria-checked]').forEach((el) => {");
            _js.AppendLine("                        if (!isVisibleElement(el)) return;");
            _js.AppendLine("                        const text = normalizeText(el.textContent);");
            _js.AppendLine("                        if (!privacyKeywords.includes(text)) return;");
            _js.AppendLine("                        if (el.querySelector('li, [role=\"menuitem\"], [role=\"option\"]')) return;");
            _js.AppendLine("                        const rect = el.getBoundingClientRect();");
            _js.AppendLine("                        candidates.push({ el, area: rect.width * rect.height });");
            _js.AppendLine("                    });");
            _js.AppendLine("                    candidates.sort((a, b) => a.area - b.area);");
            _js.AppendLine("                    return candidates[0]?.el || null;");
            _js.AppendLine("                };");
            _js.AppendLine("                let pickEl = pickPrivacyLeaf();");
            _js.AppendLine("                if (!pickEl) {");
            _js.AppendLine("                    await humanClick(privacyButton);");
            _js.AppendLine("                    await randomDelay(800, 1200);");
            _js.AppendLine("                    pickEl = pickPrivacyLeaf();");
            _js.AppendLine("                }");
            _js.AppendLine("                if (pickEl) await humanClick(pickEl);");
            _js.AppendLine("                else console.warn('[发个人帖] 未找到隐私选项，使用默认');");
            _js.AppendLine("                await randomDelay(500, 1000);");
            _js.AppendLine("                const doneButton = Array.from(document.querySelectorAll('[role=\"dialog\"] [role=\"button\"][aria-label]'))");
            _js.AppendLine("                    .find(x => /done|完成|save|保存/i.test(x.getAttribute('aria-label') || ''));");
            _js.AppendLine("                if (doneButton) {");
            _js.AppendLine("                    await humanClick(doneButton);");
            _js.AppendLine("                    await randomDelay(800, 1500);");
            _js.AppendLine("                }");
            _js.AppendLine("            }");
            _js.AppendLine("            await blurActiveEditor();");
            _js.AppendLine("");
        }

        private void AppendInputContentSteps(string content)
        {
            _js.AppendLine("            console.log('[发个人帖] 输入帖子内容');");
            _js.AppendLine("            const composer = getComposerDialog();");
            _js.AppendLine("            if (!composer) throw new Error('发帖 composer 未就绪');");
            _js.AppendLine("            const textbox = composer.querySelector('[role=\"textbox\"]');");
            _js.AppendLine("            if (!textbox) throw new Error('未找到帖子输入框');");
            _js.AppendLine($"            const content = {JsonConvert.SerializeObject(content)};");
            _js.AppendLine("            textbox.focus();");
            _js.AppendLine("            await randomDelay(300, 600);");
            _js.AppendLine("            try {");
            _js.AppendLine("                document.execCommand('insertText', false, content);");
            _js.AppendLine("                // execCommand 已经触发 input，不能再次派发带完整内容的 InputEvent。");
            _js.AppendLine("            } catch (e) {");
            _js.AppendLine("                for (const ch of content) {");
            _js.AppendLine("                    document.execCommand('insertText', false, ch);");
            _js.AppendLine("                    // 每个 execCommand 都会触发对应的 input 事件。");
            _js.AppendLine("                    await randomDelay(20, 60);");
            _js.AppendLine("                }");
            _js.AppendLine("            }");
            _js.AppendLine("            await randomDelay(800, 1500);");
            _js.AppendLine("            await blurActiveEditor();");
            _js.AppendLine("");
        }

        private void AppendClickPostSteps()
        {
            _js.AppendLine("            console.log('[发个人帖] 点击 Post');");
            _js.AppendLine("            await blurActiveEditor();");
            _js.AppendLine("            const composer = getComposerDialog();");
            _js.AppendLine("            let postButton = composer?.querySelector('[role=\"button\"][aria-label=\"Post\"]:not([aria-disabled=\"true\"]), [role=\"button\"][aria-label=\"发帖\"]:not([aria-disabled=\"true\"])');");
            _js.AppendLine("            if (!postButton) {");
            _js.AppendLine("                postButton = document.querySelector('div[role=\"dialog\"] [role=\"button\"][aria-label=\"Post\"]:not([aria-disabled=\"true\"]), div[role=\"dialog\"] [role=\"button\"][aria-label=\"发帖\"]:not([aria-disabled=\"true\"])');");
            _js.AppendLine("            }");
            _js.AppendLine("            const nextButton = document.querySelector('div[role=\"dialog\"] [role=\"button\"][aria-label=\"Next\"]:not([aria-disabled=\"true\"]), div[role=\"dialog\"] [role=\"button\"][aria-label=\"继续\"]:not([aria-disabled=\"true\"])');");
            _js.AppendLine("            if (nextButton) {");
            _js.AppendLine("                await humanClick(nextButton);");
            _js.AppendLine("                await randomDelay(1000, 2000);");
            _js.AppendLine("            }");
            _js.AppendLine("            if (!postButton) throw new Error('未找到发布按钮');");
            _js.AppendLine("            await humanClick(postButton);");
            _js.AppendLine("            console.log('[发个人帖] 已点击发布按钮');");
            _js.AppendLine("");
        }

        private void AppendWaitForPublishCompleteSteps()
        {
            _js.AppendLine("            await randomDelay(2000, 3500);");
            _js.AppendLine("            const start = Date.now();");
            _js.AppendLine("            while (Date.now() - start < 30000) {");
            _js.AppendLine("                if (!getComposerDialog()) break;");
            _js.AppendLine("                await randomDelay(500, 800);");
            _js.AppendLine("            }");
            _js.AppendLine("");
        }

        private void AddMediaUploadPlaceholder()
        {
            _js.AppendLine("            console.log('[发个人帖] 准备上传媒体文件(如有)...');");
            _js.AppendLine("");
        }
    }
}
