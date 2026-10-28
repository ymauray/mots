using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using Microsoft.Win32;

namespace Mots;

public partial class MainWindow : Window
{
    private const string FileFilter = "Document Word (*.docx)|*.docx";

    private string? _currentPath;
    private string? _importedName; // nom proposé pour un document importé, tant qu'il n'est pas enregistré
    private bool _dirty;
    private bool _updatingToolbar;

    public MainWindow()
    {
        InitializeComponent();

        StyleBox.ItemsSource = ParagraphStyles.All;
        foreach (var paragraph in Editor.Document.Blocks.OfType<Paragraph>())
            ParagraphStyles.Apply(paragraph, StyleKind.Normal);

        DisableLayoutCommands();

        // Le texte collé perd sa mise en forme d'origine : seuls les styles de Mots font foi.
        DataObject.AddPastingHandler(Editor, (_, e) =>
        {
            if (e.DataObject.GetDataPresent(DataFormats.UnicodeText))
                e.FormatToApply = DataFormats.UnicodeText;
        });

        MarksOverlay.Target = Editor;
        InitializeOutline();
        UpdateStyleBox();
        SetDirty(false); // l'application des styles au document initial a déclenché TextChanged
        UpdateStatus();
    }

    /// <summary>
    /// La mise en page vient des styles : on neutralise les commandes (et donc les raccourcis clavier) de
    /// l'éditeur qui la modifieraient paragraphe par paragraphe.
    /// </summary>
    private void DisableLayoutCommands()
    {
        RoutedCommand[] commands =
        [
            EditingCommands.AlignLeft, EditingCommands.AlignCenter, EditingCommands.AlignRight, EditingCommands.AlignJustify,
            EditingCommands.ToggleBullets, EditingCommands.ToggleNumbering,
            EditingCommands.IncreaseIndentation, EditingCommands.DecreaseIndentation,
            EditingCommands.IncreaseFontSize, EditingCommands.DecreaseFontSize,
        ];
        foreach (var command in commands)
            Editor.CommandBindings.Add(new CommandBinding(command, (_, _) => { }, (_, e) =>
            {
                e.CanExecute = false;
                e.Handled = true;
            }));
    }

    // ---------- Fichier ----------

    private void New_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        if (!ConfirmDiscard()) return;
        var paragraph = new Paragraph();
        ParagraphStyles.Apply(paragraph, StyleKind.Normal);
        ReplaceDocument(new FlowDocument(paragraph));
        _currentPath = null;
        _importedName = null;
        SetDirty(false);
    }

    private void Open_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        if (!ConfirmDiscard()) return;
        var dlg = new OpenFileDialog { Filter = FileFilter };
        if (dlg.ShowDialog(this) != true) return;

        try
        {
            ReplaceDocument(DocxConverter.Load(dlg.FileName));
            _currentPath = dlg.FileName;
            _importedName = null;
            SetDirty(false);
        }
        catch (Exception ex)
        {
            ShowError("Impossible d'ouvrir le fichier", ex);
        }
    }

    private void Import_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        if (!ConfirmDiscard()) return;
        var dlg = new OpenFileDialog { Filter = FileFilter, Title = "Importer un document Word" };
        if (dlg.ShowDialog(this) != true) return;

        Mouse.OverrideCursor = Cursors.Wait; // un manuscrit complet peut prendre quelques secondes
        try
        {
            ReplaceDocument(DocxConverter.Import(dlg.FileName));
            _currentPath = null;
            // Le nom diffère de l'original pour ne pas l'écraser par mégarde à l'enregistrement.
            _importedName = $"{Path.GetFileNameWithoutExtension(dlg.FileName)} (Mots)";
            SetDirty(true); // le document importé n'existe pas encore au format Mots
        }
        catch (Exception ex)
        {
            ShowError("Impossible d'importer le document", ex);
        }
        finally
        {
            Mouse.OverrideCursor = null;
        }
    }

    private void Save_Executed(object sender, ExecutedRoutedEventArgs e) => Save();

    private void SaveAs_Executed(object sender, ExecutedRoutedEventArgs e) => SaveAs();

    private bool Save() => _currentPath is null ? SaveAs() : TrySave(_currentPath);

    private bool SaveAs()
    {
        var dlg = new SaveFileDialog
        {
            Filter = FileFilter,
            DefaultExt = ".docx",
            FileName = DisplayName,
        };
        return dlg.ShowDialog(this) == true && TrySave(dlg.FileName);
    }

    private bool TrySave(string path)
    {
        try
        {
            DocxConverter.Save(Editor.Document, path);
            _currentPath = path;
            SetDirty(false);
            return true;
        }
        catch (Exception ex)
        {
            ShowError("Impossible d'enregistrer le fichier", ex);
            return false;
        }
    }

    private void Print_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        var dlg = new PrintDialog();
        if (dlg.ShowDialog() != true) return;

        // Copie du document pour paginer selon la taille de la page imprimée.
        var range = new TextRange(Editor.Document.ContentStart, Editor.Document.ContentEnd);
        using var ms = new MemoryStream();
        range.Save(ms, DataFormats.Xaml);
        ms.Position = 0;
        var copy = new FlowDocument();
        new TextRange(copy.ContentStart, copy.ContentEnd).Load(ms, DataFormats.Xaml);
        copy.PageWidth = dlg.PrintableAreaWidth;
        copy.PageHeight = dlg.PrintableAreaHeight;
        copy.PagePadding = new Thickness(50);
        copy.ColumnWidth = double.PositiveInfinity;

        dlg.PrintDocument(((IDocumentPaginatorSource)copy).DocumentPaginator, Title);
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (!ConfirmDiscard()) e.Cancel = true;
    }

    private bool ConfirmDiscard()
    {
        if (!_dirty) return true;
        var choice = DialogWindow.Show(this, $"Enregistrer les modifications de « {DisplayName} » ?",
            "Vos modifications seront perdues si vous ne les enregistrez pas.",
            ["Enregistrer", "Ne pas enregistrer", "Annuler"], defaultIndex: 0, cancelIndex: 2);
        return choice switch
        {
            0 => Save(),
            1 => true,
            _ => false,
        };
    }

    private void ShowError(string message, Exception ex) =>
        DialogWindow.Show(this, message, ex.Message, ["OK"], defaultIndex: 0, cancelIndex: 0);

    // ---------- Affichage ----------

    private void Marks_Changed(object sender, RoutedEventArgs e) =>
        MarksOverlay.Visibility = MarksButton.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;

    private void ToggleMarks_Executed(object sender, ExecutedRoutedEventArgs e) =>
        MarksButton.IsChecked = MarksButton.IsChecked != true;

    // ---------- Mise en forme ----------

    private void Style_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_updatingToolbar || StyleBox.SelectedValue is not StyleKind kind) return;
        ApplyStyle(kind);
    }

    private void StyleNormal_Executed(object sender, ExecutedRoutedEventArgs e) => ApplyStyle(StyleKind.Normal);

    private void StyleHeading1_Executed(object sender, ExecutedRoutedEventArgs e) => ApplyStyle(StyleKind.Heading1);

    private void ApplyStyle(StyleKind kind)
    {
        Editor.BeginChange();
        try
        {
            foreach (var paragraph in SelectedParagraphs())
                ParagraphStyles.Apply(paragraph, kind);
        }
        finally
        {
            Editor.EndChange();
        }
        SetDirty(true);
        UpdateStyleBox();
        ScheduleOutlineRefresh();
        Editor.Focus();
    }

    private List<Paragraph> SelectedParagraphs()
    {
        var paragraphs = new List<Paragraph>();
        void Add(Paragraph? p)
        {
            if (p is not null && (paragraphs.Count == 0 || paragraphs[^1] != p))
                paragraphs.Add(p);
        }

        var end = Editor.Selection.End;
        for (var pos = Editor.Selection.Start; pos is not null && pos.CompareTo(end) <= 0;
             pos = pos.GetNextContextPosition(LogicalDirection.Forward))
            Add(pos.Paragraph);
        Add(end.Paragraph);
        return paragraphs;
    }

    private void UpdateStyleBox()
    {
        var kind = Editor.Selection.Start.Paragraph is { } p ? ParagraphStyles.KindOf(p) : StyleKind.Normal;
        var wasUpdating = _updatingToolbar;
        _updatingToolbar = true;
        StyleBox.SelectedValue = kind;
        _updatingToolbar = wasUpdating;
    }

    /// <summary>« Entrée » en fin de paragraphe démarre un paragraphe du style suivant (après un titre : « Normal »).</summary>
    private void Editor_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || Keyboard.Modifiers != ModifierKeys.None || !Editor.Selection.IsEmpty) return;

        var caret = Editor.CaretPosition;
        if (caret.Paragraph is not { } current) return;
        var style = ParagraphStyles.Get(ParagraphStyles.KindOf(current));
        if (style.NextKind == style.Kind) return;
        if (new TextRange(caret, current.ContentEnd).Text.Length > 0) return;

        e.Handled = true;
        Editor.BeginChange(); // un seul pas d'annulation pour le saut de paragraphe et le changement de style
        try
        {
            EditingCommands.EnterParagraphBreak.Execute(null, Editor);
            if (Editor.CaretPosition.Paragraph is { } next && next != current)
                ParagraphStyles.Apply(next, style.NextKind);
        }
        finally
        {
            Editor.EndChange();
        }
        UpdateStyleBox();
    }

    private void Editor_SelectionChanged(object sender, RoutedEventArgs e)
    {
        _updatingToolbar = true;
        try
        {
            UpdateStyleBox();
            UpdateOutlineSelection();

            BoldButton.IsChecked = Equals(Editor.Selection.GetPropertyValue(TextElement.FontWeightProperty), FontWeights.Bold);
            ItalicButton.IsChecked = Equals(Editor.Selection.GetPropertyValue(TextElement.FontStyleProperty), FontStyles.Italic);
            UnderlineButton.IsChecked = Editor.Selection.GetPropertyValue(Inline.TextDecorationsProperty)
                is TextDecorationCollection decorations
                && decorations.Any(d => d.Location == TextDecorationLocation.Underline);
        }
        finally
        {
            _updatingToolbar = false;
        }
    }

    private void Editor_TextChanged(object sender, TextChangedEventArgs e)
    {
        SetDirty(true);
        UpdateStatus();
        ScheduleOutlineRefresh();
    }

    // ---------- Rechercher / Remplacer ----------

    private void Find_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        FindBar.Visibility = Visibility.Visible;
        if (!Editor.Selection.IsEmpty && !Editor.Selection.Text.Contains('\n'))
            FindBox.Text = Editor.Selection.Text;
        FindBox.Focus();
        FindBox.SelectAll();
    }

    private void CloseFind_Click(object sender, RoutedEventArgs e)
    {
        FindBar.Visibility = Visibility.Collapsed;
        Editor.Focus();
    }

    private void FindBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) FindNext();
        else if (e.Key == Key.Escape) CloseFind_Click(sender, e);
    }

    private void FindNext_Click(object sender, RoutedEventArgs e) => FindNext();

    private void FindNext()
    {
        if (FindBox.Text.Length == 0) return;
        var found = FindFrom(Editor.Selection.End, FindBox.Text)
                    ?? FindFrom(Editor.Document.ContentStart, FindBox.Text);
        if (found is null)
        {
            StatusText.Text = "Aucun résultat.";
            return;
        }
        Editor.Selection.Select(found.Start, found.End);
        Editor.Focus();
    }

    private static TextRange? FindFrom(TextPointer start, string text)
    {
        for (var p = start; p is not null; p = p.GetNextContextPosition(LogicalDirection.Forward))
        {
            if (p.GetPointerContext(LogicalDirection.Forward) != TextPointerContext.Text) continue;
            var run = p.GetTextInRun(LogicalDirection.Forward);
            var index = run.IndexOf(text, StringComparison.CurrentCultureIgnoreCase);
            if (index < 0) continue;

            var s = p.GetPositionAtOffset(index);
            var e = s?.GetPositionAtOffset(text.Length);
            if (s is not null && e is not null) return new TextRange(s, e);
        }
        return null;
    }

    private void Replace_Click(object sender, RoutedEventArgs e)
    {
        if (!Editor.Selection.IsEmpty &&
            string.Equals(Editor.Selection.Text, FindBox.Text, StringComparison.CurrentCultureIgnoreCase))
        {
            Editor.Selection.Text = ReplaceBox.Text;
        }
        FindNext();
    }

    private void ReplaceAll_Click(object sender, RoutedEventArgs e)
    {
        if (FindBox.Text.Length == 0) return;
        var count = 0;
        var pos = Editor.Document.ContentStart;
        while (FindFrom(pos, FindBox.Text) is { } range)
        {
            range.Text = ReplaceBox.Text;
            pos = range.End;
            count++;
        }
        StatusText.Text = $"{count} remplacement(s).";
    }

    // ---------- Divers ----------

    private void SetDirty(bool dirty)
    {
        _dirty = dirty;
        UpdateTitle();
    }

    private string DisplayName => _currentPath is null ? _importedName ?? "Sans titre" : Path.GetFileName(_currentPath);

    private void UpdateTitle() => Title = $"{(_dirty ? "*" : "")}{DisplayName} — Mots";

    private void UpdateStatus()
    {
        var text = new TextRange(Editor.Document.ContentStart, Editor.Document.ContentEnd).Text;
        var words = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
        var chars = text.TrimEnd('\r', '\n').Length;
        StatusText.Text = $"{words} mot(s) · {chars} caractère(s)";
    }
}

public static class FileCommands
{
    public static readonly RoutedCommand Import = new(nameof(Import), typeof(FileCommands));
}

public static class ViewCommands
{
    public static readonly RoutedCommand ToggleMarks = new(nameof(ToggleMarks), typeof(ViewCommands));
}

public static class StyleCommands
{
    public static readonly RoutedCommand Normal = new(nameof(Normal), typeof(StyleCommands));
    public static readonly RoutedCommand Heading1 = new(nameof(Heading1), typeof(StyleCommands));
}
