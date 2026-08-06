using System.Collections.Generic;

namespace NeoEditor.Core.Abstractions;

/// <summary>
/// Generates the game's PHP-list response bodies (getmods.php / getimages.php) in the exact
/// query-string format the SWF parses (URLVariables; spaces/line breaks break loading).
/// Implemented by the App's PhpParser; consumed by the WebView plugin's ProxyHttpModule
/// (Docs/42 §3.6) so the game can be served live data without touching disk.
/// </summary>
public interface IGamePhpGenerator
{
    /// <summary>
    /// getmods.php body: <c>nRows=N</c> + <c>&amp;strModName{i}=&lt;name&gt;&amp;strModURL{i}=&lt;relativePath&gt;</c>.
    /// </summary>
    string GenerateModsPhp(IReadOnlyList<(string Name, string Path)> mods);

    /// <summary>
    /// getimages.php body: <c>nRows=N&amp;nCols=2</c> + <c>&amp;strImageURL{i}=&lt;fileName&gt;</c>,
    /// normal image immediately followed by its x2 variant (n*2 table layout).
    /// </summary>
    string GenerateImagePhp(IReadOnlyList<(string NormalImage, string X2Image)> imagePairs);
}
