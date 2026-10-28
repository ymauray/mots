using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace Mots;

/// <summary>
/// Dessine par-dessus l'éditeur les caractères non imprimables (espaces, espaces insécables, tabulations,
/// sauts de ligne, fins de paragraphe), comme le bouton ¶ de Word. Seule la partie visible du document est parcourue.
/// </summary>
public sealed class FormattingMarksOverlay : FrameworkElement
{
    private const double NarrowSpaceScale = 0.7;

    private static readonly Brush MarkBrush = CreateBrush();
    private static readonly Typeface MarkTypeface = new("Georgia");

    private readonly Dictionary<(string Glyph, double Size), FormattedText> _glyphs = [];
    private RichTextBox? _target;

    public FormattingMarksOverlay()
    {
        IsHitTestVisible = false;
    }

    public RichTextBox? Target
    {
        get => _target;
        set
        {
            if (_target is not null)
            {
                _target.RemoveHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler(OnTargetChanged));
                _target.TextChanged -= OnTargetChanged;
                _target.SizeChanged -= OnTargetChanged;
            }
            _target = value;
            if (_target is not null)
            {
                _target.AddHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler(OnTargetChanged));
                _target.TextChanged += OnTargetChanged;
                _target.SizeChanged += OnTargetChanged;
            }
        }
    }

    private static Brush CreateBrush()
    {
        var brush = new SolidColorBrush(Color.FromArgb(190, 91, 132, 173));
        brush.Freeze();
        return brush;
    }

    private void OnTargetChanged(object sender, EventArgs e)
    {
        if (IsVisible)
            InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        if (_target is not { IsLoaded: true }) return;

        // L'éditeur rogne son contenu sous son remplissage : on dessine dans la même zone.
        var padding = _target.Padding;
        var area = new Rect(padding.Left, padding.Top,
            Math.Max(0, ActualWidth - padding.Left - padding.Right),
            Math.Max(0, ActualHeight - padding.Top - padding.Bottom));

        dc.PushClip(new RectangleGeometry(area));
        try
        {
            DrawMarks(dc, area);
        }
        finally
        {
            dc.Pop();
        }
    }

    private void DrawMarks(DrawingContext dc, Rect area)
    {
        if (_target is null) return;
        var first = _target.GetPositionFromPoint(new Point(area.Left, area.Top), true);
        var last = _target.GetPositionFromPoint(new Point(area.Right, area.Bottom), true);
        if (first is null || last is null) return;

        // On va jusqu'à la fin du dernier paragraphe visible pour ne pas perdre son « ¶ ».
        var limit = last.Paragraph?.ContentEnd ?? last;
        var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;

        for (var pos = first.Paragraph?.ContentStart ?? first;
             pos is not null && pos.CompareTo(limit) <= 0;
             pos = pos.GetNextContextPosition(LogicalDirection.Forward))
        {
            switch (pos.GetPointerContext(LogicalDirection.Forward))
            {
                case TextPointerContext.Text:
                    DrawTextMarks(dc, pos, area, dpi);
                    break;
                case TextPointerContext.ElementStart when pos.GetAdjacentElement(LogicalDirection.Forward) is LineBreak:
                    DrawAtStart(dc, pos, "↵", area, dpi);
                    break;
                case TextPointerContext.ElementEnd when pos.GetAdjacentElement(LogicalDirection.Forward) is Paragraph paragraph:
                    DrawParagraphEnd(dc, paragraph, area, dpi);
                    break;
            }
        }
    }

    private void DrawTextMarks(DrawingContext dc, TextPointer run, Rect area, double dpi)
    {
        var text = run.GetTextInRun(LogicalDirection.Forward);
        var paragraph = run.Paragraph;
        var size = paragraph?.FontSize ?? 16;

        for (var i = 0; i < text.Length; i++)
        {
            var (glyph, scale) = text[i] switch
            {
                ' ' => ("·", 1.0),
                ' ' => ("°", 1.0),
                ' ' => ("°", NarrowSpaceScale),
                '\t' => ("→", 1.0),
                _ => (null, 0.0),
            };
            if (glyph is null) continue;

            var start = run.GetPositionAtOffset(i);
            var rect = start?.GetCharacterRect(LogicalDirection.Forward) ?? Rect.Empty;
            if (rect.IsEmpty || rect.Bottom < area.Top || rect.Top > area.Bottom) continue;

            // Largeur nominale de l'espace (la justification l'étire, mais mesurer chaque espace coûterait trop cher).
            var width = size * 0.25;

            var ft = Glyph(glyph, size * scale, dpi);
            dc.DrawText(ft, new Point(rect.Left + (width - ft.Width) / 2, rect.Top));
        }
    }

    private void DrawAtStart(DrawingContext dc, TextPointer position, string glyph, Rect area, double dpi)
    {
        var rect = position.GetCharacterRect(LogicalDirection.Forward);
        if (rect.IsEmpty || rect.Bottom < area.Top || rect.Top > area.Bottom) return;
        dc.DrawText(Glyph(glyph, position.Paragraph?.FontSize ?? 16, dpi), new Point(rect.Left, rect.Top));
    }

    private void DrawParagraphEnd(DrawingContext dc, Paragraph paragraph, Rect area, double dpi)
    {
        var end = paragraph.ContentEnd.GetInsertionPosition(LogicalDirection.Backward);
        var rect = end.GetCharacterRect(LogicalDirection.Backward);
        if (rect.IsEmpty || rect.Bottom < area.Top || rect.Top > area.Bottom) return;
        dc.DrawText(Glyph("¶", paragraph.FontSize, dpi), new Point(rect.Right + 1, rect.Top));
    }

    private FormattedText Glyph(string glyph, double size, double dpi)
    {
        if (!_glyphs.TryGetValue((glyph, size), out var text))
        {
            text = new FormattedText(glyph, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                MarkTypeface, size, MarkBrush, dpi);
            _glyphs[(glyph, size)] = text;
        }
        return text;
    }
}
