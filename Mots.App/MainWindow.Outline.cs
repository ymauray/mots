using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Threading;

namespace Mots;

public sealed record OutlineItem(Paragraph Paragraph, string Title);

/// <summary>Plan du document : un élément par paragraphe au style « Titre 1 ».</summary>
public partial class MainWindow
{
    private readonly DispatcherTimer _outlineTimer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private List<OutlineItem> _outline = [];

    private void InitializeOutline()
    {
        _outlineTimer.Tick += (_, _) =>
        {
            _outlineTimer.Stop();
            RefreshOutline();
        };
        RefreshOutline();
    }

    /// <summary>Reconstruit le plan peu après la dernière modification, pas à chaque frappe.</summary>
    private void ScheduleOutlineRefresh()
    {
        _outlineTimer.Stop();
        _outlineTimer.Start();
    }

    private void ReplaceDocument(FlowDocument document)
    {
        _outline = []; // les éléments du plan désignent des paragraphes de l'ancien document
        Editor.Document = document;
        MarksOverlay.Refresh();
        RefreshOutline();
    }

    private void RefreshOutline()
    {
        _outlineTimer.Stop();
        _outline = Editor.Document.Blocks.OfType<Paragraph>()
            .Where(p => ParagraphStyles.KindOf(p) == StyleKind.Heading1)
            .Select(p => new OutlineItem(p, TitleOf(p)))
            .ToList();

        OutlineList.ItemsSource = _outline;
        OutlineEmptyText.Visibility = _outline.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        UpdateOutlineSelection();
    }

    private static string TitleOf(Paragraph paragraph)
    {
        var text = new TextRange(paragraph.ContentStart, paragraph.ContentEnd).Text.ReplaceLineEndings(" ").Trim();
        return text.Length == 0 ? "(sans titre)" : text;
    }

    /// <summary>Met en évidence le chapitre qui contient le curseur.</summary>
    private void UpdateOutlineSelection()
    {
        var caret = Editor.CaretPosition;
        var current = _outline.LastOrDefault(
            item => item.Paragraph.Parent is not null && item.Paragraph.ContentStart.CompareTo(caret) <= 0);

        if (ReferenceEquals(OutlineList.SelectedItem, current)) return;
        OutlineList.SelectedItem = current;
        if (current is not null)
            OutlineList.ScrollIntoView(current);
    }

    private void GoToChapter(OutlineItem item)
    {
        var paragraph = item.Paragraph;
        if (paragraph.Parent is null) // le titre a été supprimé ou changé de style depuis la dernière mise à jour
        {
            RefreshOutline();
            return;
        }

        Editor.CaretPosition = paragraph.ContentStart;
        Editor.Focus();
        ScrollChapterToTop(paragraph);
    }

    /// <summary>
    /// Place le titre en haut de la fenêtre. WPF estime la hauteur des parties du document qu'il n'a pas encore
    /// mises en page : la position visée dérive donc au fil de la mise en page, d'où des corrections successives
    /// jusqu'à ce que le titre soit bien placé.
    /// </summary>
    private void ScrollChapterToTop(Paragraph paragraph, int attempt = 0)
    {
        const double Gap = 24; // espace laissé au-dessus du titre, sous le remplissage de l'éditeur (qui rogne ce qui y déborde)
        const int MaxAttempts = 15;

        if (paragraph.Parent is null) return;

        // Force la mise en page jusqu'au titre : sans cela, la position d'un titre lointain est inconnue.
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

    private void OutlineList_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject source
            && ItemsControl.ContainerFromElement(OutlineList, source) is ListBoxItem { DataContext: OutlineItem item })
            GoToChapter(item);
    }

    private void OutlineList_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && OutlineList.SelectedItem is OutlineItem item)
            GoToChapter(item);
    }
}
