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

namespace NeoEditor.Plugins.JsVisualization.Services;

/// <summary>
/// D09 §二: loopback HTTP server for the JS 可视化 tab. Binds 127.0.0.1 on a
/// random port and serves only /viz/* routes:
///  - /viz/ and /viz/*.{html,js,css} → plugin Web/viz static assets (the page)
///  - /viz/data  (GET)              → EntitySnapshotDto JSON (by id or by XML)
///  - /viz/assets (GET)             → image files (absolute path or game-root relative)
///  - /viz/action (POST)            → VizActionHandler (interaction bridge)
/// The page is zero-host-dependent: it fetches relative /viz/* URLs, so the same
/// assets run in a plain browser against any static server (AI screenshot loop, D09 §六).
/// </summary>
public sealed class VizContentServer : IDisposable
{
    private static readonly Dictionary<string, string> Mime = new(StringComparer.OrdinalIgnoreCase)
    {
        [".html"] = "text/html; charset=utf-8",
        [".js"] = "application/javascript",
        [".css"] = "text/css; charset=utf-8",
        [".json"] = "application/json",
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".svg"] = "image/svg+xml",
        [".ico"] = "image/x-icon",
        [".txt"] = "text/plain; charset=utf-8",
    };

    private readonly IConfigService _config;
    private readonly VizSnapshotService _snapshots;
    private readonly VizActionHandler _actions;
    private readonly string _webRoot;

    private HttpListener? _listener;
    private CancellationTokenSource? _cts;
    private Task? _loop;

    public VizContentServer(IConfigService config, VizSnapshotService snapshots, VizActionHandler actions)
        : this(config, snapshots, actions, Path.Combine(AppContext.BaseDirectory, "Web", "viz"))
    {
    }

    /// <summary>Test seam: explicit web root (samples/ or the plugin's Web/viz).</summary>
    internal VizContentServer(IConfigService config, VizSnapshotService snapshots, VizActionHandler actions,
        string webRoot)
    {
        _config = config;
        _snapshots = snapshots;
        _actions = actions;
        _webRoot = webRoot;
    }

    /// <summary>Base URL (http://127.0.0.1:&lt;port&gt;/) once running.</summary>
    public string? BaseUrl { get; private set; }

    public bool IsRunning => _listener?.IsListening == true;

    /// <summary>Start the loopback server (idempotent). False when the port could not be bound.</summary>
    public bool Start()
    {
        if (IsRunning) return true;
        var port = ReserveLoopbackPort();
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
            Log.Logger.ForContext("Source", "JsVisualization")
                .Information("[VizContentServer] listening on {BaseUrl} (web root: {WebRoot})", BaseUrl, _webRoot);
            return true;
        }
        catch (Exception ex)
        {
            Log.Logger.ForContext("Source", "JsVisualization")
                .Error(ex, "[VizContentServer] failed to start on port {Port}", port);
            return false;
        }
    }

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
            Log.Logger.ForContext("Source", "JsVisualization")
                .Error(ex, "[VizContentServer] could not reserve a loopback port");
            return null;
        }
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

            // ── POST /viz/action — interaction bridge (D09 §五) ───────────────
            if (request.HttpMethod == "POST" && path.Equals("/viz/action", StringComparison.OrdinalIgnoreCase))
            {
                using var reader = new StreamReader(request.InputStream, Encoding.UTF8);
                var body = await reader.ReadToEndAsync().ConfigureAwait(false);
                var error = _actions.TryParse(body, out var action) ? _actions.Handle(action) : "invalid action JSON";
                if (error is null)
                {
                    response.StatusCode = 204;
                    response.Close();
                }
                else
                {
                    WriteResponse(response, 400, "text/plain; charset=utf-8", Encoding.UTF8.GetBytes(error));
                }
                return;
            }

            if (request.HttpMethod is not ("GET" or "HEAD"))
            {
                response.StatusCode = 405;
                response.Close();
                return;
            }

            var headOnly = request.HttpMethod == "HEAD";

            // ── GET /viz/xmlfile — 调试：游戏目录 XML 文件 → 全量语义快照 ────
            // （开发/AI 验证通道：?file=data/encounters.xml 直接从游戏目录加载真实数据，
            //   不走手造 sample；越界/不存在 404）
            if (path.Equals("/viz/xmlfile", StringComparison.OrdinalIgnoreCase))
            {
                var xmlFile = request.QueryString["path"];
                if (string.IsNullOrWhiteSpace(xmlFile) || xmlFile.Contains("..", StringComparison.Ordinal))
                {
                    WriteResponse(response, 400, "text/plain; charset=utf-8", Encoding.UTF8.GetBytes("bad 'path'"));
                    return;
                }

                var gameRoot = _config.Config.GameRootDir;
                if (string.IsNullOrWhiteSpace(gameRoot))
                {
                    WriteResponse(response, 404, "text/plain; charset=utf-8", Array.Empty<byte>(), headOnly);
                    return;
                }
                var fullPath = Path.GetFullPath(Path.Combine(gameRoot, xmlFile.Replace('/', Path.DirectorySeparatorChar)));
                var rootDir = Path.GetFullPath(gameRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                if (!fullPath.StartsWith(rootDir, StringComparison.OrdinalIgnoreCase) || !File.Exists(fullPath))
                {
                    WriteResponse(response, 404, "text/plain; charset=utf-8", Array.Empty<byte>(), headOnly);
                    return;
                }

                var xml = File.ReadAllText(fullPath);
                var entityType = XmlTableNameToType(xml);
                if (entityType is null)
                {
                    WriteResponse(response, 400, "text/plain; charset=utf-8",
                        Encoding.UTF8.GetBytes("unsupported table (仅 encounters)"));
                    return;
                }

                var snapshot = _snapshots.BuildFromXml(entityType, xml);
                if (snapshot is null)
                {
                    WriteResponse(response, 404, "text/plain; charset=utf-8", Array.Empty<byte>(), headOnly);
                    return;
                }

                WriteResponse(response, 200, "application/json; charset=utf-8",
                    Encoding.UTF8.GetBytes(_snapshots.Serialize(snapshot)), headOnly, cacheControl: "no-store");
                return;
            }

            // ── GET /viz/data — entity snapshot (by id or by XML) ────────────
            if (path.Equals("/viz/data", StringComparison.OrdinalIgnoreCase))
            {
                var type = request.QueryString["type"];
                var id = request.QueryString["id"];
                var xml = request.QueryString["xml"];
                var pre = request.QueryString["pre"];

                if (string.IsNullOrWhiteSpace(type))
                {
                    WriteResponse(response, 400, "text/plain; charset=utf-8", Encoding.UTF8.GetBytes("missing 'type'"));
                    return;
                }

                EntitySnapshotDto? snapshot = null;
                if (!string.IsNullOrWhiteSpace(xml))
                    snapshot = _snapshots.BuildFromXml(type, xml);
                else if (!string.IsNullOrWhiteSpace(id))
                {
                    var preConds = string.IsNullOrWhiteSpace(pre)
                        ? null
                        : new HashSet<string>(pre.Split(',', StringSplitOptions.RemoveEmptyEntries));
                    snapshot = _snapshots.BuildById(type, id, preConds);
                }

                if (snapshot is null)
                {
                    WriteResponse(response, 404, "text/plain; charset=utf-8",
                        Encoding.UTF8.GetBytes($"no snapshot for {type}#{id ?? "<xml>"}"));
                    return;
                }

                WriteResponse(response, 200, "application/json; charset=utf-8",
                    Encoding.UTF8.GetBytes(_snapshots.Serialize(snapshot)), headOnly, cacheControl: "no-store");
                return;
            }

            // ── GET /viz/assets — image file (absolute path from findImage, or game-root relative) ──
            if (path.Equals("/viz/assets", StringComparison.OrdinalIgnoreCase))
            {
                var assetPath = request.QueryString["path"];
                if (string.IsNullOrWhiteSpace(assetPath))
                {
                    WriteResponse(response, 400, "text/plain; charset=utf-8", Encoding.UTF8.GetBytes("missing 'path'"));
                    return;
                }

                string candidate = Path.IsPathRooted(assetPath)
                    ? assetPath
                    : Path.Combine(_config.Config.GameRootDir ?? "", assetPath);
                if (!File.Exists(candidate))
                {
                    WriteResponse(response, 404, "text/plain; charset=utf-8", Array.Empty<byte>(), headOnly);
                    return;
                }

                ServeFile(response, candidate, headOnly);
                return;
            }

            // ── GET /viz/* — static page assets (Web/viz) ────────────────────
            var rel = path.TrimStart('/');
            if (rel.StartsWith("viz/", StringComparison.OrdinalIgnoreCase)) rel = rel["viz/".Length..];
            if (rel.Length == 0) rel = "index.html";
            if (rel.Contains("..", StringComparison.Ordinal)) rel = "__traversal__"; // guard

            var file = Path.Combine(_webRoot, rel.Replace('/', Path.DirectorySeparatorChar));
            if (Path.GetFullPath(file).StartsWith(Path.GetFullPath(_webRoot),
                    StringComparison.OrdinalIgnoreCase) && File.Exists(file))
            {
                ServeFile(response, file, headOnly);
                return;
            }

            WriteResponse(response, 404, "text/plain; charset=utf-8", Array.Empty<byte>(), headOnly);
        }
        catch (Exception ex)
        {
            Log.Logger.ForContext("Source", "JsVisualization")
                .Warning(ex, "[VizContentServer] error handling {Url}", request.Url);
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

    /// <summary>从 XML 文本推断实体类型（v1 仅 encounters 全语义）。</summary>
    private static string? XmlTableNameToType(string xml)
    {
        var m = System.Text.RegularExpressions.Regex.Match(xml,
            @"<table\s+name=""([^""]+)""", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return m.Success && m.Groups[1].Value.Equals("encounters", StringComparison.OrdinalIgnoreCase)
            ? "Encounter"
            : null;
    }

    private static void ServeFile(HttpListenerResponse response, string filePath, bool headOnly)
    {
        try
        {
            var body = File.ReadAllBytes(filePath);
            var mime = Mime.GetValueOrDefault(Path.GetExtension(filePath), "application/octet-stream");
            WriteResponse(response, 200, mime, body, headOnly, cacheControl:
                Path.GetExtension(filePath) is ".html" or ".js" or ".css"
                    ? "no-store"   // dev iteration: never cache the page
                    : "public, max-age=3600");
        }
        catch (Exception ex)
        {
            Log.Logger.ForContext("Source", "JsVisualization")
                .Warning(ex, "[VizContentServer] file serve error: {Path}", filePath);
            WriteResponse(response, 500, "text/plain; charset=utf-8", Array.Empty<byte>(), headOnly);
        }
    }

    private static void WriteResponse(HttpListenerResponse response, int status, string contentType,
        byte[] body, bool headOnly = false, string? cacheControl = null)
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
