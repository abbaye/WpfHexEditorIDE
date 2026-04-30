// ==========================================================
// Project: WpfHexEditor.Core.Options
// File: Pages/MarkdownEditorOptionsPage.cs
// Description:
//     Options page for Markdown Editor settings.
//     Category: Text Editor › Markdown
// Architecture notes:
//     Code-only UserControl (no XAML) following the pattern established by
//     CodeEditorFormattingPage and StructureEditorOptionsPage.
// ==========================================================

using System;
using System.Windows;
using System.Windows.Controls;
using WpfHexEditor.Core.Options.Properties;

namespace WpfHexEditor.Core.Options.Pages;

/// <summary>
/// IDE options page — Text Editor › Markdown.
/// Sections: Preview, Editing, Display.
/// </summary>
public sealed class MarkdownEditorOptionsPage : UserControl, IOptionsPage
{
    public event EventHandler? Changed;

    private bool _loading;

    private readonly CheckBox _syncScrollCheck;
    private readonly CheckBox _enableAutoPairCheck;
    private readonly CheckBox _enableListContinuationCheck;
    private readonly CheckBox _showYamlFrontmatterCheck;
    private readonly ComboBox _defaultLayoutCombo;

    public MarkdownEditorOptionsPage()
    {
        var root = new StackPanel { Margin = new Thickness(12) };

        // ── Preview ──────────────────────────────────────────────────────────
        root.Children.Add(OptionsPageHelper.SectionHeader("Preview"));

        _syncScrollCheck = MakeCheck("Synchronize scroll between editor and preview");
        root.Children.Add(_syncScrollCheck);

        var layoutPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };
        layoutPanel.Children.Add(new TextBlock { Text = "Default layout:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) });
        _defaultLayoutCombo = new ComboBox { Width = 140 };
        _defaultLayoutCombo.Items.Add(OptionsResources.Opt_MdLayout_PreviewRight);
        _defaultLayoutCombo.Items.Add(OptionsResources.Opt_MdLayout_PreviewBottom);
        _defaultLayoutCombo.Items.Add(OptionsResources.Opt_MdLayout_EditorOnly);
        _defaultLayoutCombo.Items.Add(OptionsResources.Opt_MdLayout_PreviewOnly);
        _defaultLayoutCombo.SelectionChanged += (_, _) => OnChanged();
        layoutPanel.Children.Add(_defaultLayoutCombo);
        root.Children.Add(layoutPanel);

        root.Children.Add(new Separator { Margin = new Thickness(0, 8, 0, 8) });

        // ── Editing ──────────────────────────────────────────────────────────
        root.Children.Add(OptionsPageHelper.SectionHeader("Editing"));

        _enableAutoPairCheck = MakeCheck("Auto-pair Markdown characters (* ` [ ()");
        root.Children.Add(_enableAutoPairCheck);

        _enableListContinuationCheck = MakeCheck("Continue lists on Enter (auto-insert list marker)");
        root.Children.Add(_enableListContinuationCheck);

        root.Children.Add(new Separator { Margin = new Thickness(0, 8, 0, 8) });

        // ── Display ──────────────────────────────────────────────────────────
        root.Children.Add(OptionsPageHelper.SectionHeader("Display"));

        _showYamlFrontmatterCheck = MakeCheck("Render YAML front-matter block in preview");
        root.Children.Add(_showYamlFrontmatterCheck);

        Content = new ScrollViewer
        {
            Content = root,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        };
    }

    // ── IOptionsPage ─────────────────────────────────────────────────────────

    public void Load(AppSettings settings)
    {
        _loading = true;
        try
        {
            var s = settings.MarkdownEditorDefaults;
            _syncScrollCheck.IsChecked             = s.SyncScroll;
            _enableAutoPairCheck.IsChecked         = s.EnableAutoPair;
            _enableListContinuationCheck.IsChecked = s.EnableListContinuation;
            _showYamlFrontmatterCheck.IsChecked    = s.ShowYamlFrontmatter;
            _defaultLayoutCombo.SelectedItem       = s.DefaultLayout;
            if (_defaultLayoutCombo.SelectedIndex < 0)
                _defaultLayoutCombo.SelectedIndex = 0;
        }
        finally { _loading = false; }
    }

    public void Flush(AppSettings settings)
    {
        var s = settings.MarkdownEditorDefaults;
        s.SyncScroll             = _syncScrollCheck.IsChecked == true;
        s.EnableAutoPair         = _enableAutoPairCheck.IsChecked == true;
        s.EnableListContinuation = _enableListContinuationCheck.IsChecked == true;
        s.ShowYamlFrontmatter    = _showYamlFrontmatterCheck.IsChecked == true;
        s.DefaultLayout          = _defaultLayoutCombo.SelectedItem as string ?? "PreviewRight";
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private void OnChanged()
    {
        if (!_loading) Changed?.Invoke(this, EventArgs.Empty);
    }

    private CheckBox MakeCheck(string label)
    {
        var cb = new CheckBox { Content = label, Margin = new Thickness(0, 3, 0, 0) };
        cb.Checked   += (_, _) => OnChanged();
        cb.Unchecked += (_, _) => OnChanged();
        return cb;
    }
}
