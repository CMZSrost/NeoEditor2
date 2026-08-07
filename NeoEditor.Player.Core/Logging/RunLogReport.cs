using System.Collections.Generic;
using System.Text;

namespace NeoEditor.Player.Core.Logging;

/// <summary>
/// Plain-text export report builder (R38): header + localStorage snapshot + every captured
/// run's log lines. Pure function — the view model supplies header/dump and writes the file.
/// </summary>
public static class RunLogReport
{
    /// <summary>
    /// Build the report text. <paramref name="header"/> is caller-provided multi-line info
    /// (export time, game root, log dir...); <paramref name="localStorageDump"/> is the raw
    /// JSON from <c>window.__dumpLocalStorage()</c> (or null when the webview is unavailable).
    /// </summary>
    public static string Build(string header, IEnumerable<PlayerRunRecord> runs, string? localStorageDump)
    {
        var sb = new StringBuilder();
        sb.AppendLine("NeoEditor.Player 运行日志导出");
        sb.AppendLine(header);
        sb.AppendLine();
        sb.AppendLine("== localStorage 快照 ==");
        sb.AppendLine(string.IsNullOrWhiteSpace(localStorageDump)
            ? "(webview 未加载或无数据)"
            : localStorageDump);
        sb.AppendLine();
        sb.AppendLine("== 运行日志 ==");
        foreach (var run in runs)
        {
            sb.Append('[').Append(run.RunId).Append(" · ").Append(run.StartedAt.ToString("yyyy-MM-dd HH:mm:ss")).AppendLine("]");
            foreach (var line in run.Lines)
                sb.Append(line.Timestamp.ToString("HH:mm:ss.fff"))
                    .Append(" [").Append(line.Level).Append("] ")
                    .AppendLine(line.Message);
            sb.AppendLine();
        }
        return sb.ToString();
    }
}
