using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SocialMatrix.WpfHost.Windows;

namespace SocialMatrix.WpfHost.Services
{
    /// <summary>
    /// JS 桥接服务 - 供 Vue 前端调用 WPF 功能
    /// </summary>
    [ClassInterface(ClassInterfaceType.AutoDual)]
    [ComVisible(true)]
    public class JsBridgeService
    {
        private readonly MainWindow _mainWindow;
        private readonly ConcurrentDictionary<string, byte> _directPublishAccounts = new();
        private static readonly HttpClient FileClient = new()
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        public JsBridgeService(MainWindow mainWindow)
        {
            _mainWindow = mainWindow;
        }

        /// <summary>
        /// 将 HTTP 文件资源转换为 data URL，供 HTTPS 的 WPF Vue 页面显示。
        /// </summary>
        public string GetFileDataUrl(string fileUrl)
        {
            try
            {
                if (!Uri.TryCreate(fileUrl, UriKind.Absolute, out var uri)
                    || !uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
                    || !uri.AbsolutePath.StartsWith("/admin-api/infra/file/", StringComparison.OrdinalIgnoreCase))
                {
                    return string.Empty;
                }

                using var response = FileClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead)
                    .GetAwaiter().GetResult();
                if (!response.IsSuccessStatusCode)
                {
                    return string.Empty;
                }

                var content = response.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
                var mediaType = response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";
                return $"data:{mediaType};base64,{Convert.ToBase64String(content)}";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"文件转 data URL 失败: {ex.Message}");
                return string.Empty;
            }
        }

        /// <summary>
        /// Vue 调用此方法启动浏览器并开始自动化采集
        /// </summary>
        /// <param name="detailId">明细ID(不是taskId)</param>
        /// <param name="accountId">账号ID(fbAccount)</param>
        /// <param name="cookie">Cookie</param>
        /// <param name="searchUrl">搜索URL</param>
        /// <param name="expectedCount">期望采集数量</param>
        /// <param name="taskType">任务类型(1主页/2帖子/3用户/4群组/5活动/6评论)</param>
        /// <param name="config">配置JSON字符串（可选）</param>
        /// <param name="isOperation">是否为运营任务（true=运营任务如加组/私信/转帖，false=采集任务）</param>
        public void StartBrowser(string detailId, string accountId, string cookie, string searchUrl, int expectedCount, int taskType = 1, string config = null, bool isOperation = false, string deviceId = null, string password = null, string tfa = null, string fbAccount = null, string proxyConfigJson = null)
        {
            _mainWindow.ApplyPendingUpdateOnUserStart();
            // 记录配置信息（如果有）
            if (!string.IsNullOrEmpty(config))
            {
                System.Diagnostics.Debug.WriteLine($"📋 收到采集配置: {config}");
            }

            Application.Current.Dispatcher.Invoke(() =>
            {
                long? parsedDeviceId = long.TryParse(deviceId, out var value) ? value : null;
                _mainWindow.CreateBrowserForAccount(detailId, accountId,
                    string.IsNullOrEmpty(cookie) ? null : cookie,
                    string.IsNullOrEmpty(searchUrl) ? null : searchUrl,
                    expectedCount,
                    taskType: taskType,
                    config: config,
                    isOperation: isOperation,
                    deviceId: parsedDeviceId,
                    password: password,
                    tfa: tfa,
                    loginAccountId: fbAccount,
                    proxyConfigJson: proxyConfigJson);
            });
        }

        /// <summary>
        /// Vue 调用此方法关闭浏览器
        /// </summary>
        public void StopBrowser(string accountId)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                _mainWindow.CloseBrowserForAccount(accountId);
            });
        }

        public void OpenMessageManagerWindow()
        {
            Application.Current.Dispatcher.Invoke(_mainWindow.OpenMessageManagerWindow);
        }

        /// <summary>
        /// 启动 Facebook 主页资料修改任务。浏览器仍由统一指纹浏览器矩阵承载。
        /// </summary>
        public void StartProfileUpdateTask(string taskId, string accountId, string cookie,
            string deviceId, string profileConfigJson, string proxyConfigJson = null)
        {
            _mainWindow.ApplyPendingUpdateOnUserStart();
            Application.Current.Dispatcher.Invoke(() =>
            {
                long? parsedDeviceId = long.TryParse(deviceId, out var value) ? value : null;
                _mainWindow.CreateBrowserForAccount(
                    detailId: $"profile_{taskId}_{accountId}",
                    accountId: accountId,
                    cookie: string.IsNullOrWhiteSpace(cookie) ? null : cookie,
                    searchUrl: "https://www.facebook.com/profile.php",
                    expectedCount: 0,
                    taskType: 18,
                    config: profileConfigJson,
                    isOperation: true,
                    deviceId: parsedDeviceId,
                    proxyConfigJson: proxyConfigJson);
            });
        }

        public void StartAccountLoginBatch(string accountsJson)
        {
            _mainWindow.ApplyPendingUpdateOnUserStart();
            Application.Current.Dispatcher.Invoke(() =>
            {
                _mainWindow.StartAccountLoginBatch(accountsJson);
            });
        }

        /// <summary>
        /// 获取当前还可启动的账号浏览器窗口数量，供 Vue 领取待执行采集明细时控制并发。
        /// </summary>
        public int GetAvailableBrowserSlots()
        {
            try
            {
                var current = _mainWindow.GetBrowserWindowCount();
                return Math.Max(BrowserMatrixWindow.MaxConcurrentBrowsers - current, 0);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"⚠️ 获取浏览器空闲槽位失败: {ex.Message}");
                return 0;
            }
        }

        public void StartMessageMonitor(string monitorId, string accountId, string cookie,
            string deviceId, string url, string mode, string proxyConfigJson = null)
        {
            _mainWindow.ApplyPendingUpdateOnUserStart();
            Application.Current.Dispatcher.Invoke(() => _mainWindow.StartMessageMonitorTask(
                monitorId, accountId, cookie, deviceId, mode, $"message-monitor-{monitorId}", proxyConfigJson));
        }

        /// <summary>
        /// 显示消息提示
        /// </summary>
        public void ShowMessage(string message)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                MessageBox.Show(message, "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            });
        }

        /// <summary>
        /// Vue 调用此方法批量切换 Facebook 账号语言。不同账号按可用槽位并行执行，同一账号保持任务串行。
        /// </summary>
        /// <param name="accountPayload">账号对象数组(JSON字符串)，包含 accountId 和 cookie</param>
        /// <param name="languageSpec">语言描述JSON，包含 code、nativeName、englishName</param>
        public void SetAccountLanguage(string accountPayload, string languageSpec)
        {
            Application.Current.Dispatcher.Invoke(async () =>
            {
                try
                {
                    var accountItems = new List<(string AccountId, string Cookie)>();
                    var tokens = JArray.Parse(accountPayload ?? "[]");
                    foreach (var token in tokens)
                    {
                        var accountId = token.Type == JTokenType.String
                            ? token.ToString()
                            : token["accountId"]?.ToString() ?? "";
                        if (!string.IsNullOrWhiteSpace(accountId))
                        {
                            accountItems.Add((accountId, token.Type == JTokenType.Object
                                ? token["cookie"]?.ToString() ?? ""
                                : ""));
                        }
                    }

                    var languageObject = languageSpec?.TrimStart().StartsWith("{") == true
                        ? JObject.Parse(languageSpec)
                        : new JObject();
                    var languageCode = languageObject["code"]?.ToString() ?? languageSpec ?? "";
                    var nativeName = languageObject["nativeName"]?.ToString() ?? languageCode;
                    var englishName = languageObject["englishName"]?.ToString() ?? languageCode;
                    if (accountItems.Count == 0 || string.IsNullOrWhiteSpace(languageCode))
                    {
                        MessageBox.Show("没有选择账号或语言", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    System.Diagnostics.Debug.WriteLine($"🌐 开始为 {accountItems.Count} 个账号设置语言: {languageCode}");

                    var mainWindow = Application.Current.MainWindow as MainWindow;
                    if (mainWindow == null)
                    {
                        MessageBox.Show("主窗口未找到", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    int successCount = 0;
                    int failCount = 0;
                    async Task ProcessAccountAsync((string AccountId, string Cookie) item)
                    {
                        try
                        {
                            await SwitchBrowserLanguage(mainWindow, item.AccountId, item.Cookie, languageCode, nativeName, englishName);
                            Interlocked.Increment(ref successCount);
                            System.Diagnostics.Debug.WriteLine($"✅ 账号 {item.AccountId} 语言切换成功");
                        }
                        catch (Exception ex)
                        {
                            Interlocked.Increment(ref failCount);
                            System.Diagnostics.Debug.WriteLine($"❌ 账号 {item.AccountId} 语言切换失败: {ex.Message}");
                        }
                    }

                    var existingAccounts = accountItems
                        .Where(item => mainWindow.GetBrowserMatrixWindowForAccount(item.AccountId)?.HasBrowser(item.AccountId) == true)
                        .ToList();
                    var newAccounts = accountItems
                        .Where(item => mainWindow.GetBrowserMatrixWindowForAccount(item.AccountId)?.HasBrowser(item.AccountId) != true)
                        .ToList();

                    // 已有 Tab 不新增槽位，可以并行切换；每个账号自身仍由账号任务锁保证串行。
                    await Task.WhenAll(existingAccounts.Select(ProcessAccountAsync));

                    // 新 Tab 按当前可用槽位分批并行，任务完成后 Tab 会释放，下一批再进入。
                    var occupied = mainWindow.GetActiveBrowserWindowCount();
                    var availableSlots = Math.Max(0, BrowserMatrixWindow.MaxConcurrentBrowsers - occupied);
                    if (newAccounts.Count > 0 && availableSlots == 0)
                    {
                        System.Diagnostics.Debug.WriteLine(
                            $"⛔ 语言切换未启动新账号：当前浏览器已达到最大槽位 {BrowserMatrixWindow.MaxConcurrentBrowsers}");
                        failCount += newAccounts.Count;
                    }
                    else
                    {
                        for (var offset = 0; offset < newAccounts.Count; offset += Math.Max(1, availableSlots))
                        {
                            var wave = newAccounts.Skip(offset).Take(Math.Max(1, availableSlots)).ToList();
                            await Task.WhenAll(wave.Select(ProcessAccountAsync));
                        }
                    }

                    // 语言切换结束后不要关闭整个统一窗口。临时 Tab 即使被释放，
                    // 后续帖子采集仍应复用这个窗口；窗口为空时由下一次任务自然创建 Tab。
                    System.Diagnostics.Debug.WriteLine($"📊 语言设置完成 - 总计:{accountItems.Count}, 成功:{successCount}, 失败:{failCount}");
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        $"设置语言失败: {ex.Message}\n\n{ex.StackTrace}",
                        "错误",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error
                    );
                }
            });
        }

        // 兼容旧版前端调用。
        public void SetAccountLanguage(string accountPayload, int language)
        {
            SetAccountLanguage(accountPayload, JsonConvert.SerializeObject(new
            {
                code = language == 1 ? "en_US" : "zh_CN",
                nativeName = language == 1 ? "English (US)" : "中文(简体)",
                englishName = language == 1 ? "English (US)" : "Simplified Chinese (China)"
            }));
        }
        
        /// <summary>
        /// Vue 调用此方法更新指纹浏览器全局配置（立即生效）
        /// </summary>
        /// <param name="disableImages">是否禁用图片</param>
        /// <param name="disableVideos">是否禁用视频</param>
        /// <param name="maxConcurrent">最大并发数</param>
        public void UpdateGlobalConfig(bool disableImages, bool disableVideos, int maxConcurrent)
        {
            BrowserMatrixWindow.UpdateGlobalConfig(disableImages, disableVideos, maxConcurrent);
            System.Diagnostics.Debug.WriteLine($"✅ 全局配置已同步到WPF: DisableImages={disableImages}, DisableVideos={disableVideos}, MaxConcurrent={maxConcurrent}");
        }

        /// <summary>
        /// Vue 调用此方法启动私信发送任务
        /// </summary>
        /// <param name="taskId">任务ID</param>
        /// <param name="detailId">明细ID</param>
        /// <param name="accountId">账号ID</param>
        /// <param name="cookie">Cookie</param>
        /// <param name="fbUserId">目标用户FB ID</param>
        /// <param name="messageText">消息内容</param>
        public async void StartDmTask(string taskId, string detailId, string accountId, string cookie, string fbUserId, string messageText, string password = null, string tfa = null, string proxyConfigJson = null)
        {
            _mainWindow.ApplyPendingUpdateOnUserStart();
            Application.Current.Dispatcher.Invoke(async () =>
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine($"🚀 启动私信任务: TaskId={taskId}, DetailId={detailId}, AccountId={accountId}, TargetUser={fbUserId}");
                    
                    // 获取BrowserMatrixWindow实例
                    var mainWindow = Application.Current.MainWindow as MainWindow;
                    if (mainWindow == null)
                    {
                        System.Diagnostics.Debug.WriteLine("❌ 主窗口未找到");
                        return;
                    }

                    var browserMatrixWindow = mainWindow.GetBrowserMatrixWindowForAccount(accountId);
                    
                    bool needCreateBrowser = browserMatrixWindow == null ||
                                            !browserMatrixWindow.HasBrowser(accountId);

                    if (needCreateBrowser)
                    {
                        System.Diagnostics.Debug.WriteLine($"🌐 创建浏览器并导航到私信页面...");
                        var dmConfig = Newtonsoft.Json.JsonConvert.SerializeObject(new {
                            taskId = taskId,
                            fbUserId = fbUserId,
                            messageText = messageText
                        });
                        mainWindow.CreateBrowserForAccount(
                            detailId,
                            accountId,
                            string.IsNullOrEmpty(cookie) ? null : cookie,
                            $"https://www.facebook.com/messages/t/{fbUserId}/",
                            expectedCount: 0,
                            taskType: 14,
                            config: dmConfig,
                            isOperation: true,
                            password: password,
                            tfa: tfa,
                            loginAccountId: accountId,
                            proxyConfigJson: proxyConfigJson);
                        System.Diagnostics.Debug.WriteLine($"✅ 私信任务已提交执行: TaskId={taskId}, DetailId={detailId}");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"♻️ 复用已有浏览器执行私信: AccountId={accountId}, DetailId={detailId}");
                        await browserMatrixWindow.ExecuteDmDetailAsync(taskId, detailId, accountId, fbUserId, messageText);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"❌ 私信任务异常: {ex.Message}\n{ex.StackTrace}");
                }
            });
        }

        /// <summary>
        /// 为单个账号切换浏览器语言
        /// </summary>
        private async Task SwitchBrowserLanguage(MainWindow mainWindow, string accountId, string cookie,
            string languageCode, string nativeName, string englishName)
        {
            var browserMatrixWindow = mainWindow.GetOrCreateBrowserMatrixWindow(accountId);
            var hadExistingBrowser = browserMatrixWindow.HasBrowser(accountId);
            Task<BrowserMatrixWindow.FacebookPageState>? readyTask = null;
            if (!hadExistingBrowser)
            {
                // 注册首页完成事件后再创建 Tab，不能先创建再延迟等待，否则会错过初始化事件。
                readyTask = browserMatrixWindow.WaitForBrowserReadyAsync(accountId);
                var detailId = $"lang_{accountId}_{DateTime.Now.Ticks}";
                mainWindow.CreateBrowserForAccount(detailId, accountId, cookie, null, 0, taskType: 99);
                var completed = await Task.WhenAny(readyTask, Task.Delay(30000));
                if (completed != readyTask)
                {
                    throw new TimeoutException($"账号 {accountId} Facebook 首页初始化超时");
                }
                var state = await readyTask;
                if (state != BrowserMatrixWindow.FacebookPageState.Authenticated)
                {
                    throw new InvalidOperationException(
                        $"账号 {accountId} Facebook 首页未完成登录: {state}");
                }
            }
            await browserMatrixWindow.SwitchLanguageForAccount(
                accountId, cookie, languageCode, nativeName, englishName, closeAfterTask: !hadExistingBrowser);
        }
        
        /// <summary>
        /// 等待浏览器实例就绪
        /// </summary>
        private async Task WaitForBrowserReady(BrowserMatrixWindow browserMatrixWindow, string accountId, int timeoutMs = 15000)
        {
            var startTime = DateTime.Now;
            int checkInterval = 200;
            
            while ((DateTime.Now - startTime).TotalMilliseconds < timeoutMs)
            {
                // 检查浏览器是否已创建
                if (browserMatrixWindow.GetActiveBrowserCount() > 0)
                {
                    System.Diagnostics.Debug.WriteLine($"✅ 浏览器实例已就绪: {accountId}");
                    await Task.Delay(500); // 额外等待确保页面开始加载
                    return;
                }
                
                await Task.Delay(checkInterval);
            }
            
            throw new TimeoutException($"等待浏览器就绪超时 ({timeoutMs}ms)");
        }

        /// <summary>
        /// Vue 调用此方法启动发个人帖任务
        /// </summary>
        /// <param name="taskId">任务ID</param>
        /// <param name="accountId">账号ID</param>
        /// <param name="cookie">Cookie</param>
        /// <param name="actionConfigJson">动作配置JSON</param>
        public async void StartPublishPostTask(string taskId, string accountId, string cookie, string actionConfigJson, string password = null, string tfa = null, string detailId = null, string proxyConfigJson = null)
        {
            _mainWindow.ApplyPendingUpdateOnUserStart();
            Application.Current.Dispatcher.Invoke(async () =>
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine($"🚀 启动发个人帖任务: TaskId={taskId}, AccountId={accountId}");
                    if (!_directPublishAccounts.TryAdd(accountId, 0))
                    {
                        System.Diagnostics.Debug.WriteLine($"⛔ 忽略重复的发个人帖调用: account={accountId}, taskId={taskId}");
                        return;
                    }
                    
                    // 获取BrowserMatrixWindow实例
                    var mainWindow = Application.Current.MainWindow as MainWindow;
                    if (mainWindow == null)
                    {
                        System.Diagnostics.Debug.WriteLine("❌ 主窗口未找到");
                        return;
                    }

                    var browserMatrixWindow = mainWindow.GetBrowserMatrixWindowForAccount(accountId);
                    
                    // 如果窗口不存在或浏览器不存在，先创建
                    bool needCreateBrowser = browserMatrixWindow == null || 
                                            !browserMatrixWindow.HasBrowser(accountId);
                    
                    if (needCreateBrowser)
                    {
                        System.Diagnostics.Debug.WriteLine($"🆕 为账号 {accountId} 创建浏览器");

                        // 必须在创建 Tab 前注册首页初始化信号，等待 Cookie/密码/2FA 登录流程完成。
                        browserMatrixWindow ??= mainWindow.GetOrCreateBrowserMatrixWindow(accountId);
                        var readyTask = browserMatrixWindow.WaitForBrowserReadyAsync(accountId);
                        
                        // 创建浏览器（不指定URL，稍后脚本会导航）
                        var callbackDetailId = string.IsNullOrWhiteSpace(detailId)
                            ? $"publish_post_{taskId}_{accountId}"
                            : detailId;
                        mainWindow.CreateBrowserForAccount(
                            detailId: callbackDetailId,
                            accountId: accountId,
                            cookie: string.IsNullOrEmpty(cookie) ? null : cookie,
                            searchUrl: null,
                            expectedCount: 0,
                            taskType: 12,
                            password: password,
                            tfa: tfa,
                            loginAccountId: accountId,
                            proxyConfigJson: proxyConfigJson); // 发个人帖任务类型

                        browserMatrixWindow = mainWindow.GetBrowserMatrixWindowForAccount(accountId);

                        var completed = await Task.WhenAny(readyTask, Task.Delay(30000));
                        if (completed != readyTask)
                        {
                            throw new TimeoutException($"账号 {accountId} 首页登录状态确认超时");
                        }
                        var pageState = await readyTask;
                        if (pageState != BrowserMatrixWindow.FacebookPageState.Authenticated)
                        {
                            throw new InvalidOperationException(
                                $"账号 {accountId} 未完成登录，无法发个人帖: {pageState}");
                        }
                    }

                    if (browserMatrixWindow != null)
                    {
                        await browserMatrixWindow.WaitForAccountPageReady(accountId, 30000);

                        // 执行发个人帖
                        System.Diagnostics.Debug.WriteLine($"📝 开始执行发个人帖...");
                        await browserMatrixWindow.ExecutePublishPost(accountId, actionConfigJson);

                        var callbackDetailId = string.IsNullOrWhiteSpace(detailId)
                            ? $"publish_post_{taskId}_{accountId}"
                            : detailId;
                        var resultJson = JsonConvert.SerializeObject(new
                        {
                            success = true,
                            taskId,
                            detailId = callbackDetailId,
                            accountId,
                            actualCount = 1,
                            message = "发个人帖成功"
                        });
                        browserMatrixWindow.NotifyCollectionComplete(
                            callbackDetailId, accountId, resultJson, 12);
                        
                        System.Diagnostics.Debug.WriteLine($"✅ 发个人帖任务完成: TaskId={taskId}");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"❌ BrowserMatrixWindow 未找到");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"❌ 发个人帖任务异常: {ex.Message}\n{ex.StackTrace}");
                }
                finally
                {
                    _directPublishAccounts.TryRemove(accountId, out _);
                }
            });
        }

        /// <summary>
        /// Vue 调用此方法启动发群帖任务
        /// </summary>
        /// <param name="taskId">任务ID</param>
        /// <param name="accountId">账号ID</param>
        /// <param name="cookie">Cookie</param>
        /// <param name="actionConfigJson">动作配置JSON</param>
        public async void StartGroupPublishTask(string taskId, string accountId, string cookie, string actionConfigJson, string detailId = "", string password = null, string tfa = null, string fbAccount = null, string proxyConfigJson = null)
        {
            _mainWindow.ApplyPendingUpdateOnUserStart();
            Application.Current.Dispatcher.Invoke(async () =>
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine($"🚀 启动发群帖任务: TaskId={taskId}, AccountId={accountId}");
                    
                    // 获取BrowserMatrixWindow实例
                    var mainWindow = Application.Current.MainWindow as MainWindow;
                    if (mainWindow == null)
                    {
                        System.Diagnostics.Debug.WriteLine("❌ 主窗口未找到");
                        return;
                    }

                    var browserMatrixWindow = mainWindow.GetBrowserMatrixWindowForAccount(accountId);
                    
                    // 如果窗口不存在或浏览器不存在，先创建
                    bool needCreateBrowser = browserMatrixWindow == null || 
                                            !browserMatrixWindow.HasBrowser(accountId);
                    
                    if (needCreateBrowser)
                    {
                        System.Diagnostics.Debug.WriteLine($"🆕 为账号 {accountId} 创建浏览器");

                        // 发群帖也必须等待首页完成 Cookie/密码/2FA 登录，不能只等待页面可执行脚本。
                        browserMatrixWindow ??= mainWindow.GetOrCreateBrowserMatrixWindow(accountId);
                        var readyTask = browserMatrixWindow.WaitForBrowserReadyAsync(accountId);
                        
                        // 创建浏览器（不指定URL，稍后脚本会导航）
                        mainWindow.CreateBrowserForAccount(
                            detailId: string.IsNullOrEmpty(detailId) ? $"group_publish_{taskId}_{accountId}" : detailId,
                            accountId: accountId,
                            cookie: string.IsNullOrEmpty(cookie) ? null : cookie,
                            searchUrl: null,
                            expectedCount: 0,
                            taskType: 13,
                            password: password,
                            tfa: tfa,
                            loginAccountId: string.IsNullOrWhiteSpace(fbAccount) ? accountId : fbAccount,
                            proxyConfigJson: proxyConfigJson); // 发群帖任务类型
                        
                        browserMatrixWindow = mainWindow.GetBrowserMatrixWindowForAccount(accountId);

                        var completed = await Task.WhenAny(readyTask, Task.Delay(30000));
                        if (completed != readyTask)
                        {
                            throw new TimeoutException($"账号 {accountId} 首页登录状态确认超时");
                        }
                        var pageState = await readyTask;
                        if (pageState != BrowserMatrixWindow.FacebookPageState.Authenticated)
                        {
                            throw new InvalidOperationException(
                                $"账号 {accountId} 未完成登录，无法发群帖: {pageState}");
                        }
                    }

                    if (browserMatrixWindow != null)
                    {
                        await browserMatrixWindow.WaitForAccountPageReady(accountId, 30000);

                        // 执行发群帖
                        System.Diagnostics.Debug.WriteLine($"👥 开始执行发群帖...");
                        await browserMatrixWindow.ExecuteGroupPublish(accountId, actionConfigJson, detailId);
                        
                        System.Diagnostics.Debug.WriteLine($"✅ 发群帖任务完成: TaskId={taskId}");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"❌ BrowserMatrixWindow 未找到");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"❌ 发群帖任务异常: {ex.Message}\n{ex.StackTrace}");
                }
            });
        }

        /// <summary>
        /// Vue 调用此方法打开文件选择对话框（支持多选）
        /// </summary>
        /// <param name="filter">文件过滤器，如 "图片/视频|*.jpg;*.jpeg;*.png;*.gif;*.mp4;*.avi;*.mov"</param>
        /// <returns>JSON数组字符串，包含选中的文件完整路径</returns>
        public string SelectMediaFiles(string filter = "")
        {
            string[] selectedFiles = Array.Empty<string>();
            Exception dialogException = null;
            var dialogThread = new Thread(() =>
            {
                try
                {
                    var dialog = new Microsoft.Win32.OpenFileDialog
                    {
                        Multiselect = true,
                        Title = "选择图片或视频",
                        Filter = string.IsNullOrEmpty(filter) 
                            ? "图片/视频|*.jpg;*.jpeg;*.png;*.gif;*.bmp;*.webp;*.mp4;*.avi;*.mov;*.wmv;*.flv|所有文件|*.*"
                            : filter
                    };

                    if (dialog.ShowDialog() == true)
                    {
                        selectedFiles = dialog.FileNames;
                        System.Diagnostics.Debug.WriteLine($"✅ 已选择 {selectedFiles.Length} 个文件");
                        foreach (var file in selectedFiles)
                        {
                            System.Diagnostics.Debug.WriteLine($"   - {file}");
                        }
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("⚠️ 用户取消了文件选择");
                    }
                }
                catch (Exception ex)
                {
                    dialogException = ex;
                }
            });
            dialogThread.SetApartmentState(ApartmentState.STA);
            dialogThread.IsBackground = true;
            dialogThread.Start();
            dialogThread.Join();

            if (dialogException != null)
            {
                System.Diagnostics.Debug.WriteLine($"❌ 文件选择失败: {dialogException}");
                throw new InvalidOperationException("打开文件选择窗口失败", dialogException);
            }

            // 返回 JSON 数组字符串
            return JsonConvert.SerializeObject(selectedFiles);
        }

    }
}
