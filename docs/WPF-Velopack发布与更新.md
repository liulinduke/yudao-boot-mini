# WPF Velopack 发布与更新

本文适用于 `EyochSocial`（.NET 8、WPF、CefSharp）的 Windows x64 发布。

## 1. 发布结构

官网和后台分开：

```text
https://eyoch.com/                         官网
https://admin.eyoch.com/                   Vue 管理后台
http://1.14.181.156/downloads/wpf/        WPF 安装包和更新文件（域名备案后再切回域名）
```

WPF 更新文件使用官网静态目录提供，不需要后端接口，也不需要 WPF 连接 WebSocket。

服务器目录：

```text
/www/wwwroot/eyoch/marketing-site/downloads/wpf/
```

Nginx 将 `marketing-site` 挂载到官网根目录，因此该目录对应：

```text
http://1.14.181.156/downloads/wpf/
```

## 2. 安装 Velopack 工具

本地执行一次：

```powershell
dotnet tool install -g vpk
```

如果已经安装过：

```powershell
dotnet tool update -g vpk
```

检查：

```powershell
vpk --version
```

## 3. 本地发布 WPF

## 3.1 构建并复制 WPF 内嵌前端

WPF 生产版从本地 `wwwroot/index.html` 加载 Vue。每次发布 WPF 前，必须先构建 WPF 专用前端并复制到 WPF 项目目录：

```powershell
cd D:\Work\yudao-boot-mini\yudao-ui\yudao-ui-admin-vue3

& "C:\Users\10378\.cache\codex-runtimes\codex-primary-runtime\dependencies\node\bin\node.exe" `
  --max-old-space-size=8192 `
  .\node_modules\vite\bin\vite.js build --mode wpf

cd D:\Work\yudao-boot-mini

New-Item -ItemType Directory `
  -Path .\SocialMatrix.WpfHost\wwwroot `
  -Force | Out-Null

Copy-Item `
  .\yudao-ui\yudao-ui-admin-vue3\dist-wpf\* `
  .\SocialMatrix.WpfHost\wwwroot\ `
  -Recurse -Force
```

检查复制结果：

```powershell
Test-Path .\SocialMatrix.WpfHost\wwwroot\index.html
Get-ChildItem .\SocialMatrix.WpfHost\wwwroot\assets | Select-Object -First 5
```

必须存在 `wwwroot\index.html` 和 `wwwroot\assets`，否则 WPF 发布后会黑屏。

在仓库根目录执行。不要在服务器上编译：

```powershell
cd D:\Work\yudao-boot-mini

dotnet publish .\SocialMatrix.WpfHost\SocialMatrix.WpfHost.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=false `
  -p:PublishReadyToRun=false `
  -o .\artifacts\wpf-publish
```

说明：

- `win-x64`：只发布 Windows 64 位版本，匹配项目的 `PlatformTarget=x64`。
- `--self-contained true`：用户不需要额外安装 .NET 运行时。
- `PublishSingleFile=false`：CefSharp 需要多个 DLL 和资源文件，不能只保留一个 EXE。
- 发布目录必须整体保留，不能只上传 `EyochSocial.exe`。

检查发布结果：

```powershell
Test-Path .\artifacts\wpf-publish\EyochSocial.exe
```

应输出 `True`。

## 4. 混淆 EyochSocial.dll

只混淆项目自己的 `EyochSocial.dll`，不要混淆 CefSharp、WebView2、Velopack、System 等依赖 DLL。`Scripts\*.js` 当前保持原样，运行功能不受影响。

### 4.1 安装 Obfuscar

Obfuscar 是免费的 .NET 混淆工具，只需安装一次：

```powershell
dotnet tool install -g Obfuscar.GlobalTool
```

已经安装过时更新：

```powershell
dotnet tool update -g Obfuscar.GlobalTool
```

检查命令：

```powershell
obfuscar.console --help
```

### 4.2 混淆发布目录中的 DLL

必须在 `dotnet publish` 成功后、`vpk pack` 之前执行：

```powershell
cd D:\Work\yudao-boot-mini

Remove-Item .\artifacts\wpf-obfuscated -Recurse -Force -ErrorAction SilentlyContinue

obfuscar.console .\docs\obfuscar.EyochSocial.xml

Copy-Item `
  .\artifacts\wpf-obfuscated\EyochSocial.dll `
  .\artifacts\wpf-publish\EyochSocial.dll `
  -Force

Remove-Item .\artifacts\wpf-publish\EyochSocial.pdb -Force -ErrorAction SilentlyContinue
```

混淆后先直接启动 `EyochSocial.exe`，确认 WPF 窗口、Vue 通信、CefSharp 浏览器、登录和 FB 操作正常，再进行 Velopack 打包。

> 不要把 `wpf-obfuscated` 目录直接拿去打包。Obfuscar 输出目录通常只包含被处理的 DLL，Velopack 仍然要使用完整的 `wpf-publish` 目录。

## 5. 使用 Velopack 打包

首次版本示例使用 `1.0.0`，发布目录会自动生成 `wpf-releases-1.0.0`：

```powershell
$Version = "1.0.0"
$ReleaseDir = ".\artifacts\wpf-releases-$Version"

vpk pack `
  --packId EyochSocial `
  --packVersion $Version `
  --packDir .\artifacts\wpf-publish `
  --mainExe EyochSocial.exe `
  --outputDir $ReleaseDir
```

输出目录通常包含：

```text
Setup.exe
RELEASES
*.nupkg
*.json
```

不同 Velopack 版本生成的文件名可能略有区别，以 `wpf-releases-$Version` 实际输出为准，所有文件都要上传。

## 6. 首次安装

将 `wpf-releases-1.0.0` 中的全部文件上传到服务器：

```text
/www/wwwroot/eyoch/marketing-site/downloads/wpf/
```

用户首次下载并运行：

```text
http://1.14.181.156/downloads/wpf/Setup.exe
```

Velopack 会将程序安装到用户本机的应用目录。用户以后应从安装后的快捷方式启动，不要直接运行下载目录里的文件。

## 7. 生成新版本

修改代码后递增版本号，例如 `1.0.1`：

```powershell
cd D:\Work\yudao-boot-mini

Remove-Item .\artifacts\wpf-publish -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item .\artifacts\wpf-releases-* -Recurse -Force -ErrorAction SilentlyContinue

$Version = "1.0.8"
$ReleaseDir = ".\artifacts\wpf-releases-$Version"

dotnet publish .\SocialMatrix.WpfHost\SocialMatrix.WpfHost.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=false `
  -p:PublishReadyToRun=false `
  -o .\artifacts\wpf-publish

Remove-Item .\artifacts\wpf-obfuscated -Recurse -Force -ErrorAction SilentlyContinue

obfuscar.console .\docs\obfuscar.EyochSocial.xml

Copy-Item `
  .\artifacts\wpf-obfuscated\EyochSocial.dll `
  .\artifacts\wpf-publish\EyochSocial.dll `
  -Force

Remove-Item .\artifacts\wpf-publish\EyochSocial.pdb -Force -ErrorAction SilentlyContinue

vpk pack `
  --packId EyochSocial `
  --packVersion $Version `
  --packDir .\artifacts\wpf-publish `
  --mainExe EyochSocial.exe `
  --outputDir $ReleaseDir
```

把新版本生成的 `wpf-releases-$Version` 全部文件覆盖上传到服务器目录：

```text
/www/wwwroot/eyoch/marketing-site/downloads/wpf/
```

不要删除旧版本的更新元数据后再只上传 EXE。Velopack 需要 `RELEASES`、包文件和版本元数据判断更新内容。

## 8. WPF 增加更新检查

Velopack 打包不会自动更新，程序需要在启动时检查更新。项目添加 Velopack 包：

```powershell
dotnet add .\SocialMatrix.WpfHost\SocialMatrix.WpfHost.csproj package Velopack
```

在应用启动早期初始化 Velopack：

```csharp
VelopackApp.Build().Run();
```

在主窗口加载完成后检查更新，避免影响窗口启动：

```csharp
private async Task CheckForUpdatesAsync()
{
    try
    {
        var manager = new UpdateManager("http://1.14.181.156/downloads/wpf/");
        var update = await manager.CheckForUpdatesAsync();
        if (update == null)
        {
            return;
        }

        await manager.DownloadUpdatesAsync(update);
        manager.ApplyUpdatesAndRestart(update);
    }
    catch (Exception ex)
    {
        // 更新失败不能阻止 WPF 主程序启动，记录日志即可。
        logger.Error(ex, "WPF 更新检查失败");
    }
}
```

实际 API 以当前安装的 Velopack 版本为准。更新检查失败必须被捕获，不能因为官网暂时不可访问而阻止程序启动。

推荐更新时机：

- 用户登录前或主窗口加载完成后检查。
- 下载完成后提示用户重启，或在没有任务运行时自动重启。
- 有采集/运营任务执行时不要立即重启。

## 9. 服务器上传方式

本地将生成目录打包，便于通过宝塔上传：

```powershell
Compress-Archive `
  -Path .\artifacts\wpf-releases-1.0.8\* `
  -DestinationPath .\artifacts\EyochSocial-v1.0.8-releases.zip `
  -Force
```

上传到：

```text
/www/wwwroot/eyoch/marketing-site/downloads/wpf/
```

服务器解压：

```bash
cd /www/wwwroot/eyoch/marketing-site/downloads/wpf
unzip -o /www/wwwroot/eyoch/marketing-site/downloads/wpf-release.zip
chmod -R a+rX .
```

官网 Nginx 已经挂载 `marketing-site`，通常不需要重启 Nginx。若修改了 Nginx 配置或 Docker 挂载配置，再执行：

```bash
cd /www/wwwroot/eyoch/script/docker
docker compose --env-file docker.env up -d --force-recreate nginx
docker exec yudao-nginx nginx -t
```

## 10. 未签名发布说明

当前不购买代码签名证书也可以发布 Velopack，但首次安装可能出现 Windows SmartScreen 的“未知发布者”提示。这不影响程序运行，但用户需要手动选择允许运行。

HTTPS 证书和 EXE 代码签名证书是两种不同证书：

- 腾讯云免费 SSL：保护官网和下载链路。
- 代码签名证书：减少 EXE 的未知发布者提示。

## 11. 发布检查清单

- [ ] 版本号比服务器当前版本高。
- [ ] `EyochSocial.exe` 位于 Velopack 发布包中。
- [ ] `RELEASES` 和所有 `.nupkg`/元数据文件已上传。
- [ ] 更新地址可以直接访问。
- [ ] WPF 更新检查地址以 `/` 结尾。
- [ ] 有任务运行时不会自动重启。
- [ ] 官网、后台和 API 的 DNS 记录已生效。
