using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using NeoEditor.Player.Core.Logging;
using NeoEditor.Player.Core.Services;
using Xunit;

namespace NeoEditor.Player.Core.Tests;

/// <summary>
/// Loopback port persistence tests (Docs/42 v2.36): a stable origin keeps the game's
/// localStorage saves across launches, so the configured port must be reused and an
/// occupied port bumps +1 (writing the winner back).
/// </summary>
public class PortPersistenceTests : IDisposable
{
    private readonly string _webRoot;
    private readonly FakeConfigService _config = new();
    private readonly GameContentServer _server;

    public PortPersistenceTests()
    {
        _webRoot = TestFs.NewTempDir();
        File.WriteAllText(Path.Combine(_webRoot, "host.html"), "<html>host</html>");
        Directory.CreateDirectory(Path.Combine(_webRoot, "ruffle"));
        File.WriteAllText(Path.Combine(_webRoot, "ruffle", "ruffle.js"), "// ruffle loader");

        _config.Config.GameRootDir = TestFs.NewTempDir();
        var proxy = new ProxyHttpModule(_config, new FakePhpGenerator(), new FakeGameDataExportService());
        _server = new GameContentServer(_config, proxy, new SwfLogBridge(new RunLogStore()), _webRoot);
    }

    public void Dispose() => _server.Dispose();

    [Fact]
    public void UsesConfiguredPort()
    {
        _config.Config.ServerPort = 18001;

        Assert.True(_server.Start());
        Assert.Contains(":18001/", _server.BaseUrl);
        Assert.Equal(18001, _config.Config.ServerPort);
    }

    [Fact]
    public void BumpsOccupiedPortAndWritesBack()
    {
        // Occupy the configured port with a plain listener.
        using var blocker = new TcpListener(IPAddress.Loopback, 18002);
        blocker.Start();
        _config.Config.ServerPort = 18002;

        Assert.True(_server.Start());
        Assert.Contains(":18003/", _server.BaseUrl);
        Assert.Equal(18003, _config.Config.ServerPort);   // winner persisted → stable next run
    }

    [Fact]
    public void PicksAndPersistsRandomPortWhenUnset()
    {
        Assert.True(_server.Start());
        Assert.NotNull(_server.BaseUrl);
        Assert.InRange(_config.Config.ServerPort, 1024, 65535);
    }
}
