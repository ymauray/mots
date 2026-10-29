using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using NUnit.Framework;

namespace Mots.Tests;

public class MainWindowStartupTests : WindowTestBase
{
    [Test]
    public void Starts_untitled_and_unmodified()
    {
        Assert.Multiple(() =>
        {
            Assert.That(H.Window.Title, Is.EqualTo("Sans titre — Mots"));
            Assert.That(H.Dirty, Is.False);
        });
    }

    [Test]
    public void Starts_with_one_empty_normal_paragraph()
    {
        Assert.Multiple(() =>
        {
            Assert.That(H.Kinds, Is.EqualTo(new[] { StyleKind.Normal }));
            Assert.That(Docs.Text(H.Paragraphs[0]), Is.Empty);
            Assert.That(H.Paragraphs[0].FontFamily.Source, Is.EqualTo("Georgia"));
        });
    }

    [Test]
    public void Style_selector_lists_the_three_styles_by_display_name()
    {
        Assert.Multiple(() =>
        {
            Assert.That(H.StyleBox.Items.Count, Is.EqualTo(3));
            Assert.That(H.StyleBox.DisplayMemberPath, Is.EqualTo("DisplayName"));
            Assert.That(H.StyleBox.Items.Cast<ParagraphStyles.Definition>().Select(d => d.DisplayName),
                Is.EqualTo(new[] { "Normal", "Titre 1", "Ellipse" }));
            Assert.That(H.StyleBox.SelectedValue, Is.EqualTo(StyleKind.Normal));
        });
    }

    [Test]
    public void Starts_with_empty_status_counters()
    {
        Assert.That(H.StatusText.Text, Is.EqualTo("0 mot(s) · 0 caractère(s)"));
    }

    [Test]
    public void Starts_at_100_percent_zoom_with_marks_hidden()
    {
        Assert.Multiple(() =>
        {
            Assert.That(H.Zoom, Is.EqualTo(100));
            Assert.That(H.ZoomText.Content, Is.EqualTo("100 %"));
            Assert.That(H.MarksButton.IsChecked, Is.Not.EqualTo(true));
            Assert.That(H.MarksOverlay.Visibility, Is.EqualTo(Visibility.Collapsed));
            Assert.That(H.FindBar.Visibility, Is.EqualTo(Visibility.Collapsed));
        });
    }

    [Test]
    public void Starts_with_an_empty_outline_and_its_hint_visible()
    {
        Assert.Multiple(() =>
        {
            Assert.That(H.OutlineList.Items.Count, Is.EqualTo(0));
            Assert.That(H.OutlineEmptyText.Visibility, Is.EqualTo(Visibility.Visible));
        });
    }

    [Test]
    public void Spell_check_is_off_and_language_is_french()
    {
        Assert.Multiple(() =>
        {
            Assert.That(H.Editor.SpellCheck.IsEnabled, Is.False); // coûteux à la frappe ; la correction se fait dans Antidote
            Assert.That(H.Editor.Language.IetfLanguageTag, Is.EqualTo("fr-FR").IgnoreCase);
        });
    }
}

public class MainWindowEditingTests : WindowTestBase
{
    [Test]
    public void Typing_marks_the_document_as_modified()
    {
        H.Editor.CaretPosition.InsertTextInRun("Bonjour");

        Assert.Multiple(() =>
        {
            Assert.That(H.Dirty, Is.True);
            Assert.That(H.Window.Title, Is.EqualTo("*Sans titre — Mots"));
        });
    }

    [Test]
    public void Typing_updates_the_word_and_character_counts_after_a_delay()
    {
        var timer = H.Field<System.Windows.Threading.DispatcherTimer>("_statusTimer");

        H.Editor.CaretPosition.InsertTextInRun("Un deux  trois");

        Assert.Multiple(() =>
        {
            Assert.That(H.StatusText.Text, Is.EqualTo("0 mot(s) · 0 caractère(s)"), "pas de recomptage à la frappe");
            Assert.That(timer.IsEnabled, Is.True);
        });

        timer.Interval = TimeSpan.FromMilliseconds(1);
        timer.Stop();
        timer.Start();
        System.Threading.Thread.Sleep(20);
        Wpf.Pump();

        Assert.Multiple(() =>
        {
            Assert.That(H.StatusText.Text, Is.EqualTo("3 mot(s) · 14 caractère(s)"));
            Assert.That(timer.IsEnabled, Is.False);
        });
    }

    [Test]
    public void Counts_span_all_paragraphs()
    {
        H.LoadParagraphs(Docs.P(StyleKind.Normal, "un deux"), Docs.P(StyleKind.Normal, "trois"));
        H.Invoke("UpdateStatus");

        Assert.That(H.StatusText.Text, Does.StartWith("3 mot(s)"));
    }

    [Test]
    public void Accented_text_counts_each_character_once()
    {
        H.LoadParagraphs(Docs.P(StyleKind.Normal, "été"));
        H.Invoke("UpdateStatus");

        Assert.That(H.StatusText.Text, Is.EqualTo("1 mot(s) · 3 caractère(s)"));
    }

    [Test]
    public void Pasting_rich_text_applies_the_plain_text_format()
    {
        var data = new DataObject();
        data.SetData(DataFormats.UnicodeText, "texte");
        data.SetData(DataFormats.Rtf, @"{
tf1 { texte}}");
        var args = new DataObjectPastingEventArgs(data, false, DataFormats.Rtf) { RoutedEvent = DataObject.PastingEvent };

        H.Editor.RaiseEvent(args);

        Assert.That(args.FormatToApply, Is.EqualTo(DataFormats.UnicodeText));
    }

    [Test]
    public void Pasting_without_text_keeps_the_requested_format()
    {
        var data = new DataObject();
        data.SetData(DataFormats.Rtf, @"{
tf1 x}");
        var args = new DataObjectPastingEventArgs(data, false, DataFormats.Rtf) { RoutedEvent = DataObject.PastingEvent };

        H.Editor.RaiseEvent(args);

        Assert.That(args.FormatToApply, Is.EqualTo(DataFormats.Rtf));
    }

    [Test]
    public void Layout_commands_are_disabled()
    {
        RoutedCommand[] disabled =
        [
            EditingCommands.AlignLeft, EditingCommands.AlignCenter, EditingCommands.AlignRight, EditingCommands.AlignJustify,
            EditingCommands.ToggleBullets, EditingCommands.ToggleNumbering,
            EditingCommands.IncreaseIndentation, EditingCommands.DecreaseIndentation,
            EditingCommands.IncreaseFontSize, EditingCommands.DecreaseFontSize,
            EditingCommands.ToggleSubscript, EditingCommands.ToggleSuperscript,
        ];

        Assert.Multiple(() =>
        {
            foreach (var command in disabled)
                Assert.That(command.CanExecute(null, H.Editor), Is.False, command.Name);
        });
    }

    [Test]
    public void Character_formatting_commands_remain_available()
    {
        H.Editor.CaretPosition.InsertTextInRun("abc");
        H.SelectAll();

        Assert.Multiple(() =>
        {
            Assert.That(EditingCommands.ToggleBold.CanExecute(null, H.Editor), Is.True);
            Assert.That(EditingCommands.ToggleItalic.CanExecute(null, H.Editor), Is.True);
            Assert.That(EditingCommands.ToggleUnderline.CanExecute(null, H.Editor), Is.True);
        });
    }

    [Test]
    public void Bold_command_makes_the_selection_bold()
    {
        H.Editor.CaretPosition.InsertTextInRun("abc");
        H.SelectAll();
        EditingCommands.ToggleBold.Execute(null, H.Editor);

        Assert.That(H.Editor.Selection.GetPropertyValue(TextElement.FontWeightProperty), Is.EqualTo(FontWeights.Bold));
    }
}

public class MainWindowStyleTests : WindowTestBase
{
    private void ThreeParagraphs() => H.LoadParagraphs(
        Docs.P(StyleKind.Normal, "un"), Docs.P(StyleKind.Normal, "deux"), Docs.P(StyleKind.Normal, "trois"));

    [Test]
    public void Applying_a_style_changes_the_paragraph_and_marks_modified()
    {
        ThreeParagraphs();
        H.MarkClean();
        H.PlaceCaretAtEnd(1);

        H.Invoke("ApplyStyle", StyleKind.Heading1);

        Assert.Multiple(() =>
        {
            Assert.That(H.Kinds, Is.EqualTo(new[] { StyleKind.Normal, StyleKind.Heading1, StyleKind.Normal }));
            Assert.That(H.Dirty, Is.True);
            Assert.That(H.Paragraphs[1].BreakPageBefore, Is.True);
        });
    }

    [Test]
    public void Applying_a_style_to_a_multi_paragraph_selection_changes_all_of_them()
    {
        ThreeParagraphs();
        H.SelectAll();

        H.Invoke("ApplyStyle", StyleKind.Ellipse);

        Assert.That(H.Kinds, Is.EqualTo(new[] { StyleKind.Ellipse, StyleKind.Ellipse, StyleKind.Ellipse }));
    }

    [Test]
    public void A_selection_across_two_paragraphs_only_changes_those_two()
    {
        ThreeParagraphs();
        H.Editor.Selection.Select(H.Paragraphs[0].ContentEnd.GetPositionAtOffset(-1)!, H.Paragraphs[1].ContentStart.GetPositionAtOffset(2)!);

        H.Invoke("ApplyStyle", StyleKind.Heading1);

        Assert.That(H.Kinds, Is.EqualTo(new[] { StyleKind.Heading1, StyleKind.Heading1, StyleKind.Normal }));
    }

    [Test]
    public void Applying_a_style_keeps_the_text()
    {
        ThreeParagraphs();
        H.SelectAll();

        H.Invoke("ApplyStyle", StyleKind.Heading1);

        Assert.That(H.Paragraphs.Select(Docs.Text), Is.EqualTo(new[] { "un", "deux", "trois" }));
    }

    [Test]
    public void The_selector_follows_the_caret_paragraph()
    {
        H.LoadParagraphs(Docs.P(StyleKind.Heading1, "T"), Docs.P(StyleKind.Normal, "n"), Docs.P(StyleKind.Ellipse, "*"));

        H.PlaceCaretAtEnd(0);
        Assert.That(H.StyleBox.SelectedValue, Is.EqualTo(StyleKind.Heading1));
        H.PlaceCaretAtEnd(2);
        Assert.That(H.StyleBox.SelectedValue, Is.EqualTo(StyleKind.Ellipse));
        H.PlaceCaretAtEnd(1);
        Assert.That(H.StyleBox.SelectedValue, Is.EqualTo(StyleKind.Normal));
    }

    [Test]
    public void Choosing_a_style_in_the_selector_applies_it()
    {
        ThreeParagraphs();
        H.PlaceCaretAtEnd(2);

        H.StyleBox.SelectedValue = StyleKind.Ellipse;

        Assert.That(H.Kinds, Is.EqualTo(new[] { StyleKind.Normal, StyleKind.Normal, StyleKind.Ellipse }));
    }

    [Test]
    public void Updating_the_selector_from_the_caret_does_not_apply_a_style()
    {
        H.LoadParagraphs(Docs.P(StyleKind.Heading1, "T"), Docs.P(StyleKind.Normal, "n"));
        H.MarkClean();

        H.PlaceCaretAtEnd(0);
        H.PlaceCaretAtEnd(1);

        Assert.Multiple(() =>
        {
            Assert.That(H.Kinds, Is.EqualTo(new[] { StyleKind.Heading1, StyleKind.Normal }));
            Assert.That(H.Dirty, Is.False);
        });
    }

    [Test]
    public void Style_commands_apply_normal_and_heading_1()
    {
        ThreeParagraphs();
        H.PlaceCaretAtEnd(0);

        StyleCommands.Heading1.Execute(null, H.Window);
        Assert.That(H.Kinds[0], Is.EqualTo(StyleKind.Heading1));

        StyleCommands.Normal.Execute(null, H.Window);
        Assert.That(H.Kinds[0], Is.EqualTo(StyleKind.Normal));
    }

    [Test]
    public void SelectedParagraphs_of_a_caret_is_the_current_paragraph()
    {
        ThreeParagraphs();
        H.PlaceCaretAtEnd(1);

        var selected = (List<Paragraph>)H.Invoke("SelectedParagraphs")!;

        Assert.That(selected, Is.EqualTo(new[] { H.Paragraphs[1] }));
    }

    [Test]
    public void SelectedParagraphs_of_a_full_selection_is_every_paragraph_once()
    {
        ThreeParagraphs();
        H.SelectAll();

        var selected = (List<Paragraph>)H.Invoke("SelectedParagraphs")!;

        Assert.That(selected, Is.EqualTo(H.Paragraphs));
    }

    [Test]
    public void Heading_style_survives_typing_in_the_paragraph()
    {
        H.LoadParagraphs(Docs.P(StyleKind.Heading1, "Titre"));
        H.PlaceCaretAtEnd(0);

        H.Editor.CaretPosition.InsertTextInRun(" suite");

        Assert.Multiple(() =>
        {
            Assert.That(H.Kinds, Is.EqualTo(new[] { StyleKind.Heading1 }));
            Assert.That(Docs.Text(H.Paragraphs[0]), Is.EqualTo("Titre suite"));
        });
    }
}

public class MainWindowEnterKeyTests : ShownWindowTestBase
{
    [Test]
    public void Enter_at_the_end_of_a_heading_starts_a_normal_paragraph()
    {
        H.LoadParagraphs(Docs.P(StyleKind.Heading1, "Chapitre"));
        H.PlaceCaretAtEnd(0);

        var handled = H.PressKey(Key.Enter);

        Assert.Multiple(() =>
        {
            Assert.That(handled, Is.True);
            Assert.That(H.Kinds, Is.EqualTo(new[] { StyleKind.Heading1, StyleKind.Normal }));
            Assert.That(H.Paragraphs.Select(Docs.Text), Is.EqualTo(new[] { "Chapitre", "" }));
        });
    }

    [Test]
    public void Enter_in_the_middle_of_a_heading_is_left_to_the_editor()
    {
        H.LoadParagraphs(Docs.P(StyleKind.Heading1, "Chapitre"));
        H.Editor.CaretPosition = H.Paragraphs[0].ContentStart.GetPositionAtOffset(3)!;

        var handled = H.PressKey(Key.Enter);

        Assert.That(handled, Is.False);
    }

    [Test]
    public void Enter_at_the_end_of_a_normal_paragraph_is_left_to_the_editor()
    {
        H.LoadParagraphs(Docs.P(StyleKind.Normal, "Texte"));
        H.PlaceCaretAtEnd(0);

        Assert.That(H.PressKey(Key.Enter), Is.False);
    }

    [Test]
    public void Enter_at_the_end_of_an_ellipse_starts_a_normal_paragraph()
    {
        H.LoadParagraphs(Docs.P(StyleKind.Ellipse, "***"));
        H.PlaceCaretAtEnd(0);

        H.PressKey(Key.Enter);

        Assert.That(H.Kinds, Is.EqualTo(new[] { StyleKind.Ellipse, StyleKind.Normal }));
    }

    [Test]
    public void Enter_with_a_selection_is_left_to_the_editor()
    {
        H.LoadParagraphs(Docs.P(StyleKind.Heading1, "Chapitre"));
        H.SelectAll();

        Assert.That(H.PressKey(Key.Enter), Is.False);
    }

    [Test]
    public void Enter_at_the_end_of_a_heading_is_a_single_undo_step()
    {
        H.LoadParagraphs(Docs.P(StyleKind.Heading1, "Chapitre"));
        H.PlaceCaretAtEnd(0);
        H.Editor.IsUndoEnabled = true;
        H.Editor.UndoLimit = 100;

        H.PressKey(Key.Enter);
        ApplicationCommands.Undo.Execute(null, H.Editor);

        Assert.That(H.Kinds, Is.EqualTo(new[] { StyleKind.Heading1 }));
    }

    [Test]
    public void Other_keys_are_not_handled()
    {
        H.LoadParagraphs(Docs.P(StyleKind.Heading1, "Chapitre"));
        H.PlaceCaretAtEnd(0);

        Assert.That(H.PressKey(Key.A), Is.False);
    }
}

public class MainWindowZoomTests : WindowTestBase
{
    [TestCase(100, 100)]
    [TestCase(150, 150)]
    [TestCase(50, 50)]
    [TestCase(300, 300)]
    [TestCase(10, 50)]
    [TestCase(0, 50)]
    [TestCase(-40, 50)]
    [TestCase(1000, 300)]
    [TestCase(107, 105)]
    [TestCase(102, 100)]
    [TestCase(103, 105)]
    [TestCase(52, 50)]
    [TestCase(298, 300)]
    public void SetZoom_rounds_to_5_and_clamps_to_50_300(double requested, double expected)
    {
        H.SetZoom(requested);

        Assert.That(H.Zoom, Is.EqualTo(expected));
    }

    [Test]
    public void SetZoom_scales_the_page_and_updates_label_and_slider()
    {
        H.SetZoom(150);

        Assert.Multiple(() =>
        {
            Assert.That(H.PageScale.ScaleX, Is.EqualTo(1.5).Within(1e-9));
            Assert.That(H.PageScale.ScaleY, Is.EqualTo(1.5).Within(1e-9));
            Assert.That(H.ZoomText.Content, Is.EqualTo("150 %"));
            Assert.That(H.ZoomSlider.Value, Is.EqualTo(150));
        });
    }

    [Test]
    public void Moving_the_slider_changes_the_zoom()
    {
        H.ZoomSlider.Value = 200;

        Assert.Multiple(() =>
        {
            Assert.That(H.Zoom, Is.EqualTo(200));
            Assert.That(H.PageScale.ScaleX, Is.EqualTo(2).Within(1e-9));
            Assert.That(H.ZoomText.Content, Is.EqualTo("200 %"));
        });
    }

    [Test]
    public void Zoom_in_and_out_commands_step_by_10()
    {
        ViewCommands.ZoomIn.Execute(null, H.Window);
        Assert.That(H.Zoom, Is.EqualTo(110));
        ViewCommands.ZoomOut.Execute(null, H.Window);
        ViewCommands.ZoomOut.Execute(null, H.Window);
        Assert.That(H.Zoom, Is.EqualTo(90));
    }

    [Test]
    public void Zoom_in_stops_at_300_and_out_at_50()
    {
        for (var i = 0; i < 40; i++) ViewCommands.ZoomIn.Execute(null, H.Window);
        Assert.That(H.Zoom, Is.EqualTo(300));
        for (var i = 0; i < 40; i++) ViewCommands.ZoomOut.Execute(null, H.Window);
        Assert.That(H.Zoom, Is.EqualTo(50));
    }

    [Test]
    public void Zoom_buttons_step_and_reset()
    {
        H.Invoke("ZoomIn_Click", null, new RoutedEventArgs());
        H.Invoke("ZoomIn_Click", null, new RoutedEventArgs());
        Assert.That(H.Zoom, Is.EqualTo(120));

        H.Invoke("ZoomOut_Click", null, new RoutedEventArgs());
        Assert.That(H.Zoom, Is.EqualTo(110));

        H.Invoke("ZoomReset_Click", null, new RoutedEventArgs());
        Assert.That(H.Zoom, Is.EqualTo(100));
        Assert.That(H.ZoomText.Content, Is.EqualTo("100 %"));
    }

    [Test]
    public void Zoom_does_not_modify_the_document()
    {
        H.MarkClean();

        H.SetZoom(200);

        Assert.That(H.Dirty, Is.False);
    }

    [Test]
    public void Zoom_survives_loading_another_document()
    {
        H.SetZoom(180);

        H.Load(Docs.Doc(Docs.P(StyleKind.Normal, "x")));

        Assert.That(H.Zoom, Is.EqualTo(180));
    }
}

public class MainWindowFindTests : WindowTestBase
{
    private void Document(params string[] texts) =>
        H.LoadParagraphs(texts.Select(t => Docs.P(StyleKind.Normal, t)).ToArray());

    private string Selected => H.Editor.Selection.Text;

    private void FindNext(string text)
    {
        H.FindBox.Text = text;
        H.Invoke("FindNext");
    }

    [Test]
    public void FindNext_selects_the_first_match()
    {
        Document("un chat noir");
        H.PlaceCaretAtStart(0);

        FindNext("chat");

        Assert.That(Selected, Is.EqualTo("chat"));
    }

    [Test]
    public void FindNext_is_case_insensitive()
    {
        Document("Un CHAT noir");
        H.PlaceCaretAtStart(0);

        FindNext("chat");

        Assert.That(Selected, Is.EqualTo("CHAT"));
    }

    [Test]
    public void Successive_FindNext_walk_through_the_matches_and_wrap_around()
    {
        Document("a b a", "a");
        H.PlaceCaretAtStart(0);
        FindNext("a");
        var first = H.Editor.Selection.Start.GetOffsetToPosition(H.Editor.Document.ContentStart);

        H.Invoke("FindNext");
        var second = H.Editor.Selection.Start.GetOffsetToPosition(H.Editor.Document.ContentStart);
        H.Invoke("FindNext");
        var third = H.Editor.Selection.Start.GetOffsetToPosition(H.Editor.Document.ContentStart);
        H.Invoke("FindNext");
        var wrapped = H.Editor.Selection.Start.GetOffsetToPosition(H.Editor.Document.ContentStart);

        Assert.Multiple(() =>
        {
            Assert.That(new[] { first, second, third }.Distinct().Count(), Is.EqualTo(3));
            Assert.That(wrapped, Is.EqualTo(first));
        });
    }

    [Test]
    public void FindNext_finds_text_in_a_later_paragraph()
    {
        Document("rien ici", "mais là : lune");
        H.PlaceCaretAtStart(0);

        FindNext("lune");

        Assert.That(Selected, Is.EqualTo("lune"));
    }

    [Test]
    public void FindNext_without_result_reports_it_and_keeps_the_selection()
    {
        Document("un chat");
        H.PlaceCaretAtStart(0);

        FindNext("chien");

        Assert.Multiple(() =>
        {
            Assert.That(H.StatusText.Text, Is.EqualTo("Aucun résultat."));
            Assert.That(H.Editor.Selection.IsEmpty, Is.True);
        });
    }

    [Test]
    public void FindNext_with_empty_text_does_nothing()
    {
        Document("un chat");
        H.PlaceCaretAtStart(0);

        FindNext("");

        Assert.That(H.Editor.Selection.IsEmpty, Is.True);
    }

    [Test]
    public void FindNext_matches_accented_text()
    {
        Document("Il était là");
        H.PlaceCaretAtStart(0);

        FindNext("été");

        Assert.That(Selected, Is.Empty.Or.EqualTo("été"));
        FindNext("était");
        Assert.That(Selected, Is.EqualTo("était"));
    }

    [Test]
    public void Replace_replaces_the_current_match_then_selects_the_next()
    {
        Document("a b a");
        H.PlaceCaretAtStart(0);
        FindNext("a");
        H.ReplaceBox.Text = "xx";

        H.Invoke("Replace_Click", null, new RoutedEventArgs());

        Assert.Multiple(() =>
        {
            Assert.That(Docs.Text(H.Paragraphs[0]), Is.EqualTo("xx b a"));
            Assert.That(Selected, Is.EqualTo("a"));
        });
    }

    [Test]
    public void Replace_without_a_current_match_only_finds_the_next()
    {
        Document("a b a");
        H.PlaceCaretAtStart(0);
        H.FindBox.Text = "b";
        H.ReplaceBox.Text = "zz";

        H.Invoke("Replace_Click", null, new RoutedEventArgs());

        Assert.Multiple(() =>
        {
            Assert.That(Docs.Text(H.Paragraphs[0]), Is.EqualTo("a b a"));
            Assert.That(Selected, Is.EqualTo("b"));
        });
    }

    [Test]
    public void ReplaceAll_replaces_every_occurrence_and_counts_them()
    {
        Document("chat chat", "un Chat");
        H.FindBox.Text = "chat";
        H.ReplaceBox.Text = "chien";

        H.Invoke("ReplaceAll_Click", null, new RoutedEventArgs());

        Assert.Multiple(() =>
        {
            Assert.That(H.Paragraphs.Select(Docs.Text), Is.EqualTo(new[] { "chien chien", "un chien" }));
            Assert.That(H.StatusText.Text, Is.EqualTo("3 remplacement(s)."));
        });
    }

    [Test]
    public void ReplaceAll_with_no_match_reports_zero()
    {
        Document("abc");
        H.FindBox.Text = "z";
        H.ReplaceBox.Text = "y";

        H.Invoke("ReplaceAll_Click", null, new RoutedEventArgs());

        Assert.That(H.StatusText.Text, Is.EqualTo("0 remplacement(s)."));
    }

    [Test]
    public void ReplaceAll_with_a_replacement_containing_the_search_text_terminates()
    {
        Document("a a a");
        H.FindBox.Text = "a";
        H.ReplaceBox.Text = "aa";

        H.Invoke("ReplaceAll_Click", null, new RoutedEventArgs());

        Assert.That(Docs.Text(H.Paragraphs[0]), Is.EqualTo("aa aa aa"));
    }

    [Test]
    public void ReplaceAll_with_an_empty_replacement_deletes_the_matches()
    {
        Document("a-b-c");
        H.FindBox.Text = "-";
        H.ReplaceBox.Text = "";

        H.Invoke("ReplaceAll_Click", null, new RoutedEventArgs());

        Assert.That(Docs.Text(H.Paragraphs[0]), Is.EqualTo("abc"));
    }

    [Test]
    public void ReplaceAll_keeps_the_paragraph_styles()
    {
        H.LoadParagraphs(Docs.P(StyleKind.Heading1, "chat"), Docs.P(StyleKind.Normal, "chat"));
        H.FindBox.Text = "chat";
        H.ReplaceBox.Text = "chien";

        H.Invoke("ReplaceAll_Click", null, new RoutedEventArgs());

        Assert.That(H.Kinds, Is.EqualTo(new[] { StyleKind.Heading1, StyleKind.Normal }));
    }

    [Test]
    public void ReplaceAll_marks_the_document_as_modified()
    {
        Document("chat");
        H.MarkClean();
        H.FindBox.Text = "chat";
        H.ReplaceBox.Text = "chien";

        H.Invoke("ReplaceAll_Click", null, new RoutedEventArgs());

        Assert.That(H.Dirty, Is.True);
    }

    [Test]
    public void The_find_bar_closes_on_request()
    {
        H.FindBar.Visibility = Visibility.Visible;

        H.Invoke("CloseFind_Click", null, new RoutedEventArgs());

        Assert.That(H.FindBar.Visibility, Is.EqualTo(Visibility.Collapsed));
    }

    [Test]
    public void The_find_command_shows_the_bar()
    {
        ApplicationCommands.Find.Execute(null, H.Window);

        Assert.That(H.FindBar.Visibility, Is.EqualTo(Visibility.Visible));
    }

    [Test]
    public void The_find_command_prefills_the_search_with_the_selection()
    {
        Document("un chat noir");
        H.Select(0, 3, 4);

        ApplicationCommands.Find.Execute(null, H.Window);

        Assert.That(H.FindBox.Text, Is.EqualTo("chat"));
    }
}

public class MainWindowViewTests : WindowTestBase
{
    [Test]
    public void Toggling_marks_shows_and_hides_the_overlay()
    {
        H.MarksButton.IsChecked = true;
        Assert.That(H.MarksOverlay.Visibility, Is.EqualTo(Visibility.Visible));

        H.MarksButton.IsChecked = false;
        Assert.That(H.MarksOverlay.Visibility, Is.EqualTo(Visibility.Collapsed));
    }

    [Test]
    public void The_toggle_marks_command_flips_the_button()
    {
        ViewCommands.ToggleMarks.Execute(null, H.Window);
        Assert.That(H.MarksButton.IsChecked, Is.True);
        ViewCommands.ToggleMarks.Execute(null, H.Window);
        Assert.That(H.MarksButton.IsChecked, Is.False);
    }

    [Test]
    public void Toggling_marks_does_not_modify_the_document()
    {
        H.MarksButton.IsChecked = true;

        Assert.That(H.Dirty, Is.False);
    }

    [Test]
    public void Bold_button_reflects_the_selection()
    {
        H.LoadParagraphs(Docs.P(StyleKind.Normal, new Run("gras") { FontWeight = FontWeights.Bold }, new Run(" maigre")));
        var bold = H.Element<System.Windows.Controls.Primitives.ToggleButton>("BoldButton");

        H.Select(0, 0, 4);
        Assert.That(bold.IsChecked, Is.True);

        H.Select(0, 5, 3);
        Assert.That(bold.IsChecked, Is.False);
    }

    [Test]
    public void Italic_and_underline_buttons_reflect_the_selection()
    {
        H.LoadParagraphs(Docs.P(StyleKind.Normal,
            new Run("it") { FontStyle = FontStyles.Italic },
            new Run("so") { TextDecorations = TextDecorations.Underline }));
        var italic = H.Element<System.Windows.Controls.Primitives.ToggleButton>("ItalicButton");
        var underline = H.Element<System.Windows.Controls.Primitives.ToggleButton>("UnderlineButton");

        var runs = Docs.Runs(H.Paragraphs[0]);
        H.Editor.Selection.Select(runs[0].ContentStart, runs[0].ContentEnd);
        Assert.Multiple(() =>
        {
            Assert.That(italic.IsChecked, Is.True);
            Assert.That(underline.IsChecked, Is.False);
        });

        H.Editor.Selection.Select(runs[1].ContentStart, runs[1].ContentEnd);
        Assert.Multiple(() =>
        {
            Assert.That(italic.IsChecked, Is.False);
            Assert.That(underline.IsChecked, Is.True);
        });
    }
}

public class MainWindowFileTests : WindowTestBase
{
    [Test]
    public void Saving_to_a_path_writes_the_file_clears_modified_and_names_the_window()
    {
        H.Editor.CaretPosition.InsertTextInRun("Bonjour");
        var path = PathOf("roman.docx");

        var saved = (bool)H.Invoke("TrySave", path)!;

        Assert.Multiple(() =>
        {
            Assert.That(saved, Is.True);
            Assert.That(File.Exists(path), Is.True);
            Assert.That(H.Dirty, Is.False);
            Assert.That(H.Window.Title, Is.EqualTo("roman.docx — Mots"));
        });
    }

    [Test]
    public void Saved_file_can_be_reopened_with_the_same_content()
    {
        H.LoadParagraphs(Docs.P(StyleKind.Heading1, "Un"), Docs.P(StyleKind.Normal, "Deux"));
        var path = PathOf("roman.docx");

        H.Invoke("TrySave", path);
        var loaded = DocxConverter.Load(path);

        Assert.Multiple(() =>
        {
            Assert.That(Docs.Kinds(loaded), Is.EqualTo(new[] { StyleKind.Heading1, StyleKind.Normal }));
            Assert.That(Docs.Paragraphs(loaded).Select(Docs.Text), Is.EqualTo(new[] { "Un", "Deux" }));
        });
    }

    [Test]
    public void Typing_after_saving_marks_the_window_modified_again()
    {
        H.Invoke("TrySave", PathOf("roman.docx"));

        H.Editor.CaretPosition.InsertTextInRun("x");

        Assert.That(H.Window.Title, Is.EqualTo("*roman.docx — Mots"));
    }

    [Test]
    public void Save_uses_the_current_path_without_asking()
    {
        var path = PathOf("roman.docx");
        H.Invoke("TrySave", path);
        H.Editor.CaretPosition.InsertTextInRun("x");

        var saved = (bool)H.Invoke("Save")!;

        Assert.Multiple(() =>
        {
            Assert.That(saved, Is.True);
            Assert.That(H.Dirty, Is.False);
            Assert.That(Docs.Text(Docs.Paragraphs(DocxConverter.Load(path)).Single()), Does.Contain("x"));
        });
    }

    [Test]
    public void New_discards_a_clean_document_without_asking()
    {
        H.LoadParagraphs(Docs.P(StyleKind.Heading1, "T"));
        H.MarkClean();
        H.SetField("_currentPath", PathOf("roman.docx"));

        ApplicationCommands.New.Execute(null, H.Window);

        Assert.Multiple(() =>
        {
            Assert.That(H.Kinds, Is.EqualTo(new[] { StyleKind.Normal }));
            Assert.That(H.Window.Title, Is.EqualTo("Sans titre — Mots"));
            Assert.That(H.Dirty, Is.False);
        });
    }

    [Test]
    public void An_imported_document_proposes_its_name_until_saved()
    {
        H.SetField("_importedName", "Anya (Mots)");
        H.Invoke("UpdateTitle");

        Assert.That(H.Window.Title, Is.EqualTo("Anya (Mots) — Mots"));
    }

    [Test]
    public void The_saved_name_replaces_the_imported_name()
    {
        H.SetField("_importedName", "Anya (Mots)");

        H.Invoke("TrySave", PathOf("Anya (Mots).docx"));

        Assert.That(H.Window.Title, Is.EqualTo("Anya (Mots).docx — Mots"));
    }
}

public class MainWindowOutlineTests : WindowTestBase
{
    private void Book() => H.LoadParagraphs(
        Docs.P(StyleKind.Normal, "préface"),
        Docs.P(StyleKind.Heading1, "Un"),
        Docs.P(StyleKind.Normal, "texte"),
        Docs.P(StyleKind.Heading1, "Deux"),
        Docs.P(StyleKind.Ellipse, "***"),
        Docs.P(StyleKind.Heading1, "Trois"));

    private List<string> Titles => H.OutlineList.Items.Cast<OutlineItem>().Select(i => i.Title).ToList();

    [Test]
    public void Outline_lists_the_heading_1_paragraphs_in_order()
    {
        Book();

        Assert.That(Titles, Is.EqualTo(new[] { "Un", "Deux", "Trois" }));
    }

    [Test]
    public void Outline_hides_its_hint_once_there_are_chapters()
    {
        Book();

        Assert.That(H.OutlineEmptyText.Visibility, Is.EqualTo(Visibility.Collapsed));
    }

    [Test]
    public void Outline_shows_its_hint_again_when_chapters_disappear()
    {
        Book();

        H.LoadParagraphs(Docs.P(StyleKind.Normal, "rien"));

        Assert.Multiple(() =>
        {
            Assert.That(Titles, Is.Empty);
            Assert.That(H.OutlineEmptyText.Visibility, Is.EqualTo(Visibility.Visible));
        });
    }

    [Test]
    public void An_empty_heading_is_titled_sans_titre()
    {
        H.LoadParagraphs(Docs.P(StyleKind.Heading1));

        Assert.That(Titles, Is.EqualTo(new[] { "(sans titre)" }));
    }

    [Test]
    public void A_whitespace_only_heading_is_titled_sans_titre()
    {
        H.LoadParagraphs(Docs.P(StyleKind.Heading1, "   "));

        Assert.That(Titles, Is.EqualTo(new[] { "(sans titre)" }));
    }

    [Test]
    public void Line_breaks_in_a_heading_become_spaces_in_the_title()
    {
        H.LoadParagraphs(Docs.P(StyleKind.Heading1, new Run("Un"), new LineBreak(), new Run("titre")));

        Assert.That(Titles, Is.EqualTo(new[] { "Un titre" }));
    }

    [Test]
    public void Outline_items_point_at_their_paragraphs()
    {
        Book();

        var items = H.OutlineList.Items.Cast<OutlineItem>().ToList();
        Assert.That(items.Select(i => i.Paragraph), Is.EqualTo(new[] { H.Paragraphs[1], H.Paragraphs[3], H.Paragraphs[5] }));
    }

    [Test]
    public void Changing_a_style_updates_the_outline_after_the_refresh_delay()
    {
        Book();
        H.PlaceCaretAtEnd(2);

        H.Invoke("ApplyStyle", StyleKind.Heading1);
        H.Invoke("RefreshOutline"); // le minuteur de 250 ms est court-circuité

        Assert.That(Titles, Is.EqualTo(new[] { "Un", "texte", "Deux", "Trois" }));
    }

    [Test]
    public void The_caret_selects_its_chapter_in_the_outline()
    {
        Book();
        var items = H.OutlineList.Items.Cast<OutlineItem>().ToList();

        H.PlaceCaretAtEnd(2); // « texte », sous « Un »
        Assert.That(H.OutlineList.SelectedItem, Is.SameAs(items[0]));

        H.PlaceCaretAtEnd(4); // « *** », sous « Deux »
        Assert.That(H.OutlineList.SelectedItem, Is.SameAs(items[1]));

        H.PlaceCaretAtEnd(5);
        Assert.That(H.OutlineList.SelectedItem, Is.SameAs(items[2]));
    }

    [Test]
    public void Before_the_first_chapter_nothing_is_selected()
    {
        Book();

        H.PlaceCaretAtEnd(0);

        Assert.That(H.OutlineList.SelectedItem, Is.Null);
    }

    [Test]
    public void Replacing_the_document_drops_the_old_items()
    {
        Book();

        H.LoadParagraphs(Docs.P(StyleKind.Heading1, "Nouveau"));

        var items = H.OutlineList.Items.Cast<OutlineItem>().ToList();
        Assert.Multiple(() =>
        {
            Assert.That(items, Has.Count.EqualTo(1));
            Assert.That(items[0].Paragraph, Is.SameAs(H.Paragraphs[0]));
        });
    }
}
