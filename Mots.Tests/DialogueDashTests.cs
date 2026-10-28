using System.Windows.Documents;
using System.Windows.Input;
using NUnit.Framework;

namespace Mots.Tests;

public class DialogueDashTests : ShownWindowTestBase
{
    private const string Dash = "—";
    private const string Nbsp = " ";

    private string Text(int paragraph = 0) => Docs.Text(H.Paragraphs[paragraph]);

    /// <summary>Simule la saisie d'un texte dans l'éditeur ; renvoie true si l'application a pris la main.</summary>
    private bool Type(string text)
    {
        H.Editor.Focus();
        var composition = new TextComposition(InputManager.Current, H.Editor, text);
        var args = new TextCompositionEventArgs(Keyboard.PrimaryDevice, composition)
        {
            RoutedEvent = TextCompositionManager.PreviewTextInputEvent,
        };
        H.Editor.RaiseEvent(args);
        return args.Handled;
    }

    private void Undo() => ApplicationCommands.Undo.Execute(null, H.Editor);

    [Test]
    public void A_dash_at_the_start_of_a_paragraph_is_followed_by_a_non_breaking_space()
    {
        H.LoadParagraphs(Docs.P(StyleKind.Normal));
        H.PlaceCaretAtStart(0);

        var handled = Type(Dash);

        Assert.Multiple(() =>
        {
            Assert.That(handled, Is.True);
            Assert.That(Text(), Is.EqualTo(Dash + Nbsp));
        });
    }

    [Test]
    public void The_caret_ends_after_the_space_so_the_dialogue_can_be_typed()
    {
        H.LoadParagraphs(Docs.P(StyleKind.Normal));
        H.PlaceCaretAtStart(0);

        Type(Dash);
        H.Editor.CaretPosition.InsertTextInRun("Bonjour");

        Assert.That(Text(), Is.EqualTo(Dash + Nbsp + "Bonjour"));
    }

    [Test]
    public void The_first_undo_removes_only_the_space_the_second_removes_the_dash()
    {
        H.LoadParagraphs(Docs.P(StyleKind.Normal));
        H.PlaceCaretAtStart(0);
        Type(Dash);

        Undo();
        Assert.That(Text(), Is.EqualTo(Dash));

        Undo();
        Assert.That(Text(), Is.EqualTo(""));
    }

    [Test]
    public void A_dash_in_the_middle_of_a_sentence_is_left_alone()
    {
        H.LoadParagraphs(Docs.P(StyleKind.Normal, "Bonjour"));
        H.PlaceCaretAtEnd(0);

        Assert.That(Type(Dash), Is.False);
        Assert.That(Text(), Is.EqualTo("Bonjour"));
    }

    [Test]
    public void A_dash_after_a_line_break_counts_as_a_line_start()
    {
        H.LoadParagraphs(Docs.P(StyleKind.Normal, new Run("— Bonjour"), new LineBreak()));
        H.PlaceCaretAtEnd(0);

        Assert.That(Type(Dash), Is.True);
        Assert.That(Text(), Does.EndWith(Dash + Nbsp));
    }

    [Test]
    public void No_space_is_added_when_one_already_follows()
    {
        H.LoadParagraphs(Docs.P(StyleKind.Normal, " Bonjour"));
        H.PlaceCaretAtStart(0);

        Type(Dash);

        Assert.That(Text(), Is.EqualTo(Dash + " Bonjour"));
    }

    [Test]
    public void No_space_is_added_before_an_existing_non_breaking_space()
    {
        H.LoadParagraphs(Docs.P(StyleKind.Normal, Nbsp + "Bonjour"));
        H.PlaceCaretAtStart(0);

        Type(Dash);

        Assert.That(Text(), Is.EqualTo(Dash + Nbsp + "Bonjour"));
    }

    [Test]
    public void A_dash_added_before_existing_text_gets_its_space()
    {
        H.LoadParagraphs(Docs.P(StyleKind.Normal, "Bonjour"));
        H.PlaceCaretAtStart(0);

        Type(Dash);

        Assert.That(Text(), Is.EqualTo(Dash + Nbsp + "Bonjour"));
    }

    [TestCase("-")]
    [TestCase("–")] // demi-cadratin
    [TestCase("a")]
    [TestCase(" ")]
    public void Other_characters_are_left_alone(string typed)
    {
        H.LoadParagraphs(Docs.P(StyleKind.Normal));
        H.PlaceCaretAtStart(0);

        Assert.That(Type(typed), Is.False);
        Assert.That(Text(), Is.EqualTo(""));
    }

    [Test]
    public void With_a_selection_the_dash_replaces_it_without_automation()
    {
        H.LoadParagraphs(Docs.P(StyleKind.Normal, "Bonjour"));
        H.SelectAll();

        Assert.That(Type(Dash), Is.False);
    }

    [Test]
    public void Works_in_a_dialogue_paragraph_of_any_style()
    {
        H.LoadParagraphs(Docs.P(StyleKind.Heading1), Docs.P(StyleKind.Ellipse));
        H.PlaceCaretAtStart(1);

        Type(Dash);

        Assert.Multiple(() =>
        {
            Assert.That(Text(1), Is.EqualTo(Dash + Nbsp));
            Assert.That(H.Kinds, Is.EqualTo(new[] { StyleKind.Heading1, StyleKind.Ellipse }));
        });
    }

    [Test]
    public void It_marks_the_document_as_modified()
    {
        H.LoadParagraphs(Docs.P(StyleKind.Normal));
        H.MarkClean();
        H.PlaceCaretAtStart(0);

        Type(Dash);

        Assert.That(H.Dirty, Is.True);
    }

    [Test]
    public void The_dash_and_space_survive_saving_and_reopening()
    {
        H.LoadParagraphs(Docs.P(StyleKind.Normal));
        H.PlaceCaretAtStart(0);
        Type(Dash);
        H.Editor.CaretPosition.InsertTextInRun("Bonjour");
        var path = PathOf("d.docx");

        H.Invoke("TrySave", path);

        Assert.That(Docs.Text(Docs.Paragraphs(DocxConverter.Load(path))[0]), Is.EqualTo(Dash + Nbsp + "Bonjour"));
    }
}
