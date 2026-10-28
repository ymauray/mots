using System.Windows.Documents;
using System.Windows.Input;

namespace Mots;

public static class TypographyCommands
{
    public static readonly RoutedCommand NonBreakingSpace = new(nameof(NonBreakingSpace), typeof(TypographyCommands));
    public static readonly RoutedCommand NarrowNonBreakingSpace = new(nameof(NarrowNonBreakingSpace), typeof(TypographyCommands));
}

/// <summary>Saisie des caractères typographiques que le clavier ne donne pas directement.</summary>
public partial class MainWindow
{
    public const char NonBreakingSpaceChar = '\u00A0';
    public const char NarrowNonBreakingSpaceChar = '\u202F';
    public const char EmDashChar = '\u2014';

    private void NonBreakingSpace_Executed(object sender, ExecutedRoutedEventArgs e) =>
        InsertCharacter(NonBreakingSpaceChar);

    private void NarrowNonBreakingSpace_Executed(object sender, ExecutedRoutedEventArgs e) =>
        InsertCharacter(NarrowNonBreakingSpaceChar);

    /// <summary>
    /// Un tiret cadratin tapé en début de ligne signale un dialogue : une espace insécable le suit. Les deux
    /// insertions sont des pas d'annulation distincts (un premier Ctrl+Z retire l'espace, le suivant le tiret).
    /// </summary>
    private void Editor_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (e.Text != EmDashChar.ToString() || !Editor.Selection.IsEmpty) return;

        var caret = Editor.CaretPosition;
        if (caret.Paragraph is not { } paragraph || !IsAtLineStart(paragraph, caret)) return;

        // Une espace est déjà là (tiret ajouté devant un dialogue existant) : rien à ajouter.
        var addSpace = !new TextRange(caret, paragraph.ContentEnd).Text.StartsWith(' ')
                       && !new TextRange(caret, paragraph.ContentEnd).Text.StartsWith(NonBreakingSpaceChar);

        e.Handled = true;
        InsertCharacter(EmDashChar);
        if (addSpace)
            InsertCharacter(NonBreakingSpaceChar);
    }

    private static bool IsAtLineStart(Paragraph paragraph, TextPointer position)
    {
        var before = new TextRange(paragraph.ContentStart, position).Text;
        return before.Length == 0 || before.EndsWith('\n');
    }

    /// <summary>Insère le caractère à la place de la sélection, comme une frappe (un seul pas d'annulation).</summary>
    private void InsertCharacter(char c)
    {
        Editor.Selection.Text = c.ToString();
        Editor.CaretPosition = Editor.Selection.End;
        Editor.Focus();
    }
}
