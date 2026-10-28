using System.Windows;
using System.Windows.Documents;
using NUnit.Framework;

namespace Mots.Tests;

public class ParagraphStylesTests
{
    [Test]
    public void All_contains_the_three_styles_in_display_order()
    {
        Assert.That(ParagraphStyles.All.Select(d => d.Kind),
            Is.EqualTo(new[] { StyleKind.Normal, StyleKind.Heading1, StyleKind.Ellipse }));
    }

    [Test]
    public void Every_kind_has_a_definition()
    {
        foreach (var kind in Enum.GetValues<StyleKind>())
            Assert.That(ParagraphStyles.Get(kind).Kind, Is.EqualTo(kind));
    }

    [Test]
    public void Word_ids_and_names_are_unique()
    {
        Assert.That(ParagraphStyles.All.Select(d => d.WordId).Distinct().Count(), Is.EqualTo(ParagraphStyles.All.Count));
        Assert.That(ParagraphStyles.All.Select(d => d.WordName).Distinct().Count(), Is.EqualTo(ParagraphStyles.All.Count));
    }

    [Test]
    public void Normal_definition()
    {
        var d = ParagraphStyles.Normal;
        Assert.Multiple(() =>
        {
            Assert.That(d.WordId, Is.EqualTo("Normal"));
            Assert.That(d.WordName, Is.EqualTo("Normal"));
            Assert.That(d.DisplayName, Is.EqualTo("Normal"));
            Assert.That(d.FontSizePt, Is.EqualTo(12));
            Assert.That(d.Bold, Is.False);
            Assert.That(d.Alignment, Is.EqualTo(TextAlignment.Justify));
            Assert.That(d.FirstLineIndentCm, Is.EqualTo(0.5));
            Assert.That(d.SpaceBeforePt, Is.EqualTo(0));
            Assert.That(d.SpaceAfterPt, Is.EqualTo(6));
            Assert.That(d.LineSpacing, Is.EqualTo(1.5));
            Assert.That(d.PageBreakBefore, Is.False);
            Assert.That(d.OutlineLevel, Is.Null);
            Assert.That(d.NextKind, Is.EqualTo(StyleKind.Normal));
            Assert.That(d.IsCustom, Is.False);
        });
    }

    [Test]
    public void Heading1_definition()
    {
        var d = ParagraphStyles.Heading1;
        Assert.Multiple(() =>
        {
            Assert.That(d.WordId, Is.EqualTo("Titre1"));
            Assert.That(d.WordName, Is.EqualTo("heading 1"));
            Assert.That(d.DisplayName, Is.EqualTo("Titre 1"));
            Assert.That(d.FontSizePt, Is.EqualTo(18));
            Assert.That(d.Bold, Is.True);
            Assert.That(d.Alignment, Is.EqualTo(TextAlignment.Left));
            Assert.That(d.SpaceBeforePt, Is.EqualTo(24));
            Assert.That(d.SpaceAfterPt, Is.EqualTo(12));
            Assert.That(d.PageBreakBefore, Is.True);
            Assert.That(d.OutlineLevel, Is.EqualTo(0));
            Assert.That(d.NextKind, Is.EqualTo(StyleKind.Normal));
            Assert.That(d.IsCustom, Is.False);
        });
    }

    [Test]
    public void Ellipse_definition()
    {
        var d = ParagraphStyles.Ellipse;
        Assert.Multiple(() =>
        {
            Assert.That(d.WordId, Is.EqualTo("Ellipse"));
            Assert.That(d.Alignment, Is.EqualTo(TextAlignment.Center));
            Assert.That(d.FirstLineIndentCm, Is.EqualTo(0));
            Assert.That(d.SpaceBeforePt, Is.EqualTo(12));
            Assert.That(d.SpaceAfterPt, Is.EqualTo(12));
            Assert.That(d.OutlineLevel, Is.Null);
            Assert.That(d.NextKind, Is.EqualTo(StyleKind.Normal));
            Assert.That(d.IsCustom, Is.True);
        });
    }

    [TestCase(StyleKind.Normal)]
    [TestCase(StyleKind.Heading1)]
    [TestCase(StyleKind.Ellipse)]
    public void Apply_tags_the_paragraph_and_KindOf_reads_it_back(StyleKind kind)
    {
        var paragraph = new Paragraph();
        ParagraphStyles.Apply(paragraph, kind);

        Assert.That(paragraph.Tag, Is.EqualTo(ParagraphStyles.Get(kind).WordId));
        Assert.That(ParagraphStyles.KindOf(paragraph), Is.EqualTo(kind));
    }

    [Test]
    public void KindOf_defaults_to_Normal_without_tag_or_with_unknown_tag()
    {
        Assert.That(ParagraphStyles.KindOf(new Paragraph()), Is.EqualTo(StyleKind.Normal));
        Assert.That(ParagraphStyles.KindOf(new Paragraph { Tag = "Inconnu" }), Is.EqualTo(StyleKind.Normal));
        Assert.That(ParagraphStyles.KindOf(new Paragraph { Tag = 42 }), Is.EqualTo(StyleKind.Normal));
    }

    [Test]
    public void Apply_uses_georgia_and_converts_points_to_pixels()
    {
        var paragraph = new Paragraph();
        ParagraphStyles.Apply(paragraph, StyleKind.Normal);

        Assert.Multiple(() =>
        {
            Assert.That(paragraph.FontFamily.Source, Is.EqualTo("Georgia"));
            Assert.That(paragraph.FontSize, Is.EqualTo(16).Within(1e-9)); // 12 pt = 16 px
            Assert.That(paragraph.TextIndent, Is.EqualTo(0.5 * 96 / 2.54).Within(1e-9));
            Assert.That(paragraph.Margin, Is.EqualTo(new Thickness(0, 0, 0, 8))); // 6 pt = 8 px
            Assert.That(paragraph.TextAlignment, Is.EqualTo(TextAlignment.Justify));
            Assert.That(paragraph.FontWeight, Is.EqualTo(FontWeights.Normal));
            Assert.That(paragraph.MinWidowLines, Is.EqualTo(2));
            Assert.That(paragraph.MinOrphanLines, Is.EqualTo(2));
            Assert.That(paragraph.BreakPageBefore, Is.False);
            Assert.That(paragraph.LineStackingStrategy, Is.EqualTo(LineStackingStrategy.BlockLineHeight));
        });
    }

    [Test]
    public void Apply_Heading1_sets_size_weight_margins_and_page_break()
    {
        var paragraph = new Paragraph();
        ParagraphStyles.Apply(paragraph, StyleKind.Heading1);

        Assert.Multiple(() =>
        {
            Assert.That(paragraph.FontSize, Is.EqualTo(24).Within(1e-9)); // 18 pt
            Assert.That(paragraph.FontWeight, Is.EqualTo(FontWeights.Bold));
            Assert.That(paragraph.Margin, Is.EqualTo(new Thickness(0, 32, 0, 16)));
            Assert.That(paragraph.TextAlignment, Is.EqualTo(TextAlignment.Left));
            Assert.That(paragraph.BreakPageBefore, Is.True);
        });
    }

    [Test]
    public void Apply_line_height_scales_with_font_size()
    {
        var normal = new Paragraph();
        var heading = new Paragraph();
        ParagraphStyles.Apply(normal, StyleKind.Normal);
        ParagraphStyles.Apply(heading, StyleKind.Heading1);

        Assert.That(heading.LineHeight / normal.LineHeight, Is.EqualTo(24.0 / 16).Within(1e-9));
    }

    [Test]
    public void Apply_can_switch_a_paragraph_back_and_forth()
    {
        var paragraph = new Paragraph();
        ParagraphStyles.Apply(paragraph, StyleKind.Heading1);
        ParagraphStyles.Apply(paragraph, StyleKind.Normal);

        Assert.Multiple(() =>
        {
            Assert.That(paragraph.FontWeight, Is.EqualTo(FontWeights.Normal));
            Assert.That(paragraph.BreakPageBefore, Is.False);
            Assert.That(paragraph.FontSize, Is.EqualTo(16).Within(1e-9));
            Assert.That(ParagraphStyles.KindOf(paragraph), Is.EqualTo(StyleKind.Normal));
        });
    }

    [Test]
    public void Twips_constant_matches_one_centimetre()
    {
        Assert.That(ParagraphStyles.TwipsPerCm, Is.EqualTo(1440 / 2.54).Within(0.001));
    }
}
