using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SocialMatrix.WpfHost.Services;

/// <summary>
/// 为 WPF 内嵌 Vue 提供本地 HTTP 静态文件服务。
/// 使用 HTTP 页面可以连接服务器的 ws:// WebSocket，不触发混合内容限制。
/// </summary>
public sealed class LocalVueServer : IDisposable
{
    private readonly CancellationTokenSource _shutdown = new();
    private TcpListener? _listener;
    private string _rootPath = string.Empty;

    public int Port { get; private set; }

    public async Task<Uri> StartAsync(string rootPath, int preferredPort = 18765)
    {
        _rootPath = Path.GetFullPath(rootPath);
        // The port is part of the browser origin. It must remain stable or
        // WebView2 will create a different localStorage namespace and require
        // the user to log in again after an update.
        for (var port = preferredPort; port <= preferredPort; port++)
        {
            try
            {
                var listener = new TcpListener(IPAddress.Loopback, port);
                listener.Start();
                _listener = listener;
                Port = port;
                _ = AcceptLoopAsync(listener, _shutdown.Token);
                return new Uri($"http://127.0.0.1:{port}/");
            }
            catch (SocketException)
            {
                _listener?.Stop();
                _listener = null;
            }
        }

        throw new InvalidOperationException("无法启动 WPF 本地 Vue HTTP 服务");
    }

    private async Task AcceptLoopAsync(TcpListener listener, CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var client = await listener.AcceptTcpClientAsync(cancellationToken);
                _ = HandleClientAsync(client, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
    {
        using (client)
        using (var stream = client.GetStream())
        {
            try
            {
                var request = await ReadRequestAsync(stream, cancellationToken);
                var firstLine = request.Split("\r\n", StringSplitOptions.None).FirstOrDefault() ?? string.Empty;
                var parts = firstLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2 || (parts[0] != "GET" && parts[0] != "HEAD"))
                {
                    await WriteResponseAsync(stream, 405, "Method Not Allowed", "text/plain", [], cancellationToken);
                    return;
                }

                var requestPath = parts[1].Split('?', 2)[0];
                var relativePath = Uri.UnescapeDataString(requestPath.TrimStart('/'));
                if (string.IsNullOrWhiteSpace(relativePath)) relativePath = "index.html";

                var root = Path.GetFullPath(_rootPath);
                var filePath = Path.GetFullPath(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
                if (!filePath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                {
                    await WriteResponseAsync(stream, 403, "Forbidden", "text/plain", [], cancellationToken);
                    return;
                }

                // Vue history/hash routes fall back to index.html.
                if (!File.Exists(filePath)) filePath = Path.Combine(root, "index.html");
                if (!File.Exists(filePath))
                {
                    await WriteResponseAsync(stream, 404, "Not Found", "text/plain", [], cancellationToken);
                    return;
                }

                var content = await File.ReadAllBytesAsync(filePath, cancellationToken);
                await WriteResponseAsync(stream, 200, "OK", GetContentType(filePath), content,
                    cancellationToken, parts[0] == "HEAD");
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"本地 Vue HTTP 服务请求失败: {ex.Message}");
            }
        }
    }

    private static async Task<string> ReadRequestAsync(NetworkStream stream, CancellationToken cancellationToken)
    {
        var buffer = new byte[16 * 1024];
        var length = 0;
        while (length < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(length, buffer.Length - length), cancellationToken);
            if (read == 0) break;
            length += read;
            if (Encoding.ASCII.GetString(buffer, 0, length).Contains("\r\n\r\n", StringComparison.Ordinal)) break;
        }
        return Encoding.UTF8.GetString(buffer, 0, length);
    }

    private static async Task WriteResponseAsync(NetworkStream stream, int statusCode, string reason,
        string contentType, byte[] content, CancellationToken cancellationToken, bool headOnly = false)
    {
        var headers = $"HTTP/1.1 {statusCode} {reason}\r\nContent-Type: {contentType}\r\nContent-Length: {content.Length}\r\nConnection: close\r\n\r\n";
        var headerBytes = Encoding.UTF8.GetBytes(headers);
        await stream.WriteAsync(headerBytes, cancellationToken);
        if (!headOnly && content.Length > 0) await stream.WriteAsync(content, cancellationToken);
    }

    private static string GetContentType(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".html" => "text/html; charset=utf-8",
        ".js" => "text/javascript; charset=utf-8",
        ".css" => "text/css; charset=utf-8",
        ".json" => "application/json; charset=utf-8",
        ".svg" => "image/svg+xml",
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".gif" => "image/gif",
        ".ico" => "image/x-icon",
        ".woff" => "font/woff",
        ".woff2" => "font/woff2",
        _ => "application/octet-stream"
    };

    public void Dispose()
    {
        _shutdown.Cancel();
        _listener?.Stop();
        _listener = null;
        _shutdown.Dispose();
    }
}
