using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using NUnit.Framework;

namespace Mots.Tests;

/// <summary>L'éditeur n'affiche que le chapitre du curseur et ses voisins ; le manuscrit entier reste enregistré, compté et cherché.</summary>
public class ChaptersTests : WindowTestBase
{
    private void Book() => H.LoadParagraphs(
        Docs.P(StyleKind.Normal, "préface"),
        Docs.P(StyleKind.Heading1, "Un"),
        Docs.P(StyleKind.Normal, "texte"),
        Docs.P(StyleKind.Heading1, "Deux"),
        Docs.P(StyleKind.Ellipse, "***"),
        Docs.P(StyleKind.Heading1, "Trois"));

    /// <summary>Six chapitres : « Titre n » suivi de « texte n ».</summary>
    private void LongBook()
    {
        var paragraphs = new List<Paragraph>();
        for (var i = 1; i <= 6; i++)
        {
            paragraphs.Add(Docs.P(StyleKind.Heading1, $"Titre {i}"));
            paragraphs.Add(Docs.P(StyleKind.Normal, $"texte {i}"));
        }
        H.LoadParagraphs([.. paragraphs]);
    }

    private List<string> Shown => H.ShownParagraphs.Select(Docs.Text).ToList();

    private void Next() => H.Invoke("NextChapter_Executed", null, null);

    private void Previous() => H.Invoke("PreviousChapter_Executed", null, null);

    private void PlaceCaretIn(string text)
    {
        var paragraph = H.ShownParagraphs.Single(p => Docs.Text(p) == text);
        H.Editor.CaretPosition = paragraph.ContentEnd;
    }

    [Test]
    public void The_window_opens_on_the_first_chapter_and_its_neighbour()
    {
        Book();

        Assert.Multiple(() =>
        {
            Assert.That(H.ChapterCount, Is.EqualTo(4));
            Assert.That(H.Paragraphs, Has.Count.EqualTo(6));
            Assert.That(Shown, Is.EqualTo(new[] { "préface", "Un", "texte" }));
        });
    }

    [Test]
    public void A_document_without_heading_is_a_single_chapter()
    {
        H.LoadParagraphs(Docs.P(StyleKind.Normal, "un"), Docs.P(StyleKind.Normal, "deux"));

        Assert.Multiple(() =>
        {
            Assert.That(H.ChapterCount, Is.EqualTo(1));
            Assert.That(Shown, Is.EqualTo(new[] { "un", "deux" }));
        });
    }

    [Test]
    public void The_outline_opens_a_chapter_with_its_neighbours()
    {
        Book();
        var items = H.OutlineList.Items.Cast<OutlineItem>().ToList();

        H.Invoke("GoToChapter", items[1]); // « Deux »

        Assert.Multiple(() =>
        {
            Assert.That(Shown, Is.EqualTo(new[] { "Un", "texte", "Deux", "***", "Trois" }));
            Assert.That(H.CurrentChapter, Is.EqualTo(2));
            Assert.That(H.OutlineList.SelectedItem, Is.SameAs(items[1]));
            Assert.That(H.Editor.CaretPosition.Paragraph, Is.SameAs(H.Paragraphs[3]));
        });
    }

    [Test]
    public void Next_and_previous_chapter_stop_at_the_ends()
    {
        Book();

        Previous();
        Assert.That(H.CurrentChapter, Is.EqualTo(0));

        Next();
        Next();
        Assert.That(H.CurrentChapter, Is.EqualTo(2));

        Next();
        Next();
        Assert.Multiple(() =>
        {
            Assert.That(H.CurrentChapter, Is.EqualTo(3));
            Assert.That(Shown, Is.EqualTo(new[] { "Deux", "***", "Trois" }));
        });

        Previous();
        Assert.That(H.CurrentChapter, Is.EqualTo(2));
    }

    [Test]
    public void Changing_chapter_is_not_a_modification()
    {
        Book();
        H.MarkClean();

        Next();

        Assert.That(H.Dirty, Is.False);
    }

    [Test]
    public void The_window_follows_the_caret_from_chapter_to_chapter()
    {
        LongBook();
        H.PlaceCaretAtEnd(5); // « texte 3 »
        Assert.That(Shown, Is.EqualTo(new[] { "Titre 2", "texte 2", "Titre 3", "texte 3", "Titre 4", "texte 4" }));

        PlaceCaretIn("texte 4");

        Assert.Multiple(() =>
        {
            Assert.That(Shown, Is.EqualTo(new[] { "Titre 3", "texte 3", "Titre 4", "texte 4", "Titre 5", "texte 5" }));
            Assert.That(H.CurrentChapter, Is.EqualTo(3));
            Assert.That(H.Paragraphs, Has.Count.EqualTo(12));
            Assert.That(H.Dirty, Is.False);
        });
    }

    [Test]
    public void The_window_can_skip_several_chapters_at_once()
    {
        LongBook();
        H.PlaceCaretAtEnd(1); // « texte 1 »

        PlaceCaretIn("texte 2");
        Assert.That(H.CurrentChapter, Is.EqualTo(1));

        H.Invoke("OpenChapter", 5);
        Assert.That(Shown, Is.EqualTo(new[] { "Titre 5", "texte 5", "Titre 6", "texte 6" }));
    }

    [Test]
    public void Undo_leaves_the_chapters_in_place_after_the_window_moved()
    {
        LongBook();
        H.PlaceCaretAtEnd(1);
        H.Editor.CaretPosition.InsertTextInRun("!");
        PlaceCaretIn("texte 2");
        H.Editor.CaretPosition = H.Paragraphs[5].ContentEnd; // « texte 3 » : la fenêtre s'est décalée

        ApplicationCommands.Undo.Execute(null, H.Editor);

        Assert.That(H.Paragraphs.Select(p => Docs.Text(p).TrimEnd('!')),
            Is.EqualTo(new[] { "Titre 1", "texte 1", "Titre 2", "texte 2", "Titre 3", "texte 3", "Titre 4", "texte 4", "Titre 5", "texte 5", "Titre 6", "texte 6" }));
    }

    [Test]
    public void A_new_heading_in_the_middle_of_a_chapter_opens_a_chapter()
    {
        Book();
        H.PlaceCaretAtEnd(2); // « texte », sous « Un »

        H.Invoke("ApplyStyle", StyleKind.Heading1);

        Assert.Multiple(() =>
        {
            Assert.That(H.ChapterCount, Is.EqualTo(5));
            Assert.That(H.CurrentChapter, Is.EqualTo(2));
            Assert.That(Shown, Is.EqualTo(new[] { "Un", "texte", "Deux", "***" }));
            Assert.That(H.Paragraphs.Select(Docs.Text), Is.EqualTo(new[] { "préface", "Un", "texte", "Deux", "***", "Trois" }));
        });
    }

    [Test]
    public void A_heading_turned_into_normal_text_joins_the_previous_chapter()
    {
        Book();
        H.PlaceCaretAtEnd(3); // « Deux »

        H.Invoke("ApplyStyle", StyleKind.Normal);

        Assert.Multiple(() =>
        {
            Assert.That(H.ChapterCount, Is.EqualTo(3));
            Assert.That(H.CurrentChapter, Is.EqualTo(1));
            Assert.That(H.Paragraphs.Select(Docs.Text), Is.EqualTo(new[] { "préface", "Un", "texte", "Deux", "***", "Trois" }));
        });
    }

    [Test]
    public void The_first_chapter_of_the_window_that_loses_its_heading_pulls_in_the_hidden_one_before_it()
    {
        Book();
        H.Invoke("OpenChapter", 3); // fenêtre : « Deux », « Trois » ; « Un » est masqué
        Assert.That(Shown, Is.EqualTo(new[] { "Deux", "***", "Trois" }));
        H.Editor.CaretPosition = H.ShownParagraphs[0].ContentEnd;

        H.Invoke("ApplyStyle", StyleKind.Normal);

        Assert.Multiple(() =>
        {
            Assert.That(Shown, Is.EqualTo(new[] { "préface", "Un", "texte", "Deux", "***", "Trois" }));
            Assert.That(H.ChapterCount, Is.EqualTo(3));
        });
    }

    [Test]
    public void The_outline_follows_chapter_changes()
    {
        Book();
        H.PlaceCaretAtEnd(2);

        H.Invoke("ApplyStyle", StyleKind.Heading1);
        H.Invoke("RefreshOutline"); // le minuteur de 250 ms est court-circuité

        var titles = H.OutlineList.Items.Cast<OutlineItem>().Select(i => i.Title);
        Assert.That(titles.ToList(), Is.EqualTo(new[] { "Un", "texte", "Deux", "Trois" }));
    }

    [Test]
    public void Saving_writes_every_chapter_in_order()
    {
        Book();
        H.PlaceCaretAtEnd(2);
        H.Editor.CaretPosition.InsertTextInRun(" modifié");
        var path = PathOf("livre.docx");

        H.Invoke("TrySave", path);

        var saved = DocxConverter.Load(path);
        Assert.That(Docs.Paragraphs(saved).Select(Docs.Text),
            Is.EqualTo(new[] { "préface", "Un", "texte modifié", "Deux", "***", "Trois" }));
    }

    [Test]
    public void Counts_span_every_chapter()
    {
        LongBook();

        H.Invoke("UpdateStatus");

        Assert.That(H.StatusText.Text, Does.StartWith("24 mot(s)"));
    }

    [Test]
    public void Find_goes_through_the_chapters_and_wraps_around()
    {
        Book();
        H.FindBox.Text = "Trois";

        H.Invoke("FindNext");

        Assert.Multiple(() =>
        {
            Assert.That(Shown, Is.EqualTo(new[] { "Deux", "***", "Trois" }));
            Assert.That(H.Editor.Selection.Text, Is.EqualTo("Trois"));
        });

        H.FindBox.Text = "préface";
        H.Invoke("FindNext");

        Assert.Multiple(() =>
        {
            Assert.That(Shown, Is.EqualTo(new[] { "préface", "Un", "texte" }));
            Assert.That(H.Editor.Selection.Text, Is.EqualTo("préface"));
        });
    }

    [Test]
    public void Replace_all_reaches_the_chapters_that_are_not_shown()
    {
        LongBook();
        H.MarkClean();
        H.FindBox.Text = "texte";
        H.ReplaceBox.Text = "suite";

        H.Invoke("ReplaceAll_Click", null, new RoutedEventArgs());

        Assert.Multiple(() =>
        {
            Assert.That(H.Paragraphs.Select(Docs.Text).Where(t => t.StartsWith("suite")).ToList(), Has.Count.EqualTo(6));
            Assert.That(H.StatusText.Text, Is.EqualTo("6 remplacement(s)."));
            Assert.That(H.Dirty, Is.True);
        });
    }
}

/// <summary>Quand la fenêtre se décale, le texte ne doit pas bouger à l'écran.</summary>
public class ChapterWindowScrollTests : ShownWindowTestBase
{
    private void Book(int paragraphsPerChapter)
    {
        var paragraphs = new List<Paragraph>();
        for (var i = 1; i <= 8; i++)
        {
            paragraphs.Add(Docs.P(StyleKind.Heading1, $"Titre {i}"));
            for (var j = 1; j <= paragraphsPerChapter; j++)
                paragraphs.Add(Docs.P(StyleKind.Normal, $"texte {i}.{j}"));
        }
        H.LoadParagraphs([.. paragraphs]);
    }

    private static double Top(Paragraph paragraph) =>
        paragraph.ContentStart.GetCharacterRect(LogicalDirection.Forward).Top;

    private Paragraph Find(string text) => H.Paragraphs.Single(p => Docs.Text(p) == text);

    [Test]
    public void The_text_does_not_move_when_the_window_shifts()
    {
        Book(paragraphsPerChapter: 6);
        H.Invoke("OpenChapter", 3); // fenêtre : « Titre 3 » à « Titre 5 », curseur sur « Titre 4 »
        Wpf.Pump();
        H.Editor.ScrollToVerticalOffset(H.Editor.VerticalOffset + 60); // « Titre 3 » est tout entier sorti de la vue
        H.Editor.UpdateLayout();
        var watched = Find("texte 4.2");
        var before = Top(watched);

        H.Editor.CaretPosition = Find("Titre 5").ContentEnd; // « Titre 5 » est visible : le chapitre suivant devient le chapitre courant
        H.Editor.UpdateLayout();

        Assert.Multiple(() =>
        {
            Assert.That(H.ShownParagraphs.Select(Docs.Text).First(), Is.EqualTo("Titre 4"));
            Assert.That(Top(watched), Is.EqualTo(before).Within(2));
        });
    }

    [Test]
    public void A_chapter_still_in_view_is_kept_rather_than_making_the_text_jump()
    {
        Book(paragraphsPerChapter: 1);
        H.Invoke("OpenChapter", 3);
        H.Editor.CaretPosition = Find("texte 4.1").ContentEnd;
        Wpf.Pump();
        H.Editor.UpdateLayout();
        var watched = Find("texte 4.1");
        var before = Top(watched);

        H.Editor.CaretPosition = Find("Titre 5").ContentEnd;
        H.Editor.UpdateLayout();

        Assert.Multiple(() =>
        {
            Assert.That(H.ShownParagraphs.Select(Docs.Text).First(), Is.EqualTo("Titre 3"));
            Assert.That(Top(watched), Is.EqualTo(before).Within(2));
        });
    }
}
