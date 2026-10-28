using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Mots;

/// <summary>Menu des documents récents, sous le bouton « Ouvrir ».</summary>
public partial class MainWindow
{
    private void Recent_Click(object sender, RoutedEventArgs e)
    {
        var menu = BuildRecentMenu();
        menu.PlacementTarget = RecentButton;
        menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
        menu.IsOpen = true;
    }

    private ContextMenu BuildRecentMenu()
    {
        // Le menu hérite de la police du bouton (icônes, corps 10) : on remet celle du système.
        var menu = new ContextMenu
        {
            FontFamily = SystemFonts.MessageFontFamily,
            FontSize = SystemFonts.MessageFontSize,
        };
        if (_recent.Items.Count == 0)
        {
            menu.Items.Add(new MenuItem { Header = "Aucun document récent", IsEnabled = false });
            return menu;
        }

        foreach (var path in _recent.Items)
        {
            var item = new MenuItem { Header = RecentHeader(path), ToolTip = path };
            item.Click += (_, _) => OpenRecent(path);
            menu.Items.Add(item);
        }

        menu.Items.Add(new Separator());
        var clear = new MenuItem { Header = "Effacer la liste" };
        clear.Click += (_, _) => _recent.Clear();
        menu.Items.Add(clear);
        return menu;
    }

    /// <summary>Nom du fichier, et dessous son dossier pour distinguer deux fichiers de même nom.</summary>
    private static StackPanel RecentHeader(string path)
    {
        var panel = new StackPanel();
        panel.Children.Add(new TextBlock { Text = Path.GetFileName(path) });
        panel.Children.Add(new TextBlock
        {
            Text = Path.GetDirectoryName(path),
            FontSize = 11,
            Opacity = 0.6,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxWidth = 360,
        });
        return panel;
    }

    private void OpenRecent(string path)
    {
        if (!ConfirmDiscard()) return;
        OpenPath(path);
    }
}
