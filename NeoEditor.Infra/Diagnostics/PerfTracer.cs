using System;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace NeoEditor.Diagnostics;

/// <summary>
/// 轻量性能埋点工具：以「流程(flow) + 阶段(stage)」为单位用 Stopwatch 记录累计耗时，
/// 输出到 Serilog（[Perf] 前缀，便于 grep 定位启动/加载流程的慢环节）。纯日志、无行为变更。
/// 用法：
///   PerfTracer.Start("profile-open");
///   ...
///   PerfTracer.Checkpoint("profile-open", "PreLoad");          // 输出累计 ms (+本段增量)
///   using (PerfTracer.Scope("profile-open", "ComputeMerge"))   // Dispose 时输出该段自身耗时
///   { ... }
///   PerfTracer.End("profile-open");                            // 输出总耗时并清除
/// Scope 自带独立秒表，不要求流程已 Start，可单独使用（如 ModManager 冷导入明细）。
/// </summary>
public static class PerfTracer
{
    private static readonly ConcurrentDictionary<string, FlowState> Flows = new();
    private static readonly Serilog.ILogger Log = Serilog.Log.ForContext("SourceContext", "PerfTracer");

    /// <summary>开启（或重置）一条流程的计时。</summary>
    public static void Start(string flow)
    {
        Flows[flow] = new FlowState { Sw = Stopwatch.StartNew() };
    }

    /// <summary>记录流程累计耗时（ms=从 Start 起累计，delta=距上一次 Checkpoint 的增量）。
    /// 流程未 Start 时静默忽略。</summary>
    public static void Checkpoint(string flow, string stage)
    {
        if (!Flows.TryGetValue(flow, out var state)) return;
        var ms = state.Sw.ElapsedMilliseconds;
        var delta = ms - state.LastMs;
        state.LastMs = ms;
        Log.Information("[Perf] flow={Flow} stage={Stage} ms={Ms} (+{Delta})", flow, stage, ms, delta);
    }

    /// <summary>该流程是否已在计时（未 Start 返回 false）。</summary>
    public static bool IsActive(string flow) => Flows.ContainsKey(flow);

    /// <summary>输出流程总耗时并清除该流程。</summary>
    public static void End(string flow)
    {
        if (!Flows.TryRemove(flow, out var state)) return;
        state.Sw.Stop();
        Log.Information("[Perf] flow={Flow} END total={Ms} ms", flow, state.Sw.ElapsedMilliseconds);
    }

    /// <summary>返回一个作用域，Dispose 时输出该段自身耗时（不依赖 Start）。</summary>
    public static IDisposable Scope(string flow, string stage) => new PerfScope(flow, stage);

    private sealed class FlowState
    {
        public Stopwatch Sw = null!;
        public long LastMs;
    }

    private sealed class PerfScope : IDisposable
    {
        private readonly string _flow;
        private readonly string _stage;
        private readonly Stopwatch _sw = Stopwatch.StartNew();
        private bool _disposed;

        public PerfScope(string flow, string stage)
        {
            _flow = flow;
            _stage = stage;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _sw.Stop();
            Log.Information("[Perf] flow={Flow} stage={Stage} ms={Ms}", _flow, _stage, _sw.ElapsedMilliseconds);
        }
    }
}
