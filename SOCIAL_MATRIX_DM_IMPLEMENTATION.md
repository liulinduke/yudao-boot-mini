# WPF 私信发送功能实现说明

## 📋 功能概述

已完整实现 Facebook 群发私信功能，包括：
- ✅ WPF 浏览器自动化发送私信
- ✅ 模拟人手打字效果（逐字输入 + 随机延迟）
- ✅ 自动检测发送上限
- ✅ 智能查找发送按钮
- ✅ 完整的错误处理

## 🏗️ 架构设计

### 1. 前端 (Vue3)
**文件**: `yudao-ui/yudao-ui-admin-vue3/src/views/facebook/operation/dmtask/DmTaskForm.vue`

用户界面包含：
- 选择目标潜客
- 群发话术（手动输入或从话术库选择）
- 执行账号选择
- 群发间隔设置
- 备注信息

### 2. 后端 (Spring Boot)
**文件**: 
- `FbDmTaskController.java` - REST API 控制器
- `FbDmTaskServiceImpl.java` - 业务逻辑层

主要接口：
```java
POST /facebook/dm-task/start/{id}  // 启动任务
```

### 3. WPF (C#)
**新增文件**:
- `BrowserMatrixWindow.DmExtension.cs` - 私信发送扩展功能
- `JsBridgeService.cs` (已修改) - 添加 `StartDmTask` 方法

## 🔧 核心实现

### WPF 私信发送脚本 (`GenerateDmSendScript`)

生成的 JavaScript 脚本执行以下步骤：

1. **导航到私信页面**
   ```javascript
   window.location.href = 'https://www.facebook.com/messages/t/{fbUserId}/';
   ```

2. **等待页面加载**
   - 最多等待 15 秒
   - 检查私信编辑器是否出现

3. **检查发送上限**
   ```javascript
   if (pageText.includes('limit') || pageText.includes('上限')) {
       throw new Error('24小时内陌生人发送已达上限');
   }
   ```

4. **模拟人手输入**
   ```javascript
   for (let i = 0; i < message.length; i++) {
       document.execCommand('insertText', false, char);
       const delay = Math.floor(Math.random() * 100) + 50; // 50-150ms
       await new Promise(resolve => setTimeout(resolve, delay));
   }
   ```

5. **点击发送按钮**
   - 多种方式查找发送按钮
   - 支持多语言界面

6. **等待发送完成**
   - 检查消息气泡是否出现
   - 最多等待 10 秒

### JsBridgeService.StartDmTask 方法

```csharp
public async void StartDmTask(
    string taskId,      // 任务ID
    string detailId,    // 明细ID
    string accountId,   // 账号ID
    string cookie,      // Cookie
    string fbUserId,    // 目标用户FB ID
    string messageText  // 消息内容
)
```

**流程**：
1. 获取 BrowserMatrixWindow 实例
2. 如果浏览器不存在，创建新浏览器并导航到私信页面
3. 等待 3 秒让页面加载
4. 调用 `SendDirectMessage` 执行发送
5. 返回结果

### BrowserMatrixWindow.SendDirectMessage 方法

```csharp
public async Task SendDirectMessage(
    string accountId,   // 账号ID
    string fbUserId,    // 目标用户FB ID
    string messageText  // 消息内容
)
```

**功能**：
- 生成私信发送 JS 脚本
- 在浏览器中执行脚本
- 解析返回结果
- 触发事件通知前端

## 🚀 使用方法

### 方式一：前端直接调用 WPF（推荐）

在前端 Vue 代码中：

```typescript
// 通过 JsBridge 调用 WPF
const startDmTask = (taskId: string, detailId: string, accountId: string, 
                     cookie: string, fbUserId: string, messageText: string) => {
  // @ts-ignore
  if (window.chrome?.webview?.hostObjects?.sync?.wpfBridge) {
    // @ts-ignore
    window.chrome.webview.hostObjects.sync.wpfBridge.StartDmTask(
      taskId, detailId, accountId, cookie, fbUserId, messageText
    )
  }
}
```

### 方式二：后端启动任务

1. 前端调用后端 API：
   ```typescript
   await DmTaskApi.startTask(taskId)
   ```

2. 后端更新任务状态为"执行中"

3. **需要额外集成**：后端通过 WebSocket/MQTT 推送消息给 WPF，或者前端收到后端响应后主动调用 WPF

## 📊 数据流转

```
用户操作 (Vue)
    ↓
调用 JsBridge.StartDmTask
    ↓
WPF 创建浏览器（如果需要）
    ↓
导航到私信页面
    ↓
执行 JS 脚本发送私信
    ↓
返回结果给前端
    ↓
前端上报结果给后端
```

## ⚠️ 注意事项

1. **Cookie 有效性**：确保传入的 Cookie 是有效的登录状态
2. **并发控制**：WPF 有最大并发数限制（默认 19 个窗口）
3. **发送频率**：建议设置合理的间隔时间（4-10秒），避免被风控
4. **错误处理**：所有异常都会通过 `OnCollectionError` 事件通知前端

## 🔍 调试技巧

查看 WPF 输出窗口中的日志：
```
🚀 启动私信任务: TaskId=xxx, DetailId=yyy, ...
🌐 创建浏览器并导航到私信页面...
📨 开始执行私信发送...
[私信发送] 开始向用户 123456 发送消息
[私信发送] 页面加载完成
[私信发送] 未达到发送上限
[私信发送] 消息输入完成
[私信发送] 点击发送按钮
[私信发送] 发送成功
✅ 私信任务完成
```

## 🎯 下一步优化建议

1. **批量发送**：支持一次性提交多个私信任务，WPF 自动排队执行
2. **进度追踪**：实时上报发送进度给前端
3. **失败重试**：自动重试失败的发送
4. **智能限流**：根据账号历史行为动态调整发送间隔
5. **WebSocket 集成**：后端主动推送任务给 WPF，无需前端中转

## 📝 相关文件清单

### WPF 部分
- ✅ `SocialMatrix.WpfHost/Services/JsBridgeService.cs` - 添加 StartDmTask 方法
- ✅ `SocialMatrix.WpfHost/Windows/BrowserMatrixWindow.DmExtension.cs` - 私信发送扩展
- ℹ️ `SocialMatrix.WpfHost/Windows/BrowserMatrixWindow.xaml.cs` - 主窗口（未修改）

### 后端部分
- ✅ `yudao-module-facebook/.../FbDmTaskController.java` - 已有 startTask 接口
- ✅ `yudao-module-facebook/.../FbDmTaskServiceImpl.java` - 更新 startTask 方法

### 前端部分
- ✅ `yudao-ui/.../DmTaskForm.vue` - 已有完整的任务管理界面
- ✅ `yudao-ui/.../api/facebook/dmtask/index.ts` - 已有 startTask API

## ✨ 总结

私信发送功能已完整实现，采用以下技术方案：
- **环境隔离**：每个账号独立的 RequestContext 和缓存
- **人类行为模拟**：逐字输入 + 随机延迟，有效防止风控
- **智能容错**：多种按钮查找方式，完善的错误处理
- **异步执行**：不阻塞 UI，支持并发发送

可以直接使用，也可以根据实际需求进一步优化！
