using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;

namespace Mots;

public partial class MainWindow : Window
{
    private const string FileFilter = "Document Word (*.docx)|*.docx";

    private string? _currentPath;
    private bool _dirty;
    private bool _updatingToolbar;

    public MainWindow()
    {
        InitializeComponent();

        FontFamilyBox.ItemsSource = Fonts.SystemFontFamilies.OrderBy(f => f.Source).ToList();
        // Tailles en points (comme Word) ; WPF travaille en pixels, soit pt × 4/3.
        FontSizeBox.ItemsSource = new double[] { 8, 9, 10, 11, 12, 14, 16, 18, 20, 24, 28, 36, 48, 72 };

        UpdateTitle();
        UpdateStatus();
    }

    // ---------- Fichier ----------

    private void New_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        if (!ConfirmDiscard()) return;
        Editor.Document = new FlowDocument(new Paragraph()) { TextAlignment = TextAlignment.Left };
        _currentPath = null;
        SetDirty(false);
    }

    private void Open_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        if (!ConfirmDiscard()) return;
        var dlg = new OpenFileDialog { Filter = FileFilter };
        if (dlg.ShowDialog(this) != true) return;

        try
        {
            Editor.Document = DocxConverter.Load(dlg.FileName);
            _currentPath = dlg.FileName;
            SetDirty(false);
        }
        catch (Exception ex)
        {
            ShowError("Impossible d'ouvrir le fichier", ex);
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
            FileName = _currentPath is null ? "Sans titre" : Path.GetFileName(_currentPath),
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
        var name = _currentPath is null ? "Sans titre" : Path.GetFileName(_currentPath);
        var choice = DialogWindow.Show(this, $"Enregistrer les modifications de « {name} » ?",
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

    // ---------- Mise en forme ----------

    private void FontFamily_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_updatingToolbar || FontFamilyBox.SelectedItem is not FontFamily family) return;
        Editor.Selection.ApplyPropertyValue(TextElement.FontFamilyProperty, family);
        Editor.Focus();
    }

    private void FontSize_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_updatingToolbar || FontSizeBox.SelectedItem is not double points) return;
        Editor.Selection.ApplyPropertyValue(TextElement.FontSizeProperty, points * 4 / 3);
        Editor.Focus();
    }

    private void Editor_SelectionChanged(object sender, RoutedEventArgs e)
    {
        _updatingToolbar = true;
        try
        {
            var family = Editor.Selection.GetPropertyValue(TextElement.FontFamilyProperty);
            FontFamilyBox.SelectedItem = family is FontFamily f
                ? FontFamilyBox.Items.Cast<FontFamily>().FirstOrDefault(x => x.Source == f.Source)
                : null;

            var size = Editor.Selection.GetPropertyValue(TextElement.FontSizeProperty);
            FontSizeBox.SelectedItem = size is double px ? Math.Round(px * 0.75, 1) : null;

            BoldButton.IsChecked = Equals(Editor.Selection.GetPropertyValue(TextElement.FontWeightProperty), FontWeights.Bold);
            ItalicButton.IsChecked = Equals(Editor.Selection.GetPropertyValue(TextElement.FontStyleProperty), FontStyles.Italic);
            UnderlineButton.IsChecked = Editor.Selection.GetPropertyValue(Inline.TextDecorationsProperty)
                is TextDecorationCollection decorations
                && decorations.Any(d => d.Location == TextDecorationLocation.Underline);

            var align = Editor.Selection.GetPropertyValue(Block.TextAlignmentProperty);
            AlignLeftButton.IsChecked = Equals(align, TextAlignment.Left);
            AlignCenterButton.IsChecked = Equals(align, TextAlignment.Center);
            AlignRightButton.IsChecked = Equals(align, TextAlignment.Right);
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

    private void UpdateTitle() =>
        Title = $"{(_dirty ? "*" : "")}{(_currentPath is null ? "Sans titre" : Path.GetFileName(_currentPath))} — Mots";

    private void UpdateStatus()
    {
        var text = new TextRange(Editor.Document.ContentStart, Editor.Document.ContentEnd).Text;
        var words = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
        var chars = text.TrimEnd('\r', '\n').Length;
        StatusText.Text = $"{words} mot(s) · {chars} caractère(s)";
    }
}
