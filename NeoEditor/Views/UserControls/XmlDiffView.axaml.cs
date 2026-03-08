using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using DiffPlex.DiffBuilder;
using DiffPlex.DiffBuilder.Model;
using Microsoft.Extensions.DependencyInjection;
using NeoEditor.Helper;
using NeoEditor.Helper.AttachedProperties;
using NeoEditor.Services;

namespace NeoEditor.Views.UserControls;

public partial class XmlDiffView : UserControl
{
    private const double PreviewMinBlockHeight = 2d;
    private const double ViewportMinHeight = 12d;

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

    private TextEditor OldEditorControl => this.FindControl<TextEditor>("OldEditor")!;
    private TextEditor NewEditorControl => this.FindControl<TextEditor>("NewEditor")!;
    public LocalizationService Loc { get; set; }

    public XmlDiffView()
    {
        InitializeComponent();
        AttachedToVisualTree += OnAttachedToVisualTree;
        OldEditorControl.TextChanged += OnEditorTextChanged;
        NewEditorControl.TextChanged += OnEditorTextChanged;
        OldPreviewCanvas.SizeChanged += OnPreviewCanvasSizeChanged;
        NewPreviewCanvas.SizeChanged += OnPreviewCanvasSizeChanged;
        UpdateDiffNavigationUi();
        QueueRefreshHighlighting();
        Loc = App.ServiceProvider.GetRequiredService<LocalizationService>();
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
        Dispatcher.UIThread.Post(EnsureOverviewBehaviors, DispatcherPriority.Loaded);
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

    private void QueueRefreshHighlighting()
    {
        Dispatcher.UIThread.Post(RefreshHighlighting, DispatcherPriority.Background);
    }

    private void RefreshHighlighting()
    {
        EnsureOverviewBehaviors();

        var oldText = OldXml?.Text ?? string.Empty;
        var newText = NewXml?.Text ?? string.Empty;

        if (string.Equals(oldText, newText, StringComparison.Ordinal))
        {
            _diffResult = null;
            _diffBlocks.Clear();
            _currentDiffIndex = -1;
            HighlightBackgroundRenderHelper.Clear(OldEditorControl);
            HighlightBackgroundRenderHelper.Clear(NewEditorControl);
            ClearPreviewSidebars();
            UpdateDiffNavigationUi();
            return;
        }

        _diffResult = SideBySideDiffBuilder.Diff(oldText, newText);
        if (!_diffResult.OldText.HasDifferences && !_diffResult.NewText.HasDifferences)
        {
            _diffResult = null;
            _diffBlocks.Clear();
            _currentDiffIndex = -1;
            HighlightBackgroundRenderHelper.Clear(OldEditorControl);
            HighlightBackgroundRenderHelper.Clear(NewEditorControl);
            ClearPreviewSidebars();
            UpdateDiffNavigationUi();
            return;
        }

        RebuildDiffBlocks();
        ApplyDiffHighlights();
        RenderPreviewSidebars();
        UpdateDiffNavigationUi();
    }

    private void ApplyDiffHighlights()
    {
        if (_diffResult == null)
        {
            HighlightBackgroundRenderHelper.Clear(OldEditorControl);
            HighlightBackgroundRenderHelper.Clear(NewEditorControl);
            return;
        }

        var oldHighlight = HighlightBackgroundRenderHelper.BuildHighlightData(_diffResult.OldText.Lines);
        var newHighlight = HighlightBackgroundRenderHelper.BuildHighlightData(_diffResult.NewText.Lines);

        HighlightBackgroundRenderHelper.Apply(OldEditorControl,
            MergeNavigationBlockHighlight(oldHighlight, isNewText: false),
            HighlightBackgroundRenderHelper.GetRangeBrush(isNewText: false));
        HighlightBackgroundRenderHelper.Apply(NewEditorControl,
            MergeNavigationBlockHighlight(newHighlight, isNewText: true),
            HighlightBackgroundRenderHelper.GetRangeBrush(isNewText: true));
    }

    private HighlightBackgroundRenderHelper.HighlightData MergeNavigationBlockHighlight(
        HighlightBackgroundRenderHelper.HighlightData highlightData, bool isNewText)
    {
        if (GetCurrentDiffBlock() is not { } currentBlock)
        {
            return highlightData;
        }

        var startLineNumber = isNewText ? currentBlock.NewStartLineNumber : currentBlock.OldStartLineNumber;
        var endLineNumber = isNewText ? currentBlock.NewEndLineNumber : currentBlock.OldEndLineNumber;
        if (startLineNumber <= 0 || endLineNumber < startLineNumber)
        {
            return highlightData;
        }

        var mergedLineBrushes = highlightData.LineBrushes
            .Where(x => x.LineNumber < startLineNumber || x.LineNumber > endLineNumber)
            .ToList();
        var navigationBrush = HighlightBackgroundRenderHelper.GetNavigationLineBrush(isNewText);

        for (var lineNumber = startLineNumber; lineNumber <= endLineNumber; lineNumber++)
        {
            mergedLineBrushes.Add((lineNumber, navigationBrush));
        }

        return highlightData with { LineBrushes = mergedLineBrushes.OrderBy(x => x.LineNumber).ToList() };
    }

    private void NavigateToCurrentDiff()
    {
        if (GetCurrentDiffBlock() is not { } currentBlock)
        {
            UpdateDiffNavigationUi();
            return;
        }

        NavigateEditorToLine(OldEditorControl,
            ResolveNavigationLineNumber(currentBlock.OldStartLineNumber, currentBlock.OldEndLineNumber));
        NavigateEditorToLine(NewEditorControl,
            ResolveNavigationLineNumber(currentBlock.NewStartLineNumber, currentBlock.NewEndLineNumber));
        ApplyDiffHighlights();
        UpdateDiffNavigationUi();
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
        var pairCount = Math.Min(oldLines.Count, newLines.Count);
        var oldTotalLines = OldEditorControl.Document?.LineCount ?? 0;
        var newTotalLines = NewEditorControl.Document?.LineCount ?? 0;
        var oldProcessedLineCount = 0;
        var newProcessedLineCount = 0;
        var inBlock = false;
        var currentOldStart = 0;
        var currentOldEnd = 0;
        var currentNewStart = 0;
        var currentNewEnd = 0;

        for (var i = 0; i < pairCount; i++)
        {
            var oldLine = oldLines[i];
            var newLine = newLines[i];
            var hasDifference = oldLine.Type != ChangeType.Unchanged || newLine.Type != ChangeType.Unchanged;

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

                if (oldLine.Type != ChangeType.Imaginary && oldTotalLines > 0)
                {
                    currentOldEnd = Math.Clamp(oldProcessedLineCount + 1, 1, oldTotalLines);
                }

                if (newLine.Type != ChangeType.Imaginary && newTotalLines > 0)
                {
                    currentNewEnd = Math.Clamp(newProcessedLineCount + 1, 1, newTotalLines);
                }
            }
            else if (inBlock)
            {
                _diffBlocks.Add(new DiffBlock(currentOldStart, currentOldEnd, currentNewStart, currentNewEnd));
                inBlock = false;
            }

            if (oldLine.Type != ChangeType.Imaginary)
            {
                oldProcessedLineCount++;
            }

            if (newLine.Type != ChangeType.Imaginary)
            {
                newProcessedLineCount++;
            }
        }

        if (inBlock)
        {
            _diffBlocks.Add(new DiffBlock(currentOldStart, currentOldEnd, currentNewStart, currentNewEnd));
        }
    }

    private static int GetAnchorLineNumber(int processedLineCount, int totalLines)
    {
        if (totalLines <= 0)
        {
            return 0;
        }

        return Math.Clamp(processedLineCount + 1, 1, totalLines);
    }

    private DiffBlock? GetCurrentDiffBlock()
    {
        return _currentDiffIndex >= 0 && _currentDiffIndex < _diffBlocks.Count ? _diffBlocks[_currentDiffIndex] : null;
    }

    private static int ResolveNavigationLineNumber(int startLineNumber, int endLineNumber)
    {
        return startLineNumber > 0 ? startLineNumber : endLineNumber;
    }

    private void UpdateDiffNavigationUi()
    {
        var totalCount = _diffBlocks.Count;
        var currentDisplayIndex = _currentDiffIndex >= 0 && _currentDiffIndex < totalCount ? _currentDiffIndex + 1 : 0;

        DiffNavigationText.Text = $"差异 {currentDisplayIndex}/{totalCount}";
        PreviousDiffButton.IsEnabled = totalCount > 0 && _currentDiffIndex > 0;
        NextDiffButton.IsEnabled = totalCount > 0 && _currentDiffIndex < totalCount - 1;
    }

    private static void NavigateEditorToLine(TextEditor editor, int targetLineNumber)
    {
        var document = editor.Document;
        if (document == null || document.LineCount <= 0 || targetLineNumber <= 0)
        {
            return;
        }

        var lineNumber = Math.Clamp(targetLineNumber, 1, document.LineCount);
        var line = document.GetLineByNumber(lineNumber);
        editor.CaretOffset = line.Offset;
        editor.ScrollToLine(lineNumber);
    }

    private void EnsureOverviewBehaviors()
    {
        ConfigureOverviewBehavior(OldEditorControl, OldPreviewCanvas, OldPreviewViewportBorder);
        ConfigureOverviewBehavior(NewEditorControl, NewPreviewCanvas, NewPreviewViewportBorder);
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
        RenderPreviewCanvas(OldPreviewCanvas, OldEditorControl, _diffResult.OldText.Lines, currentBlock,
            isNewText: false);
        RenderPreviewCanvas(NewPreviewCanvas, NewEditorControl, _diffResult.NewText.Lines, currentBlock,
            isNewText: true);
    }

    private void ClearPreviewSidebars()
    {
        OldPreviewCanvas.Children.Clear();
        NewPreviewCanvas.Children.Clear();
    }

    private static void RenderPreviewCanvas(Canvas previewCanvas, TextEditor editor, IReadOnlyList<DiffPiece> lines,
        DiffBlock? currentBlock, bool isNewText)
    {
        previewCanvas.Children.Clear();

        var markers = BuildPreviewMarkers(lines);
        if (markers.Count == 0 || editor.Document == null)
        {
            return;
        }

        var rectangles = GeneratePreviewRectangles(markers, previewCanvas.Bounds.Height, editor.Document.LineCount,
            previewCanvas.Bounds.Width);
        foreach (var (x, y, width, height, brush) in rectangles)
        {
            previewCanvas.Children.Add(new Border
            {
                Background = brush,
                Width = width,
                Height = height,
                Margin = new Thickness(x, y, 0, 0),
                IsHitTestVisible = false
            });
        }

        AddCurrentBlockPreviewMarker(previewCanvas, editor.Document.LineCount, currentBlock, isNewText);
    }

    private static List<(int Index, IBrush Brush, int Count)> BuildPreviewMarkers(IReadOnlyList<DiffPiece> lines)
    {
        var markers = new List<(int Index, IBrush Brush, int Count)>();
        var documentLineNumber = 0;

        foreach (var line in lines)
        {
            if (line.Type == ChangeType.Imaginary)
            {
                continue;
            }

            documentLineNumber++;
            var brush = HighlightBackgroundRenderHelper.GetIndicatorBrush(line.Type);
            if (brush != null)
            {
                markers.Add((documentLineNumber, brush, 1));
            }
        }

        return markers;
    }

    private static List<(double X, double Y, double Width, double Height, IBrush Brush)> GeneratePreviewRectangles(
        IEnumerable<(int Index, IBrush Brush, int Count)> indexes, double totalHeight, int totalLines, double width)
    {
        var rects = new List<(double X, double Y, double Width, double Height, IBrush Brush)>();
        if (totalHeight <= 0 || totalLines <= 0 || width <= 0)
        {
            return rects;
        }

        var lineHeight = totalHeight / totalLines;
        foreach (var group in GroupConsecutiveIndexes(indexes))
        {
            var first = group.First();
            var startY = Math.Max(0, (first.Index - 1) * lineHeight);
            var groupLineCount = group.Sum(x => x.Count);
            var height = Math.Max(PreviewMinBlockHeight, groupLineCount * lineHeight);
            height = Math.Min(height, totalHeight - startY);
            if (height <= 0)
            {
                continue;
            }

            rects.Add((0, startY, width, height, first.Brush));
        }

        return rects;
    }

    private static List<List<(int Index, IBrush Brush, int Count)>> GroupConsecutiveIndexes(
        IEnumerable<(int Index, IBrush Brush, int Count)> indexes)
    {
        var groupedIndexes = new List<List<(int Index, IBrush Brush, int Count)>>();
        List<(int Index, IBrush Brush, int Count)>? currentGroup = null;

        foreach (var index in indexes.OrderBy(x => x.Index))
        {
            var canAppend = currentGroup != null &&
                            index.Index == currentGroup.Last().Index + currentGroup.Last().Count &&
                            ReferenceEquals(index.Brush, currentGroup.Last().Brush);

            if (!canAppend)
            {
                currentGroup = new List<(int Index, IBrush Brush, int Count)> { index };
                groupedIndexes.Add(currentGroup);
            }
            else if (currentGroup != null)
            {
                currentGroup.Add(index);
            }
        }

        return groupedIndexes;
    }

    private static void AddCurrentBlockPreviewMarker(Canvas previewCanvas, int totalLines, DiffBlock? currentBlock,
        bool isNewText)
    {
        if (currentBlock == null || totalLines <= 0 || previewCanvas.Bounds.Height <= 0 ||
            previewCanvas.Bounds.Width <= 0)
        {
            return;
        }

        var startLineNumber = isNewText ? currentBlock.Value.NewStartLineNumber : currentBlock.Value.OldStartLineNumber;
        var endLineNumber = isNewText ? currentBlock.Value.NewEndLineNumber : currentBlock.Value.OldEndLineNumber;
        if (startLineNumber <= 0 || endLineNumber < startLineNumber)
        {
            return;
        }

        var lineHeight = previewCanvas.Bounds.Height / totalLines;
        var startY = Math.Max(0, (startLineNumber - 1) * lineHeight);
        var height = Math.Max(PreviewMinBlockHeight * 1.5, (endLineNumber - startLineNumber + 1) * lineHeight);
        height = Math.Min(height, previewCanvas.Bounds.Height - startY);
        if (height <= 0)
        {
            return;
        }

        previewCanvas.Children.Add(new Border
        {
            Background = HighlightBackgroundRenderHelper.GetNavigationPreviewBrush(),
            BorderBrush = HighlightBackgroundRenderHelper.GetNavigationPreviewBorderBrush(),
            BorderThickness = new Thickness(1),
            Width = previewCanvas.Bounds.Width,
            Height = height,
            Margin = new Thickness(0, startY, 0, 0),
            IsHitTestVisible = false
        });
    }


    private static ScrollViewer? GetScrollViewer(TextEditor editor)
    {
        return typeof(TextEditor).GetProperty("ScrollViewer",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(editor) as ScrollViewer;
    }
}