using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using NUnit.Framework;

namespace Mots.Tests;

public class DialogWindowTests
{
    private static DialogWindow Create(string heading, string message)
    {
        var ctor = typeof(DialogWindow).GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance).Single();
        var dialog = (DialogWindow)ctor.Invoke([heading, message, new[] { "Enregistrer", "Ne pas enregistrer", "Annuler" }, 0, 2]);
        dialog.Left = -20000; // hors de l'écran
        dialog.ShowActivated = false;
        dialog.Show();
        Wpf.Pump();
        return dialog;
    }

    private static double SingleLineHeight(TextBlock like)
    {
        var probe = new TextBlock { Text = "Ag", FontSize = like.FontSize, FontWeight = like.FontWeight, FontFamily = like.FontFamily };
        probe.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        return probe.DesiredSize.Height;
    }

    [Test]
    public void The_save_confirmation_for_an_untitled_document_fits_on_one_line_each()
    {
        var dialog = Create("Enregistrer les modifications de « Sans titre » ?",
            "Vos modifications seront perdues si vous ne les enregistrez pas.");
        try
        {
            var heading = (TextBlock)dialog.FindName("HeadingText");
            var message = (TextBlock)dialog.FindName("MessageText");
            Assert.Multiple(() =>
            {
                Assert.That(heading.ActualHeight, Is.LessThan(SingleLineHeight(heading) * 1.5), "titre sur plusieurs lignes");
                Assert.That(message.ActualHeight, Is.LessThan(SingleLineHeight(message) * 1.5), "message sur plusieurs lignes");
            });
        }
        finally
        {
            dialog.Close();
        }
    }

    [Test]
    public void A_long_message_wraps_instead_of_being_cut()
    {
        var dialog = Create("Impossible d'importer le document", new string('x', 40) + " " + string.Join(' ', Enumerable.Repeat("mot", 80)));
        try
        {
            var message = (TextBlock)dialog.FindName("MessageText");
            Assert.That(message.ActualHeight, Is.GreaterThan(SingleLineHeight(message) * 1.5));
        }
        finally
        {
            dialog.Close();
        }
    }
}
