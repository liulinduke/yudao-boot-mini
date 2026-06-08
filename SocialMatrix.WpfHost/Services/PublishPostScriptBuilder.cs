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
            _actionConfigJson = actionConfigJson;
        }
        
        /// <summary>
        /// 生成完整的发个人帖脚本
        /// </summary>
        public override string Build()
        {
            var config = JObject.Parse(_actionConfigJson);
            var postContent = config["postContent"]?.ToString() ?? "";
            var privacySetting = config["privacySetting"]?.Value<int>() ?? 1;
            
            BeginScript();
            
            // 1. 导航到Facebook首页
            NavigateToHome();
            
            // 2. 点击菜单按钮
            ClickMenuButton();
            
            // 3. 点击"帖子"菜单项
            ClickPostMenuItem();
            
            // 4. 设置隐私选项
            SetPrivacy(privacySetting);
            
            // 5. 输入帖子内容
            if (!string.IsNullOrEmpty(postContent))
            {
                InputPostContent(postContent);
            }
            
            // 6-7. 上传图片/视频和发布（由C#层面控制）
            AddMediaUploadPlaceholder();
            
            return EndScript();
        }
        
        /// <summary>
        /// 生成继续执行脚本（文件上传后点击发布）
        /// </summary>
        public string BuildContinueScript()
        {
            BeginScript();
            
            // 等待发布按钮出现
            WaitForElement("div[role=dialog] div[role=button][aria-label=Post]:not([aria-disabled]), div[role=dialog] div[role=button][aria-label=发帖]:not([aria-disabled])", 10000);
            
            // 如果有'下一步'按钮，先点击它
            _js.AppendLine("        const nextButton = document.querySelector('div[role=dialog] div[role=button][aria-label=Next]:not([aria-disabled]), div[role=dialog] div[role=button][aria-label=继续]:not([aria-disabled])');");
            _js.AppendLine("        if (nextButton) {");
            _js.AppendLine("            await humanClick(nextButton);");
            _js.AppendLine("            await randomDelay(1000, 2000);");
            _js.AppendLine("        }");
            _js.AppendLine("");
            
            // 点击发布按钮
            _js.AppendLine("        const postButton = document.querySelector('div[role=dialog] div[role=button][aria-label=Post]:not([aria-disabled]), div[role=dialog] div[role=button][aria-label=发帖]:not([aria-disabled])');");
            _js.AppendLine("        if (!postButton) throw new Error('未找到发布按钮');");
            _js.AppendLine("        await humanClick(postButton);");
            _js.AppendLine("        console.log('[发个人帖] 已点击发布按钮');");
            _js.AppendLine("");
            
            // 等待发布完成
            WaitForDialogClose(30000);
            
            return EndScript();
        }
        
        private void NavigateToHome()
        {
            _js.AppendLine("        // 1. 导航到Facebook首页");
            _js.AppendLine("        window.location.href = 'https://www.facebook.com';");
            _js.AppendLine("        await randomDelay(2000, 3000);");
            _js.AppendLine("        console.log('[发个人帖] 页面加载完成');");
            _js.AppendLine("");
        }
        
        private void ClickMenuButton()
        {
            _js.AppendLine("        // 2. 点击菜单按钮");
            _js.AppendLine("        const menuButton = document.querySelector('div[role=navigation] div[aria-label*=菜单], div[role=navigation] div[aria-label*=Menu i]');");
            _js.AppendLine("        if (!menuButton) throw new Error('未找到菜单按钮');");
            _js.AppendLine("        await humanClick(menuButton);");
            _js.AppendLine("        await randomDelay(500, 1000);");
            _js.AppendLine("");
        }
        
        private void ClickPostMenuItem()
        {
            _js.AppendLine("        // 3. 点击'帖子'菜单项");
            _js.AppendLine("        const postMenuItem = Array.from(document.querySelectorAll('div[role=listitem] span[id]'))");
            _js.AppendLine("            .find(x => x.innerText === '帖子' || x.innerText === 'Post');");
            _js.AppendLine("        if (!postMenuItem) throw new Error('未找到帖子菜单项');");
            _js.AppendLine("        await humanClick(postMenuItem);");
            _js.AppendLine("        await randomDelay(1000, 2000);");
            _js.AppendLine("");
        }
        
        private void SetPrivacy(int privacySetting)
        {
            _js.AppendLine($"        // 4. 设置隐私选项 (privacySetting={privacySetting})");
            _js.AppendLine("        const privacyButton = Array.from(document.querySelectorAll('div[role=dialog] div[role=button][aria-label]'))");
            _js.AppendLine("            .find(x => {");
            _js.AppendLine("                const label = x.getAttribute('aria-label');");
            _js.AppendLine("                return label && (label.startsWith('Edit privacy') || label.startsWith('编辑隐私'));");
            _js.AppendLine("            });");
            _js.AppendLine("        if (privacyButton) {");
            _js.AppendLine("            await humanClick(privacyButton);");
            _js.AppendLine("            await randomDelay(500, 1000);");
            _js.AppendLine("            ");
            _js.AppendLine($"            const privacyIndex = {privacySetting - 1};");
            _js.AppendLine("            const privacyOptions = document.querySelectorAll('div[role=dialog] [role=radiogroup] label, div[role=dialog] label div[aria-checked]');");
            _js.AppendLine("            if (privacyOptions && privacyOptions.length > privacyIndex) {");
            _js.AppendLine("                await humanClick(privacyOptions[privacyIndex]);");
            _js.AppendLine("                await randomDelay(500, 1000);");
            _js.AppendLine("            }");
            _js.AppendLine("            ");
            _js.AppendLine("            const doneButton = document.querySelector('div[role=dialog] div[role=button][aria-label*=Done]:not([aria-disabled]), div[role=dialog] div[role=button][aria-label*=完成]:not([aria-disabled])');");
            _js.AppendLine("            if (doneButton) {");
            _js.AppendLine("                await humanClick(doneButton);");
            _js.AppendLine("                await randomDelay(500, 1000);");
            _js.AppendLine("            }");
            _js.AppendLine("        }");
            _js.AppendLine("");
        }
        
        private void InputPostContent(string content)
        {
            _js.AppendLine("        // 5. 输入帖子内容");
            _js.AppendLine("        const textbox = document.querySelector('div[role=dialog] form div[role=textbox]');");
            _js.AppendLine("        if (textbox) {");
            _js.AppendLine($"            const content = `{EscapeForJsTemplate(content)}`;");
            _js.AppendLine("            await humanTypeText(textbox, content);");
            _js.AppendLine("            await randomDelay(500, 1000);");
            _js.AppendLine("            ");
            _js.AppendLine("            const dialog = document.querySelector('div[role=dialog] form div[role=dialog]');");
            _js.AppendLine("            if (dialog) {");
            _js.AppendLine("                await humanClick(dialog);");
            _js.AppendLine("                await randomDelay(1000, 2000);");
            _js.AppendLine("            }");
            _js.AppendLine("        }");
            _js.AppendLine("");
        }
        
        private void AddMediaUploadPlaceholder()
        {
            _js.AppendLine("        // 6. 上传图片/视频（由C#层面处理）");
            _js.AppendLine("        console.log('[发个人帖] 准备上传媒体文件...');");
            _js.AppendLine("");
        }
    }
}
