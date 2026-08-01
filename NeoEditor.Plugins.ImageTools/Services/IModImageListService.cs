using System.Collections.Generic;
using System.Threading.Tasks;

namespace NeoEditor.Plugins.ImageTools.Services;

/// <summary>
/// Abstraction for mod image list operations that depend on App-side services
/// (PhpParser and RenameImagePairDialog). Implemented in App, used by Plugin.
/// Created during M11 migration to break App dependency.
/// </summary>
public interface IModImageListService
{
    IReadOnlyList<(string NormalImage, string X2Image)> ParseImagePairs(string getImagesPath);
    string GenerateImagePhp(IReadOnlyList<(string NormalImage, string X2Image)> imagePairs);
    Task<(string NormalFileName, string X2FileName)?> RequestRenameAsync(
        string imageDirectory, string currentNormalPath, string currentX2Path);
}
