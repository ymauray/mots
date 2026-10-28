using System.Windows.Threading;
using NUnit.Framework;

// WPF exige un thread STA : tous les tests s'exécutent dans cet apartment.
[assembly: Apartment(ApartmentState.STA)]

namespace Mots.Tests;

internal static class Wpf
{
    /// <summary>Laisse le dispatcher traiter les mises en page et les rendus en attente.</summary>
    public static void Pump()
    {
        for (var i = 0; i < 4; i++)
            Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
    }
}

/// <summary>Fournit un dossier temporaire propre à chaque test.</summary>
public abstract class FileTestBase
{
    protected string Dir { get; private set; } = "";

    [SetUp]
    public void CreateTempDir()
    {
        Dir = Path.Combine(Path.GetTempPath(), "MotsTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Dir);
    }

    [TearDown]
    public void DeleteTempDir()
    {
        try
        {
            Directory.Delete(Dir, recursive: true);
        }
        catch (IOException)
        {
            // un fichier encore ouvert par un test en échec : le dossier temporaire sera nettoyé plus tard
        }
    }

    protected string PathOf(string fileName) => Path.Combine(Dir, fileName);
}
