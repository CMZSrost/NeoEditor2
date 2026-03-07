using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;
using AvaloniaEdit;
using DiffPlex.DiffBuilder;
using DiffPlex.DiffBuilder.Model;
using NeoEditor.Helper;
using NeoEditor.ViewModels;

namespace NeoEditor.Views.UserControls;

public partial class DiffView : UserControl
{
    private const int ScrollIndicatorWidth = 20;
    private readonly double _lineHeight;
    private readonly DispatcherTimer _scrollIndicatorTimer;
    private readonly DiffViewModel _viewModel;

    private SideBySideDiffModel? _diffResult;
    private bool _isClearing;
    private bool _isLeftScrolling;
    private bool _isReplacingText;
    private bool _isRightScrolling;
    private ScrollViewer? _leftScrollViewer;
    private ScrollViewer? _rightScrollViewer;

    public static readonly StyledProperty<string?> NewTextProperty =
        AvaloniaProperty.Register<DiffView, string?>("NewText");

    public static readonly StyledProperty<string?> OldTextProperty =
        AvaloniaProperty.Register<DiffView, string?>("OldText");

    public DiffView()
    {
        InitializeComponent();

        _viewModel = new DiffViewModel();
        DataContext = _viewModel;

        _lineHeight = NewerEditor.TextArea.TextView.DefaultLineHeight;

        OlderEditor.TextArea.TextView.ScrollOffsetChanged += OnLeftScrollChanged;
        NewerEditor.TextArea.TextView.ScrollOffsetChanged += RightScrollChanged;
        OlderEditor.TextChanged += OnLeftScrollChanged;
        NewerEditor.TextChanged += RightScrollChanged;
        OlderEditor.TextChanged += OnEdit;
        NewerEditor.TextChanged += OnEdit;
        _viewModel.DoClearDiff += ClearDiff;
        _viewModel.DoRender += Render;

        _scrollIndicatorTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _scrollIndicatorTimer.Tick += ScrollIndicatorTimer_Tick;
    }

    public string? OldText
    {
        get { return (string?)GetValue(OldTextProperty); }
        set { SetValue(OldTextProperty, value); }
    }

    public string? NewText
    {
        get { return (string?)GetValue(NewTextProperty); }
        set { SetValue(NewTextProperty, value); }
    }

    private void ScrollIndicatorTimer_Tick(object? sender, EventArgs e)
    {
        _scrollIndicatorTimer.IsEnabled = false; // 使用 IsEnabled 而不是 Stop
        RenderScrollIndicators();
    }

    private void ScheduleRenderScrollIndicators()
    {
        // 如果计时器未启动，则启用计时器
        if (!_scrollIndicatorTimer.IsEnabled) _scrollIndicatorTimer.IsEnabled = true;
    }

    #region Render Diff

    // 编辑事件
    private void OnEdit(object? sender, EventArgs e)
    {
        if (_isReplacingText) return;
        if (_isClearing) return;
        if (!_viewModel.RealTimeDiffering) return;
        Render();
    }

    private void Render()
    {
        var olderText = OlderEditor.Text.Replace("\u00a0\r\n", string.Empty).Replace("\u00a0", " ");
        var newerText = NewerEditor.Text.Replace("\u00a0\r\n", string.Empty).Replace("\u00a0", " ");
        Render(olderText, newerText, false);
    }

    // 渲染差异
    private void Render(string oldText, string newText, bool ignoreWhitespace = true, bool ignoreCase = false)
    {
        _diffResult = SideBySideDiffBuilder.Diff(oldText, newText, ignoreWhitespace, ignoreCase);

        if (_diffResult == null || (!_diffResult.NewText.HasDifferences && !_diffResult.OldText.HasDifferences))
        {
            ClearDiff();
            return;
        }

        RenderTextDiff();
        ScheduleRenderScrollIndicators();
    }

    private void ClearDiff()
    {
        Dispatcher.UIThread.Post(() =>
        {
            _isClearing = true;
            OlderEditor.Text = OlderEditor.Text.Replace("\u00a0\r\n", string.Empty).Replace("\u00a0", " ");
            NewerEditor.Text = NewerEditor.Text.Replace("\u00a0\r\n", string.Empty).Replace("\u00a0", " ");
            HighlightBackgroundRenderHelper.Clear(OlderEditor);
            HighlightBackgroundRenderHelper.Clear(NewerEditor);
            OlderEditorScrollIndicatorCanvas.Children.Clear();
            NewerEditorScrollIndicatorCanvas.Children.Clear();
            _isClearing = false;
        }, DispatcherPriority.Background);
    }

    private void RenderTextDiff()
    {
        // 处理文本差异
        var oldHighlight = GenerateTextHighLight(_diffResult!.OldText.Lines);
        var newHighlight = GenerateTextHighLight(_diffResult!.NewText.Lines);

        HighlightBackgroundRenderHelper.Apply(OlderEditor, oldHighlight,
            HighlightBackgroundRenderHelper.GetRangeBrush(isNewText: false));
        HighlightBackgroundRenderHelper.Apply(NewerEditor, newHighlight,
            HighlightBackgroundRenderHelper.GetRangeBrush(isNewText: true));

        // 保存光标位置
        var olderEditorCaretOffset = OlderEditor.CaretOffset;
        var newerEditorCaretOffset = NewerEditor.CaretOffset;

        // 替换空行
        Dispatcher.UIThread.Post(() =>
        {
            _isReplacingText = true;
            var olderSb = new StringBuilder();
            var newerSb = new StringBuilder();

            foreach (var line in _diffResult.OldText.Lines)
            {
                if (!_viewModel.EnableDiff && line.Type == ChangeType.Imaginary) continue;
                olderSb.AppendLine(line.Type == ChangeType.Imaginary ? "\u00a0" : line.Text);
            }

            foreach (var line in _diffResult.NewText.Lines)
            {
                if (!_viewModel.EnableDiff && line.Type == ChangeType.Imaginary) continue;
                newerSb.AppendLine(line.Type == ChangeType.Imaginary ? "\u00a0" : line.Text);
            }

            OlderEditor.Text = olderSb.ToString().TrimEnd('\r', '\n');
            NewerEditor.Text = newerSb.ToString().TrimEnd('\r', '\n');

            // 恢复光标位置
            try
            {
                OlderEditor.CaretOffset = olderEditorCaretOffset;
            }
            catch (ArgumentOutOfRangeException)
            {
                // ignored
            }

            try
            {
                NewerEditor.CaretOffset = newerEditorCaretOffset;
            }
            catch (ArgumentOutOfRangeException)
            {
                // ignored
            }

            _isReplacingText = false;
        }, DispatcherPriority.Background);
    }

    private static HighlightBackgroundRenderHelper.HighlightData GenerateTextHighLight(List<DiffPiece> lines)
    {
        return HighlightBackgroundRenderHelper.BuildHighlightData(lines,
            changeType => HighlightBackgroundRenderHelper.GetLineBrush(changeType, includeImaginary: true),
            skipImaginaryLines: false);
    }

    private void RenderScrollIndicators()
    {
        // 清空现有 Canvas
        OlderEditorScrollIndicatorCanvas.Children.Clear();
        NewerEditorScrollIndicatorCanvas.Children.Clear();

        // 获取文本框的总高度和每行的高度
        var olderEditorHeight = Math.Min(OlderEditor.Bounds.Height, OlderEditor.LineCount * _lineHeight);
        var newerEditorHeight = Math.Min(NewerEditor.Bounds.Height, NewerEditor.LineCount * _lineHeight);

        // 获取总行数
        var olderTotalLines = OlderEditor.Document.LineCount;
        var newerTotalLines = NewerEditor.Document.LineCount;

        // 计算需要显示差异的行
        var olderDiffIndexes = GetDiffIndexes(_diffResult?.OldText.Lines);
        var newerDiffIndexes = GetDiffIndexes(_diffResult?.NewText.Lines);

        // 通过索引计算需要显示的矩形
        var olderRects =
            GenerateRectangles(olderDiffIndexes, olderEditorHeight, olderTotalLines, _lineHeight);
        var newerRects =
            GenerateRectangles(newerDiffIndexes, newerEditorHeight, newerTotalLines, _lineHeight);

        // 将矩形添加到 OlderEditor 的 Canvas 中
        foreach (var (x, y, width, height, brush) in olderRects)
            OlderEditorScrollIndicatorCanvas.Children.Add(new Border
            {
                Background = brush,
                Width = width,
                Height = height,
                Margin = new Thickness(x, y, 0, 0)
            });

        // 将矩形添加到 NewerEditor 的 Canvas 中
        foreach (var (x, y, width, height, brush) in newerRects)
            NewerEditorScrollIndicatorCanvas.Children.Add(new Border
            {
                Background = brush,
                Width = width,
                Height = height,
                Margin = new Thickness(x, y, 0, 0)
            });
    }


    private static List<(double X, double Y, double Width, double Height, IBrush Brush)> GenerateRectangles(
        IEnumerable<(int Index, IBrush Brush, int Count)> indexes, double totalHeight, int totalLines,
        double lineHeight)
    {
        var rects = new List<(double X, double Y, double Width, double Height, IBrush Brush)>();

        // 计算比例因子
        var ratio = totalHeight / (totalLines * lineHeight);

        // 分组连续的行
        var groupedIndexes = GroupConsecutiveIndexes(indexes);

        foreach (var group in groupedIndexes)
        {
            var first = group.First();
            var startY = first.Index * ratio * lineHeight;
            var height = Math.Min(group.Sum(x => x.Count) * ratio * lineHeight, group.Sum(x => x.Count) * lineHeight);

            rects.Add((0, startY, ScrollIndicatorWidth, height, first.Brush));
        }

        return rects;
    }


    private static List<(int Index, IBrush Brush, int Count)> GetDiffIndexes(IEnumerable<DiffPiece>? lines)
    {
        if (lines == null) return [];

        var diffIndexes = new List<(int Index, IBrush Brush, int Count)>();

        var diffPieces = lines.ToList();
        for (var i = 0; i < diffPieces.Count; i++)
        {
            var line = diffPieces.ElementAt(i);
            var brush = HighlightBackgroundRenderHelper.GetIndicatorBrush(line.Type);

            if (brush != null) diffIndexes.Add((i, brush, 1));
        }

        return diffIndexes;
    }


    private static List<List<(int Index, IBrush Brush, int Count)>> GroupConsecutiveIndexes(
        IEnumerable<(int Index, IBrush Brush, int Count)> indexes)
    {
        var groupedIndexes = new List<List<(int Index, IBrush Brush, int Count)>>();
        List<(int Index, IBrush Brush, int Count)>? currentGroup = null;

        foreach (var index in indexes.OrderBy(x => x.Index))
            if (currentGroup == null || index.Index != currentGroup.Last().Index + 1)
            {
                currentGroup = new List<(int Index, IBrush Brush, int Count)> { index };
                groupedIndexes.Add(currentGroup);
            }
            else
            {
                currentGroup.Add(index);
            }

        return groupedIndexes;
    }

    #endregion

    #region Scrolling

    private void OnLeftScrollChanged(object? sender, EventArgs e)
    {
        if (!_viewModel.SynchronousScrolling) return;
        if (_isRightScrolling) return;
        if (_diffResult == null) return;
        if (_leftScrollViewer == null || _rightScrollViewer == null)
        {
            GetScrollViewer();
            if (_leftScrollViewer == null || _rightScrollViewer == null) return;
        }

        _isLeftScrolling = true;

        // 取得当前滚动位置
        var verticalOffset = OlderEditor.VerticalOffset;
        var horizontalOffset = OlderEditor.HorizontalOffset;

        _rightScrollViewer.Offset = new Vector(horizontalOffset, verticalOffset);

        _isLeftScrolling = false;
    }

    private void RightScrollChanged(object? sender, EventArgs e)
    {
        if (!_viewModel.SynchronousScrolling) return;
        if (_isLeftScrolling) return;
        if (_diffResult == null) return;
        if (_leftScrollViewer == null || _rightScrollViewer == null)
        {
            GetScrollViewer();
            if (_leftScrollViewer == null || _rightScrollViewer == null) return;
        }

        _isRightScrolling = true;

        // 取得当前滚动位置
        var verticalOffset = NewerEditor.VerticalOffset;
        var horizontalOffset = NewerEditor.HorizontalOffset;

        _leftScrollViewer.Offset = new Vector(horizontalOffset, verticalOffset);

        _isRightScrolling = false;
    }

    private void GetScrollViewer()
    {
        _leftScrollViewer = (ScrollViewer)typeof(TextEditor).GetProperty("ScrollViewer",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(OlderEditor)!;
        _rightScrollViewer = (ScrollViewer)typeof(TextEditor).GetProperty("ScrollViewer",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(NewerEditor)!;
    }

    #endregion

    #region Import / Export

    private void ImportBoth(object? sender, RoutedEventArgs e)
    {
        Import(EditorSelection.Left | EditorSelection.Right);
    }

    private void ImportLeft(object? sender, RoutedEventArgs e)
    {
        Import(EditorSelection.Left);
    }

    private void ImportRight(object? sender, RoutedEventArgs e)
    {
        Import(EditorSelection.Right);
    }

    private void ExportLeft(object? sender, RoutedEventArgs e)
    {
        Export(EditorSelection.Left);
    }

    private void ExportRight(object? sender, RoutedEventArgs e)
    {
        Export(EditorSelection.Right);
    }

    private void Import(EditorSelection editorSelection)
    {
        var openFileDialog = new OpenFileDialog
        {
            Title = "Import Text",
            AllowMultiple = false
        };

        var filePath = openFileDialog.ShowAsync((Window)Parent!).Result;
        if (filePath == null) return;

        var text = File.ReadAllText(filePath[0]);
        _viewModel.RealTimeDiffering = false;
        if (editorSelection.HasFlag(EditorSelection.Left)) OlderEditor.Text = text;
        if (editorSelection.HasFlag(EditorSelection.Right)) NewerEditor.Text = text;
        _viewModel.RealTimeDiffering = true;
    }

    private async void Export(EditorSelection editorSelection)
    {
        ClearDiff();

        var saveFileDialog = new SaveFileDialog
        {
            Title = "Export Text",
            InitialFileName = "Export.txt"
        };

        var filePath = await saveFileDialog.ShowAsync((Window)Parent!);
        if (filePath != null)
        {
            var text = editorSelection switch
            {
                EditorSelection.Left => OlderEditor.Text,
                EditorSelection.Right => NewerEditor.Text,
                _ => throw new ArgumentOutOfRangeException(nameof(editorSelection), editorSelection, null)
            };

            await File.WriteAllTextAsync(filePath, text);
        }

        if (_viewModel.EnableDiff) Render();
    }

    #endregion
}

[Flags]
public enum EditorSelection
{
    Left = 1,
    Right = 2
}