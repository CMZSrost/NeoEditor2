using System;
using NeoEditor.Player.Core.Logging;
using Xunit;

namespace NeoEditor.Player.Core.Tests;

public class RunLogReportTests
{
    [Fact]
    public void BuildIncludesHeaderDumpAndAllRuns()
    {
        var store = new RunLogStore();
        store.Append("run-1", "info", "NE v2.53 展开器就绪");
        store.Append("run-1", "clipboard", "游戏剪贴板日志(截获): hello");
        store.Append("run-2", "error", "window.onerror: boom");

        var report = RunLogReport.Build("导出时间: 2026-08-06 12:00:00", store.Runs, "[]");

        Assert.Contains("导出时间: 2026-08-06 12:00:00", report);
        Assert.Contains("== localStorage 快照 ==", report);
        Assert.Contains("[]", report);
        Assert.Contains("[run-1", report);
        Assert.Contains("[run-2", report);
        Assert.Contains("NE v2.53 展开器就绪", report);
        Assert.Contains("游戏剪贴板日志(截获): hello", report);
        Assert.Contains("window.onerror: boom", report);
        // Run sections in chronological order
        Assert.True(report.IndexOf("[run-1", StringComparison.Ordinal)
                    < report.IndexOf("[run-2", StringComparison.Ordinal));
    }

    [Fact]
    public void BuildHandlesNullDumpAndEmptyRuns()
    {
        var report = RunLogReport.Build("header", [], null);

        Assert.Contains("header", report);
        Assert.Contains("== 运行日志 ==", report);
        // localStorage section falls back to a hint instead of "null"
        Assert.Contains("webview 未加载", report);
        Assert.DoesNotContain("null", report);
    }
}
