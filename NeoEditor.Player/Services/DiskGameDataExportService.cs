using System.Threading.Tasks;
using NeoEditor.Player.Core.Services;

namespace NeoEditor.Player.Services;

/// <summary>
/// Disk-mode data export: the standalone player has no editor session, so the reverse
/// proxy never produces live data — combined with ProxyEnabled=false, every game request
/// falls through to the disk file (Docs/42 §3.8).
/// </summary>
public sealed class DiskGameDataExportService : IGameDataExportService
{
    public Task<string?> ExportTableXmlAsync(string tableName) => Task.FromResult<string?>(null);
}
