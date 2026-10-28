using System.IO.Compression;
using System.Windows;
using System.Windows.Documents;
using NUnit.Framework;
using static Mots.Tests.WordFile;

namespace Mots.Tests;

public class DocxReadTests : FileTestBase
{
    private FlowDocument RoundTrip(FlowDocument document) => Docs.RoundTrip(document, PathOf("rt.docx"));

    // ---------- Aller-retour ----------

    [Test]
    public void Round_trip_keeps_styles_and_text()
    {
        var loaded = RoundTrip(Docs.Doc(
            Docs.P(StyleKind.Heading1, "Chapitre un"),
            Docs.P(StyleKind.Normal, "Il était une fois."),
            Docs.P(StyleKind.Ellipse, "***"),
            Docs.P(StyleKind.Normal, "Fin.")));

        Assert.Multiple(() =>
        {
            Assert.That(Docs.Kinds(loaded), Is.EqualTo(new[] { StyleKind.Heading1, StyleKind.Normal, StyleKind.Ellipse, StyleKind.Normal }));
            Assert.That(Docs.Paragraphs(loaded).Select(Docs.Text),
                Is.EqualTo(new[] { "Chapitre un", "Il était une fois.", "***", "Fin." }));
        });
    }

    [Test]
    public void Round_trip_keeps_run_formatting()
    {
        var loaded = RoundTrip(Docs.Doc(Docs.P(StyleKind.Normal,
            new Run("n"),
            new Run("g") { FontWeight = FontWeights.Bold },
            new Run("i") { FontStyle = FontStyles.Italic },
            new Run("s") { TextDecorations = TextDecorations.Underline })));

        var runs = Docs.Runs(Docs.Paragraphs(loaded).Single());
        Assert.Multiple(() =>
        {
            Assert.That(runs.Select(r => r.Text), Is.EqualTo(new[] { "n", "g", "i", "s" }));
            Assert.That(runs[0].FontWeight, Is.EqualTo(FontWeights.Normal));
            Assert.That(runs[1].FontWeight, Is.EqualTo(FontWeights.Bold));
            Assert.That(runs[2].FontStyle, Is.EqualTo(FontStyles.Italic));
            Assert.That(runs[3].TextDecorations, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public void Round_trip_keeps_non_bold_text_in_a_heading()
    {
        var loaded = RoundTrip(Docs.Doc(Docs.P(StyleKind.Heading1,
            new Run("gras "), new Run("maigre") { FontWeight = FontWeights.Normal })));

        var runs = Docs.Runs(Docs.Paragraphs(loaded).Single());
        Assert.Multiple(() =>
        {
            Assert.That(runs[0].FontWeight, Is.EqualTo(FontWeights.Bold)); // hérité du style
            Assert.That(runs[1].FontWeight, Is.EqualTo(FontWeights.Normal));
        });
    }

    [Test]
    public void Round_trip_keeps_tabs_line_breaks_and_special_spaces()
    {
        const string text = "a\tb c d  e";
        var loaded = RoundTrip(Docs.Doc(Docs.P(StyleKind.Normal, new Run(text), new LineBreak(), new Run("suite"))));

        var paragraph = Docs.Paragraphs(loaded).Single();
        Assert.Multiple(() =>
        {
            Assert.That(Docs.Runs(paragraph).First().Text, Is.EqualTo(text));
            Assert.That(paragraph.Inlines.OfType<LineBreak>().Count(), Is.EqualTo(1));
            Assert.That(Docs.Runs(paragraph).Last().Text, Is.EqualTo("suite"));
        });
    }

    [Test]
    public void Round_trip_keeps_a_newline_inside_a_run_as_a_line_break()
    {
        var loaded = RoundTrip(Docs.Doc(Docs.P(StyleKind.Normal, "a\nb")));

        var paragraph = Docs.Paragraphs(loaded).Single();
        Assert.That(paragraph.Inlines.Select(i => i.GetType()), Is.EqualTo(new[] { typeof(Run), typeof(LineBreak), typeof(Run) }));
    }

    [Test]
    public void Round_trip_keeps_empty_paragraphs()
    {
        var loaded = RoundTrip(Docs.Doc(Docs.P(StyleKind.Normal, "a"), Docs.P(StyleKind.Normal), Docs.P(StyleKind.Heading1)));

        Assert.That(Docs.Kinds(loaded), Is.EqualTo(new[] { StyleKind.Normal, StyleKind.Normal, StyleKind.Heading1 }));
        Assert.That(Docs.Paragraphs(loaded).Select(Docs.Text), Is.EqualTo(new[] { "a", "", "" }));
    }

    [Test]
    public void Round_trip_keeps_unicode()
    {
        const string text = "Élodie — « ça va ? » œuvre 日本語 😀 <&>";
        var loaded = RoundTrip(Docs.Doc(Docs.P(StyleKind.Normal, text)));

        Assert.That(Docs.Text(Docs.Paragraphs(loaded).Single()), Is.EqualTo(text));
    }

    [Test]
    public void Round_trip_applies_styles_to_the_loaded_paragraphs()
    {
        var loaded = RoundTrip(Docs.Doc(Docs.P(StyleKind.Heading1, "T")));
        var paragraph = Docs.Paragraphs(loaded).Single();

        Assert.Multiple(() =>
        {
            Assert.That(paragraph.BreakPageBefore, Is.True);
            Assert.That(paragraph.FontWeight, Is.EqualTo(FontWeights.Bold));
            Assert.That(paragraph.FontSize, Is.EqualTo(24).Within(1e-9));
        });
    }

    [Test]
    public void Save_load_save_is_stable()
    {
        var first = PathOf("1.docx");
        var second = PathOf("2.docx");
        var document = Docs.Doc(
            Docs.P(StyleKind.Heading1, "T"),
            Docs.P(StyleKind.Normal, new Run("a") { FontWeight = FontWeights.Bold }, new LineBreak(), new Run("b\tc")),
            Docs.P(StyleKind.Ellipse, "***"));

        DocxConverter.Save(document, first);
        DocxConverter.Save(DocxConverter.Load(first), second);

        Assert.That(new DocxInspector(second).Document.ToString(), Is.EqualTo(new DocxInspector(first).Document.ToString()));
    }

    [Test]
    public void Load_of_an_empty_saved_document_gives_one_normal_paragraph()
    {
        var loaded = RoundTrip(new FlowDocument());

        Assert.That(Docs.Kinds(loaded), Is.EqualTo(new[] { StyleKind.Normal }));
    }

    // ---------- Marqueur ----------

    [Test]
    public void Load_rejects_a_word_document_without_marker()
    {
        var path = Create(PathOf("w.docx"), [P(null, "x")], StandardStyles());

        var ex = Assert.Throws<IncompatibleFileException>(() => DocxConverter.Load(path))!;
        Assert.That(ex.Message, Is.EqualTo("Ce document n'a pas été créé avec Mots. Seuls les documents enregistrés par Mots peuvent être ouverts."));
    }

    [Test]
    public void Load_accepts_a_document_with_the_current_marker()
    {
        var path = Create(PathOf("w.docx"), [P(null, "x")], StandardStyles(), Marker(MotsMetadata.CurrentFormatVersion));

        Assert.That(Docs.Text(Docs.Paragraphs(DocxConverter.Load(path)).Single()), Is.EqualTo("x"));
    }

    [Test]
    public void Load_accepts_an_older_format_version()
    {
        var path = Create(PathOf("w.docx"), [P(null, "x")], StandardStyles(), Marker(1));

        Assert.That(() => DocxConverter.Load(path), Throws.Nothing);
    }

    [Test]
    public void Load_rejects_a_newer_format_version()
    {
        var path = Create(PathOf("w.docx"), [P(null, "x")], StandardStyles(), Marker(MotsMetadata.CurrentFormatVersion + 1));

        var ex = Assert.Throws<IncompatibleFileException>(() => DocxConverter.Load(path))!;
        Assert.That(ex.Message, Does.Contain("version plus récente de Mots"));
    }

    [TestCase("0")]
    [TestCase("-3")]
    [TestCase("abc")]
    [TestCase("")]
    public void Load_rejects_an_unreadable_format_version(string version)
    {
        var path = Create(PathOf("w.docx"), [P(null, "x")], StandardStyles(), Marker(version));

        var ex = Assert.Throws<IncompatibleFileException>(() => DocxConverter.Load(path))!;
        Assert.That(ex.Message, Does.Contain("illisibles"));
    }

    [Test]
    public void Load_rejects_a_marker_without_version_attribute()
    {
        var path = Create(PathOf("w.docx"), [P(null, "x")], StandardStyles(),
            "<mots xmlns=\"urn:mots:metadata\" application=\"Mots\"/>");

        Assert.That(() => DocxConverter.Load(path), Throws.InstanceOf<IncompatibleFileException>().With.Message.Contain("illisibles"));
    }

    [Test]
    public void Load_ignores_foreign_custom_xml_parts()
    {
        var path = Create(PathOf("w.docx"), [P(null, "x")], StandardStyles(), "<autre xmlns=\"urn:autre\"/>");

        Assert.That(() => DocxConverter.Load(path), Throws.InstanceOf<IncompatibleFileException>().With.Message.Contain("pas été créé avec Mots"));
    }

    [Test]
    public void Load_ignores_malformed_custom_xml_parts()
    {
        var path = Create(PathOf("w.docx"), [P(null, "x")], StandardStyles(), "<pas du xml");

        Assert.That(() => DocxConverter.Load(path), Throws.InstanceOf<IncompatibleFileException>().With.Message.Contain("pas été créé avec Mots"));
    }

    [Test]
    public void Import_does_not_require_the_marker()
    {
        var path = Create(PathOf("w.docx"), [P(null, "x")], StandardStyles());

        Assert.That(Docs.Text(Docs.Paragraphs(DocxConverter.Import(path)).Single()), Is.EqualTo("x"));
    }

    [Test]
    public void Import_ignores_a_newer_format_version()
    {
        var path = Create(PathOf("w.docx"), [P(null, "x")], StandardStyles(), Marker(99));

        Assert.That(() => DocxConverter.Import(path), Throws.Nothing);
    }

    // ---------- Fichiers invalides ----------

    [Test]
    public void Load_of_a_missing_file_throws_FileNotFound()
    {
        Assert.That(() => DocxConverter.Load(PathOf("absent.docx")), Throws.InstanceOf<FileNotFoundException>());
    }

    [Test]
    public void Load_of_a_missing_directory_throws_DirectoryNotFound()
    {
        Assert.That(() => DocxConverter.Load(Path.Combine(Dir, "x", "absent.docx")), Throws.InstanceOf<DirectoryNotFoundException>());
    }

    [TestCase("")]
    [TestCase("ceci n'est pas un docx")]
    public void Load_and_Import_reject_a_file_that_is_not_a_zip(string content)
    {
        var path = PathOf("bad.docx");
        File.WriteAllText(path, content);

        Assert.Multiple(() =>
        {
            Assert.That(() => DocxConverter.Load(path), Throws.InstanceOf<IncompatibleFileException>().With.Message.Contain("pas un document Word valide"));
            Assert.That(() => DocxConverter.Import(path), Throws.InstanceOf<IncompatibleFileException>().With.Message.Contain("pas un document Word valide"));
        });
    }

    [Test]
    public void Import_rejects_an_empty_zip()
    {
        var path = PathOf("empty.docx");
        using (ZipFile.Open(path, ZipArchiveMode.Create)) { }

        Assert.That(() => DocxConverter.Import(path), Throws.InstanceOf<IncompatibleFileException>());
    }

    [Test]
    public void Import_rejects_a_zip_that_is_not_an_office_package()
    {
        var path = PathOf("zip.docx");
        using (var zip = ZipFile.Open(path, ZipArchiveMode.Create))
            zip.CreateEntry("hello.txt");

        Assert.That(() => DocxConverter.Import(path), Throws.InstanceOf<IncompatibleFileException>());
    }

    [Test]
    public void Import_rejects_a_truncated_docx()
    {
        var good = Create(PathOf("good.docx"), [P(null, "x")], StandardStyles());
        var bytes = File.ReadAllBytes(good);
        var path = PathOf("cut.docx");
        File.WriteAllBytes(path, bytes[..(bytes.Length / 2)]);

        Assert.That(() => DocxConverter.Import(path), Throws.InstanceOf<IncompatibleFileException>());
    }

    [Test]
    public void Load_can_read_a_file_open_elsewhere_for_reading_and_writing()
    {
        var path = PathOf("rt.docx");
        DocxConverter.Save(Docs.Doc(Docs.P(StyleKind.Normal, "x")), path);

        using var other = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
        Assert.That(() => DocxConverter.Load(path), Throws.Nothing);
    }

    [Test]
    public void Load_of_a_file_locked_exclusively_throws_IOException()
    {
        var path = PathOf("rt.docx");
        DocxConverter.Save(Docs.Doc(Docs.P(StyleKind.Normal, "x")), path);

        using var other = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        Assert.That(() => DocxConverter.Load(path), Throws.InstanceOf<IOException>());
    }

    // ---------- Contenu Word exotique ----------

    [Test]
    public void Body_without_paragraphs_gives_one_normal_paragraph()
    {
        var path = Create(PathOf("w.docx"), [], StandardStyles());

        Assert.That(Docs.Kinds(DocxConverter.Import(path)), Is.EqualTo(new[] { StyleKind.Normal }));
    }

    [Test]
    public void Table_cells_are_flattened_to_paragraphs()
    {
        var table = new DocumentFormat.OpenXml.Wordprocessing.Table(
            new DocumentFormat.OpenXml.Wordprocessing.TableRow(
                new DocumentFormat.OpenXml.Wordprocessing.TableCell(P(null, "A1")),
                new DocumentFormat.OpenXml.Wordprocessing.TableCell(P(null, "B1"))),
            new DocumentFormat.OpenXml.Wordprocessing.TableRow(
                new DocumentFormat.OpenXml.Wordprocessing.TableCell(P(null, "A2")),
                new DocumentFormat.OpenXml.Wordprocessing.TableCell(P(null, "B2"))));
        var path = Create(PathOf("w.docx"), [P(null, "avant"), table, P(null, "après")], StandardStyles());

        Assert.That(Docs.Paragraphs(DocxConverter.Import(path)).Select(Docs.Text),
            Is.EqualTo(new[] { "avant", "A1", "B1", "A2", "B2", "après" }));
    }

    [Test]
    public void Block_level_content_controls_are_flattened()
    {
        var sdt = new DocumentFormat.OpenXml.Wordprocessing.SdtBlock(
            new DocumentFormat.OpenXml.Wordprocessing.SdtContentBlock(P(null, "dans un contrôle")));
        var path = Create(PathOf("w.docx"), [sdt, P(null, "après")], StandardStyles());

        Assert.That(Docs.Paragraphs(DocxConverter.Import(path)).Select(Docs.Text), Is.EqualTo(new[] { "dans un contrôle", "après" }));
    }

    [Test]
    public void Run_properties_bold_italic_underline_are_imported()
    {
        var path = Create(PathOf("w.docx"),
            [P(null, R("g", bold: true), R("i", italic: true), R("s", underline: "single"), R("n"))], StandardStyles());

        var runs = Docs.Runs(Docs.Paragraphs(DocxConverter.Import(path)).Single());
        Assert.Multiple(() =>
        {
            Assert.That(runs[0].FontWeight, Is.EqualTo(FontWeights.Bold));
            Assert.That(runs[1].FontStyle, Is.EqualTo(FontStyles.Italic));
            Assert.That(runs[2].TextDecorations, Has.Count.EqualTo(1));
            Assert.That(runs[3].FontWeight, Is.EqualTo(FontWeights.Normal));
            Assert.That(runs[3].FontStyle, Is.EqualTo(FontStyles.Normal));
            Assert.That(runs[3].TextDecorations, Is.Empty);
        });
    }

    [Test]
    public void Explicitly_disabled_run_properties_are_not_applied()
    {
        var path = Create(PathOf("w.docx"),
            [P(null, R("a", bold: false, italic: false, underline: "none"))], StandardStyles());

        var run = Docs.Runs(Docs.Paragraphs(DocxConverter.Import(path)).Single()).Single();
        Assert.Multiple(() =>
        {
            Assert.That(run.FontWeight, Is.EqualTo(FontWeights.Normal));
            Assert.That(run.FontStyle, Is.EqualTo(FontStyles.Normal));
            Assert.That(run.TextDecorations, Is.Empty);
        });
    }

    [Test]
    public void Explicit_not_bold_in_a_heading_is_kept()
    {
        var path = Create(PathOf("w.docx"), [P("Titre1", R("x", bold: false))], StandardStyles());

        var run = Docs.Runs(Docs.Paragraphs(DocxConverter.Import(path)).Single()).Single();
        Assert.That(run.FontWeight, Is.EqualTo(FontWeights.Normal));
    }

    [Test]
    public void Fonts_and_sizes_from_the_file_are_ignored()
    {
        var run = new DocumentFormat.OpenXml.Wordprocessing.Run(
            new DocumentFormat.OpenXml.Wordprocessing.RunProperties(
                new DocumentFormat.OpenXml.Wordprocessing.RunFonts { Ascii = "Comic Sans MS" },
                new DocumentFormat.OpenXml.Wordprocessing.FontSize { Val = "72" }),
            new DocumentFormat.OpenXml.Wordprocessing.Text("x"));
        var path = Create(PathOf("w.docx"), [P(null, run)], StandardStyles());

        var imported = Docs.Paragraphs(DocxConverter.Import(path)).Single();
        Assert.Multiple(() =>
        {
            Assert.That(imported.FontFamily.Source, Is.EqualTo("Georgia"));
            Assert.That(imported.FontSize, Is.EqualTo(16).Within(1e-9));
            Assert.That(Docs.Runs(imported).Single().FontSize, Is.EqualTo(imported.FontSize));
        });
    }

    [Test]
    public void Tab_break_and_no_break_hyphen_are_imported()
    {
        var run = new DocumentFormat.OpenXml.Wordprocessing.Run(
            new DocumentFormat.OpenXml.Wordprocessing.Text("a"),
            new DocumentFormat.OpenXml.Wordprocessing.TabChar(),
            new DocumentFormat.OpenXml.Wordprocessing.Text("b"),
            new DocumentFormat.OpenXml.Wordprocessing.NoBreakHyphen(),
            new DocumentFormat.OpenXml.Wordprocessing.Text("c"),
            new DocumentFormat.OpenXml.Wordprocessing.Break(),
            new DocumentFormat.OpenXml.Wordprocessing.Text("d"));
        var path = Create(PathOf("w.docx"), [P(null, run)], StandardStyles());

        var paragraph = Docs.Paragraphs(DocxConverter.Import(path)).Single();
        Assert.Multiple(() =>
        {
            Assert.That(Docs.Runs(paragraph)[0].Text, Is.EqualTo("a\tb-c"));
            Assert.That(paragraph.Inlines.OfType<LineBreak>().Count(), Is.EqualTo(1));
            Assert.That(Docs.Runs(paragraph)[1].Text, Is.EqualTo("d"));
        });
    }

    [Test]
    public void Runs_inside_hyperlinks_are_imported()
    {
        var link = new DocumentFormat.OpenXml.Wordprocessing.Hyperlink(R("lien")) { Anchor = "ancre" };
        var paragraph = new DocumentFormat.OpenXml.Wordprocessing.Paragraph(R("avant "), link, R(" après"));
        var path = Create(PathOf("w.docx"), [paragraph], StandardStyles());

        Assert.That(Docs.Text(Docs.Paragraphs(DocxConverter.Import(path)).Single()), Is.EqualTo("avant lien après"));
    }

    [Test]
    public void Whitespace_is_preserved_on_import()
    {
        var path = Create(PathOf("w.docx"), [P(null, "  a  ")], StandardStyles());

        Assert.That(Docs.Runs(Docs.Paragraphs(DocxConverter.Import(path)).Single()).Single().Text, Is.EqualTo("  a  "));
    }

    [Test]
    public void Document_without_a_styles_part_is_read_with_ids_as_names()
    {
        var path = Create(PathOf("w.docx"), [P("Titre1", "T"), P(null, "n"), P("Ellipse", "*")]);

        Assert.That(Docs.Kinds(DocxConverter.Import(path)),
            Is.EqualTo(new[] { StyleKind.Heading1, StyleKind.Normal, StyleKind.Ellipse }));
    }

    [Test]
    public void Document_without_a_styles_part_rejects_unknown_ids()
    {
        var path = Create(PathOf("w.docx"), [P("Citation", "T")]);

        Assert.That(() => DocxConverter.Import(path), Throws.InstanceOf<IncompatibleFileException>().With.Message.Contain("« Citation » (1 paragraphe)"));
    }

    [Test]
    public void Many_paragraphs_are_imported_in_order()
    {
        var body = Enumerable.Range(0, 1000).Select(i => (DocumentFormat.OpenXml.OpenXmlElement)P(null, $"p{i}"));
        var path = Create(PathOf("w.docx"), body, StandardStyles());

        var paragraphs = Docs.Paragraphs(DocxConverter.Import(path));
        Assert.That(paragraphs, Has.Count.EqualTo(1000));
        Assert.That(Docs.Text(paragraphs[999]), Is.EqualTo("p999"));
    }
}
