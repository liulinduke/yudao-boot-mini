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
        public record AccountLoginRequest(long Id, string AccountId, string? Password, string? Tfa, string? Cookie);
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
                    isOperation: false);
            });

            await Task.Delay(2500);

            AccountLoginResult result;
            try
            {
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
            try
            {
                System.Diagnostics.Debug.WriteLine(
                    $"🔐 账号管理登录开始: account={account.AccountId}, " +
                    $"cookie={(HasUsableFacebookCookie(account.Cookie) ? "有效" : "空")}, " +
                    $"password={(!string.IsNullOrWhiteSpace(account.Password) ? "已配置" : "未配置")}, " +
                    $"tfa={(!string.IsNullOrWhiteSpace(account.Tfa) ? "已配置" : "未配置")}");
                await WaitForPageLoad(browser, 30000);
            }
            catch (Exception ex)
            {
                return new AccountLoginResult(account.Id, account.AccountId, "network_error", ErrorReason: $"网络异常或页面加载超时: {ex.Message}");
            }
            await Task.Delay(1500);

            var pageState = await DetectFacebookPageStateAsync(browser, account.AccountId);
            if (pageState == FacebookPageState.Unknown)
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
                    await Task.Delay(1800);

                    var continueAuthState = await DetectFacebookAuthStateAsync(browser);
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
                }
            }
            if (pageState == FacebookPageState.NetworkError || pageState == FacebookPageState.PageLoading || pageState == FacebookPageState.Unknown)
            {
                return new AccountLoginResult(account.Id, account.AccountId, "network_error", ErrorReason: GetPageStateMessage(pageState));
            }

            if (pageState == FacebookPageState.Authenticated)
            {
                // 有效 Cookie 已直接进入首页，不经过密码登录，不处理 Remember password 弹框。
                System.Diagnostics.Debug.WriteLine($"✅ 账号 {account.AccountId} Cookie 已直接进入 Facebook 首页，跳过 Remember password");
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
            System.Diagnostics.Debug.WriteLine($"🔑 账号 {account.AccountId} Cookie 不可用，开始账号密码登录");
            await NavigateBrowserToUrlAsync(browser, account.AccountId, "https://www.facebook.com/login", 30000);
            await WaitForPageLoad(browser, 30000);
            await Task.Delay(1000);

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

        private async Task<bool> ClickFacebookContinueAsync(ChromiumWebBrowser browser)
        {
            try
            {
                var result = await browser.EvaluateScriptAsync($@"
                    new Promise(async function(resolve) {{
                        try {{
                            {BuildLoginHumanHelpers()}
                            const norm = (text) => (text || '').replace(/\s+/g, ' ').trim().toLowerCase();
                            const isContinue = (el) => {{
                                if (!isVisible(el) || el.getAttribute('aria-disabled') === 'true') return false;
                                const text = norm(el.innerText || el.textContent || el.getAttribute('aria-label'));
                                return text === 'continue' || text.startsWith('continue ') || text === '继续' || text.startsWith('继续 ');
                            }};
                            const candidates = [...document.querySelectorAll('[role=""button""], button')]
                                .filter(isContinue)
                                .sort((a, b) => {{
                                    const aa = a.getAttribute('aria-label') || '';
                                    const bb = b.getAttribute('aria-label') || '';
                                    const aSpecific = /continue\s+.+/i.test(aa) ? 0 : 1;
                                    const bSpecific = /continue\s+.+/i.test(bb) ? 0 : 1;
                                    return aSpecific - bSpecific;
                                }});
                            if (!candidates.length) {{ resolve(false); return; }}
                            await humanClick(candidates[0]);
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
            var code = GenerateTotpCode(account.Tfa);
            if (string.IsNullOrWhiteSpace(code))
            {
                return new AccountLoginResult(account.Id, account.AccountId, "failed", "credential", "Failed to generate 2FA code");
            }

            var twoFactorResult = await browser.EvaluateScriptAsync(BuildTwoFactorSubmitScript(code));
            if (!twoFactorResult.Success || !(twoFactorResult.Result is bool submitted) || !submitted)
            {
                return new AccountLoginResult(account.Id, account.AccountId, "failed", "credential", "2FA submission failed");
            }

            await Task.Delay(5000);
            var authState = await DetectFacebookAuthStateAsync(browser);
            if (authState == "remember_browser")
            {
                await browser.EvaluateScriptAsync(BuildRememberBrowserScript());
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
                        url.includes('/two_step_verification/two_factor') ||
                        url.includes('/checkpoint/1501092823525282') ||
                        hasSelector([
                            'input[name=""approvals_code""]',
                            'input[name=""verification_code""]',
                            'input[name=""code""]',
                            'input[id*=""approvals_code""]',
                            'input[autocomplete=""one-time-code""]',
                            'input[inputmode=""numeric""]',
                            'input[aria-label*=""code"" i]'
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
                    if (document.querySelector('[role=""feed""], [data-pagelet=""MainFeed""], nav[aria-label=""Primary""], a[aria-label=""Home""], a[aria-label=""首页""]')) return 'home';
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
                if (pageState == FacebookPageState.AccountDisabled)
                {
                    return "disabled";
                }
                var homeDomResult = await browser.EvaluateScriptAsync(@"
                    document.readyState === 'complete' && !!document.querySelector(
                        '[role=""feed""], [role=""main""], [data-pagelet=""MainFeed""], ' +
                        'nav[aria-label=""Primary""], [aria-label=""Your profile""], [aria-label=""Home""]');
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
                            'nav[aria-label=""Primary""], [aria-label=""Your profile""], [aria-label=""Home""]');
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
                var result = await browser.EvaluateScriptAsync($@"
                    new Promise(async function(resolve) {{
                        try {{
                            {BuildLoginHumanHelpers()}
                            const pageText = (document.body?.innerText || '').toLowerCase();
                            if (!pageText.includes('remember password')) {{ resolve(false); return; }}
                            const isActuallyVisible = (el) => {{
                                if (!isVisible(el) || el.getAttribute('aria-disabled') === 'true') return false;
                                const rect = el.getBoundingClientRect();
                                const style = window.getComputedStyle(el);
                                return rect.width > 8 && rect.height > 8 &&
                                    style.display !== 'none' && style.visibility !== 'hidden' && style.pointerEvents !== 'none';
                            }};
                            const okButton = [...document.querySelectorAll('[role=""button""][aria-label=""OK""]')]
                                .find(isActuallyVisible) || [...document.querySelectorAll('[role=""button""], button')]
                                .find(el => {{
                                    if (!isActuallyVisible(el)) return false;
                                    const text = (el.innerText || el.textContent || el.getAttribute('aria-label') || '').trim().toLowerCase();
                                    return text === 'ok';
                                }});
                            if (!okButton) {{ resolve(false); return; }}
                            await humanClick(okButton);
                            resolve(true);
                        }} catch (e) {{
                            console.warn('[登录] 点击 Remember password 的 OK 失败:', e);
                            resolve(false);
                        }}
                    }});
                ");
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
                        const label = norm(el.getAttribute('aria-label') || el.getAttribute('placeholder') || el.name || el.id);
                        return max === 0 || max >= 4 || label.includes('code') || label.includes('验证码') || label.includes('驗證碼');
                    }}) || visibleInputs[0];
                    const disabledButtonsBeforeInput = [...document.querySelectorAll('[role=""button""]')]
                        .filter(el => isVisible(el) && el.getAttribute('aria-disabled') === 'true');

                    const clickSubmit = () => {{
                        const becameEnabled = disabledButtonsBeforeInput.find(el =>
                            isVisible(el) && el.getAttribute('aria-disabled') !== 'true'
                        );
                        if (becameEnabled) {{
                            return becameEnabled;
                        }}
                        const form = input?.closest('form');
                        const submit = form?.querySelector('button[type=""submit""], input[type=""submit""]');
                        if (submit) {{
                            return submit;
                        }}
                        return null;
                    }};

                    if (!input) {{
                        resolve(false);
                        return;
                    }}
                    await humanClick(input);
                    await humanTypeInput(input, {JsonConvert.SerializeObject(code)});
                    for (let tries = 0; tries < 16; tries++) {{
                        await randomDelay(220, 380);
                        const target = clickSubmit();
                        if (target && (tries >= 4 || target.getAttribute('role') === 'button')) {{
                            await humanClick(target);
                            resolve(true);
                            return;
                        }}
                    }}
                    const form = input.closest('form');
                    if (form?.requestSubmit) {{
                        form.requestSubmit();
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
                    const norm = (text) => (text || '').replace(/\s+/g, ' ').trim().toLowerCase();
                    const buttons = [...document.querySelectorAll('[role=""button""], button')]
                        .filter(isActuallyVisible);
                    const trustButton = buttons.find(el => {{
                        const text = norm(el.innerText || el.textContent || el.getAttribute('aria-label'));
                        return text === 'trust this device' || text === '信任此设备' || text === '信任这台设备';
                    }});
                    const primaryButtons = buttons
                        .map(el => ({{ el, rect: el.getBoundingClientRect() }}))
                        .filter(item =>
                            item.rect.width >= 160 &&
                            item.rect.height >= 32 &&
                            item.rect.top >= 100
                        )
                        .sort((a, b) => (a.rect.top - b.rect.top) || (a.rect.left - b.rect.left));

                    const submit = trustButton || primaryButtons[0]?.el;
                    if (!submit) {{
                        resolve(false);
                        return;
                    }}
                    await humanClick(submit);
                    resolve(true);
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
