using System.Threading.Tasks;

namespace NeoEditor.Player.Core.Services;

/// <summary>
/// Live data source for the preview reverse proxy (Docs/42 §3.6): serializes one game table
/// (e.g. "itemtypes") to pma_xml_export text from the editor's CURRENT state — DB baseline
/// plus the active profile's edit overlay (unsaved edits included).
/// </summary>
public interface IGameDataExportService
{
    /// <summary>Returns null when the table name is unknown to the editor.</summary>
    Task<string?> ExportTableXmlAsync(string tableName);
}
