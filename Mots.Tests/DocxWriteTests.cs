using System.Windows;
using System.Windows.Documents;
using System.Xml.Linq;
using NUnit.Framework;
using static Mots.Tests.DocxInspector;

namespace Mots.Tests;

public class DocxWriteTests : FileTestBase
{
    private string Save(FlowDocument document, string name = "doc.docx")
    {
        var path = PathOf(name);
        DocxConverter.Save(document, path);
        return path;
    }

    // ---------- Structure du paquet ----------

    [Test]
    public void Saved_file_passes_OpenXml_validation()
    {
        var path = Save(Docs.Doc(
            Docs.P(StyleKind.Heading1, "Chapitre"),
            Docs.P(StyleKind.Normal, "Texte"),
            Docs.P(StyleKind.Ellipse, "***")));

        Assert.That(OpenXmlCheck.Errors(path), Is.Empty);
    }

    [Test]
    public void Saved_file_with_formatting_passes_OpenXml_validation()
    {
        var bold = new Run("gras") { FontWeight = FontWeights.Bold };
        var italicUnderline = new Run("it") { FontStyle = FontStyles.Italic, TextDecorations = TextDecorations.Underline };
        var path = Save(Docs.Doc(Docs.P(StyleKind.Normal, bold, new LineBreak(), italicUnderline, new Run("a\tb"))));

        Assert.That(OpenXmlCheck.Errors(path), Is.Empty);
    }

    [Test]
    public void Empty_document_saves_and_validates()
    {
        var path = Save(new FlowDocument());

        Assert.That(OpenXmlCheck.Errors(path), Is.Empty);
        Assert.That(new DocxInspector(path).Paragraphs, Is.Empty);
    }

    [Test]
    public void Saved_file_carries_the_mots_marker()
    {
        var path = Save(Docs.Doc(Docs.P(StyleKind.Normal, "x")));
        var inspector = new DocxInspector(path);

        var custom = inspector.PartNames.Where(n => n.StartsWith("customXml/item", StringComparison.Ordinal) && !n.Contains("Props", StringComparison.Ordinal) && n.EndsWith(".xml", StringComparison.Ordinal)).ToList();
        Assert.That(custom, Has.Count.EqualTo(1));

        var root = inspector.Part(custom[0]).Root!;
        XNamespace ns = "urn:mots:metadata";
        Assert.Multiple(() =>
        {
            Assert.That(root.Name, Is.EqualTo(ns + "mots"));
            Assert.That((string?)root.Attribute("application"), Is.EqualTo("Mots"));
            Assert.That((string?)root.Attribute("formatVersion"), Is.EqualTo(MotsMetadata.CurrentFormatVersion.ToString()));
        });
    }

    [Test]
    public void Marker_has_a_properties_part_with_a_braced_guid()
    {
        var inspector = new DocxInspector(Save(Docs.Doc(Docs.P(StyleKind.Normal, "x"))));

        var props = inspector.PartNames.Single(n => n.StartsWith("customXml/itemProps", StringComparison.Ordinal));
        var itemId = (string?)inspector.Part(props).Root!.Attribute(XName.Get("itemID", "http://schemas.openxmlformats.org/officeDocument/2006/customXml"));
        Assert.That(itemId, Does.Match(@"^\{[0-9A-F]{8}-([0-9A-F]{4}-){3}[0-9A-F]{12}\}$"));
    }

    [Test]
    public void Two_saves_use_different_marker_ids()
    {
        string Id(string name)
        {
            var i = new DocxInspector(Save(new FlowDocument(), name));
            var props = i.PartNames.Single(n => n.StartsWith("customXml/itemProps", StringComparison.Ordinal));
            return (string)i.Part(props).Root!.Attributes().Single(a => a.Name.LocalName == "itemID");
        }

        Assert.That(Id("a.docx"), Is.Not.EqualTo(Id("b.docx")));
    }

    [Test]
    public void Page_is_A4_with_equal_margins()
    {
        var inspector = new DocxInspector(Save(Docs.Doc(Docs.P(StyleKind.Normal, "x"))));
        var sect = inspector.Document.Descendants(W + "sectPr").Single();
        var size = sect.Element(W + "pgSz")!;
        var margin = sect.Element(W + "pgMar")!;

        Assert.Multiple(() =>
        {
            Assert.That(Attr(size, "w"), Is.EqualTo("11906"));
            Assert.That(Attr(size, "h"), Is.EqualTo("16838"));
            foreach (var side in new[] { "top", "right", "bottom", "left" })
                Assert.That(Attr(margin, side), Is.EqualTo("1417"), side);
        });
    }

    // ---------- styles.xml ----------

    [Test]
    public void Styles_part_defines_the_three_styles()
    {
        var inspector = new DocxInspector(Save(new FlowDocument()));

        Assert.Multiple(() =>
        {
            Assert.That(inspector.HasStyle("Normal"));
            Assert.That(inspector.HasStyle("Titre1"));
            Assert.That(inspector.HasStyle("Ellipse"));
            Assert.That(inspector.Styles.Descendants(W + "style").Count(), Is.EqualTo(3));
        });
    }

    [Test]
    public void Normal_style_in_twips()
    {
        var style = new DocxInspector(Save(new FlowDocument())).Style("Normal");
        var ppr = style.Element(W + "pPr")!;
        var rpr = style.Element(W + "rPr")!;
        var spacing = ppr.Element(W + "spacing")!;

        Assert.Multiple(() =>
        {
            Assert.That(Attr(style, "default"), Is.EqualTo("1").Or.EqualTo("true"));
            Assert.That(Val(style, "name"), Is.EqualTo("Normal"));
            Assert.That(Attr(spacing, "before"), Is.EqualTo("0"));
            Assert.That(Attr(spacing, "after"), Is.EqualTo("120"));
            Assert.That(Attr(spacing, "line"), Is.EqualTo("360"));
            Assert.That(Attr(spacing, "lineRule"), Is.EqualTo("auto"));
            Assert.That(Attr(ppr.Element(W + "ind")!, "firstLine"), Is.EqualTo("283"));
            Assert.That(Val(ppr, "jc"), Is.EqualTo("both"));
            Assert.That(HasChild(ppr, "pageBreakBefore"), Is.False);
            Assert.That(HasChild(ppr, "outlineLvl"), Is.False);
            Assert.That(Attr(rpr.Element(W + "rFonts")!, "ascii"), Is.EqualTo("Georgia"));
            Assert.That(Attr(rpr.Element(W + "rFonts")!, "hAnsi"), Is.EqualTo("Georgia"));
            Assert.That(Val(rpr, "sz"), Is.EqualTo("24"));
            Assert.That(HasChild(rpr, "b"), Is.False);
        });
    }

    [Test]
    public void Heading1_style_in_twips()
    {
        var style = new DocxInspector(Save(new FlowDocument())).Style("Titre1");
        var ppr = style.Element(W + "pPr")!;
        var rpr = style.Element(W + "rPr")!;
        var spacing = ppr.Element(W + "spacing")!;

        Assert.Multiple(() =>
        {
            Assert.That(Val(style, "name"), Is.EqualTo("heading 1"));
            Assert.That(Val(style, "basedOn"), Is.EqualTo("Normal"));
            Assert.That(Val(style, "next"), Is.EqualTo("Normal"));
            Assert.That(Val(style, "uiPriority"), Is.EqualTo("9"));
            Assert.That(Attr(style, "customStyle"), Is.Null);
            Assert.That(HasChild(ppr, "pageBreakBefore"));
            Assert.That(Val(ppr, "outlineLvl"), Is.EqualTo("0"));
            Assert.That(Attr(spacing, "before"), Is.EqualTo("480"));
            Assert.That(Attr(spacing, "after"), Is.EqualTo("240"));
            Assert.That(Val(ppr, "jc"), Is.EqualTo("left"));
            Assert.That(HasChild(rpr, "b"));
            Assert.That(Val(rpr, "sz"), Is.EqualTo("36"));
            Assert.That(HasChild(rpr, "rFonts"), Is.False, "la police est héritée de Normal");
        });
    }

    [Test]
    public void Ellipse_style_in_twips_and_custom()
    {
        var style = new DocxInspector(Save(new FlowDocument())).Style("Ellipse");
        var ppr = style.Element(W + "pPr")!;
        var spacing = ppr.Element(W + "spacing")!;

        Assert.Multiple(() =>
        {
            Assert.That(Attr(style, "customStyle"), Is.EqualTo("1").Or.EqualTo("true"));
            Assert.That(Val(style, "name"), Is.EqualTo("Ellipse"));
            Assert.That(Val(style, "basedOn"), Is.EqualTo("Normal"));
            Assert.That(Val(style, "next"), Is.EqualTo("Normal"));
            Assert.That(HasChild(style, "uiPriority"), Is.False);
            Assert.That(Val(ppr, "jc"), Is.EqualTo("center"));
            Assert.That(Attr(ppr.Element(W + "ind")!, "firstLine"), Is.EqualTo("0"));
            Assert.That(Attr(spacing, "before"), Is.EqualTo("240"));
            Assert.That(Attr(spacing, "after"), Is.EqualTo("240"));
            Assert.That(HasChild(style.Element(W + "rPr"), "sz"), Is.False, "même taille que Normal : héritée");
        });
    }

    // ---------- Paragraphes ----------

    [Test]
    public void Normal_paragraph_has_no_style_reference()
    {
        var p = new DocxInspector(Save(Docs.Doc(Docs.P(StyleKind.Normal, "x")))).Paragraphs.Single();

        Assert.That(PPr(p), Is.Null);
    }

    [TestCase(StyleKind.Heading1, "Titre1")]
    [TestCase(StyleKind.Ellipse, "Ellipse")]
    public void Styled_paragraph_references_its_style(StyleKind kind, string expectedId)
    {
        var p = new DocxInspector(Save(Docs.Doc(Docs.P(kind, "x")))).Paragraphs.Single();

        Assert.That(Val(PPr(p), "pStyle"), Is.EqualTo(expectedId));
    }

    [Test]
    public void Paragraph_order_and_text_are_preserved()
    {
        var path = Save(Docs.Doc(
            Docs.P(StyleKind.Heading1, "Un"),
            Docs.P(StyleKind.Normal, "Deux"),
            Docs.P(StyleKind.Normal, "Trois")));

        Assert.That(new DocxInspector(path).Paragraphs.Select(TextOf), Is.EqualTo(new[] { "Un", "Deux", "Trois" }));
    }

    [Test]
    public void Empty_paragraph_is_written_without_runs()
    {
        var p = new DocxInspector(Save(Docs.Doc(Docs.P(StyleKind.Normal)))).Paragraphs.Single();

        Assert.That(RunsOf(p), Is.Empty);
    }

    [Test]
    public void Plain_run_has_no_properties()
    {
        var p = new DocxInspector(Save(Docs.Doc(Docs.P(StyleKind.Normal, "x")))).Paragraphs.Single();

        Assert.That(RPr(RunsOf(p).Single()), Is.Null);
    }

    [Test]
    public void Bold_in_normal_is_written_as_bold()
    {
        var run = new Run("x") { FontWeight = FontWeights.Bold };
        var r = RunsOf(new DocxInspector(Save(Docs.Doc(Docs.P(StyleKind.Normal, run)))).Paragraphs.Single()).Single();

        var b = RPr(r)!.Element(W + "b")!;
        Assert.That(Attr(b, "val"), Is.Null.Or.EqualTo("1").Or.EqualTo("true"));
    }

    [Test]
    public void Bold_in_heading_is_not_repeated()
    {
        var run = new Run("x") { FontWeight = FontWeights.Bold };
        var r = RunsOf(new DocxInspector(Save(Docs.Doc(Docs.P(StyleKind.Heading1, run)))).Paragraphs.Single()).Single();

        Assert.That(RPr(r), Is.Null);
    }

    [Test]
    public void Non_bold_run_in_heading_is_written_as_explicit_not_bold()
    {
        var run = new Run("x") { FontWeight = FontWeights.Normal };
        var r = RunsOf(new DocxInspector(Save(Docs.Doc(Docs.P(StyleKind.Heading1, run)))).Paragraphs.Single()).Single();

        var b = RPr(r)!.Element(W + "b")!;
        Assert.That(Attr(b, "val"), Is.EqualTo("0").Or.EqualTo("false"));
    }

    [Test]
    public void Italic_and_underline_are_written()
    {
        var run = new Run("x") { FontStyle = FontStyles.Italic, TextDecorations = TextDecorations.Underline };
        var r = RunsOf(new DocxInspector(Save(Docs.Doc(Docs.P(StyleKind.Normal, run)))).Paragraphs.Single()).Single();

        Assert.Multiple(() =>
        {
            Assert.That(HasChild(RPr(r), "i"));
            Assert.That(Val(RPr(r), "u"), Is.EqualTo("single"));
            Assert.That(HasChild(RPr(r), "b"), Is.False);
        });
    }

    [Test]
    public void Underline_carried_by_a_parent_span_is_written()
    {
        var span = new Span(new Run("x")) { TextDecorations = TextDecorations.Underline };
        var r = RunsOf(new DocxInspector(Save(Docs.Doc(Docs.P(StyleKind.Normal, span)))).Paragraphs.Single()).Single();

        Assert.That(Val(RPr(r), "u"), Is.EqualTo("single"));
    }

    [Test]
    public void Nested_spans_are_flattened_in_order()
    {
        var inner = new Span(new Run("B") { FontWeight = FontWeights.Bold });
        var outer = new Span();
        outer.Inlines.Add(new Run("A"));
        outer.Inlines.Add(inner);
        outer.Inlines.Add(new Run("C"));
        var p = new DocxInspector(Save(Docs.Doc(Docs.P(StyleKind.Normal, outer)))).Paragraphs.Single();

        Assert.Multiple(() =>
        {
            Assert.That(TextOf(p), Is.EqualTo("ABC"));
            Assert.That(RunsOf(p).Count(), Is.EqualTo(3));
            Assert.That(HasChild(RPr(RunsOf(p).ElementAt(1)), "b"));
        });
    }

    [Test]
    public void Tab_and_newline_in_text_become_tab_and_break_elements()
    {
        var p = new DocxInspector(Save(Docs.Doc(Docs.P(StyleKind.Normal, "a\tb\nc")))).Paragraphs.Single();
        var run = RunsOf(p).Single();

        Assert.Multiple(() =>
        {
            Assert.That(run.Elements().Select(e => e.Name.LocalName), Is.EqualTo(new[] { "t", "tab", "t", "br", "t" }));
            Assert.That(TextOf(p), Is.EqualTo("abc"));
        });
    }

    [Test]
    public void LineBreak_inline_becomes_a_break_run()
    {
        var path = Save(Docs.Doc(Docs.P(StyleKind.Normal, new Run("a"), new LineBreak(), new Run("b"))));
        var p = new DocxInspector(path).Paragraphs.Single();

        Assert.That(p.Descendants(W + "br").Count(), Is.EqualTo(1));
    }

    [Test]
    public void Carriage_returns_are_dropped()
    {
        var p = new DocxInspector(Save(Docs.Doc(Docs.P(StyleKind.Normal, "a\r\nb")))).Paragraphs.Single();

        Assert.That(TextOf(p), Is.EqualTo("ab"));
        Assert.That(p.Descendants(W + "br").Count(), Is.EqualTo(1));
    }

    [Test]
    public void Spaces_are_preserved()
    {
        var p = new DocxInspector(Save(Docs.Doc(Docs.P(StyleKind.Normal, "  a  b  ")))).Paragraphs.Single();
        var t = p.Descendants(W + "t").Single();

        Assert.That(t.Value, Is.EqualTo("  a  b  "));
        Assert.That((string?)t.Attribute(XNamespace.Xml + "space"), Is.EqualTo("preserve"));
    }

    [Test]
    public void Xml_special_characters_are_escaped_and_read_back()
    {
        var path = Save(Docs.Doc(Docs.P(StyleKind.Normal, "<a href=\"x\">&amp; 'q'</a>")));

        Assert.That(TextOf(new DocxInspector(path).Paragraphs.Single()), Is.EqualTo("<a href=\"x\">&amp; 'q'</a>"));
    }

    [Test]
    public void Unicode_text_is_written_as_is()
    {
        const string text = "Élodie — « ça va ? » œuvre insécable fine 日本語 😀";
        var path = Save(Docs.Doc(Docs.P(StyleKind.Normal, text)));

        Assert.That(TextOf(new DocxInspector(path).Paragraphs.Single()), Is.EqualTo(text));
    }

    [Test]
    public void Only_top_level_paragraphs_are_written()
    {
        var document = Docs.Doc(
            Docs.P(StyleKind.Normal, "a"),
            new Section(Docs.P(StyleKind.Normal, "dans une section")),
            Docs.P(StyleKind.Normal, "b"));

        Assert.That(new DocxInspector(Save(document)).Paragraphs.Select(TextOf), Is.EqualTo(new[] { "a", "b" }));
    }

    [Test]
    public void Large_document_is_written_completely()
    {
        var blocks = Enumerable.Range(0, 2000).Select(i => (Block)Docs.P(StyleKind.Normal, $"Paragraphe {i}")).ToArray();
        var inspector = new DocxInspector(Save(Docs.Doc(blocks)));

        Assert.That(inspector.Paragraphs.Count(), Is.EqualTo(2000));
        Assert.That(TextOf(inspector.Paragraphs.Last()), Is.EqualTo("Paragraphe 1999"));
    }

    // ---------- Fichier ----------

    [Test]
    public void Save_overwrites_an_existing_file()
    {
        var path = PathOf("doc.docx");
        File.WriteAllText(path, "ancien contenu");

        DocxConverter.Save(Docs.Doc(Docs.P(StyleKind.Normal, "nouveau")), path);

        Assert.That(TextOf(new DocxInspector(path).Paragraphs.Single()), Is.EqualTo("nouveau"));
    }

    [Test]
    public void Save_to_missing_directory_throws_and_creates_nothing()
    {
        var path = Path.Combine(Dir, "absent", "doc.docx");

        Assert.That(() => DocxConverter.Save(new FlowDocument(), path), Throws.InstanceOf<IOException>());
        Assert.That(Directory.Exists(Path.Combine(Dir, "absent")), Is.False);
    }

    [Test]
    public void Save_to_a_locked_file_throws_and_leaves_it_intact()
    {
        var path = PathOf("doc.docx");
        DocxConverter.Save(Docs.Doc(Docs.P(StyleKind.Normal, "original")), path);
        var before = File.ReadAllBytes(path);

        using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            Assert.That(() => DocxConverter.Save(Docs.Doc(Docs.P(StyleKind.Normal, "autre")), path), Throws.InstanceOf<IOException>());

        Assert.That(File.ReadAllBytes(path), Is.EqualTo(before));
    }

    [Test]
    public void Saving_twice_gives_equivalent_content()
    {
        var document = Docs.Doc(Docs.P(StyleKind.Heading1, "T"), Docs.P(StyleKind.Normal, "x"));
        var a = new DocxInspector(Save(document, "a.docx"));
        var b = new DocxInspector(Save(document, "b.docx"));

        Assert.That(b.Document.ToString(), Is.EqualTo(a.Document.ToString()));
        Assert.That(b.Styles.ToString(), Is.EqualTo(a.Styles.ToString()));
    }
}
