using System.Collections.Generic;

namespace NeoEditor.Plugins.ImageTools.Services;

/// <summary>
/// Image search directory resolution.
/// Created during M11 migration to break dependency on App's IImageService.
/// </summary>
public interface IImageSearchService
{
    List<string> GetImageSearchDirsForEntity(string gameRoot, string? entityFilePath);
}
