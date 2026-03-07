using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Media;
using AvaloniaEdit;
using AvaloniaEdit.Rendering;
using DiffPlex.DiffBuilder.Model;

namespace NeoEditor.Helper;

public class HighlightBackgroundRenderHelper : IBackgroundRenderer
{
    public readonly record struct HighlightData(
        IReadOnlyList<(int LineNumber, IBrush LineBrush)> LineBrushes,
        Dictionary<int, List<(int Start, int Length)>> Ranges);

    // JetBrains-like diff palette: muted red / green / blue with neutral gray.
    public static IBrush DeletedLineBrush { get; } = CreateBrush("#FFF2D6D6");
    public static IBrush InsertedLineBrush { get; } = CreateBrush("#FFDFF2E0");
    public static IBrush ModifiedLineBrush { get; } = CreateBrush("#FFDCEBFA");
    public static IBrush ImaginaryLineBrush { get; } = CreateBrush("#FFECECEC");
    public static IBrush OldRangeBrush { get; } = CreateBrush("#FFE6B8C0");
    public static IBrush NewRangeBrush { get; } = CreateBrush("#FFB8DDBA");
    public static IBrush OldNavigationLineBrush { get; } = CreateBrush("#FFFFEE6A");
    public static IBrush NewNavigationLineBrush { get; } = CreateBrush("#FFFFEE6A");
    public static IBrush NavigationPreviewBrush { get; } = CreateBrush("#D9FFF066");
    public static IBrush NavigationPreviewBorderBrush { get; } = CreateBrush("#FFFFD400");
    public static IBrush DeletedIndicatorBrush { get; } = CreateBrush("#CCCE6475");
    public static IBrush ImaginaryIndicatorBrush { get; } = CreateBrush("#CC9AA1A9");
    public static IBrush InsertedIndicatorBrush { get; } = CreateBrush("#CC65A96F");
    public static IBrush ModifiedIndicatorBrush { get; } = CreateBrush("#CC6E9FD8");

    private readonly Dictionary<int, List<(int Start, int Length)>> _lineRangesToHighlight;
    private readonly Dictionary<int, IBrush> _linesToHighlight;
    private readonly IBrush _rangeBrush;

    public HighlightBackgroundRenderHelper(
        IEnumerable<(int LineNumber, IBrush LineBrush)> linesToHighlight,
        Dictionary<int, List<(int Start, int Length)>> lineRangesToHighlight, IBrush rangeBrush)
    {
        _linesToHighlight = linesToHighlight.ToDictionary(tuple => tuple.LineNumber, tuple => tuple.LineBrush);
        _lineRangesToHighlight = lineRangesToHighlight;
        _rangeBrush = rangeBrush;
    }

    public static IBrush CreateBrush(string color)
    {
        return new SolidColorBrush(Color.Parse(color));
    }

    public static IBrush GetRangeBrush(bool isNewText)
    {
        return isNewText ? NewRangeBrush : OldRangeBrush;
    }

    public static IBrush? GetLineBrush(ChangeType changeType, bool includeImaginary = false)
    {
        return changeType switch
        {
            ChangeType.Imaginary when includeImaginary => ImaginaryLineBrush,
            ChangeType.Deleted => DeletedLineBrush,
            ChangeType.Inserted => InsertedLineBrush,
            ChangeType.Modified => ModifiedLineBrush,
            _ => null
        };
    }

    public static IBrush? GetIndicatorBrush(ChangeType changeType)
    {
        return changeType switch
        {
            ChangeType.Deleted => DeletedIndicatorBrush,
            ChangeType.Imaginary => ImaginaryIndicatorBrush,
            ChangeType.Inserted => InsertedIndicatorBrush,
            ChangeType.Modified => ModifiedIndicatorBrush,
            _ => null
        };
    }

    public static IBrush GetNavigationLineBrush(bool isNewText)
    {
        return isNewText ? NewNavigationLineBrush : OldNavigationLineBrush;
    }

    public static IBrush GetNavigationPreviewBrush()
    {
        return NavigationPreviewBrush;
    }

    public static IBrush GetNavigationPreviewBorderBrush()
    {
        return NavigationPreviewBorderBrush;
    }

    public static HighlightData BuildHighlightData(IReadOnlyList<DiffPiece> lines,
        Func<ChangeType, IBrush?>? lineBrushSelector = null, bool skipImaginaryLines = true)
    {
        var getLineBrush = lineBrushSelector ?? (changeType => GetLineBrush(changeType));
        var lineBrushes = new List<(int LineNumber, IBrush LineBrush)>();
        var ranges = new Dictionary<int, List<(int Start, int Length)>>();
        var documentLineNumber = 0;

        foreach (var line in lines)
        {
            if (skipImaginaryLines && line.Type == ChangeType.Imaginary)
            {
                continue;
            }

            documentLineNumber++;

            var brush = getLineBrush(line.Type);
            if (brush == null)
            {
                continue;
            }

            lineBrushes.Add((documentLineNumber, brush));

            var rangeList = BuildSubPieceRanges(line);
            if (rangeList.Count > 0)
            {
                ranges[documentLineNumber] = rangeList;
            }
        }

        return new HighlightData(lineBrushes, ranges);
    }

    public static void Clear(TextEditor editor)
    {
        if (editor.TextArea?.TextView == null)
        {
            return;
        }

        editor.TextArea.TextView.BackgroundRenderers.Clear();
        editor.TextArea.TextView.InvalidateVisual();
    }

    public static void Apply(TextEditor editor, HighlightData highlightData, IBrush rangeBrush)
    {
        Apply(editor, highlightData.LineBrushes, highlightData.Ranges, rangeBrush);
    }

    public static void Apply(TextEditor editor,
        IEnumerable<(int LineNumber, IBrush LineBrush)> lineBrushes,
        Dictionary<int, List<(int Start, int Length)>> ranges, IBrush rangeBrush)
    {
        if (editor.TextArea?.TextView == null)
        {
            return;
        }

        editor.TextArea.TextView.BackgroundRenderers.Clear();
        editor.TextArea.TextView.BackgroundRenderers.Add(new HighlightBackgroundRenderHelper(lineBrushes,
            ranges, rangeBrush));
        editor.TextArea.TextView.InvalidateVisual();
    }

    private static List<(int Start, int Length)> BuildSubPieceRanges(DiffPiece line)
    {
        var ranges = new List<(int Start, int Length)>();
        var currentPosition = 0;

        foreach (var subPiece in line.SubPieces)
        {
            var length = subPiece.Text?.Length ?? 0;
            if (subPiece.Type != ChangeType.Unchanged && length > 0)
            {
                ranges.Add((currentPosition, length));
            }

            currentPosition += length;
        }

        return ranges;
    }

    public KnownLayer Layer => KnownLayer.Background;

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        if (!textView.VisualLinesValid) return;

        foreach (var line in textView.VisualLines)
        {
            // 高亮整行背景
            if (_linesToHighlight.TryGetValue(line.FirstDocumentLine.LineNumber, out var lineBrush))
            {
                var rect = BackgroundGeometryBuilder.GetRectsFromVisualSegment(textView, line, 0, line.VisualLength)
                    .FirstOrDefault();
                if (rect.Width > 0 && rect.Height > 0)
                {
                    drawingContext.DrawRectangle(lineBrush, null, rect);
                }
            }

            // 高亮行中的特定范围
            if (_lineRangesToHighlight.TryGetValue(line.FirstDocumentLine.LineNumber, out var ranges))
                foreach (var (start, length) in ranges)
                {
                    var rects = BackgroundGeometryBuilder.GetRectsFromVisualSegment(textView, line, start,
                        start + length);
                    foreach (var rect in rects) drawingContext.DrawRectangle(_rangeBrush, null, rect);
                }
        }
    }
}