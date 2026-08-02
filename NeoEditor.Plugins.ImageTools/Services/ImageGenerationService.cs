using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using NeoEditor.Core.Abstractions;
using NeoEditor.Core.Model;
using NeoEditor.Data;
using NeoEditor.Data.Model.Game;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;

namespace NeoEditor.Plugins.ImageTools.Services;

/// <summary>
/// AI image generation service using OpenAI-compatible Images API.
/// Reads API key, endpoint, and image model from IConfigService (R28) with
/// environment variables as fallback (same configuration as AiChat Plugin).
/// Applies pixel art post-processing via <see cref="PixelArtConversionService"/> after generation.
/// Implements <see cref="IImageGenerationService"/> (Core interface, R17-compliant).
/// </summary>
public sealed class ImageGenerationService : IImageGenerationService
{
    private readonly IHostService _hostService;
    private readonly EntityToPromptConverter _promptConverter;
    private readonly PixelArtConversionService _pixelArtService;
    private readonly HttpClient _httpClient;
    private readonly bool _isConfigured;
    private readonly string _imageModelId;
    private readonly string _endpoint;

    public ImageGenerationService(
        IHostService hostService,
        EntityToPromptConverter promptConverter,
        PixelArtConversionService pixelArtService,
        IConfigService configService)
    {
        _hostService = hostService;
        _promptConverter = promptConverter;
        _pixelArtService = pixelArtService;

        // Config source of truth (R28 + provider list): the image model's provider is resolved
        // from the provider list (ImageProviderId), with environment variables as fallback.
        var cfg = configService.Config;
        var provider = AiProviderResolver.Resolve(
            cfg, cfg.ImageProviderId,
            Environment.GetEnvironmentVariable("OPENAI_ENDPOINT"),
            Environment.GetEnvironmentVariable("OPENAI_API_KEY"));
        _isConfigured = provider is not null;

        // Image model — config first (default: dall-e-3), then OPENAI_IMAGE_MODEL.
        // For local models via Ollama/LM Studio, set the endpoint and model accordingly.
        _imageModelId = AiProviderResolver.ResolveModelName(
            cfg.ImageModel,
            Environment.GetEnvironmentVariable("OPENAI_IMAGE_MODEL"),
            "dall-e-3");

        _endpoint = provider?.Endpoint ?? "https://api.openai.com/v1";

        _httpClient = new HttpClient();
        if (provider is not null)
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {provider.ApiKey}");
    }

    public bool IsAvailable => _isConfigured;

    /// <summary>
    /// Build a prompt for preview/debugging. Uses synchronous entity lookup —
    /// for production use prefer <see cref="GenerateForEntityAsync"/>.
    /// </summary>
    public string BuildPrompt(string entityType, string entityId)
    {
        // Use Task.Run to avoid deadlock on sync-over-async; this is a debug path.
        var entity = Task.Run(() => GetEntityByTypeAsync(entityType, entityId)).GetAwaiter().GetResult();
        if (entity is null)
            return $"Error: Entity not found: {entityType}/{entityId}";

        var options = new ImageGenerationOptions();
        return _promptConverter.BuildPrompt(entity, options);
    }

    public async Task<ImageGenerationResult> GenerateForEntityAsync(
        string entityType,
        string entityId,
        ImageGenerationOptions? options = null,
        CancellationToken ct = default)
    {
        options ??= new ImageGenerationOptions();

        if (!_isConfigured)
            throw new InvalidOperationException(
                "Image generation is not available. Set OPENAI_API_KEY environment variable.");

        var entity = await GetEntityByTypeAsync(entityType, entityId);
        if (entity is null)
            throw new ArgumentException($"Entity not found: {entityType}/{entityId}");

        var prompt = _promptConverter.BuildPrompt(entity, options);
        return await GenerateCoreAsync(prompt, options, ct);
    }

    /// <summary>
    /// Generate directly from a free-form prompt (the Image Editor workbench's AI panel).
    /// </summary>
    public async Task<ImageGenerationResult> GenerateAsync(
        string prompt,
        ImageGenerationOptions? options = null,
        CancellationToken ct = default)
    {
        options ??= new ImageGenerationOptions();

        if (!_isConfigured)
            throw new InvalidOperationException(
                "Image generation is not available. Set OPENAI_API_KEY environment variable.");

        if (string.IsNullOrWhiteSpace(prompt))
            throw new ArgumentException("Prompt is empty.", nameof(prompt));

        return await GenerateCoreAsync(prompt, options, ct);
    }

    /// <summary>
    /// Shared generation pipeline: call the OpenAI-compatible Images API with the prompt,
    /// then apply pixel art post-processing (G1 + G2 integration) and return PNG bytes.
    /// </summary>
    private async Task<ImageGenerationResult> GenerateCoreAsync(
        string prompt, ImageGenerationOptions options, CancellationToken ct)
    {
        // ── Step 1: Call OpenAI-compatible Images API ──
        var url = $"{_endpoint.TrimEnd('/')}/images/generations";

        // An explicit RequestSize (set by the workbench's free-size input) wins. Otherwise
        // fall back to a size that satisfies both provider families: Zhipu CogView requires
        // side length in [512, 2880] and a multiple of 16; OpenAI dall-e accepts 256/512/1024.
        // 512x512 and 1024x1024 satisfy both, and 512 is the smallest universal size.
        var requestSize = !string.IsNullOrWhiteSpace(options.RequestSize)
            ? options.RequestSize
            : options.Width <= 512 && options.Height <= 512
                ? "512x512"
                : "1024x1024";

        var requestBody = new
        {
            model = _imageModelId,
            prompt,
            n = 1,
            size = requestSize,
            quality = "standard",
            response_format = "b64_json"
        };

        var response = await _httpClient.PostAsJsonAsync(url, requestBody, ct);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        var root = doc.RootElement;

        // Parse the response: OpenAI returns {"data": [{"b64_json": "..."}]}; Zhipu CogView
        // ignores response_format and returns {"data": [{"url": "..."}]} instead — so we
        // accept both: prefer b64_json, otherwise download the image URL.
        var data = root.GetProperty("data")[0];
        byte[] rawBytes;
        if (data.TryGetProperty("b64_json", out var b64JsonProp) &&
            b64JsonProp.ValueKind == JsonValueKind.String &&
            !string.IsNullOrWhiteSpace(b64JsonProp.GetString()))
        {
            var b64Json = b64JsonProp.GetString();
            rawBytes = Convert.FromBase64String(b64Json!);
        }
        else if (data.TryGetProperty("url", out var urlProp) &&
                 urlProp.ValueKind == JsonValueKind.String &&
                 !string.IsNullOrWhiteSpace(urlProp.GetString()))
        {
            var imageUrl = urlProp.GetString()!;
            // Download with a fresh client: the image URL is a public temp link, and sending
            // the Bearer auth header meant for the API is unnecessary (and may be rejected).
            using var downloadClient = new HttpClient();
            rawBytes = await downloadClient.GetByteArrayAsync(imageUrl, ct);
        }
        else
        {
            throw new InvalidOperationException(
                $"Image API response did not contain 'b64_json' or 'url'. Body: {content}");
        }

        var revisedPrompt = data.TryGetProperty("revised_prompt", out var rp)
            ? rp.GetString()
            : null;

        // ── Step 2: Optional pixel art post-processing (G1 + G2 integration) ──
        // The workbench passes ApplyPixelArt:false so a realistic render (e.g. CogView) is
        // shown as-is; the user pixelates explicitly afterwards. Other callers (MCP / entity
        // generation) keep the default true and get the pixel-art style directly.
        if (!options.ApplyPixelArt)
        {
            // Providers return JPEG (CogView) or PNG bytes. Normalise to a single PNG so the
            // consumer (Avalonia Bitmap) never mis-detects the format from a temp file's
            // extension — a JPEG body with a .png name garbles the render.
            using var src = Image.Load<Rgba32>(rawBytes);
            using var pngMs = new MemoryStream();
            await src.SaveAsync(pngMs, new PngEncoder(), ct);
            return new ImageGenerationResult(pngMs.ToArray(), "png", options.Width, options.Height, revisedPrompt);
        }

        using var sourceImage = Image.Load<Rgba32>(rawBytes);
        var pixelOptions = new PixelArtConversionOptions(
            TargetWidth: options.Width,
            TargetHeight: options.Height,
            ColorCount: options.Width <= 64 ? 16 : 24,
            EdgeEnhancement: true,
            Dithering: false,
            TransparentBackground: true);

        using var pixelArtImage = await _pixelArtService.ConvertToPixelArtAsync(
            sourceImage, pixelOptions, ct);

        // Encode to PNG bytes
        using var ms = new MemoryStream();
        await pixelArtImage.SaveAsync(ms, new PngEncoder(), ct);
        var finalBytes = ms.ToArray();

        return new ImageGenerationResult(
            finalBytes, "png", options.Width, options.Height, revisedPrompt);
    }

    /// <summary>
    /// Fetch an entity by type name and ID using reflection (same pattern as EditorTools.GetEntityByTypeAsync).
    /// </summary>
    private async Task<IEntity?> GetEntityByTypeAsync(string entityType, string entityId)
    {
        var type = ResolveEntityType(entityType);
        if (type is null) return null;

        // _hostService.Repository<T>().GetByIdAsync(id) via reflection
        var repoMethod = typeof(IHostService)
            .GetMethod(nameof(IHostService.Repository))
            ?.MakeGenericMethod(type);
        if (repoMethod is null) return null;

        var repo = repoMethod.Invoke(_hostService, null);
        if (repo is null) return null;

        var getByIdMethod = repo.GetType().GetMethod("GetByIdAsync");
        if (getByIdMethod is null) return null;

        var task = (Task)getByIdMethod.Invoke(repo, [entityId])!;
        await task.ConfigureAwait(false);

        var resultProp = task.GetType().GetProperty("Result");
        return resultProp?.GetValue(task) as IEntity;
    }

    private static Type? ResolveEntityType(string entityType)
    {
        // Try exact match first
        var allTypes = Constants.GameTypes.Values;
        foreach (var t in allTypes)
        {
            if (string.Equals(t.Name, entityType, StringComparison.OrdinalIgnoreCase))
                return t;
        }

        return null;
    }
}