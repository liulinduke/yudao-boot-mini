using CefSharp;
using CefSharp.Wpf;
using Newtonsoft.Json;
using OtpNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace SocialMatrix.WpfHost.Windows
{
    public partial class BrowserMatrixWindow
    {
        public record AccountLoginRequest(long Id, string AccountId, string? Password, string? Tfa, string? Cookie, string? ProxyConfigJson = null);
        public record AccountLoginResult(
            long AccountDbId,
            string AccountId,
            string Status,
            string? LoginMode = null,
            string? ErrorReason = null,
            bool CookieSaved = false,
            bool WindowClosed = false,
            [property: JsonIgnore] string? CookieJson = null);

        private readonly Queue<AccountLoginRequest> _accountLoginQueue = new();
        private readonly List<AccountLoginResult> _accountLoginResults = new();
        private int _accountLoginRunningCount = 0;
        private bool _accountLoginBatchActive = false;
        private bool _accountLoginCloseAfterEachAccount = false;
        private readonly object _accountLoginLock = new();

        public event Action<string>? OnAccountLoginProgress;
        public event Action<string>? OnAccountLoginBatchComplete;

        /// <summary>
        /// 在指定浏览器中复用账号管理的完整登录流程。结果只通过 WPF 事件返回，
        /// 由 Vue 调用后台接口保存。
        /// </summary>
        public async Task<AccountLoginResult> LoginAccountInBrowserAsync(ChromiumWebBrowser browser, AccountLoginRequest account)
        {
            return await LoginAccountWithBrowserAsync(browser, account);
        }

        public void StartAccountLoginBatch(List<AccountLoginRequest> accounts, bool? closeAfterEachAccountOverride = null)
        {
            lock (_accountLoginLock)
            {
                _accountLoginQueue.Clear();
                _accountLoginResults.Clear();
                foreach (var account in accounts)
                {
                    _accountLoginQueue.Enqueue(account);
                }
                _accountLoginRunningCount = 0;
                _accountLoginBatchActive = true;
                _accountLoginCloseAfterEachAccount = closeAfterEachAccountOverride ?? accounts.Count > _maxConcurrentBrowsers;
            }

            _ = PumpAccountLoginQueueAsync();
        }

        private async Task PumpAccountLoginQueueAsync()
        {
            while (true)
            {
                AccountLoginRequest? nextAccount = null;
                bool shouldFinish = false;

                lock (_accountLoginLock)
                {
                    if (_accountLoginQueue.Count > 0 && _accountLoginRunningCount < _maxConcurrentBrowsers)
                    {
                        nextAccount = _accountLoginQueue.Dequeue();
                        _accountLoginRunningCount++;
                    }
                    else if (_accountLoginQueue.Count == 0 && _accountLoginRunningCount == 0 && _accountLoginBatchActive)
                    {
                        _accountLoginBatchActive = false;
                        shouldFinish = true;
                    }
                }

                if (shouldFinish)
                {
                    var payload = JsonConvert.SerializeObject(new
                    {
                        summary = new
                        {
                            total = _accountLoginResults.Count,
                            success = _accountLoginResults.Count(x => x.Status == "success"),
                            failed = _accountLoginResults.Count(x => x.Status == "failed"),
                            skipped = _accountLoginResults.Count(x => x.Status == "skipped")
                        },
                        results = _accountLoginResults.Select(result => new
                        {
                            accountDbId = result.AccountDbId.ToString(),
                            accountId = result.AccountId,
                            status = result.Status,
                            loginMode = result.LoginMode,
                            errorReason = result.ErrorReason,
                            cookieSaved = result.CookieSaved,
                            cookie = result.CookieSaved ? result.CookieJson : null,
                            windowClosed = result.WindowClosed
                        })
                    });
                    OnAccountLoginBatchComplete?.Invoke(payload);
                    if (_accountLoginCloseAfterEachAccount
                        && !KeepBrowserAfterTaskForDebug
                        && GetActiveBrowserCount() == 0)
                    {
                        Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            Close();
                        }));
                    }
                    break;
                }

                if (nextAccount == null)
                {
                    await Task.Delay(300);
                    continue;
                }

                _ = ExecuteAccountLoginAsync(nextAccount);
            }
        }

        private async Task ExecuteAccountLoginAsync(AccountLoginRequest account)
        {
            await EmitAccountLoginProgress(new AccountLoginResult(account.Id, account.AccountId, "running"));

            AccountLoginResult result;
            try
            {
                var detailId = $"account_login_{account.AccountId}_{DateTimeOffset.Now.ToUnixTimeMilliseconds()}";
                Application.Current.Dispatcher.Invoke(() =>
                {
                    CreateBrowser(
                        account.AccountId,
                        "https://www.facebook.com",
                        account.Cookie,
                        null,
                        0,
                        taskType: 1,
                        detailId: detailId,
                        isOperation: false,
                        proxyConfigJson: account.ProxyConfigJson);
                });

                await Task.Delay(2500);
                var browser = GetBrowser(account.AccountId);
                if (browser == null)
                {
                    result = new AccountLoginResult(account.Id, account.AccountId, "failed", ErrorReason: "Browser was not created");
                }
                else
                {
                    result = await LoginAccountWithBrowserAsync(browser, account);
                }
            }
            catch (Exception ex)
            {
                result = new AccountLoginResult(account.Id, account.AccountId, "failed", ErrorReason: ex.Message);
            }

            bool windowClosed = false;
            if (_accountLoginCloseAfterEachAccount && !KeepBrowserAfterTaskForDebug)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    CloseBrowser(account.AccountId);
                    windowClosed = true;
                });
            }
            result = result with { WindowClosed = windowClosed };

            await EmitAccountLoginProgress(result);

            lock (_accountLoginLock)
            {
                _accountLoginResults.Add(result);
                _accountLoginRunningCount = Math.Max(0, _accountLoginRunningCount - 1);
            }
        }

        private async Task<AccountLoginResult> LoginAccountWithBrowserAsync(ChromiumWebBrowser browser, AccountLoginRequest account)
        {
            System.Diagnostics.Debug.WriteLine(
                $"🔐 账号管理登录开始: account={account.AccountId}, " +
                $"cookie={(HasUsableFacebookCookie(account.Cookie) ? "有效" : "空")}, " +
                $"password={(!string.IsNullOrWhiteSpace(account.Password) ? "已配置" : "未配置")}, " +
                $"tfa={(!string.IsNullOrWhiteSpace(account.Tfa) ? "已配置" : "未配置")}");
            // 登录只等待 JS/关键 DOM 可检测，不等待所有图片、视频和长连接结束。
            await Task.Delay(800);

            var pageState = await DetectFacebookPageStateAsync(browser, account.AccountId);
            for (var attempt = 0; attempt < 20
                 && (pageState == FacebookPageState.PageLoading || pageState == FacebookPageState.Unknown); attempt++)
            {
                await Task.Delay(500);
                pageState = await DetectFacebookPageStateAsync(browser, account.AccountId);
            }

            // 页面资源可能仍在加载，但登录表单已经可以交互；此时不能因 readyState/CEF
            // 状态为 PageLoading 而跳过密码提交，直接依据可见 DOM 判断登录页。
            if (pageState == FacebookPageState.PageLoading || pageState == FacebookPageState.Unknown)
            {
                try
                {
                    var formResult = await browser.EvaluateScriptAsync(@"
                        (() => {
                            const visible = el => !!el && !!(el.offsetWidth || el.offsetHeight || el.getClientRects().length);
                            const email = [...document.querySelectorAll('input[name=""email""], input[type=""email""], input[autocomplete=""username""]')]
                                .some(visible);
                            const password = [...document.querySelectorAll('input[name=""pass""], input[type=""password""], input[autocomplete=""current-password""]')]
                                .some(visible);
                            return email && password;
                        })();");
                    if (formResult.Success && formResult.Result is bool hasCredentialForm && hasCredentialForm)
                    {
                        pageState = FacebookPageState.LoginPage;
                        System.Diagnostics.Debug.WriteLine($"🔍 账号 {account.AccountId} 检测到可交互登录表单，按登录页处理");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"⚠️ 账号 {account.AccountId} 登录表单兜底检测失败: {ex.Message}");
                }
            }
            System.Diagnostics.Debug.WriteLine($"🔍 账号 {account.AccountId} Cookie 后页面状态: {pageState}");

            // 只在 Facebook 的隐私引导 URL 出现时才处理引导。
            // 正常首页不扫描 Get started，避免无意义的 DOM 轮询。
            var privacyConsentOpen = await IsFacebookPrivacyConsentPageAsync(browser);
            var getStartedClicked = false;
            for (var attempt = 0; privacyConsentOpen && attempt < 20 && !getStartedClicked; attempt++)
            {
                getStartedClicked = await ClickFacebookGetStartedAsync(browser);
                if (!getStartedClicked)
                {
                    await Task.Delay(500);
                }
            }
            if (privacyConsentOpen)
            {
                if (getStartedClicked)
                {
                    System.Diagnostics.Debug.WriteLine($"▶️ 账号 {account.AccountId} 检测到新手引导，已点击主操作按钮");
                }
                var privacyPageStillOpen = false;
                for (var attempt = 0; attempt < 30; attempt++)
                {
                    await Task.Delay(500);
                    // Get started 点击后可能仍停留在隐私方案页；继续处理 Free/Continue。
                    if (await ClickFacebookGetStartedAsync(browser))
                    {
                        System.Diagnostics.Debug.WriteLine($"▶️ 账号 {account.AccountId} 引导后继续处理 Free/Continue");
                    }
                    var privacyStillOpen = await browser.EvaluateScriptAsync(
                        "location.pathname.includes('/privacy/consent/')");
                    if (privacyStillOpen.Success && privacyStillOpen.Result is bool stillOpen && stillOpen)
                    {
                        privacyPageStillOpen = true;
                        continue;
                    }
                    privacyPageStillOpen = false;
                    var guideState = await DetectFacebookAuthStateAsync(browser);
                    if (guideState == "home")
                    {
                        await DismissPostLoginOverlayAsync(browser);
                        var cookieJson = await ExportFacebookCookiesAsync(browser);
                        if (!string.IsNullOrWhiteSpace(cookieJson))
                        {
                            return new AccountLoginResult(account.Id, account.AccountId, "success", "cookie", null, true, CookieJson: cookieJson);
                        }
                        return new AccountLoginResult(account.Id, account.AccountId, "failed", "cookie", "引导页完成但 Cookie 未能保存");
                    }
                    if (guideState == "two_factor" || guideState == "checkpoint" || guideState == "disabled")
                    {
                        break;
                    }
                }
                if (privacyPageStillOpen)
                {
                    return new AccountLoginResult(
                        account.Id,
                        account.AccountId,
                        "failed",
                        "cookie",
                        "隐私方案页面未完成，Continue 仍未跳转");
                }
            }

            // Facebook 已保存账号选择页也可能位于 /login，但没有邮箱/密码输入框，
            // 需要先点击 Continue，再等待后续 2FA DOM；普通登录页没有 Continue，继续走密码登录。
            if (pageState == FacebookPageState.Unknown || pageState == FacebookPageState.LoginPage)
            {
                var continueClicked = await ClickFacebookContinueAsync(browser);
                if (continueClicked)
                {
                    System.Diagnostics.Debug.WriteLine($"▶️ 账号 {account.AccountId} 检测到 Continue，已点击并等待页面状态");
                    try
                    {
                        await WaitForPageLoad(browser, 30000);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"⚠️ 账号 {account.AccountId} 点击 Continue 后页面等待异常: {ex.Message}");
                    }
                    var continueHome = false;
                    var homeScriptSuccess = false;
                    object? homeScriptResult = null;
                    for (var attempt = 0; attempt < 20; attempt++)
                    {
                        try
                        {
                            if (browser.IsDisposed || !browser.CanExecuteJavascriptInMainFrame)
                            {
                                await Task.Delay(500);
                                continue;
                            }
                            var continueHomeResult = await RunOnBrowserUiThreadAsync(browser, () => browser.EvaluateScriptAsync(@"
                                (() => {
                                    if (document.readyState !== 'complete') return false;
                                    const hasHomeDom = !!document.querySelector(
                                        '[role=""feed""], [data-pagelet=""MainFeed""], [role=""main""]');
                                    const hasCredentialForm = !!document.querySelector(
                                        'input[name=""email""], input[name=""pass""], input[type=""password""]');
                                    return hasHomeDom && !hasCredentialForm;
                                })();"));
                            homeScriptSuccess = continueHomeResult.Success;
                            homeScriptResult = continueHomeResult.Result;
                            if (continueHomeResult.Success && continueHomeResult.Result is bool isHome && isHome)
                            {
                                continueHome = true;
                                break;
                            }
                            if (await IsFacebookHomeFromDomSourceAsync(browser))
                            {
                                continueHome = true;
                                homeScriptSuccess = true;
                                homeScriptResult = "dom-source";
                                break;
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"⚠️ 账号 {account.AccountId} Continue 后首页 DOM 检测异常: {ex.Message}");
                        }
                        await Task.Delay(500);
                    }
                    System.Diagnostics.Debug.WriteLine(
                        $"🔍 账号 {account.AccountId} Continue 后首页 DOM 检测: success={homeScriptSuccess}, result={homeScriptResult}");
                    if (continueHome)
                    {
                        System.Diagnostics.Debug.WriteLine($"✅ 账号 {account.AccountId} Continue 后页面加载完成，首页 DOM 已确认");
                        await DismissPostLoginOverlayAsync(browser);
                        var cookieJson = await ExportFacebookCookiesAsync(browser);
                        if (!string.IsNullOrWhiteSpace(cookieJson))
                        {
                            return new AccountLoginResult(account.Id, account.AccountId, "success", "cookie", null, true, CookieJson: cookieJson);
                        }
                        return new AccountLoginResult(account.Id, account.AccountId, "failed", "cookie", "首页已加载但 Cookie 未能保存");
                    }
                    var continueAuthState = "unknown";
                    for (var attempt = 0; attempt < 30; attempt++)
                    {
                        await Task.Delay(500);
                        continueAuthState = await DetectFacebookAuthStateAsync(browser);
                        if (continueAuthState == "home"
                            || continueAuthState == "two_factor"
                            || continueAuthState == "remember_browser"
                            || continueAuthState == "disabled"
                            || continueAuthState == "checkpoint"
                            || continueAuthState == "phone_verify"
                            || continueAuthState == "email_verify"
                            || continueAuthState == "identity_verify"
                            || continueAuthState == "recover_code"
                            || continueAuthState == "password_incorrect")
                        {
                            break;
                        }
                    }
                    if (continueAuthState == "unknown")
                    {
                        for (var attempt = 0; attempt < 20; attempt++)
                        {
                            var continuePageState = await DetectFacebookPageStateAsync(browser, account.AccountId);
                            if (continuePageState == FacebookPageState.Authenticated)
                            {
                                continueAuthState = "home";
                                System.Diagnostics.Debug.WriteLine($"✅ 账号 {account.AccountId} Continue 后已通过首页 DOM 确认登录成功");
                                break;
                            }
                            await Task.Delay(500);
                        }
                    }
                    System.Diagnostics.Debug.WriteLine($"🔍 账号 {account.AccountId} Continue 后页面状态: {continueAuthState}");
                    if (continueAuthState == "home")
                    {
                        await DismissPostLoginOverlayAsync(browser);
                        var cookieJson = await ExportFacebookCookiesAsync(browser);
                        if (!string.IsNullOrWhiteSpace(cookieJson))
                        {
                            return new AccountLoginResult(account.Id, account.AccountId, "success", "cookie", null, true, CookieJson: cookieJson);
                        }
                    }
                    if (continueAuthState == "two_factor")
                    {
                        return await SubmitTwoFactorAndCaptureAsync(browser, account);
                    }
                    if (await SubmitContinuePasswordAsync(browser, account.Password))
                    {
                        System.Diagnostics.Debug.WriteLine($"🔑 账号 {account.AccountId} Continue 后已提交密码登录");
                        try
                        {
                            // 与采集任务保持一致：等 CEF 页面加载完成事件结束后，再判断登录结果。
                            await WaitForPageLoad(browser, 30000);
                            System.Diagnostics.Debug.WriteLine($"📌 账号 {account.AccountId} 密码登录后的首页页面加载完成");
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"⚠️ 账号 {account.AccountId} 密码登录后页面加载等待异常: {ex.Message}");
                        }
                        var passwordAuthState = await WaitForPostCredentialStateAsync(browser, account.AccountId);
                        System.Diagnostics.Debug.WriteLine($"🔍 Continue 后密码登录状态: {passwordAuthState}");
                        if (passwordAuthState == "home")
                        {
                            await DismissPostLoginOverlayAsync(browser);
                            var cookieJson = await ExportFacebookCookiesAsync(browser);
                            if (!string.IsNullOrWhiteSpace(cookieJson))
                            {
                                return new AccountLoginResult(account.Id, account.AccountId, "success", "credential", null, true, CookieJson: cookieJson);
                            }
                            return new AccountLoginResult(account.Id, account.AccountId, "failed", "credential", "Login succeeded but cookie was not captured");
                        }
                        if (passwordAuthState == "two_factor")
                        {
                            return await SubmitTwoFactorAndCaptureAsync(browser, account);
                        }
                    }
                    pageState = await DetectFacebookPageStateAsync(browser, account.AccountId);
                    if (pageState == FacebookPageState.Unknown || pageState == FacebookPageState.PageLoading)
                    {
                        pageState = await DetectFacebookPageStateWithRetryAsync(browser, account.AccountId);
                    }
                }
            }
            if (pageState == FacebookPageState.NetworkError
                || pageState == FacebookPageState.PageLoading
                || pageState == FacebookPageState.Unknown)
            {
                return new AccountLoginResult(account.Id, account.AccountId, "network_error", ErrorReason: GetPageStateMessage(pageState));
            }

            if (pageState == FacebookPageState.Authenticated)
            {
                // 有效 Cookie 直接进入首页，不走密码登录后的 Remember password 弹框检查。
                // 该弹框只属于账号密码登录流程，Cookie 登录每次等待检查没有必要。
                System.Diagnostics.Debug.WriteLine($"✅ 账号 {account.AccountId} Cookie 已直接进入 Facebook 首页");
                var cookieJson = await ExportFacebookCookiesAsync(browser);
                if (!string.IsNullOrWhiteSpace(cookieJson))
                {
                    return new AccountLoginResult(account.Id, account.AccountId, "success", "cookie", null, true, CookieJson: cookieJson);
                }
            }

            if (pageState == FacebookPageState.AccountDisabled)
            {
                return new AccountLoginResult(account.Id, account.AccountId, "account_disabled", ErrorReason: "账号被封或已停用");
            }

            if (string.IsNullOrWhiteSpace(account.Password))
            {
                return new AccountLoginResult(
                    account.Id,
                    account.AccountId,
                    pageState == FacebookPageState.LoginPage ? "cookie_invalid" : "skipped",
                    ErrorReason: pageState == FacebookPageState.LoginPage ? "Cookie已失效，当前停留在登录页" : GetPageStateMessage(pageState));
            }

            await ClearFacebookCookiesAsync(browser);
            System.Diagnostics.Debug.WriteLine(
                $"🔑 账号 {account.AccountId} Cookie 不可用，使用 Facebook 当前登录页进行账号密码登录: {browser.Address}");

            var loginResult = await browser.EvaluateScriptAsync(BuildCredentialLoginScript(account.AccountId, account.Password));
            if (!loginResult.Success || !(loginResult.Result is bool loginStarted) || !loginStarted)
            {
                return new AccountLoginResult(account.Id, account.AccountId, "failed", "credential", "Credential login script failed");
            }

            await Task.Delay(5000);
            var authState = await DetectFacebookAuthStateAsync(browser);
            if (authState == "home")
            {
                await DismissPostLoginOverlayAsync(browser);
                var cookieJson = await ExportFacebookCookiesAsync(browser);
                if (!string.IsNullOrWhiteSpace(cookieJson))
                {
                    return new AccountLoginResult(account.Id, account.AccountId, "success", "credential", null, true, CookieJson: cookieJson);
                }
                return new AccountLoginResult(account.Id, account.AccountId, "failed", "credential", "Login succeeded but cookie was not captured");
            }

            if (authState == "two_factor")
            {
                return await SubmitTwoFactorAndCaptureAsync(browser, account);
            }

            return new AccountLoginResult(
                account.Id,
                account.AccountId,
                authState == "disabled" ? "account_disabled" : "failed",
                "credential",
                MapAuthStateToReason(authState));
        }

        private static async Task<bool> IsFacebookPrivacyConsentPageAsync(ChromiumWebBrowser browser)
        {
            try
            {
                var result = await browser.EvaluateScriptAsync(
                    "location.pathname.includes('/privacy/consent/')");
                return result.Success && result.Result is bool isPrivacyConsent && isPrivacyConsent;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"⚠️ 隐私引导 URL 检测失败: {ex.Message}");
                return false;
            }
        }

        private async Task<bool> ClickFacebookGetStartedAsync(ChromiumWebBrowser browser)
        {
            try
            {
                var result = await browser.EvaluateScriptAsync(@"
                    (async () => {
                        const visible = el => !!el && !!(el.offsetWidth || el.offsetHeight || el.getClientRects().length);
                        const enabled = el => visible(el) && el.getAttribute('aria-disabled') !== 'true' &&
                            el.getAttribute('disabled') === null && el.getAttribute('tabindex') !== '-1';
                        const sleep = ms => new Promise(resolve => setTimeout(resolve, ms));
                        const humanClick = async el => {
                            el.scrollIntoView({ block: 'center', inline: 'center' });
                            await sleep(450 + Math.random() * 900);
                            const rect = el.getBoundingClientRect();
                            const options = { bubbles: true, cancelable: true, clientX: rect.left + rect.width / 2, clientY: rect.top + rect.height / 2, button: 0 };
                            for (const type of ['pointerdown', 'mousedown', 'pointerup', 'mouseup', 'click']) {
                                el.dispatchEvent(type.startsWith('pointer')
                                    ? new PointerEvent(type, { ...options, pointerId: 1, pointerType: 'mouse', isPrimary: true })
                                    : new MouseEvent(type, options));
                                await sleep(35 + Math.random() * 80);
                            }
                            if (typeof el.click === 'function') el.click();
                            await sleep(700 + Math.random() * 900);
                        };

                        // Facebook 新版隐私/订阅确认页：Continue 初始可能是 disabled，
                        // 必须等待它变为可用，不能误点旁边的离开服务操作。
                        if (location.pathname.includes('/privacy/consent/')) {
                            const roots = [document];
                            const all = root => {
                                const items = [...root.querySelectorAll('*')];
                                for (const item of items) if (item.shadowRoot) items.push(...all(item.shadowRoot));
                                return items;
                            };
                            const nodes = all(document);
                            const clickable = el => {
                                let current = el;
                                for (let i = 0; current && i < 6; i++, current = current.parentElement) {
                                    if (current.matches && current.matches('button, [role=""button""], [role=""radio""], label, a, [tabindex]') && enabled(current)) return current;
                                }
                                return null;
                            };
                            const labelOf = el => ([(el.getAttribute && el.getAttribute('aria-label')) || '', (el.innerText || el.textContent || '')]
                                .join(' ').replace(/\s+/g, ' ').trim().toLowerCase());

                        // user_cookie_choice_v2 的最终确认区没有稳定 id/testid：两个同级宽按钮
                        // 纵向排列，下面一个是允许全部 Cookie 的主操作。
                        if (location.search.includes('flow=user_cookie_choice_v2')) {
                            const wideButtons = nodes.filter(el => {
                                if (!visible(el) || !enabled(el) || !el.matches('button, [role=""button""]')) return false;
                                const rect = el.getBoundingClientRect();
                                return rect.width >= 400 && rect.height >= 30 && rect.height <= 80;
                            });
                            const groups = new Map();
                            for (const button of wideButtons) {
                                const parent = button.parentElement;
                                if (!parent) continue;
                                const siblings = [...parent.children].filter(el => wideButtons.includes(el));
                                if (siblings.length >= 2) groups.set(parent, siblings);
                            }
                            const finalGroup = [...groups.values()].find(group => group.length >= 2);
                            if (finalGroup) {
                                const target = finalGroup.sort((a, b) => a.getBoundingClientRect().top - b.getBoundingClientRect().top).at(-1);
                                if (target) {
                                    await humanClick(target);
                                    return true;
                                }
                            }
                        }

                        // 隐私方案 URL 也可能先显示 Get started，必须先完成这一步，
                        // 不能因为 pathname 是 /privacy/consent/ 就直接跳到 Free/Continue。
                        const getStartedNode = nodes.find(el => {
                            if (!visible(el)) return false;
                            const label = labelOf(el);
                            if (!label.includes('get started') && !label.includes('开始使用')) return false;
                            const target = clickable(el);
                            return !!target && !label.includes('continue');
                        });
                        if (getStartedNode) {
                            const target = clickable(getStartedNode) || getStartedNode;
                            target.scrollIntoView({ block: 'center', inline: 'center' });
                            await humanClick(target);
                            return true;
                        }

                            // 免费广告方案的稳定 DOM 标识是 value=PA。
                            // 先选择方案，Continue 解锁后由下一轮轮询点击。
                            const freeChoice = document.querySelector(
                                'input[type=""radio""][name=""afs_choice_input_key""][value=""PA""]')
                                || [...document.querySelectorAll('input[type=""radio""]')].find(el => {
                                    const label = (el.closest('label')?.innerText || el.parentElement?.innerText || '').toLowerCase();
                                    return label.includes('free') || label.includes('免费');
                                });
                            if (freeChoice && !freeChoice.checked && freeChoice.getAttribute('aria-checked') !== 'true') {
                                const target = clickable(freeChoice) || freeChoice;
                                await humanClick(target);
                                return false;
                            }
                            const freeRadio = nodes.find(el => (el.getAttribute('role') || '') === 'radio' &&
                                (labelOf(el).includes('free') || labelOf(el).includes('免费')));
                            if (freeRadio && freeRadio.getAttribute('aria-checked') !== 'true') {
                                await humanClick(clickable(freeRadio) || freeRadio);
                                return false;
                            }

                            // Free 已选中后必须优先点击 Continue，不能再次命中 Free 卡片并提前返回。
                            const continueNodes = nodes
                                .filter(el => visible(el) && labelOf(el).includes('continue'))
                                .map(clickable).filter(Boolean)
                                .filter(el => enabled(el));
                            if (continueNodes.length) {
                                const target = continueNodes[0];
                                target.scrollIntoView({ block: 'center', inline: 'center' });
                                const rect = target.getBoundingClientRect();
                                const options = { bubbles: true, cancelable: true, clientX: rect.left + rect.width / 2, clientY: rect.top + rect.height / 2, button: 0 };
                                for (const type of ['pointerdown', 'mousedown', 'pointerup', 'mouseup', 'click']) {
                                target.dispatchEvent(type.startsWith('pointer')
                                    ? new PointerEvent(type, { ...options, pointerId: 1, pointerType: 'mouse', isPrimary: true })
                                    : new MouseEvent(type, options));
                                await sleep(35 + Math.random() * 80);
                            }
                            if (typeof target.click === 'function') target.click();
                            await sleep(1200 + Math.random() * 1200);
                            return true;
                            }

                            // 某些版本将 Free 方案渲染为整块可点击卡片，没有 radio 属性。
                            const freeCardText = !freeChoice && !freeRadio && nodes.find(el => {
                                if (!visible(el) || !labelOf(el).match(/free|免费/)) return false;
                                const target = clickable(el);
                                if (!target) return false;
                                const label = labelOf(target);
                                return !label.includes('continue') && !label.includes('离开');
                            });
                            if (freeCardText) {
                                await humanClick(clickable(freeCardText) || freeCardText);
                                return false;
                            }

                            const actions = [...document.querySelectorAll('button, [role=""button""], a, [tabindex]')]
                                .filter(enabled)
                                .filter(el => {
                                    const rect = el.getBoundingClientRect();
                                    return rect.width >= 160 && rect.height >= 28;
                                });
                            const primary = actions.find(el => {
                                const text = (el.innerText || el.textContent || '').replace(/\s+/g, ' ').trim().toLowerCase();
                                return text === 'continue' ||
                                    (el.getAttribute('data-testid') || '').toLowerCase().includes('continue');
                            });
                            // 方案选择后按钮可能还要异步解锁，继续由外层轮询。
                            if (primary) {
                                primary.scrollIntoView({ block: 'center', inline: 'center' });
                                await humanClick(primary);
                                return true;
                            }

                            // 仅在 Continue 已消失后才处理最终确认 Agree / 同意。
                            const agreeNode = nodes.find(el => {
                                if (!visible(el)) return false;
                                const label = labelOf(el);
                                return (label === 'agree' || label.includes('agree and continue') || label.includes('同意')) && !!clickable(el);
                            });
                            if (agreeNode) {
                                await humanClick(clickable(agreeNode) || agreeNode);
                                return true;
                            }

                            // Agree 后 Facebook 可能显示独立 Cookie 横幅。优先使用稳定 DOM 属性，
                            // 不依赖界面语言；文本只在结构化属性缺失时作为最后兜底。
                            const cookieDialogs = nodes.filter(el => visible(el) &&
                                ((el.getAttribute('role') || '').toLowerCase() === 'dialog' ||
                                 (el.getAttribute('aria-modal') || '').toLowerCase() === 'true' ||
                                 /cookie|consent|privacy/i.test(`${el.id || ''} ${el.getAttribute('data-testid') || ''} ${el.className || ''}`)));
                            const cookieRoots = cookieDialogs.length ? cookieDialogs : [document];
                            const structuralCookieButton = cookieRoots.flatMap(root => [...root.querySelectorAll('*')])
                                .filter(el => enabled(el) && (el.matches('button, [role=""button""], a, [tabindex]') || el.tagName === 'INPUT'))
                                .find(el => /cookie|accept|allow|all|confirm|consent|choice/i.test(
                                    `${el.getAttribute('data-testid') || ''} ${el.id || ''} ${el.getAttribute('name') || ''} ${el.getAttribute('value') || ''} ${el.getAttribute('aria-label') || ''}`));
                            const allowCookiesNode = structuralCookieButton || cookieRoots.flatMap(root => [...root.querySelectorAll('button, [role=""button""], a, [tabindex]')])
                                .filter(enabled)
                                .find(el => {
                                    const label = labelOf(el);
                                    return label.includes('allow all cookies') || label.includes('接受所有 cookie');
                                });
                            if (allowCookiesNode) {
                                console.debug('[登录] cookie consent button', allowCookiesNode.tagName, allowCookiesNode.id, allowCookiesNode.getAttribute('data-testid'));
                                await humanClick(clickable(allowCookiesNode) || allowCookiesNode);
                                return true;
                            }
                            return false;
                        }

                        // Agree 后横幅可能触发页面导航，Cookie 按钮因此出现在普通页面分支。
                        const cookieContainers = [...document.querySelectorAll('*')]
                            .filter(el => visible(el) && (
                                (el.getAttribute('role') || '').toLowerCase() === 'dialog' ||
                                (el.getAttribute('aria-modal') || '').toLowerCase() === 'true' ||
                                /cookie|consent|privacy|user_cookie_choice/i.test(`${el.id || ''} ${el.getAttribute('data-testid') || ''} ${el.className || ''}`)));
                        const cookieRoots = cookieContainers.length ? cookieContainers : [document];
                        const allowCookies = cookieRoots.flatMap(root => [...root.querySelectorAll('*')])
                            .filter(el => enabled(el) && (el.matches('button, [role=""button""], a, [tabindex]') || el.tagName === 'INPUT'))
                            .find(el => /cookie|accept|allow|all|confirm|consent|choice/i.test(
                                `${el.getAttribute('data-testid') || ''} ${el.id || ''} ${el.getAttribute('name') || ''} ${el.getAttribute('value') || ''} ${el.getAttribute('aria-label') || ''}`))
                            || cookieRoots.flatMap(root => [...root.querySelectorAll('button, [role=""button""], a, [tabindex]')])
                                .filter(enabled)
                                .find(el => {
                                    const label = `${el.getAttribute('aria-label') || ''} ${el.innerText || el.textContent || ''}`
                                        .replace(/\s+/g, ' ').trim().toLowerCase();
                                    return label.includes('allow all cookies') || label.includes('接受所有 cookie');
                                });
                        if (allowCookies) {
                            console.debug('[登录] cookie consent button', allowCookies.tagName, allowCookies.id, allowCookies.getAttribute('data-testid'));
                            await humanClick(allowCookies);
                            return true;
                        }

                        const candidates = [
                            '[data-testid*=""get_started"" i] [role=""button""]',
                            '[data-testid*=""get_started"" i]',
                            '[data-testid*=""onboarding"" i] [role=""button""]',
                            'a[href*=""get_started"" i]'
                        ].flatMap(selector => {
                            try { return [...document.querySelectorAll(selector)]; } catch { return []; }
                        });
                        const unique = [...new Set(candidates)].filter(enabled);
                        const guideButton = unique.find(el => {
                            const rect = el.getBoundingClientRect();
                            if (rect.width < 80 || rect.height < 28) return false;
                            const testId = (el.getAttribute('data-testid') || '').toLowerCase();
                            const href = (el.getAttribute('href') || '').toLowerCase();
                            return testId.includes('get_started') || href.includes('get_started');
                        });
                        if (guideButton) {
                            guideButton.scrollIntoView({ block: 'center', inline: 'center' });
                            guideButton.click();
                            return true;
                        }

                        // Cookie 注入后部分版本会把引导按钮直接渲染在首页，而不是 dialog 内。
                        const normalizeLabel = value => (value || '').replace(/\s+/g, ' ').trim().toLowerCase();
                        const getLabel = el => normalizeLabel([el.getAttribute('aria-label'), el.getAttribute('title'), el.getAttribute('value'), el.innerText || el.textContent].filter(Boolean).join(' '));
                        const getClickTarget = el => {
                            let current = el;
                            for (let i = 0; current && i < 5; i++, current = current.parentElement) {
                                if (current.matches && current.matches('button, [role=""button""], a, [tabindex]') && enabled(current)) return current;
                            }
                            return null;
                        };
                        const allElements = root => {
                            const result = [...root.querySelectorAll('*')];
                            for (const el of result) {
                                if (el.shadowRoot) result.push(...allElements(el.shadowRoot));
                            }
                            return result;
                        };
                        const fireClick = el => {
                            el.scrollIntoView({ block: 'center', inline: 'center' });
                            const rect = el.getBoundingClientRect();
                            const options = { bubbles: true, cancelable: true, clientX: rect.left + rect.width / 2, clientY: rect.top + rect.height / 2, button: 0 };
                            for (const type of ['pointerdown', 'mousedown', 'pointerup', 'mouseup', 'click']) {
                                el.dispatchEvent(type.startsWith('pointer')
                                    ? new PointerEvent(type, { ...options, pointerId: 1, pointerType: 'mouse', isPrimary: true })
                                    : new MouseEvent(type, options));
                            }
                            if (typeof el.click === 'function') el.click();
                        };
                        const pageTextButtons = allElements(document)
                            .filter(el => visible(el) && (getLabel(el).includes('get started') || getLabel(el).includes('开始使用')))
                            .map(getClickTarget).filter(Boolean);
                        if (pageTextButtons.length) {
                            fireClick(pageTextButtons[0]);
                            return true;
                        }

                        // 引导可能位于同源 iframe；跨域 iframe 会被安全策略跳过。
                        for (const frame of [...document.querySelectorAll('iframe')]) {
                            try {
                                const frameDoc = frame.contentDocument;
                                if (!frameDoc) continue;
                                const frameText = allElements(frameDoc)
                                    .find(el => visible(el) && (getLabel(el).includes('get started') || getLabel(el).includes('开始使用')));
                                const target = frameText && getClickTarget(frameText);
                                if (target) { fireClick(target); return true; }
                            } catch (_) { }
                        }

                        // 页面版本变化时没有稳定 data-testid，按引导弹层中的按钮文本兜底。
                        const guideDialogs = [...document.querySelectorAll('[role=""dialog""], [aria-modal=""true""]')]
                            .filter(visible);
                        for (const dialog of guideDialogs) {
                            if (dialog.querySelector('input[type=""password""], input[name=""email""]')) continue;
                            const textButtons = [...dialog.querySelectorAll('button, [role=""button""], a')]
                                .filter(enabled)
                                .filter(el => {
                                    const rect = el.getBoundingClientRect();
                                    if (rect.width < 100 || rect.height < 28) return false;
                                    const label = `${el.getAttribute('aria-label') || ''} ${el.innerText || el.textContent || ''}`.trim().toLowerCase();
                                    return label === 'get started' || label.includes('get started') || label.includes('开始使用');
                                });
                            if (textButtons.length) {
                                textButtons[0].scrollIntoView({ block: 'center', inline: 'center' });
                                textButtons[0].click();
                                return true;
                            }
                        }

                        // 某些版本没有稳定的按钮名称，但会把引导内容放在弹层内。
                        // 只有弹层中恰好存在一个明显的可用按钮时才点击，避免误点普通页面按钮。
                        const dialogs = [...document.querySelectorAll('[role=""dialog""], [aria-modal=""true""]')]
                            .filter(visible);
                        for (const dialog of dialogs) {
                            if (dialog.querySelector('input[type=""password""], input[name=""email""]')) continue;
                            const buttons = [...dialog.querySelectorAll('button, [role=""button""]')]
                                .filter(enabled)
                                .filter(el => {
                                    const rect = el.getBoundingClientRect();
                                    return rect.width >= 180 && rect.height >= 32;
                                });
                            if (buttons.length === 1) {
                                buttons[0].scrollIntoView({ block: 'center', inline: 'center' });
                                buttons[0].click();
                                return true;
                            }
                        }
                        return false;
                    })();");
                var clicked = result.Success && result.Result is bool value && value;
                System.Diagnostics.Debug.WriteLine($"🔎 Get started 探测: success={result.Success}, clicked={clicked}, url={browser.Address}");
                return clicked;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"⚠️ 检测新手引导按钮失败: {ex.Message}");
                return false;
            }
        }

        private async Task<bool> ClickFacebookContinueAsync(ChromiumWebBrowser browser)
        {
            try
            {
                var result = await browser.EvaluateScriptAsync($@"
                    new Promise(async function(resolve) {{
                        try {{
                            {BuildLoginHumanHelpers()}
                            const visibleInputs = [...document.querySelectorAll('input')].filter(isVisible);
                            const hasCredentialInputs = visibleInputs.some(el =>
                                el.type === 'password' || el.name === 'pass' || el.name === 'email' ||
                                el.type === 'email' || el.autocomplete === 'current-password' ||
                                el.autocomplete === 'username');
                            if (hasCredentialInputs) {{ resolve(false); return; }}

                            const candidates = [...document.querySelectorAll('[role=""button""], button')]
                                .filter(el => {{
                                    if (!isVisible(el) || el.getAttribute('aria-disabled') === 'true') return false;
                                    const rect = el.getBoundingClientRect();
                                    return rect.width >= 300 && rect.height >= 35 &&
                                        el.getAttribute('tabindex') !== '-1';
                                }})
                                .map(el => ({{ el, rect: el.getBoundingClientRect() }}))
                                .sort((a, b) => (a.rect.top - b.rect.top) || (a.rect.left - b.rect.left));
                            // 保存账号选择页至少有两个同级大按钮，第一项是当前账号的继续操作。
                            if (candidates.length < 2) {{ resolve(false); return; }}
                            await humanClick(candidates[0].el);
                            resolve(true);
                        }} catch (e) {{
                            console.warn('[登录] 点击 Continue 失败:', e);
                            resolve(false);
                        }}
                    }});
                ");
                return result.Success && result.Result is bool clicked && clicked;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"⚠️ 点击 Continue 脚本异常: {ex.Message}");
                return false;
            }
        }

        private async Task<AccountLoginResult> SubmitTwoFactorAndCaptureAsync(
            ChromiumWebBrowser browser, AccountLoginRequest account)
        {
            if (string.IsNullOrWhiteSpace(account.Tfa))
            {
                return new AccountLoginResult(account.Id, account.AccountId, "failed", "credential", "2FA required but not configured");
            }

            System.Diagnostics.Debug.WriteLine($"🔐 账号 {account.AccountId} 检测到 2FA，开始提交动态验证码");
            var twoFactorFormReady = false;
            for (var attempt = 0; attempt < 30; attempt++)
            {
                try
                {
                    var readyResult = await browser.EvaluateScriptAsync(@"
                        (() => {
                            const visible = el => !!el && !!(el.offsetWidth || el.offsetHeight || el.getClientRects().length);
                            return [...document.querySelectorAll(
                                'input[name=""approvals_code""], input[name=""verification_code""], ' +
                                'input[name=""code""], input[autocomplete=""one-time-code""], ' +
                                'input[inputmode=""numeric""], form input[type=""text""], ' +
                                'form input[type=""tel""], form input:not([type])'
                            )].some(el => visible(el) && !el.disabled && el.type !== 'hidden' && el.type !== 'submit');
                        })();");
                    if (readyResult.Success && readyResult.Result is bool ready && ready)
                    {
                        twoFactorFormReady = true;
                        break;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"⚠️ 账号 {account.AccountId} 等待 2FA 表单检测失败: {ex.Message}");
                }

                await Task.Delay(500);
            }

            if (!twoFactorFormReady)
            {
                System.Diagnostics.Debug.WriteLine($"⚠️ 账号 {account.AccountId} 2FA 页面已跳转，但验证码表单未加载");
                return new AccountLoginResult(account.Id, account.AccountId, "failed", "credential", "2FA form did not load");
            }

            System.Diagnostics.Debug.WriteLine($"✅ 账号 {account.AccountId} 2FA 验证码表单已加载，开始输入");
            var code = GenerateTotpCode(account.Tfa);
            if (string.IsNullOrWhiteSpace(code))
            {
                return new AccountLoginResult(account.Id, account.AccountId, "failed", "credential", "Failed to generate 2FA code");
            }

            var twoFactorResult = await browser.EvaluateScriptAsync(BuildTwoFactorSubmitScript(code));
            System.Diagnostics.Debug.WriteLine(
                $"🔍 账号 {account.AccountId} 2FA 提交结果: success={twoFactorResult.Success}, result={twoFactorResult.Result}");
            if (!twoFactorResult.Success || !(twoFactorResult.Result is bool submitted) || !submitted)
            {
                return new AccountLoginResult(account.Id, account.AccountId, "failed", "credential", "2FA submission failed");
            }

            var authState = "unknown";
            for (var attempt = 0; attempt < 30; attempt++)
            {
                await Task.Delay(1000);
                authState = await DetectFacebookAuthStateAsync(browser);
                System.Diagnostics.Debug.WriteLine(
                    $"🔍 账号 {account.AccountId} 2FA 提交后状态: {authState} ({attempt + 1}/30)");

                if (authState == "home"
                    || authState == "remember_browser"
                    || authState == "disabled"
                    || authState == "checkpoint"
                    || authState == "phone_verify"
                    || authState == "email_verify"
                    || authState == "identity_verify"
                    || authState == "recover_code")
                {
                    break;
                }
            }

            if (authState == "remember_browser")
            {
                var trusted = false;
                for (var attempt = 0; attempt < 30; attempt++)
                {
                    var closeResult = await browser.EvaluateScriptAsync(BuildCloseBlockingDialogScript());
                    if (closeResult.Success && closeResult.Result is bool dialogClosed && dialogClosed)
                    {
                        System.Diagnostics.Debug.WriteLine($"📌 账号 {account.AccountId} 已关闭 2FA 后阻塞弹框");
                        await Task.Delay(700);
                        continue;
                    }

                    var trustResult = await browser.EvaluateScriptAsync(BuildTrustDeviceSubmitScript());
                    if (trustResult.Success && trustResult.Result is bool trustClicked && trustClicked)
                    {
                        trusted = true;
                        System.Diagnostics.Debug.WriteLine($"📌 账号 {account.AccountId} 已点击 Trust this device 主操作");
                        break;
                    }

                    await Task.Delay(500);
                }

                if (!trusted)
                {
                    return new AccountLoginResult(account.Id, account.AccountId, "failed", "credential", "Trust device prompt did not load");
                }
                try
                {
                    await WaitForPageLoad(browser, 30000);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"⚠️ 账号 {account.AccountId} 点击 Trust this device 后等待页面异常: {ex.Message}");
                }
                await Task.Delay(3000);
                authState = await DetectFacebookAuthStateAsync(browser);
            }

            if (authState == "home")
            {
                await DismissPostLoginOverlayAsync(browser);
                var cookieJson = await ExportFacebookCookiesAsync(browser);
                if (!string.IsNullOrWhiteSpace(cookieJson))
                {
                    return new AccountLoginResult(account.Id, account.AccountId, "success", "credential", null, true, CookieJson: cookieJson);
                }
                return new AccountLoginResult(account.Id, account.AccountId, "failed", "credential", "2FA passed but cookie was not captured");
            }

            return new AccountLoginResult(
                account.Id,
                account.AccountId,
                authState == "disabled" ? "account_disabled" : "failed",
                "credential",
                MapAuthStateToReason(authState));
            }

        private async Task<bool> IsFacebookHomeFromDomSourceAsync(ChromiumWebBrowser browser)
        {
            try
            {
                var source = await browser.GetSourceAsync();
                if (string.IsNullOrWhiteSpace(source)) return false;

                var html = source.ToLowerInvariant();
                var hasHomeDom = html.Contains("role=\"feed\"", StringComparison.Ordinal)
                    || html.Contains("data-pagelet=\"mainfeed\"", StringComparison.Ordinal)
                    || html.Contains("role=\"main\"", StringComparison.Ordinal);
                var hasCredentialForm = html.Contains("name=\"email\"", StringComparison.Ordinal)
                    || html.Contains("name=\"pass\"", StringComparison.Ordinal)
                    || html.Contains("type=\"password\"", StringComparison.Ordinal);
                return hasHomeDom && !hasCredentialForm;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"⚠️ 读取 Facebook DOM Source 失败: {ex.Message}");
                return false;
            }
        }

        private async Task<bool> SubmitContinuePasswordAsync(ChromiumWebBrowser browser, string? password)
        {
            if (string.IsNullOrWhiteSpace(password))
            {
                return false;
            }
            try
            {
                var result = await browser.EvaluateScriptAsync($@"
                    new Promise(async function(resolve) {{
                        try {{
                            {BuildLoginHumanHelpers()}
                            const isActuallyVisible = (el) => {{
                                if (!isVisible(el)) return false;
                                const rect = el.getBoundingClientRect();
                                const style = window.getComputedStyle(el);
                                return rect.width > 8 && rect.height > 8 &&
                                    style.display !== 'none' && style.visibility !== 'hidden' && style.pointerEvents !== 'none';
                            }};
                            let input = null;
                            for (let attempt = 0; attempt < 30 && !input; attempt++) {{
                                input = [...document.querySelectorAll('input[name=""pass""], input[type=""password""]')]
                                    .find(el => isActuallyVisible(el) && !el.disabled) || null;
                                if (!input) await randomDelay(250, 450);
                            }}
                            if (!input) {{ resolve(false); return; }}
                            await humanClick(input);
                            await humanTypeInput(input, {JsonConvert.SerializeObject(password)});
                            await randomDelay(400, 800);
                            const form = input.closest('form');
                            const formSubmit = form
                                ? [...form.querySelectorAll('button[type=""submit""], input[type=""submit""]')]
                                    .find(el => isActuallyVisible(el))
                                : null;
                            const visibleLoginButton = [...document.querySelectorAll('[role=""button""], button, input[type=""submit""]')]
                                .find(el => isActuallyVisible(el) && /^(log in|login|登录)$/i.test((el.innerText || el.textContent || el.value || el.getAttribute('aria-label') || '').trim()));
                            const submit = formSubmit || visibleLoginButton;
                            if (!submit || !isActuallyVisible(submit)) {{ resolve(false); return; }}
                            await humanClick(submit);
                            resolve(true);
                        }} catch (e) {{
                            console.warn('[登录] Continue 后密码提交失败:', e);
                            resolve(false);
                        }}
                    }});
                ");
                var submitted = result.Success && result.Result is bool value && value;
                System.Diagnostics.Debug.WriteLine(
                    $"🔍 Continue 后密码提交结果: success={result.Success}, submitted={submitted}, result={result.Result}");
                return submitted;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"⚠️ Continue 后密码提交脚本异常: {ex.Message}");
                return false;
            }
        }

        private async Task<string> DetectFacebookAuthStateAsync(ChromiumWebBrowser browser)
        {
            var result = await browser.EvaluateScriptAsync(@"
                (function() {
                    const url = location.href;
                    const path = location.pathname;
                    const bodyText = (document.body?.innerText || '').toLowerCase();
                    const hasSelector = (selectors) => selectors.some(selector => document.querySelector(selector));
                    const hasText = (words) => words.some(word => bodyText.includes(word));

                    if (url.includes('/two_factor/remember_browser')) return 'remember_browser';
                    if (
                        url.toLowerCase().includes('/two_step_verification/two_factor') ||
                        url.includes('/checkpoint/1501092823525282') ||
                        hasSelector([
                            'input[name=""approvals_code""]',
                            'input[name=""verification_code""]',
                            'input[name=""code""]',
                            'input[id*=""approvals_code""]',
                            'input[autocomplete=""one-time-code""]',
                            'input[inputmode=""numeric""]',
                            'input[aria-label*=""code"" i]',
                            'form input[type=""text""]',
                            'form input[type=""tel""]',
                            'form input:not([type])'
                        ])
                    ) return 'two_factor';
                    if (url.includes('/recover/code')) return 'recover_code';
                    if (url.includes('/auth_platform/codesubmit') || hasText(['check your email', 'email code', 'sent to your email'])) return 'email_verify';
                    if (url.includes('/two_step_verification/authentication')) return 'identity_verify';
                    if (url.includes('/checkpoint/disabled') || url.includes('/account_disabled')) return 'disabled';
                    if (hasText(['suspended', 'disabled', 'community standards'])) return 'disabled';
                    if (hasText(['mobile number', 'phone number', 'sent to your phone', 'text message'])) return 'phone_verify';
                    if (hasText(['confirm your identity', 'verify your identity', 'identity confirmation'])) return 'identity_verify';
                    if (hasText(['another device', 'original device', 'device you used before', 'recover code'])) return 'recover_code';
                    if (document.querySelector('[role=""feed""], [data-pagelet=""MainFeed""], [role=""main""]')) return 'home';
                    if (hasSelector([
                        'input[name=""email""]',
                        'input#email',
                        'input[name=""pass""]',
                        'input#pass',
                        'input[type=""password""]'
                    ])) return 'password_incorrect';
                    if (url.includes('/checkpoint')) return 'checkpoint';
                    if (bodyText.includes('mobile number') || bodyText.includes('phone number')) return 'phone_verify';
                    if (bodyText.includes('email code') || bodyText.includes('check your email')) return 'email_verify';
                    if (bodyText.includes('confirm your identity')) return 'identity_verify';
                    return 'unknown';
                })();
            ");

            return result.Success && result.Result != null
                ? result.Result.ToString() ?? "unknown"
                : "unknown";
        }

        private async Task<string> WaitForPostCredentialStateAsync(ChromiumWebBrowser browser, string accountId)
        {
            for (var attempt = 0; attempt < 30; attempt++)
            {
                var authState = await DetectFacebookAuthStateAsync(browser);
                if (authState == "two_factor" || authState == "remember_browser" || authState == "disabled"
                    || authState == "checkpoint" || authState == "phone_verify" || authState == "email_verify"
                    || authState == "identity_verify" || authState == "recover_code")
                {
                    return authState;
                }

                var pageState = await DetectFacebookPageStateAsync(browser, accountId);
                if (pageState == FacebookPageState.Authenticated)
                {
                    return "home";
                }
                if (pageState == FacebookPageState.VerificationRequired)
                {
                    return "two_factor";
                }
                if (pageState == FacebookPageState.AccountDisabled)
                {
                    return "disabled";
                }
                var homeDomResult = await browser.EvaluateScriptAsync(@"
                    document.readyState === 'complete' && !!document.querySelector(
                        '[role=""feed""], [data-pagelet=""MainFeed""], ' +
                        '[role=""main""]');
                ");
                if (homeDomResult.Success && homeDomResult.Result is bool homeReady && homeReady)
                {
                    return "home";
                }
                await Task.Delay(1000);
            }
            return await DetectFacebookAuthStateAsync(browser);
        }

        private async Task WaitForFacebookHomeReadyAsync(ChromiumWebBrowser browser)
        {
            for (var attempt = 0; attempt < 30; attempt++)
            {
                if (browser.IsDisposed || !browser.CanExecuteJavascriptInMainFrame)
                {
                    return;
                }
                var result = await browser.EvaluateScriptAsync(@"
                    (function() {
                        if (document.readyState !== 'complete') return false;
                        return !!document.querySelector(
                            '[role=""feed""], [role=""main""], [data-pagelet=""MainFeed""], ' +
                            '[role=""main""]');
                    })();
                ");
                if (result.Success && result.Result is bool ready && ready)
                {
                    System.Diagnostics.Debug.WriteLine("📌 登录后 Facebook 首页 DOM 已就绪，开始等待 Remember password 弹框");
                    return;
                }
                await Task.Delay(500);
            }
            System.Diagnostics.Debug.WriteLine("⚠️ 登录后首页 DOM 等待超时，继续检查 Remember password 弹框");
        }

        private async Task DismissPostLoginOverlayAsync(ChromiumWebBrowser browser)
        {
            try
            {
                try
                {
                    // 使用采集同一套页面加载完成等待，避免在主界面尚未加载时处理登录浮层。
                    await WaitForPageLoad(browser, 30000);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"⚠️ 登录后处理浮层前页面加载等待异常: {ex.Message}");
                }
                await WaitForFacebookHomeReadyAsync(browser);

                // Facebook 的 Remember password 浮层通常在首页渲染完成后异步出现，轮询一小段时间避免错过。
                for (var attempt = 0; attempt < 60; attempt++)
                {
                    if (await AcceptRememberPasswordPromptAsync(browser))
                    {
                        await Task.Delay(700);
                        return;
                    }
                    await Task.Delay(500);
                }
                await browser.EvaluateScriptAsync(@"
                    new Promise(function(resolve) {
                        try {
                            const fireKey = (type) => {
                                document.dispatchEvent(new KeyboardEvent(type, {
                                    key: 'Escape',
                                    code: 'Escape',
                                    keyCode: 27,
                                    which: 27,
                                    bubbles: true,
                                    cancelable: true
                                }));
                            };
                            const getViewportIntersectionArea = (rect) => {
                                const left = Math.max(0, rect.left);
                                const top = Math.max(0, rect.top);
                                const right = Math.min(window.innerWidth, rect.right);
                                const bottom = Math.min(window.innerHeight, rect.bottom);
                                return Math.max(0, right - left) * Math.max(0, bottom - top);
                            };
                            const hasBlockingOverlay = () => {
                                const viewportArea = Math.max(1, window.innerWidth * window.innerHeight);
                                const elements = Array.from(document.querySelectorAll('body *'));
                                for (const el of elements) {
                                    const rect = el.getBoundingClientRect();
                                    const areaRatio = getViewportIntersectionArea(rect) / viewportArea;
                                    if (areaRatio < 0.45) continue;

                                    const style = window.getComputedStyle(el);
                                    if (!style || style.display === 'none' || style.visibility === 'hidden' || style.pointerEvents === 'none') continue;

                                    const position = style.position;
                                    const role = (el.getAttribute('role') || '').toLowerCase();
                                    const ariaModal = (el.getAttribute('aria-modal') || '').toLowerCase() === 'true';
                                    const isFixedLayer = position === 'fixed' || position === 'sticky';
                                    const isDialogLayer = role === 'dialog' || ariaModal || !!el.querySelector('[role=""dialog""], [aria-modal=""true""]');
                                    if (!isFixedLayer && !isDialogLayer) continue;

                                    const bg = style.backgroundColor || '';
                                    const hasVisibleBackdrop =
                                        /rgba?\(/i.test(bg) && !/rgba?\(\s*0\s*,\s*0\s*,\s*0\s*,\s*0\s*\)/i.test(bg) ||
                                        (style.backdropFilter && style.backdropFilter !== 'none') ||
                                        (style.webkitBackdropFilter && style.webkitBackdropFilter !== 'none');

                                    if (areaRatio > 0.75 && isFixedLayer && hasVisibleBackdrop) return true;
                                    if (areaRatio > 0.55 && isDialogLayer) return true;
                                }
                                return false;
                            };
                            const clickPoint = (x, y) => {
                                const target = document.elementFromPoint(x, y) || document.body || document.documentElement;
                                const opts = {
                                    view: window,
                                    bubbles: true,
                                    cancelable: true,
                                    clientX: x,
                                    clientY: y
                                };
                                target.dispatchEvent(new MouseEvent('mousemove', opts));
                                target.dispatchEvent(new MouseEvent('mousedown', opts));
                                target.dispatchEvent(new MouseEvent('mouseup', opts));
                                target.dispatchEvent(new MouseEvent('click', opts));
                            };

                            fireKey('keydown');
                            fireKey('keyup');

                            setTimeout(() => {
                                if (hasBlockingOverlay()) {
                                    const x = Math.floor(window.innerWidth * 0.52);
                                    const y = Math.floor(Math.min(window.innerHeight - 36, window.innerHeight * 0.78));
                                    clickPoint(x, y);
                                    resolve(true);
                                    return;
                                }
                                resolve(false);
                            }, 250);
                        } catch (e) {
                            console.warn('[登录] 清理登录后浮层失败:', e);
                            resolve(false);
                        }
                    });
                ");
                await Task.Delay(600);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Post-login overlay dismiss failed: {ex.Message}");
            }
        }

        private async Task<bool> AcceptRememberPasswordPromptAsync(ChromiumWebBrowser browser)
        {
            try
            {
                // 复用基于 DOM 的弹框识别，不依赖 Remember password / OK 的语言文案。
                var result = await browser.EvaluateScriptAsync(BuildRememberBrowserScript());
                var accepted = result.Success && result.Result is bool value && value;
                if (accepted)
                {
                    System.Diagnostics.Debug.WriteLine("✅ 已点击 Remember password 弹框的 OK");
                }
                return accepted;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"⚠️ Remember password 弹框处理失败: {ex.Message}");
                return false;
            }
        }

        private static string MapAuthStateToReason(string authState)
        {
            return authState switch
            {
                "recover_code" => "Original device verification required",
                "email_verify" => "Email verification required",
                "phone_verify" => "Phone verification required",
                "identity_verify" => "Identity verification required",
                "disabled" => "Account disabled",
                "checkpoint" => "Account checkpoint",
                "password_incorrect" => "Password incorrect or cookie invalid",
                "unknown" => "Login failed with unknown page state",
                _ => $"Login failed: {authState}"
            };
        }

        private static string BuildLoginHumanHelpers()
        {
            return @"
                    const randomDelay = (min, max) => new Promise(resolve => {
                        const delay = Math.floor(min + Math.random() * (max - min));
                        setTimeout(resolve, delay);
                    });
                    const isVisible = (el) => !!el && !!(el.offsetWidth || el.offsetHeight || el.getClientRects().length);
                    const setNativeValue = (el, value) => {
                        const proto = el instanceof HTMLTextAreaElement ? HTMLTextAreaElement.prototype : HTMLInputElement.prototype;
                        const setter = Object.getOwnPropertyDescriptor(proto, 'value')?.set;
                        const tracker = el._valueTracker;
                        if (tracker) tracker.setValue(el.value || '');
                        setter ? setter.call(el, value) : (el.value = value);
                    };
                    const fireInputEvents = (el, char) => {
                        const key = char || '0';
                        el.dispatchEvent(new KeyboardEvent('keydown', { bubbles: true, cancelable: true, key }));
                        try {
                            el.dispatchEvent(new InputEvent('input', { bubbles: true, cancelable: true, inputType: 'insertText', data: char || '' }));
                        } catch {
                            el.dispatchEvent(new Event('input', { bubbles: true }));
                        }
                        el.dispatchEvent(new KeyboardEvent('keyup', { bubbles: true, cancelable: true, key }));
                        el.dispatchEvent(new Event('change', { bubbles: true }));
                    };
                    const humanTypeInput = async (el, text) => {
                        if (!el) return false;
                        el.scrollIntoView({ block: 'center', inline: 'center' });
                        await randomDelay(180, 420);
                        el.focus();
                        await randomDelay(120, 260);
                        setNativeValue(el, '');
                        fireInputEvents(el, '');
                        for (const ch of String(text || '')) {
                            setNativeValue(el, (el.value || '') + ch);
                            fireInputEvents(el, ch);
                            await randomDelay(70, 190);
                        }
                        await randomDelay(180, 420);
                        return true;
                    };
                    const humanClick = async (el) => {
                        if (!el) return false;
                        el.scrollIntoView({ block: 'center', inline: 'center' });
                        await randomDelay(180, 420);
                        const rect = el.getBoundingClientRect();
                        const targetX = rect.left + rect.width * (0.35 + Math.random() * 0.3);
                        const targetY = rect.top + rect.height * (0.35 + Math.random() * 0.3);
                        const startX = Math.random() * window.innerWidth;
                        const startY = Math.random() * window.innerHeight;
                        const controlX = (startX + targetX) / 2 + (Math.random() - 0.5) * 180;
                        const controlY = (startY + targetY) / 2 + (Math.random() - 0.5) * 180;
                        const steps = 12 + Math.floor(Math.random() * 10);
                        for (let i = 0; i <= steps; i++) {
                            const t = i / steps;
                            const x = Math.pow(1 - t, 2) * startX + 2 * (1 - t) * t * controlX + Math.pow(t, 2) * targetX;
                            const y = Math.pow(1 - t, 2) * startY + 2 * (1 - t) * t * controlY + Math.pow(t, 2) * targetY;
                            document.dispatchEvent(new MouseEvent('mousemove', {
                                view: window,
                                bubbles: true,
                                cancelable: true,
                                clientX: x + (Math.random() - 0.5) * 3,
                                clientY: y + (Math.random() - 0.5) * 3
                            }));
                            await randomDelay(18, 55);
                        }
                        await randomDelay(80, 180);
                        const opts = { view: window, bubbles: true, cancelable: true, clientX: targetX, clientY: targetY };
                        el.dispatchEvent(new MouseEvent('mouseover', opts));
                        el.dispatchEvent(new MouseEvent('mousemove', opts));
                        el.dispatchEvent(new MouseEvent('mousedown', opts));
                        await randomDelay(70, 160);
                        el.dispatchEvent(new MouseEvent('mouseup', opts));
                        el.dispatchEvent(new MouseEvent('click', opts));
                        if (typeof el.click === 'function') el.click();
                        await randomDelay(300, 700);
                        return true;
                    };
            ";
        }

        private static string BuildCredentialLoginScript(string accountId, string password)
        {
            return $@"
                new Promise(function(resolve) {{
                (async function() {{
                    {BuildLoginHumanHelpers()}
                    const bySelectors = (selectors) => {{
                        for (const selector of selectors) {{
                            const el = document.querySelector(selector);
                            if (el) return el;
                        }}
                        return null;
                    }};
                    const norm = (text) => (text || '').replace(/\s+/g, ' ').trim().toLowerCase();
                    const emailInput = bySelectors([
                        'input[name=""email""]',
                        'input#email',
                        'input[type=""email""]',
                        'input[autocomplete=""username""]',
                        'input[autocomplete=""email""]',
                        'input[aria-label*=""email"" i]',
                        'input[aria-label*=""phone"" i]',
                        'input[placeholder*=""email"" i]',
                        'input[placeholder*=""phone"" i]'
                    ]);
                    const passInput = bySelectors([
                        'input[name=""pass""]',
                        'input#pass',
                        'input[type=""password""]',
                        'input[autocomplete=""current-password""]',
                        'input[aria-label*=""password"" i]',
                        'input[placeholder*=""password"" i]'
                    ]);
                    const loginButton = bySelectors([
                        '#loginbutton',
                        'button[name=""login""]',
                        'button[type=""submit""]',
                        'input[type=""submit""][name=""login""]',
                        'input[type=""submit""]',
                        'div[role=""button""][aria-label*=""Log in"" i]',
                        'div[role=""button""][aria-label*=""登录""]'
                    ]) || [...document.querySelectorAll('button, [role=""button""], input[type=""submit""]')]
                        .find(el => {{
                            const text = norm(el.innerText || el.textContent || el.value || el.getAttribute('aria-label'));
                            return ['log in', 'login', '登录'].includes(text);
                        }});
                    if (!emailInput || !passInput || !loginButton) {{
                        resolve(false);
                        return;
                    }}
                    await humanClick(emailInput);
                    await humanTypeInput(emailInput, {JsonConvert.SerializeObject(accountId)});
                    await randomDelay(250, 650);
                    await humanClick(passInput);
                    await humanTypeInput(passInput, {JsonConvert.SerializeObject(password)});
                    await randomDelay(400, 900);
                    await humanClick(loginButton);
                    resolve(true);
                }})().catch(error => {{
                    console.error('[登录] 模拟人登录脚本失败:', error);
                    resolve(false);
                }});
                }});
            ";
        }

        private static string BuildTwoFactorSubmitScript(string code)
        {
            return $@"
                new Promise(function(resolve) {{
                (async function() {{
                    {BuildLoginHumanHelpers()}
                    const norm = (text) => (text || '').replace(/\s+/g, ' ').trim().toLowerCase();
                    const candidates = [
                        'input[name=""approvals_code""]',
                        'input[name=""verification_code""]',
                        'input[name=""code""]',
                        'input[id*=""approvals_code""]',
                        'input[autocomplete=""one-time-code""]',
                        'input[inputmode=""numeric""]',
                        'input[aria-label*=""code"" i]',
                        'input[placeholder*=""code"" i]',
                        'form input[type=""text""]',
                        'form input[type=""tel""]',
                        'form input:not([type])'
                    ];
                    const visibleInputs = [...document.querySelectorAll(candidates.join(','))]
                        .filter(el => isVisible(el) && !el.disabled && el.type !== 'hidden' && el.type !== 'submit');
                    const input = visibleInputs.find(el => {{
                        const max = Number(el.getAttribute('maxlength') || 0);
                        return max === 0 || max >= 4;
                    }}) || visibleInputs[0];
                    const disabledButtonsBeforeInput = [...document.querySelectorAll('[role=""button""], button, input[type=""submit""]')]
                        .filter(el => isVisible(el) && el.getAttribute('aria-disabled') === 'true');

                    const clickSubmit = () => {{
                        const becameEnabled = disabledButtonsBeforeInput.find(el =>
                            isVisible(el) && el.getAttribute('aria-disabled') !== 'true'
                        );
                        if (becameEnabled) {{
                            return becameEnabled;
                        }}
                        const form = input?.form || input?.closest('form');
                        const formSubmit = form
                            ? [...form.querySelectorAll('[role=""button""], button')]
                                .find(el => isVisible(el) && el.getAttribute('aria-disabled') !== 'true')
                            : null;
                        if (formSubmit) return formSubmit;
                        return null;
                    }};

                    if (!input) {{
                        console.warn('[登录] 未找到 2FA 验证码输入框');
                        resolve(false);
                        return;
                    }}
                    await humanClick(input);
                    await humanTypeInput(input, {JsonConvert.SerializeObject(code)});
                    console.debug('[登录] 2FA 验证码已输入');
                    for (let tries = 0; tries < 16; tries++) {{
                        await randomDelay(220, 380);
                        const target = clickSubmit();
                        if (target && (tries >= 4 || target.getAttribute('role') === 'button')) {{
                            await humanClick(target);
                            resolve(true);
                            return;
                        }}
                    }}
                    const form = input.form || input.closest('form');
                    if (form) {{
                        if (typeof form.requestSubmit === 'function') {{
                            form.requestSubmit();
                        }} else {{
                            form.dispatchEvent(new Event('submit', {{ bubbles: true, cancelable: true }}));
                        }}
                        resolve(true);
                        return;
                    }}
                    resolve(false);
                }})().catch(error => {{
                    console.error('[登录] 模拟人2FA脚本失败:', error);
                    resolve(false);
                }});
                }});
            ";
        }

        private static string BuildCloseBlockingDialogScript()
        {
            return $@"
                new Promise(async function(resolve) {{
                try {{
                    {BuildLoginHumanHelpers()}
                    const visible = (el) => {{
                        if (!isVisible(el) || el.getAttribute('aria-disabled') === 'true') return false;
                        const rect = el.getBoundingClientRect();
                        const style = window.getComputedStyle(el);
                        return rect.width >= 16 && rect.height >= 16 && rect.top >= 0 &&
                            style.display !== 'none' && style.visibility !== 'hidden' && style.pointerEvents !== 'none';
                    }};
                    const dialogs = [...document.querySelectorAll('[role=""dialog""][aria-modal=""true""], [aria-modal=""true""]')]
                        .filter(visible)
                        .map(el => ({{ el, rect: el.getBoundingClientRect() }}));
                    if (!dialogs.length) {{ resolve(false); return; }}
                    const dialog = dialogs[dialogs.length - 1].el;
                    const dialogRect = dialog.getBoundingClientRect();
                    const closeButton = [...dialog.querySelectorAll('[role=""button""], button')]
                        .filter(visible)
                        .map(el => ({{ el, rect: el.getBoundingClientRect() }}))
                        .filter(item =>
                            item.rect.width <= 56 && item.rect.height <= 56 &&
                            item.rect.top <= dialogRect.top + 80 &&
                            item.rect.left >= dialogRect.right - 96
                        )
                        .sort((a, b) => b.rect.left - a.rect.left)[0]?.el;
                    if (!closeButton) {{ resolve(false); return; }}
                    await humanClick(closeButton);
                    resolve(true);
                }} catch (error) {{
                    console.warn('[登录] 关闭阻塞弹框失败:', error);
                    resolve(false);
                }}
                }});
            ";
        }

        private static string BuildTrustDeviceSubmitScript()
        {
            return $@"
                new Promise(async function(resolve) {{
                try {{
                    {BuildLoginHumanHelpers()}
                    const visible = (el) => {{
                        if (!isVisible(el) || el.getAttribute('aria-disabled') === 'true') return false;
                        const rect = el.getBoundingClientRect();
                        const style = window.getComputedStyle(el);
                        return rect.width >= 16 && rect.height >= 16 && rect.top >= 0 &&
                            style.display !== 'none' && style.visibility !== 'hidden' && style.pointerEvents !== 'none';
                    }};
                    if ([...document.querySelectorAll('[role=""dialog""][aria-modal=""true""], [aria-modal=""true""]')].some(visible)) {{
                        resolve(false);
                        return;
                    }}
                    const actions = [...document.querySelectorAll('[role=""button""], button')]
                        .filter(visible)
                        .map(el => ({{ el, rect: el.getBoundingClientRect() }}))
                        .filter(item => item.rect.width >= 300 && item.rect.height >= 35 && item.el.getAttribute('tabindex') !== '-1')
                        .sort((a, b) => (a.rect.top - b.rect.top) || (a.rect.left - b.rect.left));
                    const primary = actions.find((item, index) => {{
                        const next = actions[index + 1];
                        return next && Math.abs(item.rect.left - next.rect.left) <= 8 &&
                            next.rect.top > item.rect.top && next.rect.top - item.rect.top <= 96;
                    }});
                    if (!primary) {{ resolve(false); return; }}
                    await humanClick(primary.el);
                    resolve(true);
                }} catch (error) {{
                    console.warn('[登录] 点击信任设备失败:', error);
                    resolve(false);
                }}
                }});
            ";
        }

        private static string BuildRememberBrowserScript()
        {
            return $@"
                new Promise(function(resolve) {{
                (async function() {{
                    {BuildLoginHumanHelpers()}
                    const isActuallyVisible = (el) => {{
                        if (!isVisible(el) || el.getAttribute('aria-disabled') === 'true') return false;
                        const rect = el.getBoundingClientRect();
                        const style = window.getComputedStyle(el);
                        return rect.width > 8 && rect.height > 8 && rect.top >= 0 &&
                            style.display !== 'none' && style.visibility !== 'hidden' && style.pointerEvents !== 'none';
                    }};
                    // Remember password / Trust this device 弹框的文案会随语言变化，
                    // 只使用弹框图片标记、按钮布局和可用状态定位主确认按钮。
                    const marker = document.querySelector('img[src*=""comet_aswitch""], img[src*=""aswitch""]');
                    if (!marker) {{
                        resolve(false);
                        return;
                    }}
                    const markerRect = marker.getBoundingClientRect();
                    let container = marker.parentElement;
                    for (let level = 0; container && level < 8; level++, container = container.parentElement) {{
                        const candidates = [...container.querySelectorAll('[role=""button""], button')]
                            .filter(isActuallyVisible)
                            .map(el => ({{ el, rect: el.getBoundingClientRect() }}))
                            .filter(item =>
                                item.rect.width >= 160 &&
                                item.rect.height >= 32 &&
                                item.rect.top > markerRect.bottom &&
                                item.el.getAttribute('aria-disabled') !== 'true'
                            )
                            .sort((a, b) => (a.rect.top - b.rect.top) || (a.rect.left - b.rect.left));
                        if (candidates.length > 0) {{
                            const submit = candidates[0].el;
                            await humanClick(submit);
                            resolve(true);
                            return;
                        }}
                    }}

                    resolve(false);
                }})().catch(error => {{
                    console.error('[登录] 模拟人信任设备脚本失败:', error);
                    resolve(false);
                }});
                }});
            ";
        }

        private static string? GenerateTotpCode(string? secret)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(secret))
                {
                    return null;
                }

                var normalized = secret.Replace(" ", string.Empty).Trim().ToUpperInvariant();
                var bytes = Base32Encoding.ToBytes(normalized);
                var totp = new Totp(bytes);
                return totp.ComputeTotp();
            }
            catch
            {
                return null;
            }
        }

        private async Task<string?> ExportFacebookCookiesAsync(ChromiumWebBrowser browser)
        {
            var manager = browser.RequestContext.GetCookieManager(null);
            if (manager == null)
            {
                return null;
            }

            var visitor = new FacebookCookieCollector();
            if (!manager.VisitAllCookies(visitor))
            {
                return null;
            }

            var cookies = await visitor.WaitAsync(TimeSpan.FromSeconds(5));
            var facebookCookies = cookies
                .Where(cookie =>
                    !string.IsNullOrWhiteSpace(cookie.Domain) &&
                    cookie.Domain.Contains("facebook.com", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (!facebookCookies.Any(cookie => cookie.Name == "c_user"))
            {
                return null;
            }

            var payload = facebookCookies.Select(cookie => new
            {
                name = cookie.Name,
                value = cookie.Value,
                domain = cookie.Domain,
                path = string.IsNullOrWhiteSpace(cookie.Path) ? "/" : cookie.Path,
                secure = cookie.Secure,
                httpOnly = cookie.HttpOnly,
                expirationDate = cookie.Expires.HasValue
                    ? new DateTimeOffset(DateTime.SpecifyKind(cookie.Expires.Value, DateTimeKind.Utc)).ToUnixTimeSeconds()
                    : (long?)null,
                sameSite = cookie.SameSite.ToString()
            });

            return JsonConvert.SerializeObject(payload);
        }

        private sealed class FacebookCookieCollector : ICookieVisitor
        {
            private readonly List<CefSharp.Cookie> _cookies = new();
            private readonly TaskCompletionSource<List<CefSharp.Cookie>> _completion = new();

            public bool Visit(CefSharp.Cookie cookie, int count, int total, ref bool deleteCookie)
            {
                _cookies.Add(cookie);
                if (count >= total - 1)
                {
                    _completion.TrySetResult(_cookies);
                }
                return true;
            }

            public void Dispose()
            {
                _completion.TrySetResult(_cookies);
            }

            public async Task<List<CefSharp.Cookie>> WaitAsync(TimeSpan timeout)
            {
                var completed = await Task.WhenAny(_completion.Task, Task.Delay(timeout));
                return completed == _completion.Task ? await _completion.Task : _cookies;
            }
        }

        private async Task ClearFacebookCookiesAsync(ChromiumWebBrowser browser)
        {
            var manager = browser.RequestContext.GetCookieManager(null);
            if (manager != null)
            {
                await manager.DeleteCookiesAsync("https://www.facebook.com", null);
            }
        }

        private async Task EmitAccountLoginProgress(AccountLoginResult result)
        {
            OnAccountLoginProgress?.Invoke(JsonConvert.SerializeObject(new
            {
                accountDbId = result.AccountDbId.ToString(),
                accountId = result.AccountId,
                status = result.Status,
                loginMode = result.LoginMode,
                errorReason = result.ErrorReason,
                cookieSaved = result.CookieSaved,
                cookie = result.CookieSaved ? result.CookieJson : null,
                windowClosed = result.WindowClosed
            }));
            await Task.CompletedTask;
        }
    }
}
