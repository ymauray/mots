using System.IO.Compression;
using System.Text;
using System.Windows;
using System.Windows.Documents;
using System.Xml.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace Mots.Tests;

/// <summary>Construction rapide de documents WPF (FlowDocument) pour les tests.</summary>
internal static class Docs
{
    public static Paragraph P(StyleKind kind, string text = "")
    {
        var paragraph = new Paragraph();
        ParagraphStyles.Apply(paragraph, kind);
        if (text.Length > 0)
            paragraph.Inlines.Add(new Run(text));
        return paragraph;
    }

    public static Paragraph P(StyleKind kind, params Inline[] inlines)
    {
        var paragraph = new Paragraph();
        ParagraphStyles.Apply(paragraph, kind);
        foreach (var inline in inlines)
            paragraph.Inlines.Add(inline);
        return paragraph;
    }

    public static FlowDocument Doc(params Block[] blocks)
    {
        var document = new FlowDocument();
        foreach (var block in blocks)
            document.Blocks.Add(block);
        return document;
    }

    public static List<Paragraph> Paragraphs(FlowDocument document) => document.Blocks.OfType<Paragraph>().ToList();

    public static List<StyleKind> Kinds(FlowDocument document) => Paragraphs(document).Select(ParagraphStyles.KindOf).ToList();

    public static string Text(Paragraph paragraph) => new TextRange(paragraph.ContentStart, paragraph.ContentEnd).Text;

    public static List<Run> Runs(Paragraph paragraph) => paragraph.Inlines.OfType<Run>().ToList();

    /// <summary>Enregistre puis relit un document par DocxConverter.</summary>
    public static FlowDocument RoundTrip(FlowDocument document, string path)
    {
        DocxConverter.Save(document, path);
        return DocxConverter.Load(path);
    }
}

/// <summary>Lecture directe du XML d'un .docx, indépendante du SDK Open XML utilisé par Mots.</summary>
internal sealed class DocxInspector
{
    public static readonly XNamespace W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

    private readonly Dictionary<string, XDocument> _parts = [];

    public DocxInspector(string path)
    {
        using var zip = ZipFile.OpenRead(path);
        PartNames = zip.Entries.Select(e => e.FullName).ToList();
        foreach (var entry in zip.Entries.Where(e => e.FullName.EndsWith(".xml", StringComparison.Ordinal)
                                                     || e.FullName.EndsWith(".rels", StringComparison.Ordinal)))
        {
            using var stream = entry.Open();
            _parts[entry.FullName] = XDocument.Load(stream);
        }
    }

    public IReadOnlyList<string> PartNames { get; }

    public XDocument Part(string name) => _parts[name];

    public bool HasPart(string name) => _parts.ContainsKey(name);

    public XDocument Document => Part("word/document.xml");

    public XDocument Styles => Part("word/styles.xml");

    public IEnumerable<XElement> Paragraphs => Document.Descendants(W + "p");

    public XElement Style(string id) =>
        Styles.Descendants(W + "style").Single(s => Attr(s, "styleId") == id);

    public bool HasStyle(string id) => Styles.Descendants(W + "style").Any(s => Attr(s, "styleId") == id);

    public static string? Attr(XElement element, string localName) => (string?)element.Attribute(W + localName);

    /// <summary>Valeur de w:val de l'enfant donné, ou null s'il est absent.</summary>
    public static string? Val(XElement? parent, string childName) =>
        parent?.Element(W + childName) is { } child ? Attr(child, "val") : null;

    public static bool HasChild(XElement? parent, string childName) => parent?.Element(W + childName) is not null;

    public static XElement? PPr(XElement paragraph) => paragraph.Element(W + "pPr");

    public static XElement? RPr(XElement run) => run.Element(W + "rPr");

    public static IEnumerable<XElement> RunsOf(XElement paragraph) => paragraph.Elements(W + "r");

    /// <summary>Texte des w:t d'un paragraphe.</summary>
    public static string TextOf(XElement paragraph) =>
        string.Concat(paragraph.Descendants(W + "t").Select(t => t.Value));
}

/// <summary>Validation Open XML d'un fichier : renvoie les descriptions d'erreurs (vide si valide).</summary>
internal static class OpenXmlCheck
{
    public static List<string> Errors(string path)
    {
        using var package = WordprocessingDocument.Open(path, false);
        return new OpenXmlValidator().Validate(package).Select(e => $"{e.Description} ({e.Path?.XPath})").ToList();
    }
}

/// <summary>Fabrique de documents « Word » (sans marqueur Mots, sauf demande) pour tester l'import.</summary>
internal static class WordFile
{
    public static W.Style ParaStyle(string id, string name, bool isDefault = false)
    {
        var style = new W.Style(new W.StyleName { Val = name }) { Type = W.StyleValues.Paragraph, StyleId = id };
        if (isDefault)
            style.Default = true;
        return style;
    }

    public static W.Style CharStyle(string id, string name, bool isDefault = false)
    {
        var style = new W.Style(new W.StyleName { Val = name }) { Type = W.StyleValues.Character, StyleId = id };
        if (isDefault)
            style.Default = true;
        return style;
    }

    public static W.Run R(string text, bool? bold = null, bool? italic = null, string? underline = null, string? charStyle = null)
    {
        var props = new W.RunProperties();
        if (charStyle is not null)
            props.Append(new W.RunStyle { Val = charStyle });
        if (bold is { } b)
            props.Append(b ? new W.Bold() : new W.Bold { Val = false });
        if (italic is { } i)
            props.Append(i ? new W.Italic() : new W.Italic { Val = false });
        if (underline is not null)
            props.Append(new W.Underline { Val = new EnumValue<W.UnderlineValues>(
                underline == "none" ? W.UnderlineValues.None : W.UnderlineValues.Single) });

        var run = new W.Run();
        if (props.HasChildren)
            run.Append(props);
        run.Append(new W.Text(text) { Space = SpaceProcessingModeValues.Preserve });
        return run;
    }

    public static W.Paragraph P(string? styleId, params W.Run[] runs)
    {
        var paragraph = new W.Paragraph();
        if (styleId is not null)
            paragraph.Append(new W.ParagraphProperties(new W.ParagraphStyleId { Val = styleId }));
        foreach (var run in runs)
            paragraph.Append(run);
        return paragraph;
    }

    public static W.Paragraph P(string? styleId, string text) => P(styleId, R(text));

    /// <summary>Crée un .docx. <paramref name="markerXml"/> : contenu brut d'une partie Custom XML (marqueur Mots).</summary>
    public static string Create(string path, IEnumerable<OpenXmlElement> body, IEnumerable<W.Style>? styles = null,
        string? markerXml = null)
    {
        using var package = WordprocessingDocument.Create(path, WordprocessingDocumentType.Document);
        var main = package.AddMainDocumentPart();
        main.Document = new W.Document(new W.Body(body.ToArray()));

        if (styles is not null)
            main.AddNewPart<StyleDefinitionsPart>().Styles = new W.Styles(styles.ToArray());

        if (markerXml is not null)
        {
            var part = main.AddCustomXmlPart(CustomXmlPartType.CustomXml);
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(markerXml));
            part.FeedData(stream);
        }

        main.Document.Save();
        return path;
    }

    /// <summary>Contenu d'un marqueur Mots valide pour la version de format donnée.</summary>
    public static string Marker(object formatVersion) =>
        $"<?xml version=\"1.0\" encoding=\"utf-8\"?><mots xmlns=\"urn:mots:metadata\" application=\"Mots\" formatVersion=\"{formatVersion}\"/>";

    /// <summary>Styles d'un document Word typique : Normal (défaut), Titre 1 sous son identifiant français, police par défaut.</summary>
    public static List<W.Style> StandardStyles() =>
    [
        ParaStyle("Normal", "Normal", isDefault: true),
        ParaStyle("Titre1", "heading 1"),
        CharStyle("Policepardfaut", "Default Paragraph Font", isDefault: true),
    ];
}
