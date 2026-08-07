using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.RegularExpressions;
using NeoEditor.Player.Core.Logging;
using Serilog;

namespace NeoEditor.Player.Core.Services;

/// <summary>Game-side Flash events the host reacts to (Docs/42 v2.5).</summary>
public enum PlayerGameEventType
{
    /// <summary>The game asked to quit (fscommand quit / System.exit / quit flow).</summary>
    GameExit,

    /// <summary>An external navigation was blocked by openUrlMode=deny.</summary>
    NavigationBlocked,

    /// <summary>The game hit an unimplemented Ruffle API (stub warning).</summary>
    ApiStub,

    /// <summary>
    /// A fatal runtime error surfaced from the page (window.onerror / unhandled rejection /
    /// AVM crash / SWF load failure) — the host surfaces it so the crash is not silent.
    /// </summary>
    GameError,
}

/// <summary>Payload of <see cref="SwfLogBridge.GameEventDetected"/>.</summary>
public sealed class GameEventDetectedEventArgs : EventArgs
{
    public GameEventDetectedEventArgs(PlayerGameEventType type, string detail)
    {
        Type = type;
        Detail = detail;
    }

    public PlayerGameEventType Type { get; }
    public string Detail { get; }
}

/// <summary>
/// Receives the host page's forwarded console / clipboard log batches (Docs/42 §3.4,
/// channel A/B/C) via POST /__log: appends them to the <see cref="RunLogStore"/> (log
/// viewer), writes them to Serilog, and detects game-side Flash events (exit, blocked
/// navigation, API stubs) for the host to react to.
/// </summary>
public sealed class SwfLogBridge
{
    // Patterns — CALIBRATED (2026-08-05, O8): the game's quit path is
    // fscommand("quit") → Ruffle logs "unknown FSCommand:quit" (user-verified). The
    // Chinese "退出游戏" stays as a secondary signal (clipboard log, localised builds).
    private static readonly (PlayerGameEventType Type, Regex Pattern)[] GameEventPatterns =
    {
        (PlayerGameEventType.GameExit,
            new Regex(@"FSCommand:\s*quit|退出游戏|quit to desktop|exit game", RegexOptions.IgnoreCase)),
        (PlayerGameEventType.NavigationBlocked,
            new Regex(@"navigation.{0,40}blocked|blocked.{0,40}navigation", RegexOptions.IgnoreCase)),
        (PlayerGameEventType.ApiStub,
            new Regex(@"Encountered stub", RegexOptions.IgnoreCase)),
        // R38: 致命错误签名（window.onerror / unhandledrejection / AVM 崩溃 / SWF 加载失败）。
        // Ruffle 本版不派发 error 事件（只有 loadedmetadata/loadeddata），运行时 AVM 错误
        // 走 console.error 通道——由 host.html 转成 level=error 行后在这里按签名识别。
        // 命中即触发宿主「报错捕捉」弹窗（去抖 10s，VM 侧每 run 再限一次）。
        (PlayerGameEventType.GameError,
            new Regex(@"window\.onerror:|unhandledrejection|cannot convert|TypeError|ReferenceError|RangeError|SyntaxError|stack overflow|Maximum call stack|SWF 加载失败|Ruffle failed",
                RegexOptions.IgnoreCase)),
    };

    private static readonly TimeSpan EventDebounce = TimeSpan.FromSeconds(10);

    private readonly RunLogStore _store;
    private readonly Dictionary<PlayerGameEventType, DateTime> _lastFired = new();

    public SwfLogBridge(RunLogStore store)
    {
        _store = store;
    }

    /// <summary>Raised when a game-side Flash event is detected (debounced per type).</summary>
    public event EventHandler<GameEventDetectedEventArgs>? GameEventDetected;

    /// <summary>Parse a {run, level, msg} batch from the page and forward it to the store + Serilog.</summary>
    public void HandleLogBatch(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            // "run" is a Number (Date.now()) on the page side, "level"/"msg" are Strings —
            // read defensively so a type mismatch never kills the log pipeline.
            var run = root.TryGetProperty("run", out var runEl) ? ReadAsString(runEl) : null;
            var level = root.TryGetProperty("level", out var levelEl) ? ReadAsString(levelEl) : null;
            var msg = root.TryGetProperty("msg", out var msgEl) ? ReadAsString(msgEl) : null;
            if (string.IsNullOrEmpty(msg)) return;

            var runId = run ?? "?";
            _store.Append(runId, level ?? "log", msg);

            var logger = Log.Logger.ForContext("Source", "WebViewPreview")
                .ForContext("RunId", runId);

            switch ((level ?? "log").ToLowerInvariant())
            {
                case "error": logger.Error("{Message}", msg); break;
                case "warn": logger.Warning("{Message}", msg); break;
                case "debug": logger.Debug("{Message}", msg); break;
                default: logger.Information("{Message}", msg); break;
            }

            DetectGameEvents(msg);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            Log.Logger.ForContext("Source", "WebViewPreview")
                .Warning(ex, "Malformed log batch from webview page: {Body}", Truncate(body));
        }
    }

    private void DetectGameEvents(string message)
    {
        var now = DateTime.UtcNow;
        foreach (var (type, pattern) in GameEventPatterns)
        {
            if (!pattern.IsMatch(message)) continue;
            if (_lastFired.TryGetValue(type, out var last) && now - last < EventDebounce) continue;

            _lastFired[type] = now;
            GameEventDetected?.Invoke(this, new GameEventDetectedEventArgs(type, Truncate(message)));
            Log.Logger.ForContext("Source", "WebViewPreview")
                .Information("[GameEvent] {Type}: {Detail}", type, Truncate(message));
        }
    }

    private static string? ReadAsString(JsonElement element)
        => element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.GetRawText(),
            _ => null,
        };

    private static string Truncate(string s) => s.Length <= 512 ? s : s[..512] + "…";
}
