using System.Windows;
using System.Windows.Controls;

namespace Mots;

/// <summary>Boîte de dialogue au style Fluent, remplace MessageBox.</summary>
public partial class DialogWindow : Window
{
    private int _choice = -1;

    private DialogWindow(string heading, string message, string[] buttons, int defaultIndex, int cancelIndex)
    {
        InitializeComponent();
        HeadingText.Text = heading;
        MessageText.Text = message;
        MessageText.Visibility = message.Length == 0 ? Visibility.Collapsed : Visibility.Visible;

        for (var i = 0; i < buttons.Length; i++)
        {
            var index = i;
            var button = new Button
            {
                Content = buttons[i],
                MinWidth = 96,
                Padding = new Thickness(16, 6, 16, 6),
                Margin = new Thickness(i == 0 ? 0 : 8, 0, 0, 0),
                IsDefault = i == defaultIndex,
                IsCancel = i == cancelIndex,
            };
            if (i == defaultIndex) button.SetResourceReference(StyleProperty, "AccentButtonStyle");
            button.Click += (_, _) =>
            {
                _choice = index;
                Close();
            };
            ButtonsPanel.Children.Add(button);
        }
    }

    /// <summary>Affiche la boîte et renvoie l'index du bouton choisi (-1 si fermée par la croix).</summary>
    public static int Show(Window owner, string heading, string message, string[] buttons,
        int defaultIndex = 0, int cancelIndex = -1)
    {
        var dlg = new DialogWindow(heading, message, buttons, defaultIndex, cancelIndex) { Owner = owner };
        dlg.ShowDialog();
        return dlg._choice;
    }
}
