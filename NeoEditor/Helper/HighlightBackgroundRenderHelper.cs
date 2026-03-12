using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Media;
using Avalonia.Media.Immutable;
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

    private Dictionary<int, List<(int Start, int Length)>> _lineRangesToHighlight;
    private Dictionary<int, IBrush> _linesToHighlight;
    private IBrush _rangeBrush;
    private int _navigationStartLineNumber;
    private int _navigationEndLineNumber;
    private IBrush? _navigationLineBrush;

    private HighlightBackgroundRenderHelper(HighlightData highlightData, IBrush rangeBrush,
        int navigationStartLineNumber = 0, int navigationEndLineNumber = 0, IBrush? navigationLineBrush = null)
    {
        _linesToHighlight = [];
        _lineRangesToHighlight = [];
        _rangeBrush = rangeBrush;
        Update(highlightData, rangeBrush, navigationStartLineNumber, navigationEndLineNumber, navigationLineBrush);
    }

    public static IBrush CreateBrush(string color)
    {
        return new ImmutableSolidColorBrush(Color.Parse(color));
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

        var renderers = editor.TextArea.TextView.BackgroundRenderers
            .OfType<HighlightBackgroundRenderHelper>()
            .ToList();
        foreach (var renderer in renderers)
        {
            editor.TextArea.TextView.BackgroundRenderers.Remove(renderer);
        }

        editor.TextArea.TextView.InvalidateVisual();
    }

    public static void Apply(TextEditor editor, HighlightData highlightData, IBrush rangeBrush,
        int navigationStartLineNumber = 0, int navigationEndLineNumber = 0, IBrush? navigationLineBrush = null)
    {
        if (editor.TextArea?.TextView == null)
        {
            return;
        }

        var renderer = editor.TextArea.TextView.BackgroundRenderers
            .OfType<HighlightBackgroundRenderHelper>()
            .FirstOrDefault();
        if (renderer == null)
        {
            renderer = new HighlightBackgroundRenderHelper(highlightData, rangeBrush,
                navigationStartLineNumber, navigationEndLineNumber, navigationLineBrush);
            editor.TextArea.TextView.BackgroundRenderers.Add(renderer);
        }
        else
        {
            renderer.Update(highlightData, rangeBrush, navigationStartLineNumber, navigationEndLineNumber,
                navigationLineBrush);
        }

        editor.TextArea.TextView.InvalidateVisual();
    }

    private void Update(HighlightData highlightData, IBrush rangeBrush, int navigationStartLineNumber,
        int navigationEndLineNumber, IBrush? navigationLineBrush)
    {
        _linesToHighlight = highlightData.LineBrushes.ToDictionary(tuple => tuple.LineNumber, tuple => tuple.LineBrush);
        _lineRangesToHighlight = highlightData.Ranges.ToDictionary(entry => entry.Key, entry => entry.Value.ToList());
        _rangeBrush = rangeBrush;
        _navigationStartLineNumber = navigationStartLineNumber;
        _navigationEndLineNumber = navigationEndLineNumber;
        _navigationLineBrush = navigationStartLineNumber > 0 && navigationEndLineNumber >= navigationStartLineNumber
            ? navigationLineBrush
            : null;
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
            var lineNumber = line.FirstDocumentLine.LineNumber;

            // 高亮整行背景
            var hasNavigationOverlay = _navigationLineBrush != null &&
                                       lineNumber >= _navigationStartLineNumber &&
                                       lineNumber <= _navigationEndLineNumber;
            IBrush? lineBrush = null;
            if (hasNavigationOverlay)
            {
                lineBrush = _navigationLineBrush;
            }
            else
            {
                _linesToHighlight.TryGetValue(lineNumber, out lineBrush);
            }

            if (lineBrush != null)
            {
                var rect = BackgroundGeometryBuilder.GetRectsFromVisualSegment(textView, line, 0, line.VisualLength)
                    .FirstOrDefault();
                if (rect.Width > 0 && rect.Height > 0)
                {
                    drawingContext.DrawRectangle(lineBrush, null, rect);
                }
            }

            // 高亮行中的特定范围
            if (_lineRangesToHighlight.TryGetValue(lineNumber, out var ranges))
                foreach (var (start, length) in ranges)
                {
                    var rects = BackgroundGeometryBuilder.GetRectsFromVisualSegment(textView, line, start,
                        start + length);
                    foreach (var rect in rects) drawingContext.DrawRectangle(_rangeBrush, null, rect);
                }
        }
    }
}