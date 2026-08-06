using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using NeoEditor.Plugins.Paratranz.Conversion;
using Xunit;

namespace NeoEditor.Plugins.Paratranz.Tests;

public class DiffHtmlRendererTests
{
    [Fact]
    public void Render_统计条与行级三态高亮()
    {
        var rows = new[]
        {
            new DiffRow("k1", "Punch", "拳击", DiffKind.Added),
            new DiffRow("k2", "Kick", "踢击", DiffKind.Modified),
            new DiffRow("k3", "Old", "", DiffKind.Skipped),
            new DiffRow("k4", "Same", "Same", DiffKind.Unchanged),
        };

        var html = DiffHtmlRenderer.Render(rows);

        Assert.Contains("新增 <b>1</b>", html);
        Assert.Contains("修改 <b>1</b>", html);
        Assert.Contains("跳过 <b>1</b>", html);
        Assert.Contains("未变化 <b>1</b>", html);
        Assert.Contains("<tr class=\"added\">", html);
        Assert.Contains("<tr class=\"modified\">", html);
        Assert.Contains("<tr class=\"skipped\">", html);
        Assert.Contains("<tr class=\"unchanged\">", html);
        Assert.Contains("k1", html);
    }

    [Fact]
    public void Render_HTML特殊字符转义_中文与换行保留()
    {
        var rows = new[]
        {
            new DiffRow("k", "He said \"hi\" <tag> & more", "他说「嗨」\n第二行", DiffKind.Modified),
        };

        var html = DiffHtmlRenderer.Render(rows);

        Assert.DoesNotContain("<tag>", html);
        Assert.Contains("&lt;tag&gt;", html);
        Assert.Contains("他说「嗨」", html);
        Assert.Contains("&quot;hi&quot;", html);
    }

    [Fact]
    public void Render_空列表_输出完整页面骨架()
    {
        var html = DiffHtmlRenderer.Render([]);

        Assert.StartsWith("<!DOCTYPE html>", html);
        Assert.Contains("新增 <b>0</b>", html);
        Assert.Contains("</html>", html);
    }

    [Fact]
    public void Render_万行性能基准_生成时间与体积合理()
    {
        // PT3 spike: thousands of entries must render fast (NavigateToString latency budget)
        var rows = new List<DiffRow>(10000);
        for (var i = 0; i < 10000; i++)
        {
            rows.Add(new DiffRow(
                $"//table[@name=\"attackmodes\"]/column[@name=\"id\"][text()={i}]/../column[@name=\"strName\"]",
                $"Original text {i}",
                i % 3 == 0 ? $"译文 {i} 号" : "",
                i % 3 == 0 ? DiffKind.Added : DiffKind.Skipped));
        }

        var sw = Stopwatch.StartNew();
        var html = DiffHtmlRenderer.Render(rows);
        sw.Stop();

        Assert.True(sw.ElapsedMilliseconds < 2000, $"生成耗时 {sw.ElapsedMilliseconds}ms");
        Assert.True(html.Length > 10000 * 100, $"HTML 体积异常: {html.Length}");
        Assert.Equal(10000, CountOccurrences(html, "<tr class="));
    }

    private static int CountOccurrences(string text, string token)
    {
        var count = 0;
        var idx = 0;
        while ((idx = text.IndexOf(token, idx, StringComparison.Ordinal)) >= 0)
        {
            count++;
            idx += token.Length;
        }
        return count;
    }
}
