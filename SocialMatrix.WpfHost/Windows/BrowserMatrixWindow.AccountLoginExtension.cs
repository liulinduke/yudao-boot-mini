using CefSharp;
using CefSharp.Wpf;
using Newtonsoft.Json;
using OtpNet;
using SocialMatrix.WpfHost.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
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
            bool WindowClosed = false);

        private readonly Queue<AccountLoginRequest> _accountLoginQueue = new();
        private readonly List<AccountLoginResult> _accountLoginResults = new();
        private int _accountLoginRunningCount = 0;
        private bool _accountLoginBatchActive = false;
        private readonly object _accountLoginLock = new();

        public event Action<string>? OnAccountLoginProgress;
        public event Action<string>? OnAccountLoginBatchComplete;

        public void StartAccountLoginBatch(List<AccountLoginRequest> accounts)
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
                        results = _accountLoginResults
                    });
                    OnAccountLoginBatchComplete?.Invoke(payload);
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
            Application.Current.Dispatcher.Invoke(() =>
            {
                CloseBrowser(account.AccountId);
                windowClosed = true;
            });
            result = result with { WindowClosed = windowClosed };

            await PersistAccountLoginResultAsync(result);
            await EmitAccountLoginProgress(result);

            lock (_accountLoginLock)
            {
                _accountLoginResults.Add(result);
                _accountLoginRunningCount = Math.Max(0, _accountLoginRunningCount - 1);
            }
        }

        private async Task<AccountLoginResult> LoginAccountWithBrowserAsync(ChromiumWebBrowser browser, AccountLoginRequest account)
        {
            await WaitForPageLoad(browser, 30000);
            await Task.Delay(1500);

            bool isLoginPage = await CheckIfLoginPage(browser);
            if (!isLoginPage)
            {
                var cookieJson = await ExportFacebookCookiesAsync(browser);
                if (!string.IsNullOrWhiteSpace(cookieJson))
                {
                    await PersistAccountLoginResultAsync(
                        new AccountLoginResult(account.Id, account.AccountId, "success", "cookie", null, true),
                        cookieJson);
                    return new AccountLoginResult(account.Id, account.AccountId, "success", "cookie", null, true);
                }
            }

            if (string.IsNullOrWhiteSpace(account.Password))
            {
                return new AccountLoginResult(account.Id, account.AccountId, "skipped", ErrorReason: "Missing password");
            }

            await ClearFacebookCookiesAsync(browser);
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
                var cookieJson = await ExportFacebookCookiesAsync(browser);
                if (!string.IsNullOrWhiteSpace(cookieJson))
                {
                    await PersistAccountLoginResultAsync(
                        new AccountLoginResult(account.Id, account.AccountId, "success", "credential", null, true),
                        cookieJson);
                    return new AccountLoginResult(account.Id, account.AccountId, "success", "credential", null, true);
                }
                return new AccountLoginResult(account.Id, account.AccountId, "failed", "credential", "Login succeeded but cookie was not captured");
            }

            if (authState == "two_factor")
            {
                if (string.IsNullOrWhiteSpace(account.Tfa))
                {
                    return new AccountLoginResult(account.Id, account.AccountId, "failed", "credential", "2FA required but not configured");
                }

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
                authState = await DetectFacebookAuthStateAsync(browser);
                if (authState == "remember_browser")
                {
                    await browser.EvaluateScriptAsync(BuildRememberBrowserScript());
                    await Task.Delay(3000);
                    authState = await DetectFacebookAuthStateAsync(browser);
                }

                if (authState == "home")
                {
                    var cookieJson = await ExportFacebookCookiesAsync(browser);
                    if (!string.IsNullOrWhiteSpace(cookieJson))
                    {
                        await PersistAccountLoginResultAsync(
                            new AccountLoginResult(account.Id, account.AccountId, "success", "credential", null, true),
                            cookieJson);
                        return new AccountLoginResult(account.Id, account.AccountId, "success", "credential", null, true);
                    }
                    return new AccountLoginResult(account.Id, account.AccountId, "failed", "credential", "2FA passed but cookie was not captured");
                }

                return new AccountLoginResult(account.Id, account.AccountId, "failed", "credential", MapAuthStateToReason(authState));
            }

            return new AccountLoginResult(account.Id, account.AccountId, "failed", "credential", MapAuthStateToReason(authState));
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

                    if (url.includes('/two_factor/remember_browser') || hasText(['remember this browser', 'save browser'])) return 'remember_browser';
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
                        ]) ||
                        hasText(['two-factor authentication', 'two factor authentication', 'authentication code', 'login code', 'enter code', '输入验证码', '两步验证'])
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

        private static string BuildCredentialLoginScript(string accountId, string password)
        {
            return $@"
                (function() {{
                    const bySelectors = (selectors) => {{
                        for (const selector of selectors) {{
                            const el = document.querySelector(selector);
                            if (el) return el;
                        }}
                        return null;
                    }};
                    const norm = (text) => (text || '').replace(/\s+/g, ' ').trim().toLowerCase();
                    const setNativeValue = (el, value) => {{
                        const proto = el instanceof HTMLTextAreaElement ? HTMLTextAreaElement.prototype : HTMLInputElement.prototype;
                        const setter = Object.getOwnPropertyDescriptor(proto, 'value')?.set;
                        setter ? setter.call(el, value) : (el.value = value);
                        el.dispatchEvent(new Event('input', {{ bubbles: true }}));
                        el.dispatchEvent(new Event('change', {{ bubbles: true }}));
                    }};
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
                        return false;
                    }}
                    emailInput.focus();
                    setNativeValue(emailInput, {JsonConvert.SerializeObject(accountId)});
                    passInput.focus();
                    setNativeValue(passInput, {JsonConvert.SerializeObject(password)});
                    loginButton.click();
                    return true;
                }})();
            ";
        }

        private static string BuildTwoFactorSubmitScript(string code)
        {
            return $@"
                (function() {{
                    const bySelectors = (selectors) => {{
                        for (const selector of selectors) {{
                            const el = document.querySelector(selector);
                            if (el) return el;
                        }}
                        return null;
                    }};
                    const norm = (text) => (text || '').replace(/\s+/g, ' ').trim().toLowerCase();
                    const setNativeValue = (el, value) => {{
                        const proto = el instanceof HTMLTextAreaElement ? HTMLTextAreaElement.prototype : HTMLInputElement.prototype;
                        const setter = Object.getOwnPropertyDescriptor(proto, 'value')?.set;
                        setter ? setter.call(el, value) : (el.value = value);
                        el.dispatchEvent(new Event('input', {{ bubbles: true }}));
                        el.dispatchEvent(new Event('change', {{ bubbles: true }}));
                        el.dispatchEvent(new KeyboardEvent('keyup', {{ bubbles: true, key: '0' }}));
                    }};
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
                        .filter(el => el.offsetParent !== null && !el.disabled && el.type !== 'hidden');
                    const input = visibleInputs.find(el => {{
                        const max = Number(el.getAttribute('maxlength') || 0);
                        const label = norm(el.getAttribute('aria-label') || el.getAttribute('placeholder') || el.name || el.id);
                        return max === 0 || max >= 4 || label.includes('code') || label.includes('验证码');
                    }}) || visibleInputs[0];
                    const submit = bySelectors([
                        'button[type=""submit""]',
                        'input[type=""submit""]',
                        'div[role=""button""][aria-label*=""Continue"" i]',
                        'div[role=""button""][aria-label*=""Submit"" i]',
                        'div[role=""button""][aria-label*=""Next"" i]',
                        'div[role=""button""][aria-label*=""继续""]',
                        'div[role=""button""][aria-label*=""提交""]',
                        'div[role=""button""][aria-label*=""下一步""]'
                    ]) || [...document.querySelectorAll('button, [role=""button""], input[type=""submit""]')]
                        .filter(el => el.offsetParent !== null && !el.disabled && el.getAttribute('aria-disabled') !== 'true')
                        .find(el => {{
                            const text = norm(el.innerText || el.textContent || el.value || el.getAttribute('aria-label'));
                            return ['continue', 'submit', 'next', 'confirm', '继续', '提交', '下一步', '确认'].includes(text);
                        }});
                    if (!input || !submit) {{
                        return false;
                    }}
                    input.focus();
                    setNativeValue(input, {JsonConvert.SerializeObject(code)});
                    submit.click();
                    return true;
                }})();
            ";
        }

        private static string BuildRememberBrowserScript()
        {
            return @"
                (function() {
                    const norm = (text) => (text || '').replace(/\s+/g, ' ').trim().toLowerCase();
                    const submit = document.querySelector('button[type=""submit""], input[type=""submit""], div[role=""button""][aria-label*=""Continue"" i], div[role=""button""][aria-label*=""继续""]')
                        || [...document.querySelectorAll('button, [role=""button""], input[type=""submit""]')]
                            .filter(el => el.offsetParent !== null && !el.disabled && el.getAttribute('aria-disabled') !== 'true')
                            .find(el => {
                                const text = norm(el.innerText || el.textContent || el.value || el.getAttribute('aria-label'));
                                return ['continue', 'ok', 'yes', 'save', 'trust this device', '继续', '确定', '保存'].includes(text);
                            });
                    if (!submit) return false;
                    submit.click();
                    return true;
                })();
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
            var result = await browser.EvaluateScriptAsync(@"
                (function() {
                    return JSON.stringify(document.cookie.split(';').map(item => {
                        const part = item.trim();
                        const idx = part.indexOf('=');
                        if (idx <= 0) return null;
                        return { name: part.slice(0, idx), value: part.slice(idx + 1), domain: '.facebook.com', path: '/' };
                    }).filter(Boolean));
                })();
            ");

            if (!result.Success || result.Result == null)
            {
                return null;
            }

            var json = result.Result.ToString();
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            return json.Contains("c_user") ? json : null;
        }

        private async Task ClearFacebookCookiesAsync(ChromiumWebBrowser browser)
        {
            var manager = browser.RequestContext.GetCookieManager(null);
            if (manager != null)
            {
                await manager.DeleteCookiesAsync("https://www.facebook.com", null);
            }
        }

        private async Task PersistAccountLoginResultAsync(AccountLoginResult result, string? cookieJsonOverride = null)
        {
            try
            {
                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(10);
                var token = TokenManager.Get();
                if (!string.IsNullOrWhiteSpace(token))
                {
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                }

                var payload = new
                {
                    id = result.AccountDbId,
                    loginStatus = result.Status.ToUpperInvariant(),
                    loginErrorReason = result.ErrorReason,
                    cookie = cookieJsonOverride
                };

                var content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");
                await client.PutAsync("http://localhost:48080/admin-api/facebook/fb-account/update-login-result", content);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to persist login result: {ex.Message}");
            }
        }

        private async Task EmitAccountLoginProgress(AccountLoginResult result)
        {
            OnAccountLoginProgress?.Invoke(JsonConvert.SerializeObject(new
            {
                accountDbId = result.AccountDbId,
                accountId = result.AccountId,
                status = result.Status,
                loginMode = result.LoginMode,
                errorReason = result.ErrorReason,
                cookieSaved = result.CookieSaved,
                windowClosed = result.WindowClosed
            }));
            await Task.CompletedTask;
        }
    }
}
