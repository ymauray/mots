using System.Reflection;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using NUnit.Framework;

namespace Mots.Tests;

public class FormattingMarksTests : ShownWindowTestBase
{
    private sealed record Mark(string Glyph, double Size);

    private static readonly MethodInfo OnRender =
        typeof(FormattingMarksOverlay).GetMethod("OnRender", BindingFlags.NonPublic | BindingFlags.Instance)!;

    /// <summary>Dessine l'incrustation dans un DrawingVisual et relève les glyphes tracés.</summary>
    private List<Mark> Render()
    {
        H.MarksButton.IsChecked = true;
        H.Window.UpdateLayout();
        Wpf.Pump();

        var overlay = (FormattingMarksOverlay)H.MarksOverlay;
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
            OnRender.Invoke(overlay, [dc]);

        var marks = new List<Mark>();
        Collect(visual.Drawing, marks);
        return marks;
    }

    private static void Collect(Drawing? drawing, List<Mark> marks)
    {
        switch (drawing)
        {
            case GlyphRunDrawing glyphs:
                marks.Add(new Mark(new string(glyphs.GlyphRun.Characters.ToArray()), glyphs.GlyphRun.FontRenderingEmSize));
                break;
            case DrawingGroup group:
                foreach (var child in group.Children)
                    Collect(child, marks);
                break;
        }
    }

    private List<string> Glyphs() => Render().Select(m => m.Glyph).ToList();

    [Test]
    public void The_overlay_ignores_the_mouse()
    {
        Assert.That(H.MarksOverlay.IsHitTestVisible, Is.False);
    }

    [Test]
    public void The_overlay_is_wired_to_the_editor()
    {
        Assert.That(((FormattingMarksOverlay)H.MarksOverlay).Target, Is.SameAs(H.Editor));
    }

    [Test]
    public void Plain_letters_only_get_the_paragraph_mark()
    {
        H.LoadParagraphs(Docs.P(StyleKind.Normal, "abc"));

        Assert.That(Glyphs(), Is.EqualTo(new[] { "¶" }));
    }

    [Test]
    public void Each_space_gets_a_middle_dot()
    {
        H.LoadParagraphs(Docs.P(StyleKind.Normal, "un deux trois"));

        Assert.That(Glyphs().Count(g => g == "·"), Is.EqualTo(2));
    }

    [Test]
    public void Consecutive_spaces_each_get_a_dot()
    {
        H.LoadParagraphs(Docs.P(StyleKind.Normal, "a   b"));

        Assert.That(Glyphs().Count(g => g == "·"), Is.EqualTo(3));
    }

    [Test]
    public void A_non_breaking_space_gets_a_degree_sign()
    {
        H.LoadParagraphs(Docs.P(StyleKind.Normal, "a b"));

        var marks = Render();
        Assert.Multiple(() =>
        {
            Assert.That(marks.Count(m => m.Glyph == "°"), Is.EqualTo(1));
            Assert.That(marks.Count(m => m.Glyph == "·"), Is.EqualTo(0));
        });
    }

    [Test]
    public void A_narrow_non_breaking_space_gets_a_smaller_degree_sign()
    {
        H.LoadParagraphs(Docs.P(StyleKind.Normal, "a b c"));

        var degrees = Render().Where(m => m.Glyph == "°").OrderByDescending(m => m.Size).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(degrees, Has.Count.EqualTo(2));
            Assert.That(degrees[1].Size / degrees[0].Size, Is.EqualTo(0.7).Within(0.02));
        });
    }

    [Test]
    public void A_tab_gets_an_arrow()
    {
        H.LoadParagraphs(Docs.P(StyleKind.Normal, "a\tb"));

        Assert.That(Glyphs().Count(g => g == "→"), Is.EqualTo(1));
    }

    [Test]
    public void A_line_break_gets_a_return_arrow()
    {
        H.LoadParagraphs(Docs.P(StyleKind.Normal, new Run("a"), new LineBreak(), new Run("b")));

        Assert.That(Glyphs().Count(g => g == "↵"), Is.EqualTo(1));
    }

    [Test]
    public void Each_paragraph_gets_its_own_mark()
    {
        H.LoadParagraphs(Docs.P(StyleKind.Normal, "a"), Docs.P(StyleKind.Heading1, "b"), Docs.P(StyleKind.Normal));

        Assert.That(Glyphs().Count(g => g == "¶"), Is.EqualTo(3));
    }

    [Test]
    public void Marks_follow_the_paragraph_font_size()
    {
        H.LoadParagraphs(Docs.P(StyleKind.Normal, "a"), Docs.P(StyleKind.Heading1, "b"));

        var sizes = Render().Where(m => m.Glyph == "¶").Select(m => m.Size).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(sizes, Has.Count.EqualTo(2));
            Assert.That(sizes[1] / sizes[0], Is.EqualTo(24.0 / 16).Within(0.02));
        });
    }

    [Test]
    public void Mixed_content_gets_every_kind_of_mark()
    {
        H.LoadParagraphs(Docs.P(StyleKind.Normal, new Run("a b c\td"), new LineBreak(), new Run("e")));

        var glyphs = Glyphs();
        Assert.Multiple(() =>
        {
            Assert.That(glyphs, Does.Contain("·"));
            Assert.That(glyphs, Does.Contain("°"));
            Assert.That(glyphs, Does.Contain("→"));
            Assert.That(glyphs, Does.Contain("↵"));
            Assert.That(glyphs, Does.Contain("¶"));
        });
    }

    [Test]
    public void Marks_are_redrawn_for_the_new_document()
    {
        H.LoadParagraphs(Docs.P(StyleKind.Normal, "a b c d"));
        Assert.That(Glyphs().Count(g => g == "·"), Is.EqualTo(3));

        H.LoadParagraphs(Docs.P(StyleKind.Normal, "ab"));
        Assert.That(Glyphs().Count(g => g == "·"), Is.EqualTo(0));
    }

    [Test]
    public void An_overlay_without_a_target_draws_nothing()
    {
        var overlay = new FormattingMarksOverlay();
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
            OnRender.Invoke(overlay, [dc]);

        var marks = new List<Mark>();
        Collect(visual.Drawing, marks);
        Assert.That(marks, Is.Empty);
    }

    [Test]
    public void Changing_the_target_is_allowed()
    {
        var overlay = new FormattingMarksOverlay { Target = H.Editor };

        Assert.Multiple(() =>
        {
            Assert.That(() => overlay.Target = new System.Windows.Controls.RichTextBox(), Throws.Nothing);
            Assert.That(() => overlay.Target = null, Throws.Nothing);
        });
    }
}
