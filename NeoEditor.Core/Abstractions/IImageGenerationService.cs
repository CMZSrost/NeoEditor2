using System.Threading;
using System.Threading.Tasks;

namespace NeoEditor.Core.Abstractions;

/// <summary>
/// Generates pixel art images for game entities using an AI image API (DALL·E or compatible).
/// Defined in Core so that MCP Plugin and AiChat Plugin can call it via DI
/// without referencing ImageTools Plugin (R17 compliance).
/// </summary>
public interface IImageGenerationService
{
    /// <summary>
    /// Generate a pixel art image for the specified entity.
    /// </summary>
    /// <param name="entityType">Entity type name (e.g. "ItemType", "Creature").</param>
    /// <param name="entityId">Entity ID string (e.g. "item_weapon_sword").</param>
    /// <param name="options">Generation options. Uses defaults if null.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The generated image bytes (PNG) and metadata.</returns>
    Task<ImageGenerationResult> GenerateForEntityAsync(
        string entityType,
        string entityId,
        ImageGenerationOptions? options = null,
        CancellationToken ct = default);

    /// <summary>
    /// Generate a pixel art image directly from a text prompt (no entity involved).
    /// Used by the Image Editor workbench's AI panel.
    /// </summary>
    /// <param name="prompt">Free-form text prompt describing the image to generate.</param>
    /// <param name="options">Generation options. Uses defaults if null.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The generated image bytes (PNG) and metadata.</returns>
    Task<ImageGenerationResult> GenerateAsync(
        string prompt,
        ImageGenerationOptions? options = null,
        CancellationToken ct = default);

    /// <summary>
    /// Build a prompt string for the entity (for preview / debugging).
    /// </summary>
    string BuildPrompt(string entityType, string entityId);

    /// <summary>
    /// Returns true if the AI image API is configured (API key is set).
    /// </summary>
    bool IsAvailable { get; }
}

/// <summary>
/// Options controlling AI image generation behaviour.
/// </summary>
public sealed record ImageGenerationOptions(
    int Width = 64,
    int Height = 64,
    string Style = "pixel-art",
    string? RequestSize = null,
    bool ApplyPixelArt = true
);

/// <summary>
/// Result from a successful AI image generation.
/// </summary>
public sealed record ImageGenerationResult(
    byte[] ImageBytes,
    string Format,
    int Width,
    int Height,
    string? RevisedPrompt
);