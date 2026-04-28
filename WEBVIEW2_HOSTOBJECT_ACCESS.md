# WebView2 Host Object 访问指南

## 🔍 问题原因

Vue前端提示"WPF服务未启动"是因为**访问wpfBridge的方式不正确**。

### ❌ 错误写法
```javascript
// 这种方式在WebView2中不工作!
if (window.wpfBridge) {
    window.wpfBridge.SetAccountLanguage(...)
}
```

### ✅ 正确写法
```javascript
// WebView2需要通过chrome.webview.hostObjects访问
if (window.chrome?.webview?.hostObjects?.sync?.wpfBridge) {
    window.chrome.webview.hostObjects.sync.wpfBridge.SetAccountLanguage(...)
}
```

---

## 📚 WebView2 Host Object 机制

### 1. WPF端注入 (已正确配置)

**文件**: `MainWindow.xaml.cs` 第36行
```csharp
_jsBridge = new JsBridgeService(this);
VueWebView.CoreWebView2.AddHostObjectToScript("wpfBridge", _jsBridge);
```

这会在WebView2中创建一个名为`wpfBridge`的Host Object。

### 2. Vue端访问 (需要修正)

WebView2的Host Object**不是直接挂载到window对象**,而是通过特殊的路径访问:

```
window
  └─ chrome
      └─ webview
          └─ hostObjects
              ├─ sync (同步调用)
              │   └─ wpfBridge
              │       ├─ StartBrowser()
              │       ├─ StopBrowser()
              │       ├─ SaveToken()
              │       ├─ GetToken()
              │       ├─ ShowMessage()
              │       └─ SetAccountLanguage()  ← 新增方法
              └─ proxy (异步调用,返回Promise)
                  └─ wpfBridge
```

---

## 🛠️ 完整的检测方法

### 方法1: 检查WebView2环境

```javascript
// 检测是否在WebView2环境中
const isWebView2 = !!(window.chrome?.webview)

if (isWebView2) {
    console.log('✅ 运行在WebView2环境中')
    
    // 检查wpfBridge是否存在
    const hasWpfBridge = !!(window.chrome?.webview?.hostObjects?.sync?.wpfBridge)
    
    if (hasWpfBridge) {
        console.log('✅ wpfBridge已就绪')
        
        // 调用方法
        window.chrome.webview.hostObjects.sync.wpfBridge.SetAccountLanguage(
            JSON.stringify(['account1', 'account2']),
            1
        )
    } else {
        console.error('❌ wpfBridge未注入')
    }
} else {
    console.warn('⚠️ 不在WebView2环境中，可能是普通浏览器')
}
```

### 方法2: 调试window对象

```javascript
// 在浏览器Console中执行
console.log('window.chrome:', window.chrome)
console.log('window.chrome.webview:', window.chrome?.webview)
console.log('所有window属性:', Object.keys(window))
```

---

## 🧪 测试步骤

### 1. 在WPF应用中打开开发者工具

WPF应用运行时,按 `F12` 或在代码中添加:
```csharp
VueWebView.CoreWebView2.OpenDevToolsWindow();
```

### 2. 在Console中测试

```javascript
// 测试1: 检查chrome对象
console.log('chrome存在?', !!window.chrome)

// 测试2: 检查webview对象
console.log('webview存在?', !!window.chrome?.webview)

// 测试3: 检查hostObjects
console.log('hostObjects存在?', !!window.chrome?.webview?.hostObjects)

// 测试4: 检查wpfBridge
console.log('wpfBridge存在?', !!window.chrome?.webview?.hostObjects?.sync?.wpfBridge)

// 测试5: 列出wpfBridge的所有方法
if (window.chrome?.webview?.hostObjects?.sync?.wpfBridge) {
    console.log('wpfBridge方法:', 
        Object.getOwnPropertyNames(
            Object.getPrototypeOf(window.chrome.webview.hostObjects.sync.wpfBridge)
        )
    )
}

// 测试6: 调用ShowMessage测试连通性
if (window.chrome?.webview?.hostObjects?.sync?.wpfBridge) {
    window.chrome.webview.hostObjects.sync.wpfBridge.ShowMessage('测试成功!')
}
```

### 3. 预期输出

如果一切正常,应该看到:
```
✅ chrome存在? true
✅ webview存在? true
✅ hostObjects存在? true
✅ wpfBridge存在? true
📋 wpfBridge方法: ["StartBrowser", "StopBrowser", "SaveToken", "GetToken", "ShowMessage", "SetAccountLanguage"]
```

---

## 🔧 常见问题

### Q1: window.chrome是undefined

**原因**: 不是在WebView2环境中运行,而是在普通浏览器中

**解决**: 
- 确保通过WPF应用启动Vue前端
- 不要直接在Chrome/Edge浏览器中打开Vue页面

### Q2: wpfBridge是undefined

**原因**: 
1. AddHostObjectToScript未执行
2. 执行顺序问题(在注入完成前就访问)

**解决**:
```javascript
// 等待WebView2完全初始化
window.addEventListener('DOMContentLoaded', () => {
    setTimeout(() => {
        console.log('wpfBridge:', window.chrome?.webview?.hostObjects?.sync?.wpfBridge)
    }, 1000)
})
```

### Q3: 调用方法时报错

**原因**: 方法签名不匹配或参数类型错误

**解决**:
```javascript
// 确保参数类型正确
const accountIds = JSON.stringify(['account1', 'account2']) // 必须是JSON字符串
const language = 1 // 必须是数字

window.chrome.webview.hostObjects.sync.wpfBridge.SetAccountLanguage(accountIds, language)
```

---

## 📝 最佳实践

### 1. 封装访问函数

```javascript
// utils/wpfBridge.js
export function getWpfBridge() {
    return window.chrome?.webview?.hostObjects?.sync?.wpfBridge
}

export function isWpfBridgeReady() {
    return !!getWpfBridge()
}

export function callWpfMethod(methodName, ...args) {
    const bridge = getWpfBridge()
    if (!bridge) {
        throw new Error('WPF桥接服务未就绪')
    }
    
    if (typeof bridge[methodName] !== 'function') {
        throw new Error(`WPF方法 ${methodName} 不存在`)
    }
    
    return bridge[methodName](...args)
}
```

### 2. 在组件中使用

```vue
<script setup>
import { getWpfBridge, isWpfBridgeReady } from '@/utils/wpfBridge'

const handleSetLanguage = async () => {
    if (!isWpfBridgeReady()) {
        message.warning('WPF服务未启动')
        return
    }
    
    try {
        const bridge = getWpfBridge()
        bridge.SetAccountLanguage(
            JSON.stringify(selectedAccounts.value.map(a => a.fbAccount)),
            formData.value.language
        )
        message.success('语言设置成功')
    } catch (error) {
        message.error(`设置失败: ${error.message}`)
    }
}
</script>
```

---

## 🎯 当前项目状态

### ✅ 已完成
- WPF端: `AddHostObjectToScript("wpfBridge", _jsBridge)` ✓
- WPF端: `SetAccountLanguage` 方法已实现 ✓
- Vue端: 访问路径已修正为 `chrome.webview.hostObjects.sync.wpfBridge` ✓

### 🔍 待验证
- 在WPF应用中实际测试调用是否成功
- 检查Console是否有错误信息

---

## 🚀 快速测试命令

在WPF应用的Vue页面Console中粘贴执行:

```javascript
(async function testWpfBridge() {
    console.log('=== 开始测试WPF桥接 ===')
    
    // 1. 检查环境
    if (!window.chrome?.webview) {
        console.error('❌ 不在WebView2环境中')
        return
    }
    console.log('✅ WebView2环境检测通过')
    
    // 2. 检查wpfBridge
    const bridge = window.chrome.webview.hostObjects?.sync?.wpfBridge
    if (!bridge) {
        console.error('❌ wpfBridge未找到')
        return
    }
    console.log('✅ wpfBridge找到')
    
    // 3. 测试ShowMessage
    try {
        bridge.ShowMessage('桥接测试成功!')
        console.log('✅ ShowMessage调用成功')
    } catch (e) {
        console.error('❌ ShowMessage调用失败:', e)
    }
    
    console.log('=== 测试完成 ===')
})()
```

---

**最后更新**: 2026-04-27  
**相关文件**: 
- `SocialMatrix.WpfHost/MainWindow.xaml.cs` (第36行)
- `yudao-ui/.../SetLanguageDialog.vue` (第80-95行)
