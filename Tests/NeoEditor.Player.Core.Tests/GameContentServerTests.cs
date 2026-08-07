using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using NeoEditor.Player.Core.Logging;
using NeoEditor.Player.Core.Services;
using Xunit;

namespace NeoEditor.Player.Core.Tests;

public class GameContentServerTests : IDisposable
{
    private readonly GameContentServer _server;
    private readonly HttpClient _client;
    private readonly string _gameRoot;
    private readonly string _webRoot;

    public GameContentServerTests()
    {
        _gameRoot = TestFs.NewTempDir();
        _webRoot = TestFs.NewTempDir();
        File.WriteAllText(Path.Combine(_webRoot, "host.html"), "<html>host</html>");
        Directory.CreateDirectory(Path.Combine(_webRoot, "ruffle"));
        File.WriteAllText(Path.Combine(_webRoot, "ruffle", "ruffle.js"), "// ruffle loader");

        var config = new FakeConfigService { };
        config.Config.GameRootDir = _gameRoot;
        var data = new FakeGameDataExportService()
            .Add("itemtypes", "<database name=\"neogame\"><table name=\"itemtypes\"/></database>");
        var proxy = new ProxyHttpModule(config, new FakePhpGenerator(), data);

        _server = new GameContentServer(config, proxy, new SwfLogBridge(new RunLogStore()), _webRoot);
        Assert.True(_server.Start(), "server should start on a random loopback port");
        _client = new HttpClient(new HttpClientHandler { UseProxy = false })
        {
            BaseAddress = new Uri(_server.BaseUrl!),
        };
    }

    public void Dispose()
    {
        _client.Dispose();
        _server.Dispose();
    }

    [Fact]
    public async Task ServesHostPage()
    {
        var response = await _client.GetAsync("/");
        Assert.Equal(200, (int)response.StatusCode);
        Assert.StartsWith("text/html", response.Content.Headers.ContentType!.ToString());
        Assert.Equal("<html>host</html>", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task ServesRuffleAssetsWithJsMime()
    {
        var response = await _client.GetAsync("/ruffle/ruffle.js");
        Assert.Equal(200, (int)response.StatusCode);
        Assert.StartsWith("application/javascript", response.Content.Headers.ContentType!.ToString());
    }

    [Fact]
    public async Task ServesWebRootScriptsLikeLsoExpander()
    {
        // v2.47 回归: /lso-expand-web.js 之前被路由到游戏根目录 → 404 →
        // window.LsoExpand 从未加载 → 运行时存档展开从未生效。
        File.WriteAllText(Path.Combine(_webRoot, "lso-expand-web.js"), "// expander");
        var response = await _client.GetAsync("/lso-expand-web.js");
        Assert.Equal(200, (int)response.StatusCode);
        Assert.StartsWith("application/javascript", response.Content.Headers.ContentType!.ToString());
        Assert.Equal("// expander", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task WebRootDoesNotShadowGameRootFiles()
    {
        // Web 目录优先, 但游戏根目录文件(如 NEOScavenger.swf)仍可访问。
        File.WriteAllText(Path.Combine(_gameRoot, "NEOScavenger.swf"), "fake-swf");
        var response = await _client.GetAsync("/NEOScavenger.swf");
        Assert.Equal(200, (int)response.StatusCode);
        Assert.StartsWith("application/x-shockwave-flash", response.Content.Headers.ContentType!.ToString());
    }

    [Fact]
    public async Task ServesGameRootFiles()
    {
        File.WriteAllText(Path.Combine(_gameRoot, "NEOScavenger.swf"), "fake-swf");
        var response = await _client.GetAsync("/NEOScavenger.swf");
        Assert.Equal(200, (int)response.StatusCode);
        Assert.StartsWith("application/x-shockwave-flash", response.Content.Headers.ContentType!.ToString());
    }

    [Fact]
    public async Task ServesLiveDataXmlThroughProxy()
    {
        var response = await _client.GetAsync("/data/itemtypes.xml");
        Assert.Equal(200, (int)response.StatusCode);
        Assert.Contains("table name=\"itemtypes\"", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task RootNeogameXmlIs404()
    {
        var response = await _client.GetAsync("/neogame.xml");
        Assert.Equal(404, (int)response.StatusCode);
    }

    [Fact]
    public async Task MissingFileIs404()
    {
        var response = await _client.GetAsync("/no-such-file.bin");
        Assert.Equal(404, (int)response.StatusCode);
    }

    [Fact]
    public async Task EncodedTraversalIsRejected()
    {
        var response = await _client.GetAsync("/%2e%2e/%2e%2e/Windows/win.ini");
        Assert.True((int)response.StatusCode is 403 or 404, $"expected 403/404, got {(int)response.StatusCode}");
    }

    [Fact]
    public async Task LogEndpointAcceptsBatches()
    {
        var response = await _client.PostAsync("/__log", new StringContent(
            "{\"run\":\"1\",\"level\":\"warn\",\"msg\":\"test batch\"}"));
        Assert.Equal(204, (int)response.StatusCode);
    }

    [Fact]
    public async Task LogEndpointAcceptsNumericRunId()
    {
        // host.html sends run: Date.now() (a Number) — must not break the log pipeline.
        var response = await _client.PostAsync("/__log", new StringContent(
            "{\"run\":1785845000,\"level\":\"info\",\"msg\":\"numeric run ok\"}"));
        Assert.Equal(204, (int)response.StatusCode);
    }

    [Fact]
    public async Task MalformedLogBatchDoesNotCrash()
    {
        var response = await _client.PostAsync("/__log", new StringContent("not json"));
        Assert.Equal(204, (int)response.StatusCode);
    }

    [Fact]
    public async Task NonGetMethodIs405()
    {
        var request = new HttpRequestMessage(HttpMethod.Delete, "/");
        var response = await _client.SendAsync(request);
        Assert.Equal(405, (int)response.StatusCode);
    }
}
