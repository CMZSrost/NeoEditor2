using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Messaging;
using NeoEditor.Core.Abstractions;
using NeoEditor.Data.Messages;
using NeoEditor.Data.Model.Game;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace NeoEditor.Plugins.ImageTools.Services;

/// <summary>
/// Provides the "Generate Image" context action for entities in the EntityEditor.
/// Implements <see cref="IEntityContextActionProvider"/> (Core interface, R17-compliant).
/// </summary>
public sealed class EntityImageGenActionProvider : IEntityContextActionProvider
{
    private readonly IImageGenerationService _imageGenService;
    private readonly IHostService _hostService;

    /// <summary>
    /// Entity types that are visually representable (can generate images for them).
    /// </summary>
    private static readonly HashSet<string> VisualTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "ItemType", "Creature", "Recipe", "Encounter", "Condition",
        "AttackMode", "BattleMove", "CampType", "ContainerType", "Faction",
        "TreasureTable", "Map", "ChargeProfile", "BarterHex", "DmcPlace"
    };

    public EntityImageGenActionProvider(IImageGenerationService imageGenService, IHostService hostService)
    {
        _imageGenService = imageGenService;
        _hostService = hostService;
    }

    public string ActionLabel => "Generate Image";

    public bool CanHandle(string entityType)
    {
        return VisualTypes.Contains(entityType) && _imageGenService.IsAvailable;
    }

    public async Task<string> ExecuteAsync(string entityType, string entityId, CancellationToken ct = default)
    {
        try
        {
            var options = new ImageGenerationOptions(64, 64, "pixel-art");
            var result = await _imageGenService.GenerateForEntityAsync(entityType, entityId, options, ct);

            // Determine output path based on entity's FilePath
            var outputDir = DetermineOutputDirectory(entityType, entityId);
            var normalPath = Path.Combine(outputDir, $"{entityId}.png");
            var x2Path = Path.Combine(outputDir, $"x2_{entityId}.png");

            // Save images
            Directory.CreateDirectory(outputDir);
            await File.WriteAllBytesAsync(normalPath, result.ImageBytes, ct);

            // Generate and save x2 version (simple 2x upscale)
            SaveX2Version(result.ImageBytes, x2Path);

            // Publish message so the App shell can open the image
            WeakReferenceMessenger.Default.Send(
                new ImageGeneratedMessage(entityType, entityId, normalPath, x2Path));

            return $"Image generated and saved to {normalPath}";
        }
        catch (Exception ex)
        {
            return $"Image generation failed: {ex.Message}";
        }
    }

    private string DetermineOutputDirectory(string entityType, string entityId)
    {
        // Try to use the entity's file path as a hint for the output directory
        try
        {
            var entity = GetEntityByType(entityType, entityId);
            if (entity is not null && !string.IsNullOrWhiteSpace(entity.FilePath))
            {
                var dir = Path.GetDirectoryName(entity.FilePath);
                if (!string.IsNullOrWhiteSpace(dir))
                {
                    var imgDir = Path.Combine(dir, "img");
                    return imgDir;
                }
            }
        }
        catch
        {
            // Fall through to temp directory
        }

        return Path.Combine(Path.GetTempPath(), "NeoEditor", "GeneratedImages", entityType);
    }

    private static void SaveX2Version(byte[] imageBytes, string outputPath)
    {
        try
        {
            using var source = Image.Load<Rgba32>(imageBytes);
            using var x2 = source.Clone(ctx =>
            {
                ctx.Resize(new ResizeOptions
                {
                    Size = new Size(source.Width * 2, source.Height * 2),
                    Mode = ResizeMode.Stretch,
                    Sampler = KnownResamplers.NearestNeighbor,
                });
            });
            x2.SaveAsPng(outputPath);
        }
        catch
        {
            // x2 version is optional — don't fail the whole operation
        }
    }

    private IEntity? GetEntityByType(string entityType, string entityId)
    {
        try
        {
            var type = NeoEditor.Data.Constants.GameTypes.Values
                .FirstOrDefault(t => string.Equals(t.Name, entityType, StringComparison.OrdinalIgnoreCase));
            if (type is null) return null;

            var repoMethod = typeof(IHostService)
                .GetMethod(nameof(IHostService.Repository))
                ?.MakeGenericMethod(type);
            if (repoMethod is null) return null;

            var repo = repoMethod.Invoke(_hostService, null);
            if (repo is null) return null;

            var getByIdMethod = repo.GetType().GetMethod("GetByIdAsync");
            if (getByIdMethod is null) return null;

            var task = (Task)getByIdMethod.Invoke(repo, [entityId])!;
            task.Wait();
            var resultProp = task.GetType().GetProperty("Result");
            return resultProp?.GetValue(task) as IEntity;
        }
        catch
        {
            return null;
        }
    }
}
