using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using NUnit.Framework;

namespace Mots.Tests;

/// <summary>
/// Fenêtre principale réelle, pilotée par réflexion pour les membres privés (les gestionnaires d'événements
/// et l'état interne). Les dialogues modaux ne sont jamais déclenchés.
/// </summary>
internal sealed class WindowHarness : IDisposable
{
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly;

    public MainWindow Window { get; } = new();

    public WindowHarness(bool show = false)
    {
        if (!show) return;
        Window.Left = -20000; // hors de l'écran : certains tests ont besoin d'une source de présentation
        Window.Top = 0;
        Window.ShowActivated = false;
        Window.ShowInTaskbar = false;
        Window.Show();
        Wpf.Pump();
    }

    public T Element<T>(string name) where T : class =>
        Window.FindName(name) as T ?? throw new InvalidOperationException($"Élément introuvable : {name}");

    public RichTextBox Editor => Element<RichTextBox>("Editor");
    public ComboBox StyleBox => Element<ComboBox>("StyleBox");
    public TextBlock StatusText => Element<TextBlock>("StatusText");
    public Button ZoomText => Element<Button>("ZoomText");
    public Slider ZoomSlider => Element<Slider>("ZoomSlider");
    public ScaleTransform PageScale => Element<ScaleTransform>("PageScale");
    public ListBox OutlineList => Element<ListBox>("OutlineList");
    public TextBlock OutlineEmptyText => Element<TextBlock>("OutlineEmptyText");
    public TextBox FindBox => Element<TextBox>("FindBox");
    public TextBox ReplaceBox => Element<TextBox>("ReplaceBox");
    public Border FindBar => Element<Border>("FindBar");
    public System.Windows.Controls.Primitives.ToggleButton MarksButton => Element<System.Windows.Controls.Primitives.ToggleButton>("MarksButton");
    public FrameworkElement MarksOverlay => Element<FrameworkElement>("MarksOverlay");

    public List<Paragraph> Paragraphs => Editor.Document.Blocks.OfType<Paragraph>().ToList();

    public List<StyleKind> Kinds => Paragraphs.Select(ParagraphStyles.KindOf).ToList();

    public object? Invoke(string method, params object?[] args)
    {
        var m = typeof(MainWindow).GetMethod(method, Private)
                ?? throw new MissingMethodException(nameof(MainWindow), method);
        try
        {
            return m.Invoke(Window, args);
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw;
        }
    }

    public T Field<T>(string name) =>
        (T)(typeof(MainWindow).GetField(name, Private) ?? throw new MissingFieldException(nameof(MainWindow), name)).GetValue(Window)!;

    public void SetField(string name, object? value) =>
        (typeof(MainWindow).GetField(name, Private) ?? throw new MissingFieldException(nameof(MainWindow), name)).SetValue(Window, value);

    public bool Dirty => Field<bool>("_dirty");

    public double Zoom => Field<double>("_zoomPercent");

    public void SetZoom(double percent) => Invoke("SetZoom", percent);

    public void Load(FlowDocument document) => Invoke("ReplaceDocument", document);

    public void LoadParagraphs(params Paragraph[] paragraphs) => Load(Docs.Doc(paragraphs));

    /// <summary>Ramène l'état « non modifié » après la préparation d'un document de test.</summary>
    public void MarkClean() => Invoke("SetDirty", false);

    public void SelectAll() => Editor.SelectAll();

    public void Select(int paragraphIndex, int start, int length)
    {
        var paragraph = Paragraphs[paragraphIndex];
        var from = paragraph.ContentStart.GetPositionAtOffset(start + 1) ?? paragraph.ContentStart; // +1 : ouverture du Run
        var to = from.GetPositionAtOffset(length) ?? from;
        Editor.Selection.Select(from, to);
    }

    public void PlaceCaretAtEnd(int paragraphIndex) =>
        Editor.CaretPosition = Paragraphs[paragraphIndex].ContentEnd;

    public void PlaceCaretAtStart(int paragraphIndex) =>
        Editor.CaretPosition = Paragraphs[paragraphIndex].ContentStart;

    /// <summary>Simule une touche dans l'éditeur (la fenêtre doit avoir été affichée).</summary>
    public bool PressKey(Key key)
    {
        var source = PresentationSource.FromVisual(Editor)
                     ?? throw new InvalidOperationException("La fenêtre doit être affichée.");
        Editor.Focus();
        Keyboard.Focus(Editor);
        var args = new KeyEventArgs(Keyboard.PrimaryDevice, source, Environment.TickCount, key)
        {
            RoutedEvent = Keyboard.PreviewKeyDownEvent,
        };
        Editor.RaiseEvent(args);
        return args.Handled;
    }

    public void Dispose()
    {
        // Pas de dialogue « enregistrer les modifications ? » à la fermeture.
        SetField("_dirty", false);
        try
        {
            Window.Close();
        }
        catch (InvalidOperationException)
        {
            // fenêtre jamais affichée
        }
    }
}

/// <summary>Base : une fenêtre fraîche (non affichée) par test.</summary>
public abstract class WindowTestBase : FileTestBase
{
    internal WindowHarness H { get; private set; } = null!;

    [SetUp]
    public void CreateWindow() => H = new WindowHarness();

    [TearDown]
    public void DisposeWindow() => H.Dispose();
}

/// <summary>Base : une fenêtre affichée hors écran (pour les tests de clavier et de défilement).</summary>
public abstract class ShownWindowTestBase : FileTestBase
{
    internal WindowHarness H { get; private set; } = null!;

    [SetUp]
    public void CreateWindow() => H = new WindowHarness(show: true);

    [TearDown]
    public void DisposeWindow() => H.Dispose();
}
