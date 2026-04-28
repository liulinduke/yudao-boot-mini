# Facebook语言设置页面DOM结构调试指南

## 🔍 如何验证和调试选择器

### 步骤1: 手动打开语言设置页面

1. 登录Facebook账号
2. 访问: `https://www.facebook.com/settings/?tab=language_and_region`
3. 按 `F12` 打开开发者工具

### 步骤2: 检查编辑按钮

在Console中执行:
```javascript
// 查找编辑按钮
const editButton = document.querySelector('div[role=main] div[role=button]');
console.log('编辑按钮:', editButton);
console.log('按钮文本:', editButton?.innerText);

// 如果找不到,尝试其他选择器
const alternatives = [
    'div[role=main] a[role=button]',
    'div[role=main] button',
    '[aria-label*="language"]',
    '[aria-label*="Language"]'
];

alternatives.forEach(selector => {
    const el = document.querySelector(selector);
    if (el) console.log(`找到: ${selector}`, el);
});
```

### 步骤3: 点击编辑按钮后检查对话框

手动点击编辑按钮,然后在Console中执行:
```javascript
// 等待2秒让对话框出现
setTimeout(() => {
    // 查找所有radio选项
    const radios = document.querySelectorAll('div[role=dialog] div[data-visualcompletion]>div[role=radio] span[id]');
    console.log('找到的radio数量:', radios.length);
    
    radios.forEach((radio, index) => {
        console.log(`Radio ${index}:`, radio.innerText);
    });
    
    // 查找英文选项
    const englishRadio = Array.from(radios).find(span => 
        span.innerText.includes('English') && span.innerText.includes('US')
    );
    console.log('英文选项:', englishRadio);
    
    // 查找中文选项
    const chineseRadio = Array.from(radios).find(span => 
        span.innerText.includes('中文') && span.innerText.includes('简体')
    );
    console.log('中文选项:', chineseRadio);
}, 2000);
```

### 步骤4: 检查保存按钮

选择语言后,在Console中执行:
```javascript
// 查找保存按钮
const saveButtons = document.querySelectorAll('div[role=dialog] button[type=submit], div[role=dialog] div[role=button]');
console.log('找到的按钮数量:', saveButtons.length);

saveButtons.forEach((btn, index) => {
    console.log(`按钮 ${index}:`, btn.innerText);
});

// 查找包含"Save"或"保存"的按钮
const targetButton = Array.from(saveButtons).find(btn => 
    btn.innerText.includes('Save') || 
    btn.innerText.includes('保存') || 
    btn.innerText.includes('Simpan')
);
console.log('目标保存按钮:', targetButton);
```

### 步骤5: 如果选择器不匹配,记录实际结构

```javascript
// 导出对话框的完整HTML结构
const dialog = document.querySelector('div[role=dialog]');
if (dialog) {
    console.log('对话框HTML:');
    console.log(dialog.outerHTML);
    
    // 或者复制到剪贴板
    copy(dialog.outerHTML);
    console.log('已复制到剪贴板');
}
```

## 🛠️ 常见问题和解决方案

### 问题1: 找不到编辑按钮

**可能原因**: Facebook更新了页面结构

**解决方案**:
```javascript
// 尝试更通用的选择器
const editButton = document.querySelector('[aria-label*="Edit"], [aria-label*="编辑"], [data-testid*="edit"]');
```

### 问题2: Radio选择器返回空数组

**可能原因**: `data-visualcompletion`属性名称改变

**解决方案**:
```javascript
// 移除data-visualcompletion限制
const radios = document.querySelectorAll('div[role=dialog] div[role=radio] span');

// 或者使用更宽泛的选择器
const allSpans = document.querySelectorAll('div[role=dialog] span');
const languageSpans = Array.from(allSpans).filter(span => 
    span.innerText.match(/(English|中文|Español|Français)/)
);
```

### 问题3: 保存按钮找不到

**可能原因**: 按钮类型或文本变化

**解决方案**:
```javascript
// 查找对话框中所有可点击元素
const clickableElements = document.querySelectorAll('div[role=dialog] [role=button], div[role=dialog] button, div[role=dialog] a[role=button]');

clickableElements.forEach((el, index) => {
    console.log(`元素 ${index}: "${el.innerText}"`, el.tagName);
});

// 通常保存按钮是最后一个
const lastButton = clickableElements[clickableElements.length - 1];
console.log('最后一个按钮:', lastButton?.innerText);
```

## 📝 更新JavaScript脚本模板

如果发现选择器需要调整,修改 `BrowserMatrixWindow.xaml.cs` 中的 `GenerateLanguageSwitchScript` 方法:

```csharp
private string GenerateLanguageSwitchScript(int language)
{
    var js = new System.Text.StringBuilder();

    js.AppendLine("(async function() {");
    js.AppendLine("    try {");
    js.AppendLine("        console.log('[语言切换] 开始执行');");
    js.AppendLine("");
    
    // === 根据实际情况调整以下选择器 ===
    
    // 1. 编辑按钮选择器
    js.AppendLine("        const editButton = document.querySelector('YOUR_EDIT_BUTTON_SELECTOR');");
    js.AppendLine("        if (!editButton) {");
    js.AppendLine("            throw new Error('未找到编辑按钮');");
    js.AppendLine("        }");
    js.AppendLine("        editButton.click();");
    js.AppendLine("        console.log('[语言切换] 已点击编辑按钮');");
    js.AppendLine("");
    
    // 2. 等待对话框
    js.AppendLine("        await new Promise(resolve => setTimeout(resolve, 2000));");
    js.AppendLine("");
    
    // 3. Radio选项选择器
    js.AppendLine($"        const targetLang = '{(language == 1 ? "English" : "中文")}';");
    js.AppendLine($"        const subLang = '{(language == 1 ? "US" : "简体")}';");
    js.AppendLine("");
    js.AppendLine("        const radios = Array.from(document.querySelectorAll('YOUR_RADIO_SELECTOR'));");
    js.AppendLine("        const targetRadio = radios.find(span => ");
    js.AppendLine("            span.innerText.includes(targetLang) && span.innerText.includes(subLang)");
    js.AppendLine("        );");
    js.AppendLine("");
    js.AppendLine("        if (!targetRadio) {");
    js.AppendLine("            throw new Error(`未找到语言选项: ${targetLang} (${subLang})`);");
    js.AppendLine("        }");
    js.AppendLine("");
    js.AppendLine("        targetRadio.click();");
    js.AppendLine("        console.log('[语言切换] 已选择语言:', targetLang);");
    js.AppendLine("");
    
    // 4. 等待UI更新
    js.AppendLine("        await new Promise(resolve => setTimeout(resolve, 1000));");
    js.AppendLine("");
    
    // 5. 保存按钮选择器
    js.AppendLine("        const saveButton = Array.from(document.querySelectorAll('YOUR_SAVE_BUTTON_SELECTOR'))");
    js.AppendLine("            .find(btn => btn.innerText.includes('Save') || btn.innerText.includes('保存') || btn.innerText.includes('Simpan'));");
    js.AppendLine("");
    js.AppendLine("        if (saveButton) {");
    js.AppendLine("            saveButton.click();");
    js.AppendLine("            console.log('[语言切换] 已点击保存按钮');");
    js.AppendLine("        }");
    js.AppendLine("");
    
    // 6. 等待完成
    js.AppendLine("        await new Promise(resolve => setTimeout(resolve, 2000));");
    js.AppendLine("        console.log('[语言切换] 完成');");
    js.AppendLine("");
    
    js.AppendLine("        return JSON.stringify({ success: true, message: '语言切换成功' });");
    js.AppendLine("    } catch (e) {");
    js.AppendLine("        console.error('[语言切换] 错误:', e);");
    js.AppendLine("        return JSON.stringify({ success: false, message: e.message });");
    js.AppendLine("    }");
    js.AppendLine("})();");

    return js.ToString();
}
```

## 🎯 快速测试完整流程

在Browser Console中粘贴并执行:

```javascript
(async function() {
    console.log('=== 开始测试语言切换 ===');
    
    // 1. 点击编辑
    const editButton = document.querySelector('div[role=main] div[role=button]');
    if (!editButton) {
        console.error('❌ 未找到编辑按钮');
        return;
    }
    editButton.click();
    console.log('✅ 已点击编辑按钮');
    
    // 2. 等待对话框
    await new Promise(r => setTimeout(r, 2000));
    
    // 3. 查找语言选项
    const radios = Array.from(document.querySelectorAll('div[role=dialog] div[data-visualcompletion]>div[role=radio] span[id]'));
    console.log(`找到 ${radios.length} 个语言选项`);
    
    const targetRadio = radios.find(span => 
        span.innerText.includes('English') && span.innerText.includes('US')
    );
    
    if (!targetRadio) {
        console.error('❌ 未找到英文选项');
        console.log('可用选项:', radios.map(r => r.innerText));
        return;
    }
    
    console.log('✅ 找到目标选项:', targetRadio.innerText);
    targetRadio.click();
    
    // 4. 等待
    await new Promise(r => setTimeout(r, 1000));
    
    // 5. 点击保存
    const saveButton = Array.from(document.querySelectorAll('div[role=dialog] button[type=submit], div[role=dialog] div[role=button]'))
        .find(btn => btn.innerText.includes('Save') || btn.innerText.includes('保存'));
    
    if (saveButton) {
        saveButton.click();
        console.log('✅ 已点击保存按钮');
    } else {
        console.warn('⚠️ 未找到保存按钮');
    }
    
    // 6. 完成
    await new Promise(r => setTimeout(r, 2000));
    console.log('=== 测试完成 ===');
})();
```

## 📊 记录调试结果

将以下信息记录下来,以便更新代码:

- [ ] 编辑按钮的实际选择器: _______________
- [ ] Radio选项的实际选择器: _______________
- [ ] 保存按钮的实际选择器: _______________
- [ ] Facebook页面加载时间: _______ 秒
- [ ] 对话框出现延迟: _______ 秒
- [ ] 是否有额外的确认步骤: □是 □否

---

**最后更新**: 2026-04-27
**参考来源**: 竞品B FacebookSetLanguageTask.cs + Facebook通用DOM模式
