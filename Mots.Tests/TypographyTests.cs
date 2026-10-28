using System.Windows.Documents;
using System.Windows.Input;
using NUnit.Framework;

namespace Mots.Tests;

public class TypographyTests : WindowTestBase
{
    private static void Insert(RoutedCommand command, MainWindow window) => command.Execute(null, window);

    private string Text => Docs.Text(H.Paragraphs[0]);

    [Test]
    public void The_characters_are_the_expected_code_points()
    {
        Assert.Multiple(() =>
        {
            Assert.That((int)MainWindow.NonBreakingSpaceChar, Is.EqualTo(0x00A0));
            Assert.That((int)MainWindow.NarrowNonBreakingSpaceChar, Is.EqualTo(0x202F));
        });
    }

    [Test]
    public void Non_breaking_space_is_inserted_at_the_caret()
    {
        H.LoadParagraphs(Docs.P(StyleKind.Normal, "10km"));
        H.Editor.CaretPosition = H.Paragraphs[0].ContentStart.GetPositionAtOffset(3)!;

        Insert(TypographyCommands.NonBreakingSpace, H.Window);

        Assert.That(Text, Does.Contain(" "));
        Assert.That(Text.Length, Is.EqualTo(5));
    }

    [Test]
    public void The_caret_ends_after_the_inserted_space_so_typing_continues()
    {
        H.LoadParagraphs(Docs.P(StyleKind.Normal));
        H.PlaceCaretAtEnd(0);

        Insert(TypographyCommands.NonBreakingSpace, H.Window);
        H.Editor.CaretPosition.InsertTextInRun("x");

        Assert.That(Text, Is.EqualTo(" x"));
    }

    [Test]
    public void Narrow_non_breaking_space_is_inserted()
    {
        H.LoadParagraphs(Docs.P(StyleKind.Normal, "Quoi"));
        H.PlaceCaretAtEnd(0);

        Insert(TypographyCommands.NarrowNonBreakingSpace, H.Window);
        H.Editor.CaretPosition.InsertTextInRun("?");

        Assert.That(Text, Is.EqualTo("Quoi ?"));
    }

    [Test]
    public void It_replaces_the_selection_like_typing_would()
    {
        H.LoadParagraphs(Docs.P(StyleKind.Normal, "a b"));
        H.Editor.Selection.Select(H.Paragraphs[0].ContentStart.GetPositionAtOffset(2)!, H.Paragraphs[0].ContentStart.GetPositionAtOffset(3)!);

        Insert(TypographyCommands.NonBreakingSpace, H.Window);

        Assert.That(Text, Is.EqualTo("a b"));
    }

    [Test]
    public void Inserting_marks_the_document_as_modified()
    {
        H.MarkClean();

        Insert(TypographyCommands.NonBreakingSpace, H.Window);

        Assert.That(H.Dirty, Is.True);
    }

    [Test]
    public void Inserting_keeps_the_paragraph_style()
    {
        H.LoadParagraphs(Docs.P(StyleKind.Heading1, "Chapitre"));
        H.PlaceCaretAtEnd(0);

        Insert(TypographyCommands.NonBreakingSpace, H.Window);

        Assert.That(H.Kinds, Is.EqualTo(new[] { StyleKind.Heading1 }));
    }

    [Test]
    public void The_non_breaking_space_survives_saving_and_reopening()
    {
        H.LoadParagraphs(Docs.P(StyleKind.Normal, "10"));
        H.PlaceCaretAtEnd(0);
        Insert(TypographyCommands.NonBreakingSpace, H.Window);
        Insert(TypographyCommands.NarrowNonBreakingSpace, H.Window);
        H.Editor.CaretPosition.InsertTextInRun("km");
        var path = PathOf("t.docx");

        H.Invoke("TrySave", path);

        Assert.That(Docs.Text(Docs.Paragraphs(DocxConverter.Load(path))[0]), Is.EqualTo("10  km"));
    }

    [Test]
    public void The_shortcuts_are_bound_on_the_window()
    {
        var bindings = H.Window.InputBindings.OfType<KeyBinding>().ToList();

        Assert.Multiple(() =>
        {
            Assert.That(bindings.Any(b => b.Key == Key.Space && b.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift)
                                          && b.Command == TypographyCommands.NonBreakingSpace));
            Assert.That(bindings.Any(b => b.Key == Key.Space && b.Modifiers == (ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Shift)
                                          && b.Command == TypographyCommands.NarrowNonBreakingSpace));
        });
    }
}

public class TypographyUndoTests : ShownWindowTestBase
{
    [Test]
    public void The_insertion_is_a_single_undo_step()
    {
        H.LoadParagraphs(Docs.P(StyleKind.Normal, "ab"));
        H.PlaceCaretAtEnd(0);
        H.Editor.Focus();

        TypographyCommands.NonBreakingSpace.Execute(null, H.Window);
        ApplicationCommands.Undo.Execute(null, H.Editor);

        Assert.That(Docs.Text(H.Paragraphs[0]), Is.EqualTo("ab"));
    }
}
