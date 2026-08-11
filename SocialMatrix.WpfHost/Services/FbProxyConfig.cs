using Newtonsoft.Json;
using System;

namespace SocialMatrix.WpfHost.Services;

public sealed class FbProxyConfig
{
    [JsonProperty("proxyType")] public int ProxyType { get; set; }
    [JsonProperty("host")] public string Host { get; set; } = "";
    [JsonProperty("port")] public int Port { get; set; }
    [JsonProperty("username")] public string? Username { get; set; }
    [JsonProperty("password")] public string? Password { get; set; }

    public void Validate()
    {
        if (ProxyType is < 1 or > 3 || string.IsNullOrWhiteSpace(Host) || Port is < 1 or > 65535)
            throw new InvalidOperationException("代理配置无效");
    }
}
