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
    private static readonly Brush MatchFillBrush;
    private static readonly Pen MatchBorderPen;
    private static readonly Brush SelectedMatchFillBrush;
    private static readonly Pen SelectedMatchBorderPen;

    private readonly TextBox textBox;
    private readonly Func<int, bool, Rect> getCharacterRect;
    private IReadOnlyList<TextSpan> matchSpans = [];
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

        // Clip to the inner viewport area of the TextBox
        var clipRect = new Rect(2, 2, Math.Max(0, textBox.ActualWidth - 4), Math.Max(0, textBox.ActualHeight - 4));
        drawingContext.PushClip(new RectangleGeometry(clipRect));

        try
        {
            int textLength = textBox.Text.Length;

            foreach (TextSpan span in matchSpans)
            {
                DrawSpan(drawingContext, span, textLength, MatchFillBrush, MatchBorderPen);
            }

            if (selectedSpan is { } active)
            {
                DrawSpan(drawingContext, active, textLength, SelectedMatchFillBrush, SelectedMatchBorderPen);
            }
        }
        finally
        {
            drawingContext.Pop();
        }
    }

    private void DrawSpan(DrawingContext drawingContext, TextSpan span, int textLength, Brush fill, Pen pen)
    {
        if (span.Length <= 0 || span.Index < 0 || span.Index + span.Length > textLength)
        {
            return;
        }

        int startIndex = span.Index;
        int endIndex = span.Index + span.Length;

        Rect startRect = getCharacterRect(startIndex, false);
        Rect endRect = getCharacterRect(endIndex - 1, true);

        if (startRect.IsEmpty || endRect.IsEmpty)
        {
            return;
        }

        // Single-line span
        if (Math.Abs(startRect.Top - endRect.Top) < 2.0)
        {
            var rect = new Rect(startRect.TopLeft, endRect.BottomRight);
            if (rect.Width > 0 && rect.Height > 0)
            {
                drawingContext.DrawRoundedRectangle(fill, pen, rect, 2, 2);
            }
            return;
        }

        // Multi-line span: draw per-line segments
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
                if (currentLineRect.Width > 0 && currentLineRect.Height > 0)
                {
                    drawingContext.DrawRoundedRectangle(fill, pen, currentLineRect, 2, 2);
                }
                currentLineRect = charRect;
            }
        }

        if (currentLineRect.Width > 0 && currentLineRect.Height > 0)
        {
            drawingContext.DrawRoundedRectangle(fill, pen, currentLineRect, 2, 2);
        }
    }
}
