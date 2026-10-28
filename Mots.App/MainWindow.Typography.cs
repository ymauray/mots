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
    public const char NonBreakingSpaceChar = ' ';
    public const char NarrowNonBreakingSpaceChar = ' ';

    private void NonBreakingSpace_Executed(object sender, ExecutedRoutedEventArgs e) =>
        InsertCharacter(NonBreakingSpaceChar);

    private void NarrowNonBreakingSpace_Executed(object sender, ExecutedRoutedEventArgs e) =>
        InsertCharacter(NarrowNonBreakingSpaceChar);

    /// <summary>Insère le caractère à la place de la sélection, comme une frappe (un seul pas d'annulation).</summary>
    private void InsertCharacter(char c)
    {
        Editor.Selection.Text = c.ToString();
        Editor.CaretPosition = Editor.Selection.End;
        Editor.Focus();
    }
}
