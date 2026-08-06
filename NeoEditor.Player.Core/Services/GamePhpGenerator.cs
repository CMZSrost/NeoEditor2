using System.Collections.Generic;
using System.Text;
using NeoEditor.Core.Abstractions;

namespace NeoEditor.Player.Core.Services;

/// <summary>
/// Default <see cref="IGamePhpGenerator"/> for hosts without the editor's PhpParser
/// (e.g. the standalone player): produces the exact query-string format the SWF parses
/// (URLVariables — spaces/line breaks break loading). The editor keeps its own PhpParser
/// implementation; both implement the same Core contract.
/// </summary>
public sealed class GamePhpGenerator : IGamePhpGenerator
{
    public string GenerateModsPhp(IReadOnlyList<(string Name, string Path)> mods)
    {
        var sb = new StringBuilder($"nRows={mods.Count}");
        for (var i = 0; i < mods.Count; i++)
        {
            sb.Append($"&strModName{i}={mods[i].Name.Trim()}");
            sb.Append($"&strModURL{i}={mods[i].Path.Trim()}");
        }

        return sb.ToString();
    }

    public string GenerateImagePhp(IReadOnlyList<(string NormalImage, string X2Image)> imagePairs)
    {
        var sb = new StringBuilder();
        var count = 0;
        foreach (var (normal, x2) in imagePairs)
        {
            if (!string.IsNullOrWhiteSpace(normal)) { sb.Append($"&strImageURL{count++}={normal.Trim()}"); }
            if (!string.IsNullOrWhiteSpace(x2)) { sb.Append($"&strImageURL{count++}={x2.Trim()}"); }
        }

        return $"nRows={count}&nCols=2{sb}";
    }
}
