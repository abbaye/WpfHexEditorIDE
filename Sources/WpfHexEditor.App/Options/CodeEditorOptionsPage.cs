// ==========================================================
// Project: WpfHexEditor.App
// File: Options/CodeEditorOptionsPage.cs
// Description:
//     Options page for Code Editor settings including minimap configuration.
//     Category: Editor › Code Editor
// ==========================================================

using System;
using System.Windows;
using System.Windows.Controls;
using WpfHexEditor.Core.Options;

namespace WpfHexEditor.App.Options;

/// <summary>
/// IDE options page — Editor › Code Editor.
/// Sections: General (zoom, word wrap, scroll), Minimap, Brackets, Hints.
/// </summary>
public sealed class CodeEditorOptionsPage : UserControl, IOptionsPage
{
    public event EventHandler? Changed;

    private bool _loading;
    private readonly CheckBox _wordWrapCheck;
    private readonly CheckBox _foldDoubleClickCheck;
    private readonly CheckBox _autoCloseBracketsCheck;
    private readonly CheckBox _autoCloseQuotesCheck;
    private readonly CheckBox _skipClosingCharCheck;
    private readonly CheckBox _wrapSelectionCheck;
    private readonly CheckBox _showMinimapCheck;
    private readonly CheckBox _minimapRenderCharsCheck;
    private readonly ComboBox _minimapSideCombo;
    private readonly ComboBox _minimapVerticalSizeCombo;
    private readonly CheckBox _showInlineHintsCheck;
    private readonly CheckBox _showEndBlockHintCheck;
    private readonly CheckBox _stickyScrollCheck;
    private readonly CheckBox _showRefreshRateCheck;
    private readonly CheckBox _bracketPairColorizationCheck;
    private readonly CheckBox _rainbowScopeGuidesCheck;
    private readonly CheckBox _colorSwatchPreviewCheck;
    private readonly CheckBox _formatOnSaveCheck;
    private readonly CheckBox _showColumnRulersCheck;
    private readonly CheckBox _wordHighlightCheck;

    public CodeEditorOptionsPage()
    {
        var root = new StackPanel { Margin = new Thickness(12) };

        // ── General ──────────────────────────────────────────────────────────
        root.Children.Add(MakeHeader("General"));

        _wordWrapCheck = MakeCheck("Word wrap");
        root.Children.Add(_wordWrapCheck);

        _foldDoubleClickCheck = MakeCheck("Fold/unfold on double-click");
        root.Children.Add(_foldDoubleClickCheck);

        _showColumnRulersCheck = MakeCheck("Show column rulers");
        root.Children.Add(_showColumnRulersCheck);

        _showRefreshRateCheck = MakeCheck("Show refresh rate in status bar");
        root.Children.Add(_showRefreshRateCheck);

        root.Children.Add(new Separator { Margin = new Thickness(0, 8, 0, 8) });

        // ── Auto-close ──────────────────────────────────────────────────────
        root.Children.Add(MakeHeader("Auto-close & Pairs"));

        _autoCloseBracketsCheck = MakeCheck("Auto-close brackets ( { [ ");
        root.Children.Add(_autoCloseBracketsCheck);

        _autoCloseQuotesCheck = MakeCheck("Auto-close quotes \" ' `");
        root.Children.Add(_autoCloseQuotesCheck);

        _skipClosingCharCheck = MakeCheck("Skip over closing characters");
        root.Children.Add(_skipClosingCharCheck);

        _wrapSelectionCheck = MakeCheck("Wrap selection in pairs");
        root.Children.Add(_wrapSelectionCheck);

        root.Children.Add(new Separator { Margin = new Thickness(0, 8, 0, 8) });

        // ── Hints ────────────────────────────────────────────────────────────
        root.Children.Add(MakeHeader("Hints & Overlays"));

        _showInlineHintsCheck = MakeCheck("Show inline hints (references, parameters)");
        root.Children.Add(_showInlineHintsCheck);

        _wordHighlightCheck = MakeCheck("Highlight all occurrences of word under caret");
        root.Children.Add(_wordHighlightCheck);

        _showEndBlockHintCheck = MakeCheck("Show end-of-block hover hint");
        root.Children.Add(_showEndBlockHintCheck);

        _stickyScrollCheck = MakeCheck("Sticky scroll (context header)");
        root.Children.Add(_stickyScrollCheck);

        root.Children.Add(new Separator { Margin = new Thickness(0, 8, 0, 8) });

        // ── Minimap ──────────────────────────────────────────────────────────
        root.Children.Add(MakeHeader("Minimap"));

        _showMinimapCheck = MakeCheck("Show minimap");
        root.Children.Add(_showMinimapCheck);

        _minimapRenderCharsCheck = MakeCheck("Render characters (per-token coloring)");
        root.Children.Add(_minimapRenderCharsCheck);

        var sidePanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 0) };
        sidePanel.Children.Add(new TextBlock { Text = "Side:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) });
        _minimapSideCombo = new ComboBox { Width = 80 };
        _minimapSideCombo.Items.Add("Right");
        _minimapSideCombo.Items.Add("Left");
        _minimapSideCombo.SelectionChanged += (_, _) => OnChanged();
        sidePanel.Children.Add(_minimapSideCombo);
        root.Children.Add(sidePanel);

        var vsPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 0) };
        vsPanel.Children.Add(new TextBlock { Text = "Vertical size:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) });
        _minimapVerticalSizeCombo = new ComboBox { Width = 120 };
        _minimapVerticalSizeCombo.Items.Add("Proportional");
        _minimapVerticalSizeCombo.Items.Add("Fill");
        _minimapVerticalSizeCombo.Items.Add("Fit");
        _minimapVerticalSizeCombo.SelectionChanged += (_, _) => OnChanged();
        vsPanel.Children.Add(_minimapVerticalSizeCombo);
        root.Children.Add(vsPanel);

        root.Children.Add(new Separator { Margin = new Thickness(0, 8, 0, 8) });

        // ── Coloring ─────────────────────────────────────────────────────────
        root.Children.Add(MakeHeader("Coloring"));

        _bracketPairColorizationCheck = MakeCheck("Bracket pair colorization (CE_Bracket_1/2/3/4 by depth)");
        root.Children.Add(_bracketPairColorizationCheck);

        _rainbowScopeGuidesCheck = MakeCheck("Rainbow scope guides (color folding lines by bracket depth)");
        root.Children.Add(_rainbowScopeGuidesCheck);

        _colorSwatchPreviewCheck = MakeCheck("Color swatch preview (e.g. #FF5733 in CSS/XAML/C#)");
        root.Children.Add(_colorSwatchPreviewCheck);

        _formatOnSaveCheck = MakeCheck("Format on Save (Ctrl+S)");
        root.Children.Add(_formatOnSaveCheck);

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
            var s = settings.CodeEditorDefaults;
            _wordWrapCheck.IsChecked         = s.WordWrap;
            _showColumnRulersCheck.IsChecked = s.ShowColumnRulers;
            _foldDoubleClickCheck.IsChecked  = s.FoldToggleOnDoubleClick;
            _showRefreshRateCheck.IsChecked   = s.ShowRefreshRateInStatusBar;
            _autoCloseBracketsCheck.IsChecked = s.AutoClosingBrackets;
            _autoCloseQuotesCheck.IsChecked   = s.AutoClosingQuotes;
            _skipClosingCharCheck.IsChecked   = s.SkipOverClosingChar;
            _wrapSelectionCheck.IsChecked     = s.WrapSelectionInPairs;
            _showInlineHintsCheck.IsChecked   = s.ShowInlineHints;
            _wordHighlightCheck.IsChecked     = s.EnableWordHighlight;
            _showEndBlockHintCheck.IsChecked  = s.EndOfBlockHintEnabled;
            _stickyScrollCheck.IsChecked      = s.StickyScroll.Enabled;
            _showMinimapCheck.IsChecked       = s.ShowMinimap;
            _minimapRenderCharsCheck.IsChecked = s.MinimapRenderCharacters;
            _minimapSideCombo.SelectedIndex   = s.MinimapSide;
            _minimapVerticalSizeCombo.SelectedIndex = s.MinimapVerticalSize;
            _bracketPairColorizationCheck.IsChecked = s.BracketPairColorization;
            _rainbowScopeGuidesCheck.IsChecked     = s.RainbowScopeGuides;
            _colorSwatchPreviewCheck.IsChecked      = s.ColorSwatchPreview;
            _formatOnSaveCheck.IsChecked            = s.FormatOnSave;
        }
        finally { _loading = false; }
    }

    public void Flush(AppSettings settings)
    {
        var s = settings.CodeEditorDefaults;
        s.WordWrap                    = _wordWrapCheck.IsChecked == true;
        s.ShowColumnRulers            = _showColumnRulersCheck.IsChecked == true;
        s.FoldToggleOnDoubleClick     = _foldDoubleClickCheck.IsChecked == true;
        s.ShowRefreshRateInStatusBar  = _showRefreshRateCheck.IsChecked == true;
        s.AutoClosingBrackets         = _autoCloseBracketsCheck.IsChecked == true;
        s.AutoClosingQuotes           = _autoCloseQuotesCheck.IsChecked == true;
        s.SkipOverClosingChar         = _skipClosingCharCheck.IsChecked == true;
        s.WrapSelectionInPairs        = _wrapSelectionCheck.IsChecked == true;
        s.ShowInlineHints             = _showInlineHintsCheck.IsChecked == true;
        s.EnableWordHighlight         = _wordHighlightCheck.IsChecked == true;
        s.EndOfBlockHintEnabled       = _showEndBlockHintCheck.IsChecked == true;
        s.StickyScroll.Enabled        = _stickyScrollCheck.IsChecked == true;
        s.ShowMinimap                 = _showMinimapCheck.IsChecked == true;
        s.MinimapRenderCharacters     = _minimapRenderCharsCheck.IsChecked == true;
        s.MinimapSide                 = _minimapSideCombo.SelectedIndex;
        s.MinimapVerticalSize         = _minimapVerticalSizeCombo.SelectedIndex;
        s.BracketPairColorization     = _bracketPairColorizationCheck.IsChecked == true;
        s.RainbowScopeGuides          = _rainbowScopeGuidesCheck.IsChecked == true;
        s.ColorSwatchPreview          = _colorSwatchPreviewCheck.IsChecked == true;
        s.FormatOnSave                = _formatOnSaveCheck.IsChecked == true;
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

    private static TextBlock MakeHeader(string text) => OptionsPageHelper.SectionHeader(text);
}
