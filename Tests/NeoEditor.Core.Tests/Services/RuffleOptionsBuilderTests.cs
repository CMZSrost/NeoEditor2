using System;
using System.IO;
using NeoEditor.Core.Services;
using Xunit;

namespace NeoEditor.Core.Tests.Services;

/// <summary>
/// Game SWF discovery (Docs/42 preview). The old ruffle.exe command-line builder
/// (Docs/40) was removed 2026-08-05 — only FindSwfPath remains.
/// </summary>
public class RuffleOptionsBuilderTests : IDisposable
{
    private readonly string _gameDir = Path.Combine(Path.GetTempPath(), $"neogame_{Guid.NewGuid():N}");

    public RuffleOptionsBuilderTests()
    {
        Directory.CreateDirectory(_gameDir);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_gameDir, recursive: true);
        }
        catch (IOException)
        {
            // best effort cleanup
        }
    }

    private void CreateSwf(string name = RuffleOptionsBuilder.GameSwfFileName) =>
        File.WriteAllText(Path.Combine(_gameDir, name), "fake swf");

    [Fact]
    public void FindSwfPath_PrefersFixedNeoScavengerName()
    {
        CreateSwf();
        CreateSwf("Other.swf");

        Assert.Equal(Path.Combine(_gameDir, RuffleOptionsBuilder.GameSwfFileName),
            RuffleOptionsBuilder.FindSwfPath(_gameDir));
    }

    [Fact]
    public void FindSwfPath_SingleCustomNamedSwf_IsFound()
    {
        CreateSwf("Game.swf");

        Assert.Equal(Path.Combine(_gameDir, "Game.swf"), RuffleOptionsBuilder.FindSwfPath(_gameDir));
    }

    [Fact]
    public void FindSwfPath_MultipleSwfsWithoutFixedName_ReturnsNull()
    {
        CreateSwf("A.swf");
        CreateSwf("B.swf");

        Assert.Null(RuffleOptionsBuilder.FindSwfPath(_gameDir));
    }

    [Fact]
    public void FindSwfPath_MissingGameRoot_ReturnsNull()
    {
        Assert.Null(RuffleOptionsBuilder.FindSwfPath(Path.Combine(_gameDir, "nope")));
    }
}
