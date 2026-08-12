namespace NeoEditor.Plugins.ImageTools.Services;

/// <summary>
/// One user-editable AI prompt template shown in the AI generation panel dropdown.
/// <see cref="Width"/> / <see cref="Height"/> are optional — when present, selecting
/// the preset also syncs the generation size.
/// </summary>
public sealed record AiPromptPreset(
    string Name,
    string Prompt,
    int? Width = null,
    int? Height = null);
