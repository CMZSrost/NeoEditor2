namespace NeoEditor.Plugins.Paratranz.Models;

/// <summary>
/// 翻译单元——编辑器数据与 ParaTranz 词条之间的最小转换载体。
/// Key 为 xpath 定位串（D03 §3.2，与 NeoParatranz / 项目 15258 兼容）：
/// <c>//table[@name="T"]/column[@name="K"][text()=id]/../column[@name="C"]</c>。
/// </summary>
public sealed record TranslationUnit(
    string Key,
    string Original,
    string? Translation = null,
    string? Context = null)
{
    /// <summary>是否有可用译文（非空）。</summary>
    public bool HasTranslation => !string.IsNullOrEmpty(Translation);
}
