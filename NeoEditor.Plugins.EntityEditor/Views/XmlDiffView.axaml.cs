using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using DiffPlex.DiffBuilder;
using DiffPlex.DiffBuilder.Model;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NeoEditor.Infra.Services;
using NeoEditor.Plugins.EntityEditor.Helper;

namespace NeoEditor.Plugins.EntityEditor.Views;

public partial class XmlDiffView : UserControl
{
    private const double PreviewMinBlockHeight = 2d;
    private const double ViewportMinHeight = 12d;
    private const int DiffRefreshDebounceMilliseconds = 90;

    private readonly record struct DiffBlock(
        int OldStartLineNumber,
        int OldEndLineNumber,
        int NewStartLineNumber,
        int NewEndLineNumber);

    #region 依赖属性

    public static readonly StyledProperty<TextDocument?> NewXmlProperty =
        AvaloniaProperty.Register<XmlDiffView, TextDocument?>("NewXml");

    public static readonly StyledProperty<TextDocument?> OldXmlProperty =
        AvaloniaProperty.Register<XmlDiffView, TextDocument?>("OldXml");

    public static readonly StyledProperty<string?> DocSyntaxProperty =
        AvaloniaProperty.Register<XmlDiffView, string?>("DocSyntax");

    public static readonly StyledProperty<bool> ReadOnlyProperty =
        AvaloniaProperty.Register<XmlDiffView, bool>("ReadOnly");

    public static readonly StyledProperty<bool> SyncScrollingProperty =
        AvaloniaProperty.Register<XmlDiffView, bool>("SyncScrolling", true);

    public TextDocument? OldXml
    {
        get { return GetValue(OldXmlProperty); }
        set { SetValue(OldXmlProperty, value); }
    }

    public TextDocument? NewXml
    {
        get { return GetValue(NewXmlProperty); }
        set { SetValue(NewXmlProperty, value); }
    }

    public string? DocSyntax
    {
        get { return GetValue(DocSyntaxProperty); }
        set { SetValue(DocSyntaxProperty, value); }
    }

    public bool ReadOnly
    {
        get { return GetValue(ReadOnlyProperty); }
        set { SetValue(ReadOnlyProperty, value); }
    }

    public bool SyncScrolling
    {
        get { return GetValue(SyncScrollingProperty); }
        set { SetValue(SyncScrollingProperty, value); }
    }

    #endregion

    private readonly List<DiffBlock> _diffBlocks = [];
    private int _currentDiffIndex = -1;
    private SideBySideDiffModel? _diffResult;
    private HighlightBackgroundRenderHelper.HighlightData? _oldHighlightData;
    private HighlightBackgroundRenderHelper.HighlightData? _newHighlightData;
    private IReadOnlyList<DiffPreviewMarkerRun> _oldPreviewMarkerRuns = Array.Empty<DiffPreviewMarkerRun>();
    private IReadOnlyList<DiffPreviewMarkerRun> _newPreviewMarkerRuns = Array.Empty<DiffPreviewMarkerRun>();
    private CancellationTokenSource? _refreshHighlightingCancellation;
    private int _refreshHighlightingVersion;

    private TextEditor OldEditorControl => this.FindControl<TextEditor>("OldEditor")!;
    private TextEditor NewEditorControl => this.FindControl<TextEditor>("NewEditor")!;

    private ILocalizationService Loc => _loc ??= GetService<ILocalizationService>();
    private ILocalizationService? _loc;
    private ILogger<XmlDiffView> Logger => _logger ??= GetService<ILoggerFactory>().CreateLogger<XmlDiffView>();
    private ILogger<XmlDiffView>? _logger;

    private static T GetService<T>() where T : notnull
        => (Application.Current?.Resources["Services"] as IServiceProvider)!.GetRequiredService<T>();

    public static readonly StyledProperty<string> CurrentDiffDisplayIndexProperty =
        AvaloniaProperty.Register<XmlDiffView, string>(nameof(CurrentDiffDisplayIndex), "");

    public string CurrentDiffDisplayIndex
    {
        get => GetValue(CurrentDiffDisplayIndexProperty);
        set => SetValue(CurrentDiffDisplayIndexProperty, value);
    }

    public XmlDiffView()
    {
        InitializeComponent();
        AttachedToVisualTree += OnAttachedToVisualTree;
        // 追修: 阻止 AvaloniaEdit 的 Ctrl+滚轮 / 触控板捏合 文本缩放（Tunnel 早于
        // TextEditor class handler，Handled 后缩放不执行；普通滚动不受影响）。
        AddHandler(InputElement.PointerWheelChangedEvent, (_, e) =>
        {
            if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
                e.Handled = true;
        }, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(InputElement.PointerTouchPadGestureMagnifyEvent, (_, e) => e.Handled = true,
            RoutingStrategies.Tunnel, handledEventsToo: true);
        OldEditorControl.TextChanged += OnEditorTextChanged;
        NewEditorControl.TextChanged += OnEditorTextChanged;
        OldEditorControl.SizeChanged += OnEditorSizeChanged;
        NewEditorControl.SizeChanged += OnEditorSizeChanged;
        OldPreviewTrack.SizeChanged += OnPreviewCanvasSizeChanged;
        NewPreviewTrack.SizeChanged += OnPreviewCanvasSizeChanged;
        UpdateDiffNavigationUi();
        QueueRefreshHighlighting();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == OldXmlProperty || change.Property == NewXmlProperty)
        {
            QueueRefreshHighlighting();
        }
    }

    private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            EnsureOverviewBehaviors();
        }, DispatcherPriority.Loaded);
    }

    private void OnEditorTextChanged(object? sender, EventArgs e)
    {
        QueueRefreshHighlighting();
    }

    private void OnPreviewCanvasSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        EnsureOverviewBehaviors();
        RenderPreviewSidebars();
    }

    private void OnEditorSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        EnsureOverviewBehaviors();
    }

    private void OnPreviousDiffClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_diffBlocks.Count == 0)
        {
            return;
        }

        if (_currentDiffIndex < 0)
        {
            _currentDiffIndex = 0;
        }
        else if (_currentDiffIndex > 0)
        {
            _currentDiffIndex--;
        }

        NavigateToCurrentDiff();
    }

    private void OnNextDiffClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_diffBlocks.Count == 0)
        {
            return;
        }

        if (_currentDiffIndex < _diffBlocks.Count - 1)
        {
            _currentDiffIndex++;
        }

        NavigateToCurrentDiff();
    }

    private void OnJumpFromCursorClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_diffBlocks.Count == 0) return;

        var editor = NewEditorControl.IsFocused ? NewEditorControl : OldEditorControl;
        var isNewText = ReferenceEquals(editor, NewEditorControl);
        var document = editor.Document;
        if (document is null) return;

        var caretLine = document.GetLineByOffset(editor.CaretOffset)?.LineNumber ?? 1;

        var foundIndex = -1;
        for (var i = 0; i < _diffBlocks.Count; i++)
        {
            var blockStart = isNewText
                ? _diffBlocks[i].NewStartLineNumber
                : _diffBlocks[i].OldStartLineNumber;
            if (blockStart > caretLine)
            {
                foundIndex = i;
                break;
            }
        }

        if (foundIndex < 0) foundIndex = 0;

        _currentDiffIndex = foundIndex;
        NavigateToCurrentDiff();
    }

    private void OnDiffIndexInputLostFocus(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        ApplyDiffIndexInput();
    }

    private void OnDiffIndexInputKeyDown(object? sender, Avalonia.Input.KeyEventArgs e)
    {
        if (e.Key == Avalonia.Input.Key.Enter)
        {
            e.Handled = true;
            ApplyDiffIndexInput();
        }
    }

    private void ApplyDiffIndexInput()
    {
        if (_diffBlocks.Count == 0) return;
        var input = CurrentDiffDisplayIndex?.Trim();
        if (string.IsNullOrEmpty(input)) return;

        if (int.TryParse(input, out var displayIndex) && displayIndex >= 1 && displayIndex <= _diffBlocks.Count)
        {
            _currentDiffIndex = displayIndex - 1;
            NavigateToCurrentDiff();
        }
        UpdateDiffNavigationUi();
    }

    private void QueueRefreshHighlighting()
    {
        _refreshHighlightingCancellation?.Cancel();
        _refreshHighlightingCancellation?.Dispose();

        var cancellation = new CancellationTokenSource();
        _refreshHighlightingCancellation = cancellation;
        var refreshVersion = ++_refreshHighlightingVersion;
        _ = RefreshHighlightingAsync(refreshVersion, cancellation.Token);
    }

    private async Task RefreshHighlightingAsync(int refreshVersion, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(DiffRefreshDebounceMilliseconds, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        var oldText = OldXml?.Text ?? string.Empty;
        var newText = NewXml?.Text ?? string.Empty;

        if (string.Equals(oldText, newText, StringComparison.Ordinal))
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (!IsCurrentRefresh(refreshVersion, cancellationToken))
                {
                    return;
                }

                EnsureOverviewBehaviors();
                ClearDiffState();
            }, DispatcherPriority.Background);
            return;
        }

        SideBySideDiffModel diffResult;
        HighlightBackgroundRenderHelper.HighlightData? oldHighlightData = null;
        HighlightBackgroundRenderHelper.HighlightData? newHighlightData = null;
        IReadOnlyList<DiffPreviewMarkerRun> oldPreviewMarkerRuns = Array.Empty<DiffPreviewMarkerRun>();
        IReadOnlyList<DiffPreviewMarkerRun> newPreviewMarkerRuns = Array.Empty<DiffPreviewMarkerRun>();

        try
        {
            diffResult = await Task.Run(() => SideBySideDiffBuilder.Diff(oldText, newText), cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
        }
        catch (OperationCanceledException)
        {
            return;
        }

        var hasDifferences = HasDiffLines(diffResult.OldText.Lines) || HasDiffLines(diffResult.NewText.Lines);

        if (hasDifferences)
        {
            try
            {
                oldHighlightData = await Task.Run(
                    () => HighlightBackgroundRenderHelper.BuildHighlightData(diffResult.OldText.Lines), cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                newHighlightData = await Task.Run(
                    () => HighlightBackgroundRenderHelper.BuildHighlightData(diffResult.NewText.Lines), cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                oldPreviewMarkerRuns = await Task.Run(
                    () => BuildPreviewMarkerRuns(diffResult.OldText.Lines), cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                newPreviewMarkerRuns = await Task.Run(
                    () => BuildPreviewMarkerRuns(diffResult.NewText.Lines), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex,
                    "Failed to build XmlDiffView highlight data. Diff navigation will remain available without line highlights.");
                oldHighlightData = null;
                newHighlightData = null;
            }
        }

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (!IsCurrentRefresh(refreshVersion, cancellationToken))
            {
                return;
            }

            EnsureOverviewBehaviors();

            _diffResult = diffResult;
            _oldHighlightData = oldHighlightData;
            _newHighlightData = newHighlightData;
            _oldPreviewMarkerRuns = oldPreviewMarkerRuns;
            _newPreviewMarkerRuns = newPreviewMarkerRuns;
            RebuildDiffBlocks();

            if (_diffBlocks.Count == 0 && !hasDifferences)
            {
                ClearDiffState();
                return;
            }

            TryRefreshDiffPresentation();
            UpdateDiffNavigationUi();

            if (_diffBlocks.Count > 0)
            {
                Dispatcher.UIThread.Post(NavigateToCurrentDiff, DispatcherPriority.Loaded);
            }
        }, DispatcherPriority.Background);
    }

    private void ApplyDiffHighlights()
    {
        if (_diffResult == null || _oldHighlightData is null || _newHighlightData is null)
        {
            HighlightBackgroundRenderHelper.Clear(OldEditorControl);
            HighlightBackgroundRenderHelper.Clear(NewEditorControl);
            return;
        }

        var oldNavigation = GetNavigationBlockRange(isNewText: false);
        var newNavigation = GetNavigationBlockRange(isNewText: true);

        HighlightBackgroundRenderHelper.Apply(OldEditorControl,
            _oldHighlightData.Value,
            HighlightBackgroundRenderHelper.GetRangeBrush(isNewText: false),
            oldNavigation.StartLineNumber,
            oldNavigation.EndLineNumber,
            oldNavigation.StartLineNumber > 0
                ? HighlightBackgroundRenderHelper.GetNavigationLineBrush(isNewText: false)
                : null);
        HighlightBackgroundRenderHelper.Apply(NewEditorControl,
            _newHighlightData.Value,
            HighlightBackgroundRenderHelper.GetRangeBrush(isNewText: true),
            newNavigation.StartLineNumber,
            newNavigation.EndLineNumber,
            newNavigation.StartLineNumber > 0
                ? HighlightBackgroundRenderHelper.GetNavigationLineBrush(isNewText: true)
                : null);
    }

    private void NavigateToCurrentDiff()
    {
        if (GetCurrentDiffBlock() is not { } currentBlock)
        {
            UpdateDiffNavigationUi();
            return;
        }

        SyncScrolling = false;
        CenterEditorOnDiffBlock(OldEditorControl, currentBlock.OldStartLineNumber, currentBlock.OldEndLineNumber);
        CenterEditorOnDiffBlock(NewEditorControl, currentBlock.NewStartLineNumber, currentBlock.NewEndLineNumber);
        TryRefreshDiffPresentation();
        UpdateDiffNavigationUi();
    }

    private void ClearDiffState()
    {
        _diffResult = null;
        _oldHighlightData = null;
        _newHighlightData = null;
        _oldPreviewMarkerRuns = Array.Empty<DiffPreviewMarkerRun>();
        _newPreviewMarkerRuns = Array.Empty<DiffPreviewMarkerRun>();
        _diffBlocks.Clear();
        _currentDiffIndex = -1;
        HighlightBackgroundRenderHelper.Clear(OldEditorControl);
        HighlightBackgroundRenderHelper.Clear(NewEditorControl);
        ClearPreviewSidebars();
        UpdateDiffNavigationUi();
    }

    private void TryRefreshDiffPresentation()
    {
        try
        {
            ApplyDiffHighlights();
            RenderPreviewSidebars();
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex,
                "Failed to render XmlDiffView diff highlights or overview markers. Diff navigation remains available.");
        }
    }

    private void RebuildDiffBlocks()
    {
        _diffBlocks.Clear();
        _currentDiffIndex = -1;

        if (_diffResult == null)
        {
            return;
        }

        var oldLines = _diffResult.OldText.Lines;
        var newLines = _diffResult.NewText.Lines;
        var pairCount = Math.Max(oldLines.Count, newLines.Count);
        var oldTotalLines = OldXml?.LineCount ?? CountNonImaginaryLines(oldLines);
        var newTotalLines = NewXml?.LineCount ?? CountNonImaginaryLines(newLines);
        var oldProcessedLineCount = 0;
        var newProcessedLineCount = 0;
        var inBlock = false;
        var currentOldStart = 0;
        var currentOldEnd = 0;
        var currentNewStart = 0;
        var currentNewEnd = 0;

        for (var i = 0; i < pairCount; i++)
        {
            var oldLine = i < oldLines.Count ? oldLines[i] : null;
            var newLine = i < newLines.Count ? newLines[i] : null;
            var oldLineType = oldLine?.Type ?? ChangeType.Imaginary;
            var newLineType = newLine?.Type ?? ChangeType.Imaginary;
            var hasDifference = oldLineType != ChangeType.Unchanged || newLineType != ChangeType.Unchanged;

            if (hasDifference)
            {
                if (!inBlock)
                {
                    currentOldStart = GetAnchorLineNumber(oldProcessedLineCount, oldTotalLines);
                    currentOldEnd = currentOldStart;
                    currentNewStart = GetAnchorLineNumber(newProcessedLineCount, newTotalLines);
                    currentNewEnd = currentNewStart;
                    inBlock = true;
                }

                if (oldLineType != ChangeType.Imaginary && oldTotalLines > 0)
                {
                    currentOldEnd = Math.Clamp(oldProcessedLineCount + 1, 1, oldTotalLines);
                }

                if (newLineType != ChangeType.Imaginary && newTotalLines > 0)
                {
                    currentNewEnd = Math.Clamp(newProcessedLineCount + 1, 1, newTotalLines);
                }
            }
            else if (inBlock)
            {
                _diffBlocks.Add(new DiffBlock(currentOldStart, currentOldEnd, currentNewStart, currentNewEnd));
                inBlock = false;
            }

            if (oldLineType != ChangeType.Imaginary)
            {
                oldProcessedLineCount++;
            }

            if (newLineType != ChangeType.Imaginary)
            {
                newProcessedLineCount++;
            }
        }

        if (inBlock)
        {
            _diffBlocks.Add(new DiffBlock(currentOldStart, currentOldEnd, currentNewStart, currentNewEnd));
        }

        if (_diffBlocks.Count > 0)
        {
            _currentDiffIndex = 0;
        }
    }

    private static int CountNonImaginaryLines(IEnumerable<DiffPiece> lines)
    {
        return lines.Count(line => line.Type != ChangeType.Imaginary);
    }

    private static int GetAnchorLineNumber(int processedLineCount, int totalLines)
    {
        if (totalLines <= 0)
        {
            return 0;
        }

        return Math.Clamp(processedLineCount + 1, 1, totalLines);
    }

    private static bool HasDiffLines(IEnumerable<DiffPiece> lines)
    {
        foreach (var line in lines)
        {
            if (line.Type != ChangeType.Unchanged)
            {
                return true;
            }
        }

        return false;
    }

    private (int StartLineNumber, int EndLineNumber) GetNavigationBlockRange(bool isNewText)
    {
        if (GetCurrentDiffBlock() is not { } currentBlock)
        {
            return (0, 0);
        }

        int startLineNumber;
        int endLineNumber;
        if (isNewText)
        {
            startLineNumber = currentBlock.NewStartLineNumber;
            endLineNumber = currentBlock.NewEndLineNumber;
        }
        else
        {
            startLineNumber = currentBlock.OldStartLineNumber;
            endLineNumber = currentBlock.OldEndLineNumber;
        }

        return startLineNumber <= 0 || endLineNumber < startLineNumber ? (0, 0) : (startLineNumber, endLineNumber);
    }

    private DiffBlock? GetCurrentDiffBlock()
    {
        if (_currentDiffIndex < 0 || _currentDiffIndex >= _diffBlocks.Count)
        {
            return null;
        }

        return _diffBlocks[_currentDiffIndex];
    }

    private static int ResolveNavigationLineNumber(int startLineNumber, int endLineNumber)
    {
        return startLineNumber > 0 ? startLineNumber : endLineNumber;
    }

    private void UpdateDiffNavigationUi()
    {
        var totalCount = _diffBlocks.Count;
        if (totalCount <= 0)
        {
            var emptyText = Loc["DiffNavigationEmptyText"];
            DiffNavigationText.Text = string.IsNullOrWhiteSpace(emptyText) ? "Diff 0/0" : emptyText;
            PreviousDiffButton.IsEnabled = false;
            NextDiffButton.IsEnabled = false;
            return;
        }

        var currentDisplayIndex = _currentDiffIndex >= 0 && _currentDiffIndex < totalCount ? _currentDiffIndex + 1 : 1;

        DiffNavigationText.Text = $"/{totalCount}";
        CurrentDiffDisplayIndex = currentDisplayIndex.ToString();
        PreviousDiffButton.IsEnabled = totalCount > 0 && _currentDiffIndex > 0;
        NextDiffButton.IsEnabled = totalCount > 0 && _currentDiffIndex < totalCount - 1;
    }

    private static void CenterEditorOnDiffBlock(TextEditor editor, int startLineNumber, int endLineNumber)
    {
        var document = editor.Document;
        if (document == null || document.LineCount <= 0)
        {
            return;
        }

        var lineNumber = ResolveNavigationLineNumber(startLineNumber, endLineNumber);
        if (lineNumber <= 0)
        {
            return;
        }

        lineNumber = Math.Clamp(lineNumber, 1, document.LineCount);
        var line = document.GetLineByNumber(lineNumber);
        editor.CaretOffset = line.Offset;

        if (GetScrollViewer(editor) is not { } scrollViewer)
        {
            editor.ScrollToLine(lineNumber);
            return;
        }

        var lineHeight = editor.TextArea?.TextView?.DefaultLineHeight ?? 0d;
        if (lineHeight <= 0)
        {
            editor.ScrollToLine(lineNumber);
            return;
        }

        var boundedEndLine = Math.Clamp(Math.Max(lineNumber, endLineNumber), lineNumber, document.LineCount);
        var blockMidLine = (lineNumber + boundedEndLine) / 2d;
        var blockHeight = Math.Max(lineHeight, (boundedEndLine - lineNumber + 1) * lineHeight);
        var targetVerticalOffset = (blockMidLine - 1d) * lineHeight - (scrollViewer.Viewport.Height - blockHeight) / 2d;
        var maxOffsetY = Math.Max(0d, scrollViewer.Extent.Height - scrollViewer.Viewport.Height);
        var clampedOffsetY = Math.Clamp(targetVerticalOffset, 0d, maxOffsetY);

        scrollViewer.Offset = new Vector(scrollViewer.Offset.X, clampedOffsetY);
    }

    private void EnsureOverviewBehaviors()
    {
        ConfigureOverviewBehavior(OldEditorControl, OldPreviewTrack, OldPreviewViewportBorder);
        ConfigureOverviewBehavior(NewEditorControl, NewPreviewTrack, NewPreviewViewportBorder);
    }

    private static void ConfigureOverviewBehavior(TextEditor editor, Control track, Control viewportThumb)
    {
        if (GetScrollViewer(editor) is not { } scrollViewer)
        {
            return;
        }

        var existingTrack = ScrollViewerViewportOverviewAttached.GetTrack(scrollViewer);
        var existingThumb = ScrollViewerViewportOverviewAttached.GetViewportThumb(scrollViewer);
        var isEnabled = ScrollViewerViewportOverviewAttached.GetIsEnabled(scrollViewer);

        if (ReferenceEquals(existingTrack, track) &&
            ReferenceEquals(existingThumb, viewportThumb) &&
            isEnabled)
        {
            return;
        }

        ScrollViewerViewportOverviewAttached.SetTrack(scrollViewer, track);
        ScrollViewerViewportOverviewAttached.SetViewportThumb(scrollViewer, viewportThumb);
        ScrollViewerViewportOverviewAttached.SetMinThumbHeight(scrollViewer, ViewportMinHeight);
        ScrollViewerViewportOverviewAttached.SetIsEnabled(scrollViewer, true);
    }

    private void RenderPreviewSidebars()
    {
        if (_diffResult == null)
        {
            ClearPreviewSidebars();
            return;
        }

        var currentBlock = GetCurrentDiffBlock();
        UpdatePreviewTrack(OldPreviewTrack, OldEditorControl, _oldPreviewMarkerRuns, currentBlock,
            isNewText: false);
        UpdatePreviewTrack(NewPreviewTrack, NewEditorControl, _newPreviewMarkerRuns, currentBlock,
            isNewText: true);
    }

    private void ClearPreviewSidebars()
    {
        ClearPreviewTrack(OldPreviewTrack);
        ClearPreviewTrack(NewPreviewTrack);
    }

    private static void UpdatePreviewTrack(DiffPreviewTrack previewTrack, TextEditor editor,
        IReadOnlyList<DiffPreviewMarkerRun> markerRuns,
        DiffBlock? currentBlock, bool isNewText)
    {
        if (editor.Document == null)
        {
            ClearPreviewTrack(previewTrack);
            return;
        }

        previewTrack.MarkerRuns = markerRuns;
        previewTrack.TotalLines = editor.Document.LineCount;
        if (currentBlock == null)
        {
            previewTrack.CurrentBlockStartLineNumber = 0;
            previewTrack.CurrentBlockEndLineNumber = 0;
            return;
        }

        previewTrack.CurrentBlockStartLineNumber = isNewText
            ? currentBlock.Value.NewStartLineNumber
            : currentBlock.Value.OldStartLineNumber;
        previewTrack.CurrentBlockEndLineNumber = isNewText
            ? currentBlock.Value.NewEndLineNumber
            : currentBlock.Value.OldEndLineNumber;
    }

    private static void ClearPreviewTrack(DiffPreviewTrack previewTrack)
    {
        previewTrack.MarkerRuns = Array.Empty<DiffPreviewMarkerRun>();
        previewTrack.TotalLines = 0;
        previewTrack.CurrentBlockStartLineNumber = 0;
        previewTrack.CurrentBlockEndLineNumber = 0;
    }

    private static IReadOnlyList<DiffPreviewMarkerRun> BuildPreviewMarkerRuns(IReadOnlyList<DiffPiece> lines)
    {
        var markerRuns = new List<DiffPreviewMarkerRun>();
        var documentLineNumber = 0;
        DiffPreviewMarkerRun? currentRun = null;

        foreach (var line in lines)
        {
            if (line.Type == ChangeType.Imaginary)
            {
                continue;
            }

            documentLineNumber++;
            var brush = HighlightBackgroundRenderHelper.GetIndicatorBrush(line.Type);

            if (brush == null)
            {
                if (currentRun is { } runWithoutBrush)
                {
                    markerRuns.Add(runWithoutBrush);
                    currentRun = null;
                }

                continue;
            }

            if (currentRun is { } run && ReferenceEquals(run.Brush, brush) &&
                run.StartLineNumber + run.LineCount == documentLineNumber)
            {
                currentRun = run with { LineCount = run.LineCount + 1 };
                continue;
            }

            if (currentRun is { } completedRun)
            {
                markerRuns.Add(completedRun);
            }

            currentRun = new DiffPreviewMarkerRun(documentLineNumber, 1, brush);
        }

        if (currentRun is { } finalRun)
        {
            markerRuns.Add(finalRun);
        }

        return markerRuns;
    }

    private static ScrollViewer? GetScrollViewer(TextEditor editor)
    {
        return typeof(TextEditor).GetProperty("ScrollViewer",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(editor) as ScrollViewer;
    }

    private bool IsCurrentRefresh(int refreshVersion, CancellationToken cancellationToken)
    {
        return !cancellationToken.IsCancellationRequested && refreshVersion == _refreshHighlightingVersion;
    }
}
