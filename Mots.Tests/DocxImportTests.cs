using NUnit.Framework;
using static Mots.Tests.WordFile;

namespace Mots.Tests;

/// <summary>Reconnaissance des styles Word à l'import (par nom, pas par identifiant) et rejet des styles inconnus.</summary>
public class DocxImportTests : FileTestBase
{
    private List<StyleKind> ImportKinds(IEnumerable<DocumentFormat.OpenXml.OpenXmlElement> body, IEnumerable<DocumentFormat.OpenXml.Wordprocessing.Style>? styles)
    {
        var path = Create(PathOf($"{Guid.NewGuid():N}.docx"), body, styles);
        return Docs.Kinds(DocxConverter.Import(path));
    }

    private IncompatibleFileException ImportFailure(IEnumerable<DocumentFormat.OpenXml.OpenXmlElement> body, IEnumerable<DocumentFormat.OpenXml.Wordprocessing.Style>? styles)
    {
        var path = Create(PathOf($"{Guid.NewGuid():N}.docx"), body, styles);
        return Assert.Throws<IncompatibleFileException>(() => DocxConverter.Import(path))!;
    }

    // ---------- Reconnaissance ----------

    [TestCase("Titre1", "heading 1")]
    [TestCase("Heading1", "heading 1")]
    [TestCase("Titre1", "Heading 1")]
    [TestCase("MonTitre", "HEADING 1")]
    public void Heading_is_recognised_by_name_whatever_the_id_or_case(string id, string name)
    {
        var kinds = ImportKinds([P(id, "T")], [ParaStyle("Normal", "Normal", true), ParaStyle(id, name)]);

        Assert.That(kinds, Is.EqualTo(new[] { StyleKind.Heading1 }));
    }

    [Test]
    public void Normal_is_recognised_by_name_with_a_different_id()
    {
        var kinds = ImportKinds([P("Standard", "x")], [ParaStyle("Standard", "Normal", true)]);

        Assert.That(kinds, Is.EqualTo(new[] { StyleKind.Normal }));
    }

    [Test]
    public void Ellipse_is_recognised_by_name_ignoring_case()
    {
        var kinds = ImportKinds([P("e1", "***")], [ParaStyle("Normal", "Normal", true), ParaStyle("e1", "ELLIPSE")]);

        Assert.That(kinds, Is.EqualTo(new[] { StyleKind.Ellipse }));
    }

    [Test]
    public void Name_wins_over_a_misleading_id()
    {
        // L'identifiant « Titre1 » désigne ici un style nommé « Ellipse ».
        var kinds = ImportKinds([P("Titre1", "***")], [ParaStyle("Normal", "Normal", true), ParaStyle("Titre1", "Ellipse")]);

        Assert.That(kinds, Is.EqualTo(new[] { StyleKind.Ellipse }));
    }

    [Test]
    public void Paragraph_without_style_takes_the_document_default_style()
    {
        var kinds = ImportKinds([P(null, "x")], [ParaStyle("Standard", "Normal", true)]);

        Assert.That(kinds, Is.EqualTo(new[] { StyleKind.Normal }));
    }

    [Test]
    public void Unstyled_paragraph_in_a_document_whose_default_style_is_unknown_is_rejected()
    {
        var failure = ImportFailure([P(null, "x")], [ParaStyle("Corps", "Body Text", true)]);

        Assert.That(failure.Message, Does.Contain("« Body Text » (1 paragraphe)"));
    }

    [Test]
    public void Unstyled_paragraph_without_any_default_is_normal()
    {
        var kinds = ImportKinds([P(null, "x")], [ParaStyle("Titre1", "heading 1")]);

        Assert.That(kinds, Is.EqualTo(new[] { StyleKind.Normal }));
    }

    [Test]
    public void Style_order_in_the_document_is_preserved()
    {
        var kinds = ImportKinds(
            [P("Titre1", "a"), P(null, "b"), P("Ellipse", "c"), P(null, "d"), P("Titre1", "e")],
            [.. StandardStyles(), ParaStyle("Ellipse", "Ellipse")]);

        Assert.That(kinds, Is.EqualTo(new[] { StyleKind.Heading1, StyleKind.Normal, StyleKind.Ellipse, StyleKind.Normal, StyleKind.Heading1 }));
    }

    // ---------- Rejets ----------

    [Test]
    public void Unknown_paragraph_style_is_rejected_with_a_count()
    {
        var styles = StandardStyles();
        styles.Add(ParaStyle("Citation", "Quote"));

        var failure = ImportFailure([P("Citation", "a"), P("Citation", "b"), P("Citation", "c")], styles);

        Assert.That(failure.Message, Is.EqualTo(
            "Ce document utilise des styles qui n'existent pas dans Mots : « Quote » (3 paragraphes). " +
            "Remplacez-les dans Word par « Normal » ou « Titre 1 », puis réessayez."));
    }

    [Test]
    public void A_single_paragraph_uses_the_singular()
    {
        var styles = StandardStyles();
        styles.Add(ParaStyle("Citation", "Quote"));

        Assert.That(ImportFailure([P("Citation", "a")], styles).Message, Does.Contain("(1 paragraphe)").And.Not.Contain("paragraphes"));
    }

    [Test]
    public void Unknown_character_style_is_rejected_with_a_count()
    {
        var styles = StandardStyles();
        styles.Add(CharStyle("Accent", "Emphasis"));

        var failure = ImportFailure([P(null, R("a", charStyle: "Accent"), R("b", charStyle: "Accent"))], styles);

        Assert.That(failure.Message, Does.Contain("« Emphasis » (2 passages)"));
    }

    [Test]
    public void A_single_run_uses_the_singular()
    {
        var styles = StandardStyles();
        styles.Add(CharStyle("Accent", "Emphasis"));

        Assert.That(ImportFailure([P(null, R("a", charStyle: "Accent"))], styles).Message, Does.Contain("(1 passage)").And.Not.Contain("passages"));
    }

    [Test]
    public void Default_paragraph_font_character_style_is_tolerated()
    {
        var kinds = ImportKinds([P(null, R("a", charStyle: "Policepardfaut"))], StandardStyles());

        Assert.That(kinds, Is.EqualTo(new[] { StyleKind.Normal }));
    }

    [Test]
    public void Default_paragraph_font_is_tolerated_whatever_its_case()
    {
        var styles = StandardStyles();
        styles.Add(CharStyle("dpf", "default paragraph font"));

        Assert.That(ImportKinds([P(null, R("a", charStyle: "dpf"))], styles), Is.EqualTo(new[] { StyleKind.Normal }));
    }

    [Test]
    public void Unknown_character_style_without_definition_is_reported_by_id()
    {
        var failure = ImportFailure([P(null, R("a", charStyle: "Fantome"))], StandardStyles());

        Assert.That(failure.Message, Does.Contain("« Fantome » (1 passage)"));
    }

    [Test]
    public void Paragraph_and_character_styles_are_reported_together()
    {
        var styles = StandardStyles();
        styles.Add(ParaStyle("Citation", "Quote"));
        styles.Add(CharStyle("Accent", "Emphasis"));

        var failure = ImportFailure([P("Citation", R("a", charStyle: "Accent"))], styles);

        Assert.That(failure.Message, Does.Contain("« Quote » (1 paragraphe)").And.Contain("« Emphasis » (1 passage)"));
    }

    [Test]
    public void At_most_six_styles_are_listed_then_the_rest_is_counted()
    {
        var styles = StandardStyles();
        var body = new List<DocumentFormat.OpenXml.OpenXmlElement>();
        for (var i = 1; i <= 9; i++)
        {
            styles.Add(ParaStyle($"S{i}", $"Style {i}"));
            body.Add(P($"S{i}", "x"));
        }

        var message = ImportFailure(body, styles).Message;

        Assert.Multiple(() =>
        {
            for (var i = 1; i <= 6; i++)
                Assert.That(message, Does.Contain($"« Style {i} »"));
            Assert.That(message, Does.Not.Contain("« Style 7 »"));
            Assert.That(message, Does.Contain("et 3 autre(s)"));
        });
    }

    [Test]
    public void Exactly_six_styles_have_no_remainder()
    {
        var styles = StandardStyles();
        var body = new List<DocumentFormat.OpenXml.OpenXmlElement>();
        for (var i = 1; i <= 6; i++)
        {
            styles.Add(ParaStyle($"S{i}", $"Style {i}"));
            body.Add(P($"S{i}", "x"));
        }

        Assert.That(ImportFailure(body, styles).Message, Does.Not.Contain("autre(s)"));
    }

    [Test]
    public void The_whole_document_is_scanned_before_rejecting()
    {
        var styles = StandardStyles();
        styles.Add(ParaStyle("A", "Alpha"));
        styles.Add(ParaStyle("B", "Beta"));

        var message = ImportFailure([P("A", "1"), P(null, "ok"), P("B", "2")], styles).Message;

        Assert.That(message, Does.Contain("« Alpha »").And.Contain("« Beta »"));
    }

    [Test]
    public void Unknown_styles_inside_tables_are_rejected_too()
    {
        var styles = StandardStyles();
        styles.Add(ParaStyle("Citation", "Quote"));
        var table = new DocumentFormat.OpenXml.Wordprocessing.Table(
            new DocumentFormat.OpenXml.Wordprocessing.TableRow(
                new DocumentFormat.OpenXml.Wordprocessing.TableCell(P("Citation", "x"))));

        Assert.That(ImportFailure([table], styles).Message, Does.Contain("« Quote »"));
    }

    // ---------- Load vs Import ----------

    [Test]
    public void Load_also_rejects_unknown_styles_even_with_the_marker()
    {
        var styles = StandardStyles();
        styles.Add(ParaStyle("Citation", "Quote"));
        var path = Create(PathOf("m.docx"), [P("Citation", "x")], styles, Marker(MotsMetadata.CurrentFormatVersion));

        Assert.That(() => DocxConverter.Load(path), Throws.InstanceOf<IncompatibleFileException>().With.Message.Contain("« Quote »"));
    }

    [Test]
    public void Legacy_Heading1_id_files_remain_readable()
    {
        // Les fichiers écrits avant le passage à l'identifiant « Titre1 » utilisaient « Heading1 ».
        var path = Create(PathOf("old.docx"), [P("Heading1", "T"), P(null, "x")],
            [ParaStyle("Normal", "Normal", true), ParaStyle("Heading1", "heading 1")], Marker(1));

        Assert.That(Docs.Kinds(DocxConverter.Load(path)), Is.EqualTo(new[] { StyleKind.Heading1, StyleKind.Normal }));
    }
}
