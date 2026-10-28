using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using DocumentFormat.OpenXml.Packaging;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace Mots;

/// <summary>
/// Conversion entre un <see cref="FlowDocument"/> WPF et un fichier .docx (Office Open XML).
/// Pris en charge : paragraphes, alignement, gras / italique / souligné, police, taille, listes à puces
/// et numérotées (avec sous-niveaux), sauts de ligne et tabulations.
/// </summary>
public static class DocxConverter
{
    private const double PixelsPerPoint = 4.0 / 3.0;

    // ---------- Écriture ----------

    public static void Save(FlowDocument document, string path)
    {
        // Écriture en mémoire d'abord : un échec ne laisse pas un fichier à moitié écrit.
        using var ms = new MemoryStream();
        using (var package = WordprocessingDocument.Create(ms, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
        {
            var main = package.AddMainDocumentPart();
            var body = new W.Body();
            main.Document = new W.Document(body);

            var numbering = new NumberingBuilder();
            foreach (var block in document.Blocks)
                WriteBlock(body, block, numbering, 0, 0);

            body.Append(new W.SectionProperties(
                new W.PageSize { Width = 11906, Height = 16838 }, // A4
                new W.PageMargin { Top = 1417, Right = 1417, Bottom = 1417, Left = 1417, Header = 708, Footer = 708, Gutter = 0 }));

            MotsMetadata.Write(main);

            if (numbering.HasLists)
                main.AddNewPart<NumberingDefinitionsPart>().Numbering = numbering.Root;

            main.Document.Save();
        }
        File.WriteAllBytes(path, ms.ToArray());
    }

    private static void WriteBlock(W.Body body, Block block, NumberingBuilder numbering, int numId, int level)
    {
        switch (block)
        {
            case Paragraph p:
                body.Append(ToWordParagraph(p, numId, level));
                break;
            case List list:
                WriteList(body, list, numbering, numId, level);
                break;
            case Section section:
                foreach (var inner in section.Blocks)
                    WriteBlock(body, inner, numbering, numId, level);
                break;
        }
    }

    private static void WriteList(W.Body body, List list, NumberingBuilder numbering, int parentNumId, int parentLevel)
    {
        var ordered = IsOrdered(list.MarkerStyle);
        var isNested = parentNumId > 0;
        var level = isNested ? parentLevel + 1 : 0;

        // Une liste imbriquée du même genre réutilise la numérotation du parent.
        var numId = isNested && numbering.IsOrdered(parentNumId) == ordered
            ? parentNumId
            : numbering.NewInstance(ordered);

        foreach (var item in list.ListItems)
        {
            var first = true;
            foreach (var block in item.Blocks)
            {
                if (block is Paragraph p)
                {
                    body.Append(ToWordParagraph(p, first ? numId : 0, level));
                    first = false;
                }
                else if (block is List nested)
                {
                    WriteList(body, nested, numbering, numId, level);
                }
            }
            if (first) // élément sans paragraphe : on garde quand même la puce
                body.Append(ToWordParagraph(new Paragraph(), numId, level));
        }
    }

    private static bool IsOrdered(TextMarkerStyle style) => style is
        TextMarkerStyle.Decimal or TextMarkerStyle.LowerLatin or TextMarkerStyle.UpperLatin or
        TextMarkerStyle.LowerRoman or TextMarkerStyle.UpperRoman;

    private static W.Paragraph ToWordParagraph(Paragraph source, int numId, int level)
    {
        var wp = new W.Paragraph();

        var props = new W.ParagraphProperties();
        if (numId > 0)
            props.Append(new W.NumberingProperties(
                new W.NumberingLevelReference { Val = level },
                new W.NumberingId { Val = numId }));

        W.JustificationValues? justification = source.TextAlignment switch
        {
            TextAlignment.Center => W.JustificationValues.Center,
            TextAlignment.Right => W.JustificationValues.Right,
            TextAlignment.Justify => W.JustificationValues.Both,
            _ => null,
        };
        if (justification is { } jc)
            props.Append(new W.Justification { Val = jc });

        if (props.HasChildren)
            wp.Append(props);

        foreach (var inline in Flatten(source.Inlines))
        {
            switch (inline)
            {
                case Run run:
                    wp.Append(ToWordRun(run));
                    break;
                case LineBreak:
                    wp.Append(new W.Run(new W.Break()));
                    break;
            }
        }
        return wp;
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

    private static W.Run ToWordRun(Run source)
    {
        var wr = new W.Run();

        // Ordre des éléments imposé par le schéma : rFonts, b, i, sz, u.
        var props = new W.RunProperties();
        var fontName = source.FontFamily.Source.Split(',')[0].Trim();
        if (fontName.Length > 0)
            props.Append(new W.RunFonts { Ascii = fontName, HighAnsi = fontName, ComplexScript = fontName });
        if (source.FontWeight.ToOpenTypeWeight() >= 600)
            props.Append(new W.Bold());
        if (source.FontStyle != FontStyles.Normal)
            props.Append(new W.Italic());
        var halfPoints = (int)Math.Round(source.FontSize / PixelsPerPoint * 2);
        props.Append(new W.FontSize { Val = halfPoints.ToString(CultureInfo.InvariantCulture) });
        if (IsUnderlined(source))
            props.Append(new W.Underline { Val = W.UnderlineValues.Single });
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

    private sealed class NumberingBuilder
    {
        private const int BulletAbstractId = 1;
        private const int DecimalAbstractId = 2;

        private readonly Dictionary<int, bool> _orderedByNumId = [];
        private int _nextNumId = 1;

        public W.Numbering Root { get; } = new();

        public bool HasLists => _orderedByNumId.Count > 0;

        public NumberingBuilder()
        {
            Root.Append(CreateAbstract(BulletAbstractId, ordered: false));
            Root.Append(CreateAbstract(DecimalAbstractId, ordered: true));
        }

        public bool IsOrdered(int numId) => _orderedByNumId[numId];

        public int NewInstance(bool ordered)
        {
            var numId = _nextNumId++;
            var instance = new W.NumberingInstance(
                new W.AbstractNumId { Val = ordered ? DecimalAbstractId : BulletAbstractId })
            { NumberID = numId };

            // Chaque liste numérotée repart de 1.
            if (ordered)
                instance.Append(new W.LevelOverride(new W.StartOverrideNumberingValue { Val = 1 }) { LevelIndex = 0 });

            Root.Append(instance);
            _orderedByNumId[numId] = ordered;
            return numId;
        }

        private static W.AbstractNum CreateAbstract(int id, bool ordered)
        {
            var abstractNum = new W.AbstractNum(
                new W.MultiLevelType { Val = W.MultiLevelValues.HybridMultilevel })
            { AbstractNumberId = id };

            for (var level = 0; level < 9; level++)
            {
                abstractNum.Append(new W.Level(
                    new W.StartNumberingValue { Val = 1 },
                    new W.NumberingFormat { Val = ordered ? W.NumberFormatValues.Decimal : W.NumberFormatValues.Bullet },
                    new W.LevelText { Val = ordered ? $"%{level + 1}." : "•" },
                    new W.LevelJustification { Val = W.LevelJustificationValues.Left },
                    new W.PreviousParagraphProperties(
                        new W.Indentation { Left = (720 * (level + 1)).ToString(CultureInfo.InvariantCulture), Hanging = "360" }))
                { LevelIndex = level });
            }
            return abstractNum;
        }
    }

    // ---------- Lecture ----------

    public static FlowDocument Load(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var package = OpenPackage(stream);

        var main = package.MainDocumentPart
                   ?? throw new InvalidDataException("Ce fichier n'est pas un document Word valide.");
        MotsMetadata.Verify(main);
        var body = main.Document?.Body
                   ?? throw new InvalidDataException("Ce fichier n'est pas un document Word valide.");

        var document = new FlowDocument { TextAlignment = TextAlignment.Left };
        var reader = new Reader(document, main.NumberingDefinitionsPart?.Numbering);

        foreach (var element in body.ChildElements)
        {
            switch (element)
            {
                case W.Paragraph paragraph:
                    reader.Add(paragraph);
                    break;
                case W.Table or W.SdtBlock:
                    // Pas de gestion des tableaux pour l'instant : on en garde le texte, cellule par cellule.
                    foreach (var inner in element.Descendants<W.Paragraph>())
                        reader.Add(inner);
                    break;
            }
        }

        if (document.Blocks.Count == 0)
            document.Blocks.Add(new Paragraph());
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

    private sealed class Reader(FlowDocument document, W.Numbering? numbering)
    {
        private readonly Stack<List> _lists = new();
        private int _rootNumId;

        public void Add(W.Paragraph source)
        {
            var paragraph = ToParagraph(source);

            var numProps = source.ParagraphProperties?.NumberingProperties;
            var numId = numProps?.NumberingId?.Val?.Value ?? 0;
            if (numId <= 0)
            {
                _lists.Clear();
                document.Blocks.Add(paragraph);
                return;
            }

            AddListItem(paragraph, numId, numProps?.NumberingLevelReference?.Val?.Value ?? 0);
        }

        private void AddListItem(Paragraph paragraph, int numId, int level)
        {
            if (_lists.Count == 0 || (level == 0 && numId != _rootNumId))
            {
                _lists.Clear();
                var root = new List { MarkerStyle = MarkerFor(numId, 0) };
                document.Blocks.Add(root);
                _lists.Push(root);
                _rootNumId = numId;
            }

            while (_lists.Count - 1 < level)
            {
                var parent = _lists.Peek();
                var host = parent.ListItems.LastListItem;
                if (host is null)
                {
                    host = new ListItem();
                    parent.ListItems.Add(host);
                }
                var nested = new List { MarkerStyle = MarkerFor(numId, _lists.Count) };
                host.Blocks.Add(nested);
                _lists.Push(nested);
            }
            while (_lists.Count - 1 > level)
                _lists.Pop();

            _lists.Peek().ListItems.Add(new ListItem(paragraph));
        }

        private TextMarkerStyle MarkerFor(int numId, int level)
        {
            var instance = numbering?.Elements<W.NumberingInstance>().FirstOrDefault(n => n.NumberID?.Value == numId);
            var abstractId = instance?.AbstractNumId?.Val?.Value;
            var abstractNum = numbering?.Elements<W.AbstractNum>().FirstOrDefault(a => a.AbstractNumberId?.Value == abstractId);
            var format = abstractNum?.Elements<W.Level>().FirstOrDefault(l => l.LevelIndex?.Value == level)
                ?.NumberingFormat?.Val?.Value;

            if (format == W.NumberFormatValues.Bullet)
                return (level % 3) switch { 0 => TextMarkerStyle.Disc, 1 => TextMarkerStyle.Circle, _ => TextMarkerStyle.Square };
            if (format == W.NumberFormatValues.LowerLetter) return TextMarkerStyle.LowerLatin;
            if (format == W.NumberFormatValues.UpperLetter) return TextMarkerStyle.UpperLatin;
            if (format == W.NumberFormatValues.LowerRoman) return TextMarkerStyle.LowerRoman;
            if (format == W.NumberFormatValues.UpperRoman) return TextMarkerStyle.UpperRoman;
            return TextMarkerStyle.Decimal;
        }
    }

    private static Paragraph ToParagraph(W.Paragraph source)
    {
        var paragraph = new Paragraph();

        var justification = source.ParagraphProperties?.Justification?.Val?.Value;
        if (justification == W.JustificationValues.Center)
            paragraph.TextAlignment = TextAlignment.Center;
        else if (justification == W.JustificationValues.Right || justification == W.JustificationValues.End)
            paragraph.TextAlignment = TextAlignment.Right;
        else if (justification == W.JustificationValues.Both || justification == W.JustificationValues.Distribute)
            paragraph.TextAlignment = TextAlignment.Justify;

        foreach (var run in source.Descendants<W.Run>())
            AddRun(paragraph, run);

        return paragraph;
    }

    private static void AddRun(Paragraph paragraph, W.Run source)
    {
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

    private static Run StyledRun(string text, W.RunProperties? props)
    {
        var run = new Run(text);
        if (props is null) return run;

        if (props.Bold is { } bold && (bold.Val?.Value ?? true))
            run.FontWeight = FontWeights.Bold;
        if (props.Italic is { } italic && (italic.Val?.Value ?? true))
            run.FontStyle = FontStyles.Italic;
        if (props.Underline is { } underline && underline.Val?.Value != W.UnderlineValues.None)
            run.TextDecorations = TextDecorations.Underline;

        var font = props.RunFonts?.Ascii?.Value;
        if (!string.IsNullOrWhiteSpace(font))
            run.FontFamily = new FontFamily(font);

        if (double.TryParse(props.FontSize?.Val?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var halfPoints)
            && halfPoints > 0)
            run.FontSize = halfPoints / 2 * PixelsPerPoint;

        return run;
    }
}
