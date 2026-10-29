using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Threading;

namespace Mots;

public static class ChapterCommands
{
    public static readonly RoutedCommand Next = new(nameof(Next), typeof(ChapterCommands));
    public static readonly RoutedCommand Previous = new(nameof(Previous), typeof(ChapterCommands));
}

/// <summary>
/// WPF met en page tout le document de l'éditeur à chaque frappe : la saisie ralentit avec la taille du manuscrit.
/// L'éditeur n'affiche donc qu'une fenêtre de trois chapitres au plus (celui du curseur, le précédent et le
/// suivant) ; les autres chapitres attendent à l'écart, chacun dans son <see cref="FlowDocument"/>. Un chapitre est
/// un « Titre 1 » et le texte qui le suit ; le texte avant le premier titre forme un chapitre sans titre.
/// Le manuscrit est donc : chapitres masqués avant, fenêtre (<c>Editor.Document</c>), chapitres masqués après.
/// </summary>
public partial class MainWindow
{
    private List<FlowDocument> _before = [];
    private List<FlowDocument> _after = [];
    private int _switchDepth; // > 0 pendant que la fenêtre change : ce n'est pas une modification du texte

    private bool IsSwitching => _switchDepth > 0;

    private void WhileSwitching(Action change)
    {
        _switchDepth++;
        try
        {
            change();
        }
        finally
        {
            _switchDepth--;
        }
    }

    private IEnumerable<FlowDocument> AllDocuments => _before.Concat([Editor.Document]).Concat(_after);

    private IEnumerable<Paragraph> AllParagraphs => AllDocuments.SelectMany(d => d.Blocks.OfType<Paragraph>());

    private int ChapterCount => _before.Count + WindowChapters().Count + _after.Count;

    private static bool IsHeading(Block? block) =>
        block is Paragraph paragraph && ParagraphStyles.KindOf(paragraph) == StyleKind.Heading1;

    /// <summary>Répartit des blocs en chapitres : un titre ouvre un chapitre.</summary>
    private static List<List<Block>> GroupByChapter(IEnumerable<Block> blocks)
    {
        var chapters = new List<List<Block>>();
        foreach (var block in blocks)
        {
            if (chapters.Count == 0 || IsHeading(block))
                chapters.Add([]);
            chapters[^1].Add(block);
        }
        return chapters;
    }

    private List<List<Block>> WindowChapters() => GroupByChapter(Editor.Document.Blocks);

    /// <summary>Place les blocs dans un document à eux, dont ils quittent leur document d'origine.</summary>
    private static FlowDocument ToDocument(IEnumerable<Block> blocks)
    {
        var document = new FlowDocument();
        foreach (var block in blocks)
        {
            (block.Parent as FlowDocument)?.Blocks.Remove(block);
            document.Blocks.Add(block);
        }
        return document;
    }

    /// <summary>Vide le document en le répartissant en chapitres.</summary>
    private static List<FlowDocument> SplitIntoChapters(FlowDocument source)
    {
        var chapters = GroupByChapter(source.Blocks.ToList()).Select(ToDocument).ToList();
        if (chapters.Count == 0)
        {
            var empty = new Paragraph();
            ParagraphStyles.Apply(empty, StyleKind.Normal);
            chapters.Add(new FlowDocument(empty));
        }
        return chapters;
    }

    private void ReplaceDocument(FlowDocument document)
    {
        _outline = []; // les éléments du plan désignent des paragraphes de l'ancien document
        ShowWindow(SplitIntoChapters(document), 0);
        ScheduleStatusRefresh();
        RefreshOutline();
    }

    private void SetEditorDocument(FlowDocument document)
    {
        WhileSwitching(() => Editor.Document = document);
        MarksOverlay.Refresh();
    }

    /// <summary>Ramène tous les chapitres à l'écart, l'éditeur ne montrant plus rien d'eux.</summary>
    private List<FlowDocument> Detach()
    {
        List<FlowDocument> chapters = [];
        WhileSwitching(() =>
        {
            var window = WindowChapters();
            SetEditorDocument(new FlowDocument(new Paragraph())); // l'éditeur ne doit pas perdre son document en cours de route
            chapters = [.. _before, .. window.Select(ToDocument), .. _after];
        });
        return chapters;
    }

    /// <summary>Affiche le chapitre d'indice <paramref name="center"/> avec ses voisins.</summary>
    private void ShowWindow(List<FlowDocument> chapters, int center)
    {
        var first = Math.Max(0, center - 1);
        var last = Math.Min(chapters.Count - 1, center + 1);
        var window = ToDocument(chapters.GetRange(first, last - first + 1).SelectMany(c => c.Blocks.ToList()));

        _before = chapters.GetRange(0, first);
        _after = chapters.GetRange(last + 1, chapters.Count - last - 1);
        SetEditorDocument(window);
    }

    /// <summary>Indice du chapitre qui contient le paragraphe, dans tout le manuscrit.</summary>
    private int ChapterIndexOf(Paragraph paragraph)
    {
        if (paragraph.Parent is not FlowDocument document) return -1;
        if (ReferenceEquals(document, Editor.Document))
            return _before.Count + WindowChapters().FindIndex(c => c.Contains(paragraph));

        var before = _before.IndexOf(document);
        if (before >= 0) return before;
        var after = _after.IndexOf(document);
        return after >= 0 ? _before.Count + WindowChapters().Count + after : -1;
    }

    private int CaretChapterIndex() =>
        Editor.CaretPosition.Paragraph is { } paragraph ? Math.Max(0, ChapterIndexOf(paragraph)) : 0;

    /// <summary>Affiche un chapitre au centre de la fenêtre, titre en haut, curseur au début du chapitre.</summary>
    private void OpenChapter(int index)
    {
        NormalizeWindow();
        var chapters = Detach();
        index = Math.Clamp(index, 0, chapters.Count - 1);
        var start = chapters[index].Blocks.FirstBlock;
        ShowWindow(chapters, index);

        Editor.CaretPosition = start.ContentStart;
        Editor.Focus();
        ScrollChapterToTop((Paragraph)start);
    }

    private void NextChapter_Executed(object sender, ExecutedRoutedEventArgs e) =>
        OpenChapter(Math.Min(CaretChapterIndex() + 1, ChapterCount - 1));

    private void PreviousChapter_Executed(object sender, ExecutedRoutedEventArgs e) =>
        OpenChapter(CaretChapterIndex() - 1);

    /// <summary>
    /// Place le titre en haut de la fenêtre. WPF estime la hauteur des parties du document qu'il n'a pas encore
    /// mises en page : la position visée dérive donc au fil de la mise en page, d'où des corrections successives
    /// jusqu'à ce que le titre soit bien placé.
    /// </summary>
    private void ScrollChapterToTop(Paragraph paragraph, int attempt = 0)
    {
        const double Gap = 24; // espace laissé au-dessus du titre, sous le remplissage de l'éditeur (qui rogne ce qui y déborde)
        const int MaxAttempts = 15;

        if (paragraph.Parent is null || !Editor.IsLoaded) return;

        // Force la mise en page jusqu'au titre : sans cela, sa position est inconnue.
        paragraph.BringIntoView();
        Editor.UpdateLayout();

        var rect = paragraph.ContentStart.GetCharacterRect(LogicalDirection.Forward);
        if (!rect.IsEmpty)
        {
            var delta = rect.Top - (Editor.Padding.Top + Gap);
            if (Math.Abs(delta) <= 1) return;

            var before = Editor.VerticalOffset;
            Editor.ScrollToVerticalOffset(Math.Max(0, before + delta));
            if (attempt > 0 && Editor.VerticalOffset == before) return; // butée atteinte (fin du document)
        }

        if (attempt < MaxAttempts)
            Dispatcher.BeginInvoke(DispatcherPriority.Background, () => ScrollChapterToTop(paragraph, attempt + 1));
    }

    // ---------- La fenêtre suit le curseur ----------

    /// <summary>Garde, autour du chapitre du curseur, le chapitre précédent et le suivant, et rien de plus.</summary>
    private void FollowCaret()
    {
        if (IsSwitching || Editor.CaretPosition.Paragraph is not { } caret) return;
        if (NextWindowMove(caret) == WindowMove.None) return;

        KeepingCaretInView(() =>
        {
            while (NextWindowMove(caret) is var move && move != WindowMove.None)
            {
                switch (move)
                {
                    case WindowMove.TrimTop:
                        _before.Add(ToDocument(WindowChapters()[0]));
                        return; // la mise en page a changé : l'éventuel chapitre suivant à retirer attend le prochain événement
                    case WindowMove.TrimBottom:
                        _after.Insert(0, ToDocument(WindowChapters()[^1]));
                        break;
                    case WindowMove.PullTop:
                        PullFromBefore();
                        break;
                    case WindowMove.PullBottom:
                        PullFromAfter();
                        break;
                }
            }
        });
    }

    private enum WindowMove { None, TrimTop, TrimBottom, PullTop, PullBottom }

    private WindowMove NextWindowMove(Paragraph caret)
    {
        var chapters = WindowChapters();
        var k = chapters.FindIndex(c => c.Contains(caret));
        if (k < 0) return WindowMove.None;

        var below = chapters.Count - 1 - k;
        if (below > 1) return WindowMove.TrimBottom;
        if (k < 1 && _before.Count > 0) return WindowMove.PullTop;
        if (below < 1 && _after.Count > 0) return WindowMove.PullBottom;
        if (k > 1 && IsScrolledAbove(chapters[0][0], chapters[1][0])) return WindowMove.TrimTop; // en dernier : la mise en page change ensuite
        return WindowMove.None;
    }

    /// <summary>
    /// Retirer le premier chapitre ne doit pas faire sauter la vue : le texte se recale en décalant le défilement de
    /// la hauteur retirée, ce qui n'est possible que si le défilement est au moins aussi grand. Sinon, on garde le
    /// chapitre en trop jusqu'à ce qu'il soit sorti de la zone visible.
    /// </summary>
    private bool IsScrolledAbove(Block first, Block second)
    {
        const double Margin = 32; // la hauteur retirée dépasse un peu l'écart mesuré entre les deux titres (marges de bloc)

        if (!Editor.IsLoaded) return true; // rien n'est à l'écran, donc rien ne peut sauter

        var top = first.ContentStart.GetCharacterRect(LogicalDirection.Forward);
        var next = second.ContentStart.GetCharacterRect(LogicalDirection.Forward);
        return !top.IsEmpty && !next.IsEmpty && next.Top - top.Top + Margin <= Editor.VerticalOffset;
    }

    /// <summary>
    /// Une modification de style peut retirer son titre au premier chapitre de la fenêtre : il rejoint alors le
    /// chapitre masqué qui le précède, qu'on remet dans la fenêtre pour ne pas séparer un texte de son titre.
    /// </summary>
    private void NormalizeWindow()
    {
        if (_before.Count == 0 || IsHeading(Editor.Document.Blocks.FirstBlock)) return;

        KeepingCaretInView(() =>
        {
            while (_before.Count > 0 && !IsHeading(Editor.Document.Blocks.FirstBlock))
                PullFromBefore();
        });
    }

    private void PullFromBefore()
    {
        var document = _before[^1];
        _before.RemoveAt(_before.Count - 1);
        var first = Editor.Document.Blocks.FirstBlock;
        foreach (var block in document.Blocks.ToList())
        {
            document.Blocks.Remove(block);
            Editor.Document.Blocks.InsertBefore(first, block);
        }
    }

    private void PullFromAfter()
    {
        var document = _after[0];
        _after.RemoveAt(0);
        foreach (var block in document.Blocks.ToList())
        {
            document.Blocks.Remove(block);
            Editor.Document.Blocks.Add(block);
        }
    }

    /// <summary>
    /// Exécute un changement de la fenêtre sans que le texte ne bouge à l'écran : ajouter ou retirer des chapitres
    /// au-dessus du curseur décalerait sinon la vue.
    /// </summary>
    private void KeepingCaretInView(Action change)
    {
        var caret = Editor.CaretPosition;
        var paragraph = caret.Paragraph;
        var offset = paragraph?.ContentStart.GetOffsetToPosition(caret) ?? 0;

        Rect CaretRect() =>
            paragraph?.ContentStart.GetPositionAtOffset(offset)?.GetCharacterRect(LogicalDirection.Forward) ?? Rect.Empty;

        // Tout se passe en « changement de fenêtre » : la mise en page et le défilement qui en résultent ne doivent pas relancer le suivi.
        WhileSwitching(() =>
        {
            Editor.UpdateLayout();
            var before = CaretRect();

            change();

            Editor.UpdateLayout();
            var after = CaretRect();
            if (!before.IsEmpty && !after.IsEmpty)
                Editor.ScrollToVerticalOffset(Math.Max(0, Editor.VerticalOffset + after.Top - before.Top));
            Editor.UpdateLayout();
        });
    }
}
