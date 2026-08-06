using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NeoEditor.Core.Abstractions;
using Serilog;

namespace NeoEditor.Player.Core.Services;

/// <summary>
/// Loopback HTTP content server for the preview (Docs/42 §3.2). Binds 127.0.0.1 on a random
/// port, serves:
///  - /  and /host.html      → plugin Web/host.html (Ruffle embedding page)
///  - /ruffle/*              → plugin Web/ruffle/* (Ruffle self-hosted assets, correct MIME)
///  - /__log (POST)          → SwfLogBridge (page console/clipboard log forwarding)
///  - data/*.xml, *.php, neogame.xml → ProxyHttpModule (live editor state, disk fallback)
///  - everything else        → files under {gameRoot} (NEOScavenger.swf, data/, img/, Mods/)
/// Loopback-only, GET/HEAD only (plus the __log POST), path-traversal guarded, lifetime
/// tied to the panel (IDisposable).
/// </summary>
public sealed class GameContentServer : IDisposable
{
    private static readonly Dictionary<string, string> Mime = new(StringComparer.OrdinalIgnoreCase)
    {
        [".html"] = "text/html; charset=utf-8",
        [".js"] = "application/javascript",
        [".mjs"] = "application/javascript",
        [".json"] = "application/json",
        [".wasm"] = "application/wasm",
        [".map"] = "application/json",
        [".swf"] = "application/x-shockwave-flash",
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".gif"] = "image/gif",
        [".mp3"] = "audio/mpeg",
        [".ogg"] = "audio/ogg",
        [".xml"] = "text/xml; charset=utf-8",
        [".php"] = "text/plain; charset=utf-8",
        [".css"] = "text/css; charset=utf-8",
        [".txt"] = "text/plain; charset=utf-8",
        [".svg"] = "image/svg+xml",
        [".ico"] = "image/x-icon",
    };

    private readonly IConfigService _config;
    private readonly ProxyHttpModule _proxy;
    private readonly SwfLogBridge _logs;
    private readonly SaveBackupService _backups;
    private readonly string _webRoot;

    private HttpListener? _listener;
    private CancellationTokenSource? _cts;
    private Task? _loop;

    public GameContentServer(IConfigService config, ProxyHttpModule proxy, SwfLogBridge logs)
        : this(config, proxy, logs, Path.Combine(AppContext.BaseDirectory, "Web"), new SaveBackupService(config))
    {
    }

    /// <summary>Test seam: allow an explicit web root instead of the deployed output folder.</summary>
    internal GameContentServer(IConfigService config, ProxyHttpModule proxy, SwfLogBridge logs, string webRoot,
        SaveBackupService? backups = null)
    {
        _config = config;
        _proxy = proxy;
        _logs = logs;
        _webRoot = webRoot;
        _backups = backups ?? new SaveBackupService(config);
    }

    /// <summary>Base URL (http://127.0.0.1:&lt;port&gt;/) once running.</summary>
    public string? BaseUrl { get; private set; }

    public bool IsRunning => _listener?.IsListening == true;

    /// <summary>Start the loopback server (idempotent). False when the port could not be bound.</summary>
    public bool Start()
    {
        if (IsRunning) return true;

        var port = ReserveConfiguredPort();
        if (port is null) return false;

        try
        {
            var listener = new HttpListener();
            listener.Prefixes.Add($"http://127.0.0.1:{port}/");
            listener.Start();
            _listener = listener;
            BaseUrl = $"http://127.0.0.1:{port}/";
            _cts = new CancellationTokenSource();
            _loop = Task.Run(() => AcceptLoopAsync(listener, _cts.Token));
            Log.Logger.ForContext("Source", "WebViewPreview")
                .Information("[GameContentServer] listening on {BaseUrl} (web root: {WebRoot})", BaseUrl, _webRoot);
            return true;
        }
        catch (Exception ex)
        {
            Log.Logger.ForContext("Source", "WebViewPreview")
                .Error(ex, "[GameContentServer] failed to start on port {Port}", port);
            return false;
        }
    }

    /// <summary>Stop the server and release the loopback port.</summary>
    public void Stop()
    {
        _cts?.Cancel();
        _listener?.Stop();
        _listener?.Close();
        _listener = null;
        BaseUrl = null;
    }

    public void Dispose()
    {
        Stop();
        _cts?.Dispose();
        _cts = null;
    }

    private static int? ReserveLoopbackPort()
    {
        try
        {
            using var tcp = new TcpListener(IPAddress.Loopback, 0);
            tcp.Start();
            return ((IPEndPoint)tcp.LocalEndpoint).Port;
        }
        catch (Exception ex)
        {
            Log.Logger.ForContext("Source", "WebViewPreview")
                .Error(ex, "[GameContentServer] could not reserve a loopback port");
            return null;
        }
    }

    /// <summary>
    /// v2.36: the loopback port is PERSISTED (AppConfig.ServerPort) — a random port per
    /// launch would change the page origin, and WebView2 isolates localStorage per origin,
    /// which made game saves appear to vanish between runs. 0 → pick a random free port
    /// and persist it; occupied ports bump +1 (up to 20 tries) and write the winner back.
    /// </summary>
    private int? ReserveConfiguredPort()
    {
        var preferred = _config.Config.ServerPort;
        if (preferred <= 0)
        {
            var random = ReserveLoopbackPort();
            if (random is not null) _config.Config.ServerPort = random.Value;
            return random;
        }

        for (var attempt = 0; attempt < 20; attempt++)
        {
            var port = preferred + attempt;
            try
            {
                using var tcp = new TcpListener(IPAddress.Loopback, port);
                tcp.Start();
                if (port != preferred) _config.Config.ServerPort = port;   // host persists
                return port;
            }
            catch (Exception)
            {
                // occupied — try the next port
            }
        }

        Log.Logger.ForContext("Source", "WebViewPreview")
            .Warning("[GameContentServer] ports {From}..{To} all busy", preferred, preferred + 19);
        return null;
    }

    private async Task AcceptLoopAsync(HttpListener listener, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            HttpListenerContext context;
            try
            {
                context = await listener.GetContextAsync().ConfigureAwait(false);
            }
            catch (Exception) when (!ct.IsCancellationRequested)
            {
                return; // listener stopped
            }

            _ = Task.Run(() => HandleAsync(context), CancellationToken.None);
        }
    }

    private async Task HandleAsync(HttpListenerContext context)
    {
        var request = context.Request;
        var response = context.Response;
        try
        {
            var path = Uri.UnescapeDataString(request.Url?.AbsolutePath ?? "/");

            if (request.HttpMethod == "POST" && path.Equals("/__log", StringComparison.OrdinalIgnoreCase))
            {
                using var reader = new StreamReader(request.InputStream, Encoding.UTF8);
                _logs.HandleLogBatch(await reader.ReadToEndAsync().ConfigureAwait(false));
                response.StatusCode = 204;
                response.Close();
                return;
            }

            // v2.37: save backups — the page POSTs the OLD localStorage value before every
            // write/remove (the game deletes its save on death); backups land on disk.
            if (request.HttpMethod == "POST" && path.Equals("/__backup", StringComparison.OrdinalIgnoreCase))
            {
                using var reader = new StreamReader(request.InputStream, Encoding.UTF8);
                _backups.HandleBackupRequest(await reader.ReadToEndAsync().ConfigureAwait(false));
                response.StatusCode = 204;
                response.Close();
                return;
            }

            if (request.HttpMethod is not ("GET" or "HEAD"))
            {
                response.StatusCode = 405;
                response.Close();
                return;
            }

            var headOnly = request.HttpMethod == "HEAD";

            // Ruffle embedding page.
            if (path is "/" or "/index.html" or "/host.html")
            {
                ServeFile(response, Path.Combine(_webRoot, "host.html"), headOnly);
                return;
            }

            // Bundled Ruffle assets (must be under Web/ruffle, never the game dir).
            if (path.StartsWith("/ruffle/", StringComparison.OrdinalIgnoreCase))
            {
                ServeFile(response, Path.Combine(_webRoot, path.TrimStart('/')), headOnly, _webRoot);
                return;
            }

            // Reverse-proxy routes (live editor state).
            if (IsProxyPath(path))
            {
                var proxyResponse = await _proxy.TryServeAsync(path.TrimStart('/')).ConfigureAwait(false);
                if (proxyResponse is not null)
                {
                    WriteResponse(response, proxyResponse.StatusCode, proxyResponse.ContentType,
                        proxyResponse.Body, headOnly);
                    return;
                }
                // else: not proxied → fall through to the disk file below.
            }

            // Game root files (NEOScavenger.swf, data/, img/, Mods/...).
            var gameRoot = _config.Config.GameRootDir;
            ServeFile(response, Path.Combine(gameRoot, path.TrimStart('/')), headOnly, gameRoot);
        }
        catch (Exception ex)
        {
            Log.Logger.ForContext("Source", "WebViewPreview")
                .Warning(ex, "[GameContentServer] error handling {Url}", request.Url);
            try
            {
                response.StatusCode = 500;
                response.Close();
            }
            catch (Exception)
            {
                // response already closed
            }
        }
    }

    private static bool IsProxyPath(string path)
        => path.StartsWith("/data/", StringComparison.OrdinalIgnoreCase)
           || path.EndsWith(".php", StringComparison.OrdinalIgnoreCase)
           || path.Equals("/neogame.xml", StringComparison.OrdinalIgnoreCase);

    /// <summary>Serve a file from disk with MIME + no-cache; path traversal returns 404.</summary>
    private static void ServeFile(HttpListenerResponse response, string filePath, bool headOnly, string? root = null)
    {
        try
        {
            var fullPath = Path.GetFullPath(filePath);
            if (root is not null)
            {
                var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                if (!fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
                {
                    WriteResponse(response, 403, "text/plain; charset=utf-8", Array.Empty<byte>(), headOnly);
                    return;
                }
            }

            if (!File.Exists(fullPath))
            {
                WriteResponse(response, 404, "text/plain; charset=utf-8", Array.Empty<byte>(), headOnly);
                return;
            }

            var mime = Mime.GetValueOrDefault(Path.GetExtension(fullPath), "application/octet-stream");
            var body = File.ReadAllBytes(fullPath);
            WriteResponse(response, 200, mime, body, headOnly, cacheControl:
                Path.GetExtension(fullPath) is ".png" or ".jpg" or ".jpeg" or ".gif"
                    ? "public, max-age=3600"
                    : "no-cache");
        }
        catch (Exception ex)
        {
            Log.Logger.ForContext("Source", "WebViewPreview")
                .Warning(ex, "[GameContentServer] file serve error: {Path}", filePath);
            WriteResponse(response, 500, "text/plain; charset=utf-8", Array.Empty<byte>(), headOnly);
        }
    }

    private static void WriteResponse(HttpListenerResponse response, int status, string contentType,
        byte[] body, bool headOnly, string? cacheControl = null)
    {
        try
        {
            response.StatusCode = status;
            response.ContentType = contentType;
            response.ContentLength64 = body.Length;
            if (cacheControl is not null) response.Headers["Cache-Control"] = cacheControl;
            if (!headOnly && body.Length > 0) response.OutputStream.Write(body, 0, body.Length);
            response.Close();
        }
        catch (Exception)
        {
            // client aborted — nothing to do
        }
    }
}
