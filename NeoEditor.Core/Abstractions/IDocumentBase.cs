namespace NeoEditor.Core.Abstractions;

/// <summary>
/// Minimal interface for dock documents. EntityEditorPlugin's PluginDocumentBase
/// and NeoEditor.App's DocumentBase both implement this.
/// Extracted from App to Core during M10 Phase 5 migration so Plugin can use it
/// without referencing App.
/// </summary>
public interface IDocumentBase
{
    string Title { get; set; }
    bool CanClose { get; set; }
    bool NeedNotifyWhenClose { get; set; }
    void SetStaticTitle(string title);
    void SetLocalizedTitle(string key, params object[] args);
    void RefreshLocalizedText();
}
