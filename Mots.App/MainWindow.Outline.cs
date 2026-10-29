using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Threading;

namespace Mots;

public sealed record OutlineItem(Paragraph Paragraph, string Title);

/// <summary>Plan du document : un élément par chapitre, c'est-à-dire par paragraphe au style « Titre 1 ».</summary>
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

    private void RefreshOutline()
    {
        _outlineTimer.Stop();
        NormalizeWindow();
        FollowCaret();
        _outline = HeadingParagraphs().Select(p => new OutlineItem(p, TitleOf(p))).ToList();

        OutlineList.ItemsSource = _outline;
        OutlineEmptyText.Visibility = _outline.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        UpdateOutlineSelection();
    }

    /// <summary>Les titres du manuscrit, dans l'ordre. Un chapitre masqué n'a de titre qu'en première position.</summary>
    private IEnumerable<Paragraph> HeadingParagraphs() =>
        _before.Select(d => d.Blocks.FirstBlock)
            .Concat(Editor.Document.Blocks)
            .Concat(_after.Select(d => d.Blocks.FirstBlock))
            .OfType<Paragraph>()
            .Where(IsHeading);

    private static string TitleOf(Paragraph paragraph)
    {
        var text = new TextRange(paragraph.ContentStart, paragraph.ContentEnd).Text.ReplaceLineEndings(" ").Trim();
        return text.Length == 0 ? "(sans titre)" : text;
    }

    /// <summary>Met en évidence le chapitre qui contient le curseur.</summary>
    private void UpdateOutlineSelection()
    {
        Block? block = Editor.CaretPosition.Paragraph;
        while (block is not null && !IsHeading(block))
            block = block.PreviousBlock;
        var current = _outline.FirstOrDefault(item => ReferenceEquals(item.Paragraph, block));

        if (ReferenceEquals(OutlineList.SelectedItem, current)) return;
        OutlineList.SelectedItem = current;
        if (current is not null)
            OutlineList.ScrollIntoView(current);
    }

    private void GoToChapter(OutlineItem item)
    {
        var index = IsHeading(item.Paragraph) ? ChapterIndexOf(item.Paragraph) : -1;
        if (index < 0) // le titre a été supprimé ou changé de style depuis la dernière mise à jour
        {
            RefreshOutline();
            return;
        }

        OpenChapter(index);
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
