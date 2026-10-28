using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace Mots;

public enum StyleKind { Normal, Heading1, Ellipse }

/// <summary>
/// Les styles de paragraphe de Mots : source unique pour l'affichage dans l'éditeur (WPF)
/// et pour les styles écrits dans le .docx.
/// </summary>
public static class ParagraphStyles
{
    private const double PixelsPerPoint = 4.0 / 3;
    private const double PixelsPerCm = 96 / 2.54;
    public const double TwipsPerCm = 566.929;
    public const string FontName = "Georgia";

    private static readonly FontFamily Family = new(FontName);

    /// <param name="WordId">Identifiant du style dans le .docx (celui que Word en français donne à « Titre 1 »).</param>
    /// <param name="WordName">Nom interne du style (Word l'affiche localisé : « Titre 1 »).</param>
    /// <param name="LineSpacing">Multiple de l'interligne simple (1,5 = « 1,5 ligne »).</param>
    /// <param name="NextKind">Style du paragraphe créé par « Entrée » en fin de paragraphe.</param>
    /// <param name="IsCustom">Style personnalisé, par opposition aux styles intégrés de Word.</param>
    public sealed record Definition(
        StyleKind Kind, string WordId, string WordName, string DisplayName,
        double FontSizePt, bool Bold, TextAlignment Alignment, double FirstLineIndentCm,
        double SpaceBeforePt, double SpaceAfterPt, double LineSpacing,
        bool PageBreakBefore, int? OutlineLevel, StyleKind NextKind, bool IsCustom);

    public static Definition Normal { get; } = new(
        StyleKind.Normal, "Normal", "Normal", "Normal",
        FontSizePt: 12, Bold: false, TextAlignment.Justify, FirstLineIndentCm: 0.5,
        SpaceBeforePt: 0, SpaceAfterPt: 6, LineSpacing: 1.5,
        PageBreakBefore: false, OutlineLevel: null, StyleKind.Normal, IsCustom: false);

    public static Definition Heading1 { get; } = new(
        StyleKind.Heading1, "Titre1", "heading 1", "Titre 1",
        FontSizePt: 18, Bold: true, TextAlignment.Left, FirstLineIndentCm: 0.5,
        SpaceBeforePt: 24, SpaceAfterPt: 12, LineSpacing: 1.5,
        PageBreakBefore: true, OutlineLevel: 0, StyleKind.Normal, IsCustom: false);

    /// <summary>Séparateur de scène (« *** »).</summary>
    public static Definition Ellipse { get; } = new(
        StyleKind.Ellipse, "Ellipse", "Ellipse", "Ellipse",
        FontSizePt: 12, Bold: false, TextAlignment.Center, FirstLineIndentCm: 0,
        SpaceBeforePt: 12, SpaceAfterPt: 12, LineSpacing: 1.5,
        PageBreakBefore: false, OutlineLevel: null, StyleKind.Normal, IsCustom: true);

    public static IReadOnlyList<Definition> All { get; } = [Normal, Heading1, Ellipse];

    public static Definition Get(StyleKind kind) => All.First(d => d.Kind == kind);

    /// <summary>Style d'un paragraphe ; sans marque, c'est « Normal ».</summary>
    public static StyleKind KindOf(Paragraph paragraph) =>
        All.FirstOrDefault(d => d.WordId == paragraph.Tag as string)?.Kind ?? StyleKind.Normal;

    public static void Apply(Paragraph paragraph, StyleKind kind)
    {
        var d = Get(kind);
        paragraph.Tag = d.WordId;

        paragraph.FontFamily = Family;
        paragraph.FontSize = d.FontSizePt * PixelsPerPoint;
        paragraph.FontWeight = d.Bold ? FontWeights.Bold : FontWeights.Normal;
        paragraph.TextAlignment = d.Alignment;
        paragraph.TextIndent = d.FirstLineIndentCm * PixelsPerCm;
        paragraph.Margin = new Thickness(0, d.SpaceBeforePt * PixelsPerPoint, 0, d.SpaceAfterPt * PixelsPerPoint);

        // L'interligne de Word est un multiple de la hauteur de ligne naturelle de la police.
        paragraph.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
        paragraph.LineHeight = d.LineSpacing * Family.LineSpacing * paragraph.FontSize;

        paragraph.MinWidowLines = 2;
        paragraph.MinOrphanLines = 2;
        paragraph.BreakPageBefore = d.PageBreakBefore;
    }
}
