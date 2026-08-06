using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace NeoEditor.Plugins.Paratranz.Conversion;

/// <summary>diff 行状态（D03 §6.2 diff 预览三态 + 未变化）。</summary>
public enum DiffKind
{
    /// <summary>新增译文（原无译文/原文新增）。</summary>
    Added,
    /// <summary>修改（原文与译文均有且不同）。</summary>
    Modified,
    /// <summary>跳过（无译文或无法定位实体）。</summary>
    Skipped,
    /// <summary>未变化（译文与当前值相同）。</summary>
    Unchanged,
}

/// <summary>diff 预览行：key + 原文 + 译文 + 状态。</summary>
public sealed record DiffRow(string Key, string Original, string Translation, DiffKind Kind);

/// <summary>
/// 译文 diff 预览的离线 HTML 渲染（D03 §6.2/§6.3 PT3）：
/// 供 NativeWebView.NavigateToString 直接显示——内嵌 CSS、无外部依赖、中文换行友好。
/// 纯字符串构建（StringBuilder），万行级性能由基准测试兜底。
/// </summary>
public static class DiffHtmlRenderer
{
    public static string Render(IReadOnlyList<DiffRow> rows)
    {
        var added = 0;
        var modified = 0;
        var skipped = 0;
        var unchanged = 0;
        foreach (var row in rows)
        {
            switch (row.Kind)
            {
                case DiffKind.Added: added++; break;
                case DiffKind.Modified: modified++; break;
                case DiffKind.Skipped: skipped++; break;
                case DiffKind.Unchanged: unchanged++; break;
            }
        }

        var sb = new StringBuilder(rows.Count * 160);
        sb.Append("<!DOCTYPE html><html lang=\"zh\"><head><meta charset=\"utf-8\"><style>")
          .Append("body{font-family:\"Microsoft YaHei\",\"Segoe UI\",sans-serif;margin:12px;color:#ddd;background:#1e1e1e;}")
          .Append("h2{font-size:15px;margin:4px 0 10px;}")
          .Append(".stats{display:flex;gap:12px;font-size:12px;margin-bottom:10px;}")
          .Append(".stats b{color:#fff;}")
          .Append(".added{color:#4ec9b0;}.modified{color:#dcdcaa;}.skipped{color:#9d9d9d;}.unchanged{color:#6a9955;}")
          .Append("table{border-collapse:collapse;width:100%;font-size:12px;}")
          .Append("td{border-top:1px solid #333;padding:4px 6px;vertical-align:top;word-break:break-word;white-space:pre-wrap;}")
          .Append(".key{color:#888;font-family:Consolas,monospace;font-size:11px;width:30%;}")
          .Append(".orig{color:#ccc;width:35%;}.trans{color:#4ec9b0;width:35%;}")
          .Append(".skipped .orig,.skipped .trans{color:#777;}")
          .Append("</style></head><body>");
        sb.Append("<h2>译文差异预览</h2><div class=\"stats\">")
          .Append("<span class=\"added\">新增 <b>").Append(added).Append("</b></span>")
          .Append("<span class=\"modified\">修改 <b>").Append(modified).Append("</b></span>")
          .Append("<span class=\"skipped\">跳过 <b>").Append(skipped).Append("</b></span>")
          .Append("<span class=\"unchanged\">未变化 <b>").Append(unchanged).Append("</b></span>")
          .Append("</div><table>");

        foreach (var row in rows)
        {
            sb.Append("<tr class=\"").Append(KindClass(row.Kind)).Append("\">")
              .Append("<td class=\"key\">").Append(Encode(row.Key)).Append("</td>")
              .Append("<td class=\"orig\">").Append(Encode(row.Original)).Append("</td>")
              .Append("<td class=\"trans\">").Append(Encode(row.Translation)).Append("</td>")
              .Append("</tr>");
        }

        sb.Append("</table></body></html>");
        return sb.ToString();
    }

    private static string KindClass(DiffKind kind) => kind switch
    {
        DiffKind.Added => "added",
        DiffKind.Modified => "modified",
        DiffKind.Skipped => "skipped",
        _ => "unchanged",
    };

    private static string Encode(string? text) => WebUtility.HtmlEncode(text ?? "");
}
