using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Documents;
using DocumentFormat.OpenXml.Packaging;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace Mots;

/// <summary>
/// Conversion entre un <see cref="FlowDocument"/> WPF et un fichier .docx (Office Open XML).
/// Pris en charge : styles de paragraphe (voir <see cref="ParagraphStyles"/>), gras / italique / souligné,
/// sauts de ligne et tabulations. La mise en page (police, taille, alignement, retrait, interligne…) vient
/// uniquement des styles.
/// </summary>
public static class DocxConverter
{
    // ---------- Écriture ----------

    public static void Save(FlowDocument document, string path) =>
        Save(document.Blocks.OfType<Paragraph>(), path);

    public static void Save(IEnumerable<Paragraph> paragraphs, string path)
    {
        // Écriture en mémoire d'abord : un échec ne laisse pas un fichier à moitié écrit.
        using var ms = new MemoryStream();
        using (var package = WordprocessingDocument.Create(ms, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
        {
            var main = package.AddMainDocumentPart();
            var body = new W.Body();
            main.Document = new W.Document(body);

            foreach (var paragraph in paragraphs)
                body.Append(ToWordParagraph(paragraph));

            body.Append(new W.SectionProperties(
                new W.PageSize { Width = 11906, Height = 16838 }, // A4
                new W.PageMargin { Top = 1417, Right = 1417, Bottom = 1417, Left = 1417, Header = 708, Footer = 708, Gutter = 0 }));

            MotsMetadata.Write(main);
            main.AddNewPart<StyleDefinitionsPart>().Styles = BuildStyles();

            main.Document.Save();
        }
        File.WriteAllBytes(path, ms.ToArray());
    }

    private static W.Paragraph ToWordParagraph(Paragraph source)
    {
        var wp = new W.Paragraph();
        var style = ParagraphStyles.Get(ParagraphStyles.KindOf(source));

        var props = new W.ParagraphProperties();
        if (style.Kind != StyleKind.Normal)
            props.Append(new W.ParagraphStyleId { Val = style.WordId });
        if (props.HasChildren)
            wp.Append(props);

        foreach (var inline in Flatten(source.Inlines))
        {
            switch (inline)
            {
                case Run run:
                    wp.Append(ToWordRun(run, style));
                    break;
                case LineBreak:
                    wp.Append(new W.Run(new W.Break()));
                    break;
            }
        }
        return wp;
    }

    private static W.JustificationValues ToJustification(TextAlignment alignment) => alignment switch
    {
        TextAlignment.Center => W.JustificationValues.Center,
        TextAlignment.Right => W.JustificationValues.Right,
        TextAlignment.Justify => W.JustificationValues.Both,
        _ => W.JustificationValues.Left,
    };

    // ---------- Styles ----------

    private static W.Styles BuildStyles()
    {
        var styles = new W.Styles();
        foreach (var definition in ParagraphStyles.All)
            styles.Append(ToWordStyle(definition));
        return styles;
    }

    private static string Twips(double value) => Math.Round(value).ToString(CultureInfo.InvariantCulture);

    private static W.Style ToWordStyle(ParagraphStyles.Definition d)
    {
        var isNormal = d.Kind == StyleKind.Normal;
        var style = new W.Style { Type = W.StyleValues.Paragraph, StyleId = d.WordId };
        if (isNormal)
            style.Default = true;
        if (d.IsCustom)
            style.CustomStyle = true;

        // Ordre des éléments imposé par le schéma.
        style.Append(new W.StyleName { Val = d.WordName });
        if (!isNormal)
        {
            style.Append(new W.BasedOn { Val = ParagraphStyles.Normal.WordId });
            style.Append(new W.NextParagraphStyle { Val = ParagraphStyles.Get(d.NextKind).WordId });
            if (!d.IsCustom)
                style.Append(new W.UIPriority { Val = 9 });
        }
        style.Append(new W.PrimaryStyle());

        var paragraph = new W.StyleParagraphProperties();
        if (d.PageBreakBefore)
            paragraph.Append(new W.PageBreakBefore());
        paragraph.Append(new W.WidowControl());
        paragraph.Append(new W.SpacingBetweenLines
        {
            Before = Twips(d.SpaceBeforePt * 20),
            After = Twips(d.SpaceAfterPt * 20),
            Line = Twips(d.LineSpacing * 240),
            LineRule = W.LineSpacingRuleValues.Auto,
        });
        paragraph.Append(new W.Indentation { FirstLine = Twips(d.FirstLineIndentCm * ParagraphStyles.TwipsPerCm) });
        paragraph.Append(new W.Justification { Val = ToJustification(d.Alignment) });
        if (d.OutlineLevel is { } outline)
            paragraph.Append(new W.OutlineLevel { Val = outline });
        style.Append(paragraph);

        var run = new W.StyleRunProperties();
        if (isNormal) // les autres styles héritent de la police de « Normal »
            run.Append(new W.RunFonts
            {
                Ascii = ParagraphStyles.FontName,
                HighAnsi = ParagraphStyles.FontName,
                EastAsia = ParagraphStyles.FontName,
                ComplexScript = ParagraphStyles.FontName,
            });
        if (d.Bold)
            run.Append(new W.Bold());
        if (isNormal || d.FontSizePt != ParagraphStyles.Normal.FontSizePt) // sinon, hérité de « Normal »
            run.Append(new W.FontSize { Val = Twips(d.FontSizePt * 2) });
        if (run.HasChildren)
            style.Append(run);

        return style;
    }

    private static IEnumerable<Inline> Flatten(InlineCollection inlines)
    {
        foreach (var inline in inlines)
        {
            if (inline is Span span)
            {
                foreach (var inner in Flatten(span.Inlines))
                    yield return inner;
            }
            else
            {
                yield return inline;
            }
        }
    }

    private static W.Run ToWordRun(Run source, ParagraphStyles.Definition style)
    {
        var wr = new W.Run();

        // Seuls les écarts par rapport au style sont écrits. Ordre des éléments imposé par le schéma : b, i, u.
        var props = new W.RunProperties();
        var bold = source.FontWeight.ToOpenTypeWeight() >= 600;
        if (bold != style.Bold)
            props.Append(bold ? new W.Bold() : new W.Bold { Val = false });
        if (source.FontStyle != FontStyles.Normal)
            props.Append(new W.Italic());
        if (IsUnderlined(source))
            props.Append(new W.Underline { Val = W.UnderlineValues.Single });
        if (props.HasChildren)
            wr.Append(props);

        var text = new StringBuilder();
        void FlushText()
        {
            if (text.Length == 0) return;
            wr.Append(new W.Text(text.ToString()) { Space = DocumentFormat.OpenXml.SpaceProcessingModeValues.Preserve });
            text.Clear();
        }

        foreach (var c in source.Text)
        {
            switch (c)
            {
                case '\t':
                    FlushText();
                    wr.Append(new W.TabChar());
                    break;
                case '\n':
                    FlushText();
                    wr.Append(new W.Break());
                    break;
                case '\r':
                    break;
                default:
                    text.Append(c);
                    break;
            }
        }
        FlushText();
        return wr;
    }

    /// <summary>Le soulignement est porté par la Run elle-même ou par l'un de ses Span parents.</summary>
    private static bool IsUnderlined(Run run)
    {
        DependencyObject? node = run;
        while (node is Inline inline)
        {
            if (inline.TextDecorations.Any(d => d.Location == TextDecorationLocation.Underline))
                return true;
            node = inline.Parent;
        }
        return false;
    }

    // ---------- Lecture ----------

    /// <summary>Ouvre un document enregistré par Mots (le marqueur de compatibilité est exigé).</summary>
    public static FlowDocument Load(string path) => Read(path, requireMarker: true);

    /// <summary>Importe un document Word quelconque, sans marqueur Mots.</summary>
    public static FlowDocument Import(string path) => Read(path, requireMarker: false);

    /// <exception cref="IncompatibleFileException">
    /// Le fichier n'est pas un document Word valide, n'a pas le marqueur Mots (ouverture) ou utilise un style qui
    /// n'existe pas dans Mots.
    /// </exception>
    private static FlowDocument Read(string path, bool requireMarker)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var package = OpenPackage(stream);

        var main = package.MainDocumentPart
                   ?? throw new IncompatibleFileException("Ce fichier n'est pas un document Word valide.");
        if (requireMarker)
            MotsMetadata.Verify(main);
        var body = main.Document?.Body
                   ?? throw new IncompatibleFileException("Ce fichier n'est pas un document Word valide.");

        var reader = new Reader(main);
        var document = new FlowDocument();
        foreach (var element in body.ChildElements)
        {
            switch (element)
            {
                case W.Paragraph paragraph:
                    document.Blocks.Add(reader.ToParagraph(paragraph));
                    break;
                case W.Table or W.SdtBlock:
                    // Pas de gestion des tableaux : on en garde le texte, cellule par cellule.
                    foreach (var inner in element.Descendants<W.Paragraph>())
                        document.Blocks.Add(reader.ToParagraph(inner));
                    break;
            }
        }
        reader.ThrowIfUnsupportedStyles();

        if (document.Blocks.Count == 0)
        {
            var empty = new Paragraph();
            ParagraphStyles.Apply(empty, StyleKind.Normal);
            document.Blocks.Add(empty);
        }
        return document;
    }

    private static WordprocessingDocument OpenPackage(Stream stream)
    {
        try
        {
            return WordprocessingDocument.Open(stream, false);
        }
        catch (Exception ex) when (ex is FileFormatException or DocumentFormat.OpenXml.Packaging.OpenXmlPackageException)
        {
            throw new IncompatibleFileException("Ce fichier n'est pas un document Word valide.");
        }
    }

    /// <summary>
    /// Convertit les paragraphes Word. Les styles sont reconnus par leur nom (l'identifiant varie selon la langue de
    /// Word : « Titre1 » en français) ; tout style inconnu de Mots est relevé puis fait rejeter le document.
    /// </summary>
    private sealed class Reader
    {
        private const string DefaultCharacterStyleName = "Default Paragraph Font";

        private readonly Dictionary<string, string> _styleNames = [];
        private readonly string? _defaultParagraphStyleId;
        private readonly Dictionary<string, int> _unsupportedParagraphStyles = [];
        private readonly Dictionary<string, int> _unsupportedCharacterStyles = [];

        public Reader(MainDocumentPart main)
        {
            foreach (var style in main.StyleDefinitionsPart?.Styles?.Elements<W.Style>() ?? Enumerable.Empty<W.Style>())
            {
                if (style.StyleId?.Value is not { } id) continue;
                _styleNames[id] = style.StyleName?.Val?.Value ?? id;
                if (style.Type?.Value == W.StyleValues.Paragraph && style.Default?.Value == true)
                    _defaultParagraphStyleId = id;
            }
        }

        public Paragraph ToParagraph(W.Paragraph source)
        {
            var paragraph = new Paragraph();
            ParagraphStyles.Apply(paragraph, ResolveParagraphStyle(source));

            foreach (var run in source.Descendants<W.Run>())
                AddRun(paragraph, run);

            return paragraph;
        }

        public void ThrowIfUnsupportedStyles()
        {
            if (_unsupportedParagraphStyles.Count == 0 && _unsupportedCharacterStyles.Count == 0) return;

            var found = _unsupportedParagraphStyles.Select(kv => $"« {kv.Key} » ({Count(kv.Value, "paragraphe")})")
                .Concat(_unsupportedCharacterStyles.Select(kv => $"« {kv.Key} » ({Count(kv.Value, "passage")})"))
                .ToList();
            const int shown = 6;
            var list = string.Join(", ", found.Take(shown));
            if (found.Count > shown)
                list += $" et {found.Count - shown} autre(s)";

            throw new IncompatibleFileException(
                $"Ce document utilise des styles qui n'existent pas dans Mots : {list}. " +
                "Remplacez-les dans Word par « Normal » ou « Titre 1 », puis réessayez.");
        }

        private static string Count(int n, string noun) => n > 1 ? $"{n} {noun}s" : $"{n} {noun}";

        private static void Note(Dictionary<string, int> counts, string name) =>
            counts[name] = counts.GetValueOrDefault(name) + 1;

        private StyleKind ResolveParagraphStyle(W.Paragraph source)
        {
            // Sans style explicite, le paragraphe prend le style par défaut du document (« Normal » dans Word).
            var id = source.ParagraphProperties?.ParagraphStyleId?.Val?.Value ?? _defaultParagraphStyleId;
            if (id is null) return StyleKind.Normal;

            var name = _styleNames.GetValueOrDefault(id, id);
            foreach (var definition in ParagraphStyles.All)
            {
                if (string.Equals(definition.WordName, name, StringComparison.OrdinalIgnoreCase)
                    || (!_styleNames.ContainsKey(id) && definition.WordId == id))
                    return definition.Kind;
            }

            Note(_unsupportedParagraphStyles, name);
            return StyleKind.Normal;
        }

        private void AddRun(Paragraph paragraph, W.Run source)
        {
            if (source.RunProperties?.RunStyle?.Val?.Value is { } styleId)
            {
                var name = _styleNames.GetValueOrDefault(styleId, styleId);
                if (!string.Equals(name, DefaultCharacterStyleName, StringComparison.OrdinalIgnoreCase))
                    Note(_unsupportedCharacterStyles, name);
            }

            var text = new StringBuilder();
            void FlushText()
            {
                if (text.Length == 0) return;
                paragraph.Inlines.Add(StyledRun(text.ToString(), source.RunProperties));
                text.Clear();
            }

            foreach (var child in source.ChildElements)
            {
                switch (child)
                {
                    case W.Text t:
                        text.Append(t.Text);
                        break;
                    case W.TabChar:
                        text.Append('\t');
                        break;
                    case W.NoBreakHyphen:
                        text.Append('-');
                        break;
                    case W.Break:
                        FlushText();
                        paragraph.Inlines.Add(new LineBreak());
                        break;
                }
            }
            FlushText();
        }
    }

    private static Run StyledRun(string text, W.RunProperties? props)
    {
        var run = new Run(text);
        if (props is null) return run;

        // Le gras explicite (même « non gras » dans un titre) prime sur celui du style ; police et taille sont ignorées.
        if (props.Bold is { } bold)
            run.FontWeight = bold.Val?.Value ?? true ? FontWeights.Bold : FontWeights.Normal;
        if (props.Italic is { } italic && (italic.Val?.Value ?? true))
            run.FontStyle = FontStyles.Italic;
        if (props.Underline is { } underline && underline.Val?.Value != W.UnderlineValues.None)
            run.TextDecorations = TextDecorations.Underline;

        return run;
    }
}
