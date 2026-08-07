using NeoEditor.Player.Core.Logging;
using NeoEditor.Player.Core.Services;
using Xunit;

namespace NeoEditor.Player.Core.Tests;

public class SwfLogBridgeTests
{
    private static (SwfLogBridge Bridge, RunLogStore Store) Create()
    {
        var store = new RunLogStore();
        return (new SwfLogBridge(store), store);
    }

    [Fact]
    public void DetectsGameExitFromFscommandQuit()
    {
        // User-verified: the game's quit path is fscommand("quit") → Ruffle logs
        // "unknown FSCommand:quit" (O8 calibration).
        var (bridge, _) = Create();
        PlayerGameEventType? fired = null;
        bridge.GameEventDetected += (_, e) => fired = e.Type;

        bridge.HandleLogBatch(
            "{\"run\":1,\"level\":\"warn\",\"msg\":\"[warn] core/src/tag_utils.rs unknown FSCommand:quit\"}");

        Assert.Equal(PlayerGameEventType.GameExit, fired);
    }

    [Fact]
    public void DetectsExitFromLocalizedClipboardLog()
    {
        var (bridge, _) = Create();
        PlayerGameEventType? fired = null;
        bridge.GameEventDetected += (_, e) => fired = e.Type;

        bridge.HandleLogBatch(
            "{\"run\":2,\"level\":\"clipboard\",\"msg\":\"[clipboard] 游戏剪贴板日志(截获): 退出游戏\"}");

        Assert.Equal(PlayerGameEventType.GameExit, fired);
    }

    [Fact]
    public void DoesNotFireOnUnrelatedLogs()
    {
        var (bridge, store) = Create();
        var fired = 0;
        bridge.GameEventDetected += (_, _) => fired++;

        bridge.HandleLogBatch("{\"run\":3,\"level\":\"info\",\"msg\":\"[info] Loaded SWF version 15\"}");
        bridge.HandleLogBatch("{\"run\":3,\"level\":\"debug\",\"msg\":\"[debug] Audio underrun detected\"}");

        Assert.Equal(0, fired);
        Assert.Single(store.Runs);
    }

    [Fact]
    public void DebouncesRepeatedEventsPerType()
    {
        var (bridge, _) = Create();
        var fired = 0;
        bridge.GameEventDetected += (_, _) => fired++;

        var body = "{\"run\":4,\"level\":\"warn\",\"msg\":\"[warn] unknown FSCommand:quit\"}";
        bridge.HandleLogBatch(body);
        bridge.HandleLogBatch(body);

        Assert.Equal(1, fired);
    }

    [Theory]
    [InlineData("{\"run\":5,\"level\":\"error\",\"msg\":\"window.onerror: Cannot read properties of undefined @ http://127.0.0.1:1234/host.html:12\"}")]
    [InlineData("{\"run\":5,\"level\":\"error\",\"msg\":\"AVM Error: cannot convert false to Vector.<int>\"}")]
    [InlineData("{\"run\":5,\"level\":\"error\",\"msg\":\"unhandledrejection: TypeError: load failed\"}")]
    [InlineData("{\"run\":5,\"level\":\"error\",\"msg\":\"Maximum call stack size exceeded\"}")]
    [InlineData("{\"run\":5,\"level\":\"error\",\"msg\":\"SWF 加载失败: failed to fetch NEOScavenger.swf\"}")]
    public void DetectsFatalRuntimeError(string body)
    {
        var (bridge, _) = Create();
        PlayerGameEventType? fired = null;
        bridge.GameEventDetected += (_, e) => fired = e.Type;

        bridge.HandleLogBatch(body);

        Assert.Equal(PlayerGameEventType.GameError, fired);
    }

    [Theory]
    [InlineData("{\"run\":6,\"level\":\"error\",\"msg\":\"[error] Audio device error: no device found\"}")]
    [InlineData("{\"run\":6,\"level\":\"warn\",\"msg\":\"[warn] Encountered stub: loadSound\"}")]
    [InlineData("{\"run\":6,\"level\":\"clipboard\",\"msg\":\"[clipboard] 游戏剪贴板日志(截获): 探针已部署 (error 字样不应误报)\"}")]
    public void DoesNotFireGameErrorOnBenignLines(string body)
    {
        var (bridge, _) = Create();
        PlayerGameEventType? fired = null;
        bridge.GameEventDetected += (_, e) => fired = e.Type;

        bridge.HandleLogBatch(body);

        // 良性行可能命中其他事件（如 stub → ApiStub），但绝不能误报 GameError。
        Assert.NotEqual(PlayerGameEventType.GameError, fired);
    }
}
