# SocialMatrix WPF 客户端

## 项目结构

```
SocialMatrix.WpfHost/
├── App.xaml.cs                  # 应用入口，初始化 CefSharp
├── MainWindow.xaml              # 主窗口（左侧 WebView2 + 右侧 CefSharp）
├── Controls/
│   └── BrowserMatrixControl.xaml  # 12 宫格浏览器矩阵控件
├── Services/
│   ├── JsBridgeService.cs       # WPF 与 Vue 通信桥接
│   └── TokenManager.cs          # Token 加密存储（DPAPI）
└── wwwroot/                     # Vue 构建产物（自动生成）
```

## 快速开始

### 1. 安装依赖

在 Visual Studio 2022 中打开项目，NuGet 包会自动恢复：
- Microsoft.Web.WebView2
- CefSharp.Wpf
- Newtonsoft.Json

### 2. 开发环境运行

#### 步骤 1：启动 Java 后端
```bash
# 确保你的 mx-admin 后端运行在 8091 端口
```

#### 步骤 2：启动 Vue 开发服务器
```bash
cd mx-ui
npm run dev
# Vue 会运行在 http://localhost:80
```

#### 步骤 3：运行 WPF 项目
在 Visual Studio 中按 F5 运行 `SocialMatrix.WpfHost`

### 3. 生产环境打包

#### 步骤 1：构建 Vue 前端
```bash
cd mx-ui
npm run build:prod
# 产物会自动输出到 ../SocialMatrix.WpfHost/wwwroot
```

#### 步骤 2：发布 WPF 项目
在 Visual Studio 中右键项目 → 发布 → 选择目标文件夹

## 功能说明

### WebView2 + Vue 集成
- 左侧 400px 区域加载 Vue 前端
- 开发环境：`http://localhost:80`
- 生产环境：本地 `wwwroot/index.html`

### CefSharp 浏览器矩阵
- 右侧区域显示 12 宫格浏览器（3行×4列）
- 每个格子独立进程，支持指纹伪装
- 自动注入 anti-detection 脚本

### WPF ↔ Vue 通信
Vue 前端可通过以下方式调用 WPF 功能：

```javascript
// 启动浏览器
window.chrome.webview.hostObjects.sync.wpfBridge.StartBrowser('account_id');

// 关闭浏览器
window.chrome.webview.hostObjects.sync.wpfBridge.StopBrowser('account_id');

// 保存 Token
window.chrome.webview.hostObjects.sync.wpfBridge.SaveToken('your_token');
```

## 测试页面

访问 Vue 前端测试页面：`http://localhost:80/#/wpf/test`

该页面提供：
- 启动/关闭浏览器按钮
- Token 保存功能
- 状态提示

## 指纹伪装

CefSharp 浏览器自动注入以下伪装脚本：
1. 隐藏 `navigator.webdriver`
2. 模拟 Chrome 运行时对象
3. 模拟插件列表
4. 模拟语言设置
5. Canvas 指纹扰动

## 注意事项

1. **WebView2 Runtime**: Windows 10 20H2 以上系统自带，低版本需手动安装
2. **CefSharp 初始化**: 必须在 STA 线程初始化
3. **内存管理**: 关闭不用的浏览器实例会调用 `Dispose()` 释放资源
4. **跨域配置**: Vue 的 `vue.config.js` 已配置代理到 Java 后端

## 常见问题

### Q: WebView2 加载失败
A: 检查是否安装了 WebView2 Runtime，或尝试更新 Edge 浏览器

### Q: CefSharp 浏览器无法显示
A: 检查项目平台目标是否为 x64（CefSharp 不支持 AnyCPU）

### Q: Vue 调用 WPF 方法无响应
A: 确保在 WPF 环境中运行（`window.chrome.webview` 对象存在）

## 下一步开发

- [ ] 实现 Cookie 导入功能
- [ ] 添加采集任务管理界面
- [ ] 实现 WebSocket 实时进度推送
- [ ] 优化 12 宫格布局自适应
- [ ] 添加代理 IP 配置界面
