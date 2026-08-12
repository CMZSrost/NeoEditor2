using System.Collections.Generic;

namespace NeoEditor.Plugins.ImageTools.Services;

/// <summary>
/// Loads AI prompt presets from the user-editable <c>ai-prompt-presets.json</c>
/// (app working directory, next to config.json). Plugin-local interface — only the
/// AI generation panel consumes it (R17: no cross-plugin dependency).
/// </summary>
public interface IAiPromptPresetService
{
    /// <summary>All presets (name + prompt + optional size). Never throws — a missing
    /// or unparsable file falls back to the built-in defaults.</summary>
    IReadOnlyList<AiPromptPreset> GetPresets();

    /// <summary>Persist a new preset to the config file. A preset with the same name
    /// (ordinal) is replaced instead of duplicated. The in-memory list returned by
    /// <see cref="GetPresets"/> is refreshed. Never throws — a write failure keeps the
    /// updated list in memory for the session.</summary>
    AiPromptPreset AddOrUpdatePreset(AiPromptPreset preset);
}
