using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

namespace NeoEditor.Plugins.ImageTools.Services;

/// <summary>
/// Loads AI prompt presets from <c>ai-prompt-presets.json</c> in the app working
/// directory (same location as config.json). The file is user-editable: entries can
/// be added, removed or reworded freely. When the file is missing it is auto-created
/// with the built-in defaults; when it fails to parse the defaults are used instead —
/// this service never throws (the AI panel must not be blocked by a bad JSON file).
/// </summary>
public class AiPromptPresetService : IAiPromptPresetService
{
    /// <summary>Default preset file name (relative to the app working directory).</summary>
    public const string DefaultFileName = "ai-prompt-presets.json";

    /// <summary>Comment header written on top of every generated file.</summary>
    private const string FileHeaderComment =
        "NeoEditor AI 生成预设模板。每条含 name（下拉显示名）/ prompt（提示词）/"
        + " width、height（可选，选中时同步生成尺寸）。可自由增删改本条文件；删除后会自动重建默认模板。";

    private readonly string _filePath;
    private IReadOnlyList<AiPromptPreset>? _presets;

    public AiPromptPresetService(string? filePath = null)
    {
        _filePath = filePath ?? DefaultFileName;
    }

    /// <inheritdoc />
    public IReadOnlyList<AiPromptPreset> GetPresets()
    {
        return _presets ??= LoadPresets();
    }

    /// <inheritdoc />
    public AiPromptPreset AddOrUpdatePreset(AiPromptPreset preset)
    {
        // Re-read from disk so presets the user added by hand (or another panel) while
        // this session ran are not lost — then append/replace by name and write back.
        var current = LoadPresets().ToList();
        var index = current.FindIndex(p => string.Equals(p.Name, preset.Name, StringComparison.Ordinal));
        if (index >= 0)
        {
            current[index] = preset;
        }
        else
        {
            current.Add(preset);
        }

        WritePresets(current);
        _presets = current;
        return preset;
    }

    /// <summary>
    /// Built-in presets derived from the NeoScavenger texture pack (img/): weapons are
    /// photographed straight from the side on a pure black background, creatures use a
    /// 3/4 side view, worn clothing is a headless frontal torso. These match what the
    /// pixelation pipeline produces best after downsampling.
    /// </summary>
    public static IReadOnlyList<AiPromptPreset> DefaultPresets { get; } = new List<AiPromptPreset>
    {
        new("武器·正侧视", "A realistic photograph of a weapon with a weathered metal and wood body, "
            + "shot from the exact side (profile view), muzzle pointing RIGHT, lying perfectly horizontal, "
            + "no perspective, no tilt, full length visible filling 90% of the frame, "
            + "on a solid pure black background, even flat catalog lighting with a subtle top highlight "
            + "along the upper edge, no cast shadow, no hands, no text, no watermark",
            Width: 1024, Height: 512),
        new("生物·3/4侧视", "A realistic photograph of a creature, 3/4 side view angled slightly toward "
            + "the viewer, facing left-forward, standing in a calm alert pose, full body visible head to tail, "
            + "eye-level camera, no perspective distortion, centered, occupying 75% of the frame, "
            + "no ground shadow, solid pure black background, flat even daylight, no motion blur, "
            + "no text, no watermark"),
        new("服装·无头躯干", "A realistic photograph of a garment worn on a headless mannequin torso, "
            + "frontal view, perfectly symmetrical, centered, occupying 80% of the frame, "
            + "cut off at neck and waist, solid pure black background, even baked lighting "
            + "with highlight on chest and shoulders, no text, no watermark"),
        new("动作条·宽幅横条", "A realistic photograph of a single object, lying horizontally, "
            + "exact side profile view, filling the entire frame width, on a solid pure black background, "
            + "flat studio lighting with a subtle top highlight, no cast shadow, no hands, "
            + "no text, no watermark",
            Width: 1024, Height: 512),
        new("通用·单物体", "A realistic photograph of a single object, centered, occupying 80% of the frame, "
            + "on a solid pure black background, even flat studio lighting, no cast shadow, "
            + "no perspective distortion, no text, no watermark"),
    };

    private IReadOnlyList<AiPromptPreset> LoadPresets()
    {
        if (!File.Exists(_filePath))
        {
            TryWriteDefaults();
            return DefaultPresets;
        }

        try
        {
            var json = File.ReadAllText(_filePath);
            var parsed = JsonConvert.DeserializeObject<List<AiPromptPreset>>(json);
            var valid = (parsed ?? new List<AiPromptPreset>())
                .Where(p => !string.IsNullOrWhiteSpace(p.Name) && !string.IsNullOrWhiteSpace(p.Prompt))
                .ToList();
            return valid.Count > 0 ? valid : DefaultPresets;
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            // Corrupt user file — fall back to built-ins instead of blocking the panel.
            return DefaultPresets;
        }
    }

    /// <summary>Write the built-in defaults to disk (first run) so the user has a file
    /// to customize.</summary>
    private void TryWriteDefaults()
    {
        WritePresets(DefaultPresets);
    }

    /// <summary>Persist a preset list to the config file. A comment header explains the
    /// format. Write failures are swallowed — the in-memory list stays authoritative.</summary>
    private void WritePresets(IReadOnlyList<AiPromptPreset> presets)
    {
        try
        {
            using var sw = new StreamWriter(_filePath, false, Encoding.UTF8);
            using var writer = new JsonTextWriter(sw) { Formatting = Formatting.Indented };
            writer.WriteComment(FileHeaderComment);
            writer.WriteStartArray();
            foreach (var preset in presets)
            {
                writer.WriteStartObject();
                writer.WritePropertyName("name");
                writer.WriteValue(preset.Name);
                writer.WritePropertyName("prompt");
                writer.WriteValue(preset.Prompt);
                if (preset.Width is { } w)
                {
                    writer.WritePropertyName("width");
                    writer.WriteValue(w);
                }
                if (preset.Height is { } h)
                {
                    writer.WritePropertyName("height");
                    writer.WriteValue(h);
                }
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
        }
        catch (IOException)
        {
            // Read-only dir etc. — the in-memory list still serves the panel.
        }
    }
}
