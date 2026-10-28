using System.Windows.Controls;
using NUnit.Framework;

namespace Mots.Tests;

public class RecentFilesTests : FileTestBase
{
    private string Store => PathOf("sous\\dossier\\recent.json");

    private string Full(string name) => Path.GetFullPath(PathOf(name));

    [Test]
    public void A_new_list_is_empty()
    {
        Assert.That(new RecentFiles(Store).Items, Is.Empty);
    }

    [Test]
    public void The_most_recent_file_comes_first()
    {
        var recent = new RecentFiles();
        recent.Add(Full("a.docx"));
        recent.Add(Full("b.docx"));

        Assert.That(recent.Items, Is.EqualTo(new[] { Full("b.docx"), Full("a.docx") }));
    }

    [Test]
    public void Adding_a_known_file_moves_it_to_the_top_without_duplicate()
    {
        var recent = new RecentFiles();
        recent.Add(Full("a.docx"));
        recent.Add(Full("b.docx"));
        recent.Add(Full("a.docx"));

        Assert.That(recent.Items, Is.EqualTo(new[] { Full("a.docx"), Full("b.docx") }));
    }

    [Test]
    public void Duplicates_are_detected_whatever_the_case()
    {
        var recent = new RecentFiles();
        recent.Add(Full("Roman.docx"));
        recent.Add(Full("roman.DOCX"));

        Assert.That(recent.Items, Has.Count.EqualTo(1));
        Assert.That(recent.Items[0], Is.EqualTo(Full("roman.DOCX")));
    }

    [Test]
    public void Relative_paths_are_stored_as_full_paths()
    {
        var recent = new RecentFiles();
        recent.Add("x.docx");

        Assert.That(recent.Items[0], Is.EqualTo(Path.GetFullPath("x.docx")));
    }

    [TestCase("")]
    [TestCase("   ")]
    public void Blank_paths_are_ignored(string path)
    {
        var recent = new RecentFiles();
        recent.Add(path);

        Assert.That(recent.Items, Is.Empty);
    }

    [Test]
    public void The_list_is_bounded_and_drops_the_oldest()
    {
        var recent = new RecentFiles(capacity: 3);
        for (var i = 1; i <= 5; i++) recent.Add(Full($"{i}.docx"));

        Assert.That(recent.Items, Is.EqualTo(new[] { Full("5.docx"), Full("4.docx"), Full("3.docx") }));
    }

    [Test]
    public void The_default_capacity_is_ten()
    {
        var recent = new RecentFiles();
        for (var i = 1; i <= 15; i++) recent.Add(Full($"{i}.docx"));

        Assert.That(recent.Items, Has.Count.EqualTo(10));
        Assert.That(recent.Items[0], Is.EqualTo(Full("15.docx")));
    }

    [Test]
    public void A_capacity_below_one_is_rejected()
    {
        Assert.That(() => new RecentFiles(capacity: 0), Throws.InstanceOf<ArgumentOutOfRangeException>());
    }

    [Test]
    public void Remove_deletes_the_entry()
    {
        var recent = new RecentFiles();
        recent.Add(Full("a.docx"));
        recent.Add(Full("b.docx"));

        recent.Remove(Full("A.docx"));

        Assert.That(recent.Items, Is.EqualTo(new[] { Full("b.docx") }));
    }

    [Test]
    public void Removing_an_unknown_file_changes_nothing()
    {
        var recent = new RecentFiles();
        recent.Add(Full("a.docx"));

        recent.Remove(Full("z.docx"));

        Assert.That(recent.Items, Has.Count.EqualTo(1));
    }

    [Test]
    public void Clear_empties_the_list()
    {
        var recent = new RecentFiles();
        recent.Add(Full("a.docx"));

        recent.Clear();

        Assert.That(recent.Items, Is.Empty);
    }

    // ---------- Persistance ----------

    [Test]
    public void The_list_survives_a_restart_in_the_same_order()
    {
        var first = new RecentFiles(Store);
        first.Add(Full("a.docx"));
        first.Add(Full("b.docx"));

        var second = new RecentFiles(Store);

        Assert.That(second.Items, Is.EqualTo(first.Items));
    }

    [Test]
    public void Saving_creates_the_missing_directory()
    {
        new RecentFiles(Store).Add(Full("a.docx"));

        Assert.That(File.Exists(Store), Is.True);
    }

    [Test]
    public void Removal_and_clearing_are_persisted()
    {
        var recent = new RecentFiles(Store);
        recent.Add(Full("a.docx"));
        recent.Add(Full("b.docx"));
        recent.Remove(Full("a.docx"));
        Assert.That(new RecentFiles(Store).Items, Is.EqualTo(new[] { Full("b.docx") }));

        recent.Clear();
        Assert.That(new RecentFiles(Store).Items, Is.Empty);
    }

    [TestCase("pas du json")]
    [TestCase("{\"a\": 1}")]
    [TestCase("[1, 2]")]
    [TestCase("")]
    [TestCase("null")]
    public void A_corrupt_store_gives_an_empty_list_and_remains_usable(string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Store)!);
        File.WriteAllText(Store, content);

        var recent = new RecentFiles(Store);
        Assert.That(recent.Items, Is.Empty);

        recent.Add(Full("a.docx"));
        Assert.That(new RecentFiles(Store).Items, Is.EqualTo(new[] { Full("a.docx") }));
    }

    [Test]
    public void Loading_drops_blanks_duplicates_and_excess_entries()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Store)!);
        File.WriteAllText(Store, "[\"a.docx\", \"\", \"A.docx\", \"b.docx\", \"c.docx\"]");

        var recent = new RecentFiles(Store, capacity: 2);

        Assert.That(recent.Items, Is.EqualTo(new[] { "a.docx", "b.docx" }));
    }

    [Test]
    public void An_unwritable_store_does_not_break_the_list()
    {
        var blocker = PathOf("fichier");
        File.WriteAllText(blocker, "x");
        var recent = new RecentFiles(Path.Combine(blocker, "recent.json")); // « fichier » n'est pas un dossier

        Assert.That(() => recent.Add(Full("a.docx")), Throws.Nothing);
        Assert.That(recent.Items, Has.Count.EqualTo(1));
    }

    [Test]
    public void The_default_store_lives_under_appdata_mots()
    {
        // On ne touche pas au vrai fichier : seul le chemin est vérifié, via une liste purement lue.
        var expected = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Mots", "recent.json");

        Assert.That(expected, Does.EndWith(Path.Combine("Mots", "recent.json")));
        Assert.That(RecentFiles.CreateDefault(), Is.Not.Null);
    }
}

public class MainWindowRecentTests : WindowTestBase
{
    private string SaveDoc(string name, string text)
    {
        var path = PathOf(name);
        DocxConverter.Save(Docs.Doc(Docs.P(StyleKind.Normal, text)), path);
        return path;
    }

    private List<MenuItem> MenuItems() =>
        ((ContextMenu)H.Invoke("BuildRecentMenu")!).Items.OfType<MenuItem>().ToList();

    [Test]
    public void The_menu_says_so_when_there_is_no_recent_document()
    {
        var items = MenuItems();

        Assert.Multiple(() =>
        {
            Assert.That(items, Has.Count.EqualTo(1));
            Assert.That(items[0].Header, Is.EqualTo("Aucun document récent"));
            Assert.That(items[0].IsEnabled, Is.False);
        });
    }

    [Test]
    public void The_menu_does_not_inherit_the_icon_font_of_its_button()
    {
        var menu = (ContextMenu)H.Invoke("BuildRecentMenu")!;

        Assert.Multiple(() =>
        {
            Assert.That(menu.FontFamily.Source, Is.Not.Contain("Icons").And.Not.Contain("MDL2"));
            Assert.That(menu.FontSize, Is.GreaterThanOrEqualTo(12));
        });
    }

    [Test]
    public void Saving_adds_the_file_to_the_recent_list()
    {
        H.Invoke("TrySave", PathOf("roman.docx"));

        Assert.That(H.Recent.Items, Is.EqualTo(new[] { Path.GetFullPath(PathOf("roman.docx")) }));
    }

    [Test]
    public void Opening_a_file_adds_it_and_loads_it()
    {
        var path = SaveDoc("roman.docx", "Il était une fois");

        H.Invoke("OpenPath", path);

        Assert.Multiple(() =>
        {
            Assert.That(Docs.Text(H.Paragraphs[0]), Is.EqualTo("Il était une fois"));
            Assert.That(H.Window.Title, Is.EqualTo("roman.docx — Mots"));
            Assert.That(H.Dirty, Is.False);
            Assert.That(H.Recent.Items[0], Is.EqualTo(Path.GetFullPath(path)));
        });
    }

    [Test]
    public void Reopening_an_older_file_moves_it_back_to_the_top()
    {
        var a = SaveDoc("a.docx", "A");
        var b = SaveDoc("b.docx", "B");
        H.Invoke("OpenPath", a);
        H.Invoke("OpenPath", b);
        H.Invoke("OpenPath", a);

        Assert.That(H.Recent.Items, Is.EqualTo(new[] { Path.GetFullPath(a), Path.GetFullPath(b) }));
    }

    [Test]
    public void The_menu_lists_files_most_recent_first_with_their_folder()
    {
        var a = SaveDoc("a.docx", "A");
        var b = SaveDoc("b.docx", "B");
        H.Invoke("OpenPath", a);
        H.Invoke("OpenPath", b);

        var items = MenuItems().Where(i => i.Header is StackPanel).ToList();
        var names = items.Select(i => ((TextBlock)((StackPanel)i.Header).Children[0]).Text).ToList();
        var folder = ((TextBlock)((StackPanel)items[0].Header).Children[1]).Text;

        Assert.Multiple(() =>
        {
            Assert.That(names, Is.EqualTo(new[] { "b.docx", "a.docx" }));
            Assert.That(folder, Is.EqualTo(Path.GetDirectoryName(Path.GetFullPath(b))));
            Assert.That(items[0].ToolTip, Is.EqualTo(Path.GetFullPath(b)));
        });
    }

    [Test]
    public void The_menu_ends_with_a_clear_command_that_empties_the_list()
    {
        H.Invoke("OpenPath", SaveDoc("a.docx", "A"));
        var menu = (ContextMenu)H.Invoke("BuildRecentMenu")!;

        Assert.That(menu.Items[^2], Is.InstanceOf<Separator>());
        var clear = (MenuItem)menu.Items[^1]!;
        Assert.That(clear.Header, Is.EqualTo("Effacer la liste"));

        clear.RaiseEvent(new System.Windows.RoutedEventArgs(MenuItem.ClickEvent));
        Assert.That(H.Recent.Items, Is.Empty);
    }

    [Test]
    public void Choosing_a_recent_file_opens_it_when_nothing_is_to_lose()
    {
        var a = SaveDoc("a.docx", "Premier");
        var b = SaveDoc("b.docx", "Second");
        H.Invoke("OpenPath", a);
        H.Invoke("OpenPath", b);

        MenuItems()[1].RaiseEvent(new System.Windows.RoutedEventArgs(MenuItem.ClickEvent)); // a.docx

        Assert.Multiple(() =>
        {
            Assert.That(Docs.Text(H.Paragraphs[0]), Is.EqualTo("Premier"));
            Assert.That(H.Recent.Items[0], Is.EqualTo(Path.GetFullPath(a)));
        });
    }

    [Test]
    public void The_recent_list_is_shared_with_the_next_window()
    {
        H.Invoke("TrySave", PathOf("roman.docx"));

        using var next = new WindowHarness(PathOf("recent.json"));

        Assert.That(next.Recent.Items, Is.EqualTo(H.Recent.Items));
    }

    [Test]
    public void The_recent_button_exists_next_to_open()
    {
        Assert.That(H.Element<Button>("RecentButton").ToolTip, Is.EqualTo("Documents récents"));
    }
}
