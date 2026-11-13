using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using CSharpRegexTester.Models;

namespace CSharpRegexTester.Controls;

/// <summary>
/// Renders inline highlights over matched character ranges inside a WPF TextBox.
/// </summary>
public sealed class RegexMatchHighlightAdorner : Adorner
{
    public const int MaxRenderMatches = 1000;

    private static readonly Brush MatchFillBrush;
    private static readonly Pen MatchBorderPen;
    private static readonly Brush SelectedMatchFillBrush;
    private static readonly Pen SelectedMatchBorderPen;

    private readonly TextBox textBox;
    private readonly Func<int, bool, Rect> getCharacterRect;
    private IReadOnlyList<TextSpan> matchSpans = [];
    private bool spansAreAscendingNonOverlapping;
    private TextSpan? selectedSpan;

    static RegexMatchHighlightAdorner()
    {
        var matchFill = new SolidColorBrush(Color.FromArgb(65, 255, 213, 79));
        matchFill.Freeze();
        MatchFillBrush = matchFill;

        var matchBorder = new SolidColorBrush(Color.FromArgb(160, 245, 127, 23));
        matchBorder.Freeze();
        MatchBorderPen = new Pen(matchBorder, 1.0);
        MatchBorderPen.Freeze();

        var selectedFill = new SolidColorBrush(Color.FromArgb(110, 66, 165, 245));
        selectedFill.Freeze();
        SelectedMatchFillBrush = selectedFill;

        var selectedBorder = new SolidColorBrush(Color.FromArgb(220, 21, 101, 192));
        selectedBorder.Freeze();
        SelectedMatchBorderPen = new Pen(selectedBorder, 1.5);
        SelectedMatchBorderPen.Freeze();
    }

    public RegexMatchHighlightAdorner(
        TextBox adornedTextBox,
        Func<int, bool, Rect>? characterRectProvider = null)
        : base(adornedTextBox)
    {
        textBox = adornedTextBox;
        getCharacterRect = characterRectProvider ?? textBox.GetRectFromCharacterIndex;
        IsHitTestVisible = false;

        textBox.SizeChanged += (_, _) => InvalidateVisual();
        textBox.AddHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler((_, _) => InvalidateVisual()));
    }

    public void UpdateHighlights(IReadOnlyList<TextSpan> spans, TextSpan? activeSpan = null)
    {
        matchSpans = spans;
        spansAreAscendingNonOverlapping = AreAscendingNonOverlapping(spans);
        selectedSpan = activeSpan;
        InvalidateVisual();
    }

    public void SetSelectedSpan(TextSpan? activeSpan)
    {
        selectedSpan = activeSpan;
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);

        if ((matchSpans.Count == 0 && selectedSpan is null) || string.IsNullOrEmpty(textBox.Text))
        {
            return;
        }

        double width = textBox.ActualWidth;
        double height = textBox.ActualHeight;
        if (width <= 4 || height <= 4)
        {
            return;
        }

        // Visible viewport rect
        var viewportRect = new Rect(0, 0, width, height);

        // Clip to the inner viewport area of the TextBox
        var clipRect = new Rect(2, 2, width - 4, height - 4);
        drawingContext.PushClip(new RectangleGeometry(clipRect));

        try
        {
            int textLength = textBox.Text.Length;

            // Determine visible character range to skip off-screen layout queries
            int firstVisibleLine = textBox.GetFirstVisibleLineIndex();
            int lastVisibleLine = textBox.GetLastVisibleLineIndex();
            int visibleStartChar = -1;
            int visibleEndChar = -1;

            if (firstVisibleLine >= 0 && lastVisibleLine >= firstVisibleLine)
            {
                visibleStartChar = textBox.GetCharacterIndexFromLineIndex(firstVisibleLine);
                int lastLineStart = textBox.GetCharacterIndexFromLineIndex(lastVisibleLine);
                if (lastLineStart >= 0)
                {
                    visibleEndChar = lastLineStart + textBox.GetLineLength(lastVisibleLine);
                }
            }

            int startIndex = 0;
            bool hasVisibleRange = visibleStartChar >= 0 && visibleEndChar >= visibleStartChar;
            if (hasVisibleRange && spansAreAscendingNonOverlapping && matchSpans.Count > 0)
            {
                startIndex = FindFirstVisibleCandidate(matchSpans, visibleStartChar);
            }

            int renderedCount = 0;
            for (int i = startIndex; i < matchSpans.Count && renderedCount < MaxRenderMatches; i++)
            {
                TextSpan span = matchSpans[i];
                if (hasVisibleRange)
                {
                    if (span.Index > visibleEndChar)
                    {
                        if (spansAreAscendingNonOverlapping)
                        {
                            break;
                        }
                        continue;
                    }

                    if (span.Index + span.Length < visibleStartChar)
                    {
                        continue;
                    }
                }

                if (DrawSpan(drawingContext, span, textLength, viewportRect, MatchFillBrush, MatchBorderPen))
                {
                    renderedCount++;
                }
            }

            if (selectedSpan is { } active)
            {
                DrawSpan(drawingContext, active, textLength, viewportRect, SelectedMatchFillBrush, SelectedMatchBorderPen);
            }
        }
        finally
        {
            drawingContext.Pop();
        }
    }

    private static bool AreAscendingNonOverlapping(IReadOnlyList<TextSpan> spans)
    {
        int minNextIndex = 0;
        for (int i = 0; i < spans.Count; i++)
        {
            TextSpan span = spans[i];
            if (span.Length < 0 || span.Index < minNextIndex)
            {
                return false;
            }
            minNextIndex = span.Index + span.Length;
        }
        return true;
    }

    private static int FindFirstVisibleCandidate(IReadOnlyList<TextSpan> spans, int visibleStartChar)
    {
        int lo = 0;
        int hi = spans.Count;
        while (lo < hi)
        {
            int mid = lo + ((hi - lo) / 2);
            TextSpan span = spans[mid];
            if (span.Index + span.Length < visibleStartChar)
            {
                lo = mid + 1;
            }
            else
            {
                hi = mid;
            }
        }
        return lo;
    }

    private bool DrawSpan(
        DrawingContext drawingContext,
        TextSpan span,
        int textLength,
        Rect viewportRect,
        Brush fill,
        Pen pen)
    {
        if (span.Length <= 0 || span.Index < 0 || span.Index + span.Length > textLength)
        {
            return false;
        }

        int startIndex = span.Index;
        int endIndex = span.Index + span.Length;

        Rect startRect = getCharacterRect(startIndex, false);
        Rect endRect = getCharacterRect(endIndex - 1, true);

        if (startRect.IsEmpty || endRect.IsEmpty)
        {
            return false;
        }

        // Fast viewport bounds check: if entirely above or below visible viewport, skip drawing
        double spanTop = Math.Min(startRect.Top, endRect.Top);
        double spanBottom = Math.Max(startRect.Bottom, endRect.Bottom);
        if (spanBottom < viewportRect.Top || spanTop > viewportRect.Bottom)
        {
            return false;
        }

        // Single-line span
        if (Math.Abs(startRect.Top - endRect.Top) < 2.0)
        {
            var rect = new Rect(startRect.TopLeft, endRect.BottomRight);
            if (rect.Width > 0 && rect.Height > 0)
            {
                drawingContext.DrawRoundedRectangle(fill, pen, rect, 2, 2);
                return true;
            }
            return false;
        }

        // Multi-line span: draw coalesced segments by line
        int startLine = textBox.GetLineIndexFromCharacterIndex(startIndex);
        int endLine = textBox.GetLineIndexFromCharacterIndex(endIndex - 1);

        if (startLine >= 0 && endLine >= startLine)
        {
            bool drewAny = false;
            for (int line = startLine; line <= endLine; line++)
            {
                int lineStartChar = textBox.GetCharacterIndexFromLineIndex(line);
                int lineLen = textBox.GetLineLength(line);
                if (lineStartChar < 0 || lineLen <= 0)
                {
                    continue;
                }

                int segStart = Math.Max(startIndex, lineStartChar);
                int segEnd = Math.Min(endIndex, lineStartChar + lineLen);

                // Trim newline characters from the line segment
                while (segEnd > segStart && (textBox.Text[segEnd - 1] == '\r' || textBox.Text[segEnd - 1] == '\n'))
                {
                    segEnd--;
                }

                if (segEnd <= segStart)
                {
                    continue;
                }

                Rect lineStartRect = getCharacterRect(segStart, false);
                Rect lineEndRect = getCharacterRect(segEnd - 1, true);

                if (!lineStartRect.IsEmpty && !lineEndRect.IsEmpty)
                {
                    var segRect = new Rect(lineStartRect.TopLeft, lineEndRect.BottomRight);
                    if (segRect.Width > 0 && segRect.Height > 0 && segRect.IntersectsWith(viewportRect))
                    {
                        drawingContext.DrawRoundedRectangle(fill, pen, segRect, 2, 2);
                        drewAny = true;
                    }
                }
            }
            return drewAny;
        }

        // Fallback if line indexes are not available
        bool drewFallback = false;
        Rect currentLineRect = startRect;
        for (int i = startIndex; i < endIndex; i++)
        {
            Rect charStart = getCharacterRect(i, false);
            Rect charEnd = getCharacterRect(i, true);
            if (charStart.IsEmpty || charEnd.IsEmpty)
            {
                continue;
            }

            var charRect = new Rect(charStart.TopLeft, charEnd.BottomRight);
            if (Math.Abs(charRect.Top - currentLineRect.Top) < 2.0)
            {
                currentLineRect.Union(charRect);
            }
            else
            {
                if (currentLineRect.Width > 0 && currentLineRect.Height > 0 && currentLineRect.IntersectsWith(viewportRect))
                {
                    drawingContext.DrawRoundedRectangle(fill, pen, currentLineRect, 2, 2);
                    drewFallback = true;
                }
                currentLineRect = charRect;
            }
        }

        if (currentLineRect.Width > 0 && currentLineRect.Height > 0 && currentLineRect.IntersectsWith(viewportRect))
        {
            drawingContext.DrawRoundedRectangle(fill, pen, currentLineRect, 2, 2);
            drewFallback = true;
        }

        return drewFallback;
    }
}
