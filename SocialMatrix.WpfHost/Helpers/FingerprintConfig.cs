using System;
using System.Text;
using System.Threading.Tasks;
using CefSharp;
using CefSharp.Wpf;

namespace SocialMatrix.WpfHost.Helpers
{
    /// <summary>
    /// 浏览器指纹配置
    /// 用户无需填写，系统自动生成
    /// </summary>
    public class FingerprintConfig
    {
        // ==================== 从数据库读取 ====================
        public string Area { get; set; } = "US"; // 地区代码
        public double? Latitude { get; set; }    // 纬度（可选）
        public double? Longitude { get; set; }   // 经度（可选）
        public long? DeviceId { get; set; }      // 设备ID（用于生成固定的设备名称）

        // ==================== 自动推导 ====================
        /// <summary>语言（数据库没有经纬度，不注入语言，让浏览器通过代理IP自动检测）</summary>
        public string? Language => null; // null 表示不注入
        
        /// <summary>时区偏移分钟（数据库没有经纬度，不注入时区）</summary>
        public int? TimezoneOffset => null; // null 表示不注入，让浏览器通过代理IP自动检测
        
        /// <summary>设备名称（基于DeviceId生成，保证同一账号固定，不同账号不同）</summary>
        public string DeviceName => GenerateDeviceNameFromId(DeviceId);

        // ==================== 随机生成（每次打开浏览器不同） ====================
        public int HardwareConcurrency => RandomChoice(new[] { 4, 6, 8, 12, 16 });
        public int DeviceMemory => RandomChoice(new[] { 4, 8, 16, 32 });
        public string WebglVendor => RandomChoice(WebGLConfigs)[0];
        public string WebglRenderer => RandomChoice(WebGLConfigs)[1];

        // ==================== 固定值 ====================
        public string CanvasMode => "random";
        public string WebglImageMode => "random";
        public string AudioMode => "random"; // 随机 - 同一电脑上每个浏览器生成不同的 Audio
        public string GeoPermission => "allow"; // 允许获取地理位置
        public string WebrtcMode => "hide";
        public string DoNotTrack => "off"; // 关闭 - 不设置追踪个人信息
        public string SslFingerprint => "off"; // 关闭 SSL 指纹
        public string MediaDevices => "off"; // 关闭 - 使用当前电脑默认的媒体设备 ID
        public string SpeechVoices => "random"; // 随机 - 使用相匹配的值代替真实的 Speech Voices
        public string ClientRects => "random"; // 随机 - 使用相匹配的值代替真实的 ClientRects

        // ==================== WebGL配置库 ====================
        private static readonly string[][] WebGLConfigs = new[]
        {
            new[] { "Google Inc. (Intel)", "ANGLE (Intel, Intel(R) UHD Graphics 630 Direct3D11 vs_5_0 ps_5_0, D3D11)" },
            new[] { "Google Inc. (NVIDIA)", "ANGLE (NVIDIA, NVIDIA GeForce RTX 3060 Direct3D11 vs_5_0 ps_5_0, D3D11)" },
            new[] { "Google Inc. (AMD)", "ANGLE (AMD, Radeon (TM) RX 480 Graphics Direct3D11 vs_5_0 ps_5_0, D3D11)" },
        };



        private static T RandomChoice<T>(T[] array) => array[new Random().Next(array.Length)];
        
        /// <summary>
        /// 基于DeviceId生成固定的设备名称（格式：DESKTOP-XXXXXX）
        /// 同一DeviceId始终生成相同的设备名称
        /// </summary>
        private static string GenerateDeviceNameFromId(long? deviceId)
        {
            if (!deviceId.HasValue || deviceId.Value == 0)
            {
                // 如果没有DeviceId，生成随机名称
                return GenerateRandomDeviceName();
            }
            
            // 基于DeviceId生成固定的6位字符
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            long id = Math.Abs(deviceId.Value);
            var suffix = new char[6];
            
            for (int i = 0; i < 6; i++)
            {
                suffix[i] = chars[(int)(id % chars.Length)];
                id /= chars.Length;
            }
            
            return $"DESKTOP-{new string(suffix)}";
        }
        
        /// <summary>
        /// 生成随机设备名称（当没有DeviceId时使用）
        /// </summary>
        private static string GenerateRandomDeviceName()
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            var random = new Random();
            var suffix = new char[6];
            for (int i = 0; i < 6; i++)
            {
                suffix[i] = chars[random.Next(chars.Length)];
            }
            return $"DESKTOP-{new string(suffix)}";
        }
    }

    /// <summary>
    /// 指纹注入器 - 在浏览器初始化后、注入Cookie前执行
    /// </summary>
    public static class FingerprintInjector
    {
        public static async Task InjectAsync(ChromiumWebBrowser browser, FingerprintConfig config)
        {
            if (browser == null || config == null) return;

            var script = BuildScript(config);
            await browser.EvaluateScriptAsync(script);
            
            System.Diagnostics.Debug.WriteLine($"✅ 指纹注入: Language=自动检测, TZ=自动检测, DeviceName={config.DeviceName}");
        }

        private static string BuildScript(FingerprintConfig config)
        {
            var sb = new StringBuilder();

            // 1. 隐藏 webdriver
            sb.AppendLine("Object.defineProperty(navigator, 'webdriver', { get: () => false });");

            // 2. 伪装 Chrome 运行时
            sb.AppendLine("window.chrome = { runtime: { connect: () => {}, sendMessage: () => {} } };");

            // 3. 伪装 plugins
            sb.AppendLine(@"
                Object.defineProperty(navigator, 'plugins', {
                    get: () => [
                        {name: 'Chrome PDF Plugin', filename: 'internal-pdf-viewer'},
                        {name: 'Native Client', filename: 'internal-nacl-plugin'}
                    ]
                });
            ");

            // 4. 硬件配置
            sb.AppendLine($@"
                Object.defineProperty(navigator, 'hardwareConcurrency', {{ get: () => {config.HardwareConcurrency} }});
                Object.defineProperty(navigator, 'deviceMemory', {{ get: () => {config.DeviceMemory} }});
            ");

            // 5. 时区（如果为null则不注入，让浏览器通过代理IP自动检测）
            if (config.TimezoneOffset.HasValue)
            {
                sb.AppendLine($@"
                    const origTZ = Date.prototype.getTimezoneOffset;
                    Date.prototype.getTimezoneOffset = function() {{ return {config.TimezoneOffset.Value}; }};
                ");
            }

            // 6. 语言（如果为null则不注入，让浏览器通过代理IP自动检测）
            if (!string.IsNullOrEmpty(config.Language))
            {
                sb.AppendLine($@"
                    Object.defineProperty(navigator, 'language', {{ get: () => '{config.Language}' }});
                    Object.defineProperty(navigator, 'languages', {{ get: () => ['{config.Language}', '{config.Language.Substring(0, 2)}'] }});
                ");
            }

            // 7. Canvas 随机化
            if (config.CanvasMode == "random")
            {
                var noise = new Random().Next(10, 100) / 100.0;
                sb.AppendLine($@"
                    const origCanvas = HTMLCanvasElement.prototype.toDataURL;
                    HTMLCanvasElement.prototype.toDataURL = function() {{
                        const ctx = this.getContext('2d');
                        if (ctx && this.width > 0 && this.height > 0) {{
                            ctx.fillStyle = 'rgba({(int)(noise * 255)}, {(int)(noise * 200)}, {(int)(noise * 150)}, 0.001)';
                            ctx.fillRect(0, 0, this.width, this.height);
                        }}
                        return origCanvas.apply(this, arguments);
                    }};
                ");
            }

            // 8. WebGL 伪装
            if (config.WebglImageMode == "random")
            {
                sb.AppendLine($@"
                    const origGL = WebGLRenderingContext.prototype.getParameter;
                    WebGLRenderingContext.prototype.getParameter = function(p) {{
                        if (p === 37445) return '{config.WebglVendor}';
                        if (p === 37446) return '{config.WebglRenderer}';
                        return origGL.call(this, p);
                    }};
                ");
            }

            // 9. AudioContext 随机化（同一电脑上每个浏览器生成不同的 Audio）
            if (config.AudioMode == "random")
            {
                sb.AppendLine(@"
                    const origAudio = AudioContext.prototype.createAnalyser;
                    AudioContext.prototype.createAnalyser = function() {
                        const a = origAudio.call(this);
                        const origGet = a.getFloatFrequencyData;
                        const noise = (Math.random() - 0.5) * 0.1;
                        a.getFloatFrequencyData = function(arr) {
                            origGet.call(this, arr);
                            for (let i = 0; i < arr.length; i++) arr[i] += noise;
                        };
                        return a;
                    };
                ");
            }

            // 10. 地理位置权限（不注入具体经纬度，让浏览器通过代理IP自动获取）
            if (config.GeoPermission == "allow")
            {
                // 不注入具体坐标，让网站通过IP推导位置
                sb.AppendLine(@"
                    navigator.geolocation.getCurrentPosition = function(success, error, options) {
                        // 返回空坐标，让Facebook通过代理IP推导位置
                        success({
                            coords: { 
                                latitude: 0, 
                                longitude: 0, 
                                accuracy: 100000  // 低精度，表示不确定
                            },
                            timestamp: Date.now()
                        });
                    };
                ");
            }
            else if (config.GeoPermission == "deny")
            {
                sb.AppendLine(@"
                    navigator.geolocation.getCurrentPosition = function(success, error, options) {
                        if (error) error({code: 1, message: 'User denied Geolocation'});
                    };
                ");
            }

            // 11. WebRTC 隐藏
            if (config.WebrtcMode == "hide")
            {
                sb.AppendLine(@"
                    const origRTC = window.RTCPeerConnection || window.mozRTCPeerConnection || window.webkitRTCPeerConnection;
                    if (origRTC) {
                        window.RTCPeerConnection = function(cfg) {
                            const m = Object.assign({}, cfg);
                            m.iceServers = [];
                            return new origRTC(m);
                        };
                    }
                ");
            }

            // 12. Do Not Track（关闭 - 不设置追踪个人信息）
            sb.AppendLine($"Object.defineProperty(navigator, 'doNotTrack', {{ get: () => '{(config.DoNotTrack == "on" ? "1" : "0")}' }});");

            // 13. 媒体设备（关闭 - 使用当前电脑默认的媒体设备 ID）
            if (config.MediaDevices == "off")
            {
                // 不注入，使用系统默认媒体设备
            }

            // 14. Speech Voices 随机化（使用相匹配的值代替真实的 Speech Voices）
            if (config.SpeechVoices == "random")
            {
                sb.AppendLine(@"
                    const origGetVoices = speechSynthesis.getVoices;
                    speechSynthesis.getVoices = function() {
                        const voices = origGetVoices.call(this);
                        if (voices.length > 0) {
                            // 随机扰动 voice URI 防止指纹追踪
                            const noise = Math.random().toString(36).substring(2, 8);
                            voices.forEach(v => {
                                if (v.voiceURI) {
                                    Object.defineProperty(v, 'voiceURI', { value: v.voiceURI + '#' + noise, writable: false });
                                }
                            });
                        }
                        return voices;
                    };
                ");
            }

            // 15. ClientRects 随机化（使用相匹配的值代替真实的 ClientRects）
            if (config.ClientRects == "random")
            {
                sb.AppendLine(@"
                    const origGetClientRects = Element.prototype.getClientRects;
                    Element.prototype.getClientRects = function() {
                        const rects = origGetClientRects.call(this);
                        if (rects.length > 0) {
                            const noise = (Math.random() - 0.5) * 0.1;
                            const arr = Array.from(rects);
                            arr.forEach(r => {
                                r.x += noise;
                                r.y += noise;
                                r.width += noise;
                                r.height += noise;
                            });
                            return arr;
                        }
                        return rects;
                    };
                    
                    const origGetBoundingClientRect = Element.prototype.getBoundingClientRect;
                    Element.prototype.getBoundingClientRect = function() {
                        const rect = origGetBoundingClientRect.call(this);
                        const noise = (Math.random() - 0.5) * 0.1;
                        return {
                            x: rect.x + noise,
                            y: rect.y + noise,
                            width: rect.width + noise,
                            height: rect.height + noise,
                            top: rect.top + noise,
                            right: rect.right + noise,
                            bottom: rect.bottom + noise,
                            left: rect.left + noise,
                            toJSON: () => rect.toJSON()
                        };
                    };
                ");
            }

            // 16. SSL 指纹（关闭）
            if (config.SslFingerprint == "off")
            {
                // SSL 指纹由 Chromium 内核自动管理，无需额外处理
                // 设置为 off 表示不启用自定义 SSL 指纹伪装
            }

            return sb.ToString();
        }
    }
}
