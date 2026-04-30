// ==========================================================
// Project: WpfHexEditor.Core.Options
// File: Pages/CodeEditorFormattingPage.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-04-02
// Updated: 2026-04-04
// Description:
//     Dedicated options page for Code Editor formatting overrides.
//     Three-state checkboxes: checked = force on, unchecked = force off,
//     indeterminate = inherit the language .whfmt default.
//     Two-column layout: left = options, right = FormattingPreviewPanel.
//     Checkbox MouseOver triggers FormattingRuleTooltip (whfmt-driven Before/After).
//
// Architecture Notes:
//     Code-only UserControl implementing IOptionsPage.
//     Reads/writes AppSettings.CodeEditorDefaults formatting override properties.
//     Registered in OptionsPageRegistry under "Code Editor" / "Formatting".
//     IPreviewColorizer injected from App layer via constructor (null = no preview).
// ==========================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Markup;
using WpfHexEditor.Core.Options.Preview;
using WpfHexEditor.Core.Options.Properties;
using WpfHexEditor.Core.ProjectSystem.Languages;

namespace WpfHexEditor.Core.Options.Pages;

/// <summary>
/// IDE options page — Code Editor › Formatting.
/// </summary>
public sealed class CodeEditorFormattingPage : UserControl, IOptionsPage
{
    public event EventHandler? Changed;

    // ── Checkboxes ────────────────────────────────────────────────────────
    private readonly CheckBox _formatOnSave;
    private readonly CheckBox _trimTrailing;
    private readonly CheckBox _insertFinalNewline;
    private readonly CheckBox _spaceAfterKeywords;
    private readonly CheckBox _spaceAroundOperators;
    private readonly CheckBox _spaceAfterComma;
    private readonly CheckBox _indentCaseLabels;
    private readonly CheckBox _organizeImports;

    // Smart editing
    private readonly CheckBox _autoBrackets;
    private readonly CheckBox _autoQuotes;
    private readonly CheckBox _skipOverClose;
    private readonly CheckBox _wrapSelection;

    // XML / XAML (global — not per-language overrides)
    private readonly ComboBox _xmlAttrIndentLevels;
    private readonly CheckBox _xmlOneAttrPerLine;

    // Section panels (layout only — opacity updated per supportedRules).
    private readonly StackPanel _codeSectionsPanel;  // SPACING + STRUCTURE
    private readonly StackPanel _xmlSectionPanel;    // XML / XAML

    // ruleId → control — used by UpdateSectionAvailability to enable/disable per whfmt.
    private readonly Dictionary<string, Control> _ruleControls = new();

    // ── Preview ───────────────────────────────────────────────────────────
    private readonly FormattingPreviewPanel? _preview;

    // ruleId â†’ tooltip (lazy-initialised on first MouseEnter)
    private readonly Dictionary<string, FormattingRuleTooltip> _tooltips = new();

    // Map: CheckBox â†’ (ruleId, display label) for tooltip wiring
    private readonly List<(CheckBox Box, string RuleId, string Label)> _ruleMap = [];

    private readonly IPreviewColorizer? _colorizer;
    private readonly ComboBox           _languageCombo;
    private readonly Button             _resetButton;
    private readonly Button             _resetAllButton;
    private bool         _loading;
    private AppSettings? _settings;
    private string?      _currentLangId;

    // ── Construction ─────────────────────────────────────────────────────

    /// <param name="colorizer">
    /// Optional colorizer for the live preview and tooltips.
    /// When null the right column shows a placeholder message.
    /// </param>
    /// <param name="formatter">
    /// Optional formatter applied to the snippet before colorising.
    /// When null the raw snippet is displayed as-is.
    /// </param>
    public CodeEditorFormattingPage(IPreviewColorizer? colorizer = null,
                                    IPreviewFormatter? formatter = null)
    {
        _colorizer = colorizer;

        // -- Language / Reset bar (spans full width) ----------------------------------
        var langBar = new Border
        {
            Padding         = new Thickness(16, 7, 16, 7),
            BorderThickness = new Thickness(0, 0, 0, 1),
        };
        langBar.SetResourceReference(Border.BackgroundProperty,  "DockPanelBackgroundBrush");
        langBar.SetResourceReference(Border.BorderBrushProperty, "DockBorderBrush");

        var langRow = new StackPanel { Orientation = Orientation.Horizontal };

        var langLabel = new TextBlock
        {
            Text              = "Language",
            FontSize          = 11,
            FontWeight        = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
            Margin            = new Thickness(0, 0, 8, 0),
        };
        langLabel.SetResourceReference(TextBlock.ForegroundProperty, "DockMenuForegroundBrush");
        langRow.Children.Add(langLabel);

        _languageCombo = new ComboBox
        {
            FontSize                 = 11,
            MinWidth                 = 200,
            VerticalContentAlignment = VerticalAlignment.Center,
            Margin                   = new Thickness(0, 0, 8, 0),
        };
        foreach (var l in LanguageRegistry.Instance.AllLanguages().OrderBy(l => l.Name))
            _languageCombo.Items.Add(new ComboBoxItem { Content = l.Name, Tag = l });
        if (_languageCombo.Items.Count > 0)
            _languageCombo.SelectedIndex = 0;
        _languageCombo.SelectionChanged += OnLanguageComboChanged;
        langRow.Children.Add(_languageCombo);

        _resetButton = new Button
        {
            Content           = "Reset overrides",
            FontSize          = 11,
            Padding           = new Thickness(10, 3, 10, 3),
            VerticalAlignment = VerticalAlignment.Center,
            Style             = ThemedButtonStyle,
        };
        _resetButton.Click += OnResetClicked;
        langRow.Children.Add(_resetButton);

        _resetAllButton = new Button
        {
            Content           = "Reset all overrides",
            FontSize          = 11,
            Padding           = new Thickness(10, 3, 10, 3),
            VerticalAlignment = VerticalAlignment.Center,
            Margin            = new Thickness(6, 0, 0, 0),
            Style             = ThemedButtonStyle,
        };
        _resetAllButton.Click += OnResetAllClicked;
        langRow.Children.Add(_resetAllButton);

        langBar.Child = langRow;

        // -- Content: two-column Grid -------------------------------------------------
        var content = new Grid();
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 280 });
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });   // gutter
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 260 });

        // -- Left: options scroll -----------------------------------------------------
        var leftScroll = new ScrollViewer
        {
            VerticalScrollBarVisibility   = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Padding = new Thickness(16)
        };
        Grid.SetColumn(leftScroll, 0);

        var stack = new StackPanel { Orientation = Orientation.Vertical };

        // Page header
        var header = new TextBlock
        {
            Text       = "Code Editor \u2014 Formatting",
            FontSize   = 16,
            FontWeight = FontWeights.SemiBold,
            Margin     = new Thickness(0, 0, 0, 6)
        };
        stack.Children.Add(header);

        // Subtitle
        var subtitle = new TextBlock
        {
            Text         = "These settings override the language .whfmt defaults.\nLeave checkboxes at their indeterminate state (â€”) to inherit the language rules.",
            TextWrapping = TextWrapping.Wrap,
            FontSize     = 11,
            Opacity      = 0.65,
            Margin       = new Thickness(0, 0, 0, 16)
        };
        stack.Children.Add(subtitle);

        // ── General ───────────────────────────────────────────────────────
        stack.Children.Add(MakeSectionHeader("GENERAL"));

        _formatOnSave = MakeCheckBox("Format on Save (Ctrl+S)", false, "formatOnSave", "Format on Save");
        stack.Children.Add(_formatOnSave);

        // ── Whitespace ────────────────────────────────────────────────────
        stack.Children.Add(MakeSectionHeader("WHITESPACE"));

        _trimTrailing = MakeThreeStateCheckBox("Trim trailing whitespace",
            "Remove trailing spaces and tabs on each line",
            "trimTrailingWhitespace", "Trim trailing whitespace");

        _insertFinalNewline = MakeThreeStateCheckBox("Insert final newline",
            "Ensure the file ends with exactly one newline",
            "insertFinalNewline", "Insert final newline");

        stack.Children.Add(_trimTrailing);
        stack.Children.Add(_insertFinalNewline);

        // ── Code-specific sections (SPACING + STRUCTURE) ─────────────────
        // Wrapped in a panel so IsEnabled=false grays them out for XML languages.
        _codeSectionsPanel = new StackPanel();

        _codeSectionsPanel.Children.Add(MakeSectionHeader("SPACING"));

        _spaceAfterKeywords = MakeThreeStateCheckBox(
            "Space after keywords (if, for, while, switch, catch)",
            "Insert a space between control keywords and opening paren: if (...) vs if(...)",
            "spaceAfterKeywords", "Space after keywords");

        _spaceAroundOperators = MakeThreeStateCheckBox(
            "Space around binary operators (+, -, *, /, ==, !=, &&, ||)",
            "Insert spaces around binary operators: a + b vs a+b",
            "spaceAroundBinaryOperators", "Space around binary operators");

        _spaceAfterComma = MakeThreeStateCheckBox("Space after commas",
            "Insert a space after commas: (a, b, c) vs (a,b,c)",
            "spaceAfterComma", "Space after commas");

        _codeSectionsPanel.Children.Add(_spaceAfterKeywords);
        _codeSectionsPanel.Children.Add(_spaceAroundOperators);
        _codeSectionsPanel.Children.Add(_spaceAfterComma);

        _codeSectionsPanel.Children.Add(MakeSectionHeader("STRUCTURE"));

        _indentCaseLabels = MakeThreeStateCheckBox("Indent case/when labels inside switch",
            "Add one indent level to case and default labels",
            "indentCaseLabels", "Indent case labels");

        _organizeImports = MakeThreeStateCheckBox("Organize imports on format",
            "Sort using/import directives alphabetically when formatting",
            "organizeImports", "Organize imports");

        _codeSectionsPanel.Children.Add(_indentCaseLabels);
        _codeSectionsPanel.Children.Add(_organizeImports);
        stack.Children.Add(_codeSectionsPanel);

        // ── Smart Editing ─────────────────────────────────────────────────
        stack.Children.Add(MakeSectionHeader("SMART EDITING"));

        _autoBrackets  = MakeCheckBox("Auto-close brackets, braces and parentheses", false, null, null);
        _autoQuotes    = MakeCheckBox("Auto-close quotes", false, null, null);
        _skipOverClose = MakeCheckBox("Skip over closing character (avoids duplicate closing bracket/quote)", false, null, null);
        _wrapSelection = MakeCheckBox("Wrap selection in pairs (type opening bracket/quote over a selection)", false, null, null);

        stack.Children.Add(_autoBrackets);
        stack.Children.Add(_autoQuotes);
        stack.Children.Add(_skipOverClose);
        stack.Children.Add(_wrapSelection);

        // ── XML / XAML (tag-based languages only) ─────────────────────────
        // Wrapped in a panel so IsEnabled=false grays it out for non-XML languages.
        _xmlSectionPanel = new StackPanel();
        _xmlSectionPanel.Children.Add(MakeSectionHeader("XML / XAML"));

        var attrLevelRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin      = new Thickness(0, 4, 0, 4),
        };
        attrLevelRow.Children.Add(new TextBlock
        {
            Text              = "Attribute continuation indent:",
            VerticalAlignment = VerticalAlignment.Center,
            Margin            = new Thickness(0, 0, 8, 0),
            Width             = 220,
        });
        _xmlAttrIndentLevels = new ComboBox { Width = 230 };
        _xmlAttrIndentLevels.Items.Add(OptionsResources.Opt_IndentLevel_1);
        _xmlAttrIndentLevels.Items.Add(OptionsResources.Opt_IndentLevel_2);
        _xmlAttrIndentLevels.Items.Add(OptionsResources.Opt_IndentLevel_3);
        _xmlAttrIndentLevels.SelectedIndex = 1;
        _xmlAttrIndentLevels.SelectionChanged += (_, _) => { if (!_loading) { Changed?.Invoke(this, EventArgs.Empty); _preview?.Refresh(BuildOverrides()); } };
        if (_colorizer is not null)
            WireComboTooltip(_xmlAttrIndentLevels, "xmlAttributeIndentLevels", "Attribute continuation indent");
        attrLevelRow.Children.Add(_xmlAttrIndentLevels);
        _xmlSectionPanel.Children.Add(attrLevelRow);

        _xmlOneAttrPerLine = MakeCheckBox(
            "Each XML/XAML attribute on its own line  (first attribute stays on tag line)",
            false, "xmlOneAttributePerLine", "One attribute per line");
        _xmlSectionPanel.Children.Add(_xmlOneAttrPerLine);
        stack.Children.Add(_xmlSectionPanel);

        // ── ruleId → control mapping (used by UpdateSectionAvailability) ────
        _ruleControls["trimTrailingWhitespace"]     = _trimTrailing;
        _ruleControls["insertFinalNewline"]         = _insertFinalNewline;
        _ruleControls["spaceAfterKeywords"]         = _spaceAfterKeywords;
        _ruleControls["spaceAroundBinaryOperators"] = _spaceAroundOperators;
        _ruleControls["spaceAfterComma"]            = _spaceAfterComma;
        _ruleControls["indentCaseLabels"]           = _indentCaseLabels;
        _ruleControls["organizeImports"]            = _organizeImports;
        _ruleControls["xmlAttributeIndentLevels"]   = _xmlAttrIndentLevels;
        _ruleControls["xmlOneAttributePerLine"]     = _xmlOneAttrPerLine;

        leftScroll.Content = stack;
        content.Children.Add(leftScroll);

        // -- Right: preview panel ------------------------------------------------------
        if (_colorizer is not null)
        {
            _preview = new FormattingPreviewPanel();
            Grid.SetColumn(_preview, 2);
            content.Children.Add(_preview);
            _preview.Initialize(_colorizer, formatter);
        }
        else
        {
            // Placeholder when no colorizer is available
            var placeholder = new TextBlock
            {
                Text                = "Preview not available\n(colorizer not injected)",
                TextWrapping        = TextWrapping.Wrap,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment   = VerticalAlignment.Center,
                FontStyle           = FontStyles.Italic,
                Opacity             = 0.45,
                FontSize            = 11,
                Margin              = new Thickness(16),
            };
            placeholder.SetResourceReference(TextBlock.ForegroundProperty, "DockMenuForegroundBrush");
            Grid.SetColumn(placeholder, 2);
            content.Children.Add(placeholder);
        }

        // -- Outer DockPanel ----------------------------------------------------------
        var outer = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(langBar, Dock.Top);
        outer.Children.Add(langBar);
        outer.Children.Add(content);
        Content = outer;
    }

    // ── IOptionsPage ─────────────────────────────────────────────────────

    public void Load(AppSettings s)
    {
        _settings = s;
        _loading = true;
        try
        {
            var ce = s.CodeEditorDefaults;
            _formatOnSave.IsChecked         = ce.FormatOnSave;
            _trimTrailing.IsChecked         = ce.TrimTrailingWhitespace;
            _insertFinalNewline.IsChecked   = ce.InsertFinalNewline;
            _spaceAfterKeywords.IsChecked   = ce.SpaceAfterKeywords;
            _spaceAroundOperators.IsChecked = ce.SpaceAroundBinaryOperators;
            _spaceAfterComma.IsChecked      = ce.SpaceAfterComma;
            _indentCaseLabels.IsChecked     = ce.IndentCaseLabels;
            _organizeImports.IsChecked      = ce.OrganizeImports;
            _autoBrackets.IsChecked         = ce.AutoClosingBrackets;
            _autoQuotes.IsChecked           = ce.AutoClosingQuotes;
            _skipOverClose.IsChecked        = ce.SkipOverClosingChar;
            _wrapSelection.IsChecked        = ce.WrapSelectionInPairs;
            _xmlAttrIndentLevels.SelectedIndex = Math.Clamp(ce.XmlAttributeIndentLevels - 1, 0, 2);
            _xmlOneAttrPerLine.IsChecked       = ce.XmlOneAttributePerLine;
        }
        finally { _loading = false; }

        if (_currentLangId is not null)
            ApplyLanguageOverrides(_currentLangId);
        else
            _preview?.Refresh(BuildOverrides());
    }

    public void Flush(AppSettings s)
    {
        var ce = s.CodeEditorDefaults;
        ce.FormatOnSave               = _formatOnSave.IsChecked == true;
        ce.TrimTrailingWhitespace     = _trimTrailing.IsChecked;
        ce.InsertFinalNewline         = _insertFinalNewline.IsChecked;
        ce.SpaceAfterKeywords         = _spaceAfterKeywords.IsChecked;
        ce.SpaceAroundBinaryOperators = _spaceAroundOperators.IsChecked;
        ce.SpaceAfterComma            = _spaceAfterComma.IsChecked;
        ce.IndentCaseLabels           = _indentCaseLabels.IsChecked;
        ce.OrganizeImports            = _organizeImports.IsChecked;
        ce.AutoClosingBrackets        = _autoBrackets.IsChecked  == true;
        ce.AutoClosingQuotes          = _autoQuotes.IsChecked    == true;
        ce.SkipOverClosingChar        = _skipOverClose.IsChecked == true;
        ce.WrapSelectionInPairs       = _wrapSelection.IsChecked == true;
        ce.XmlAttributeIndentLevels   = _xmlAttrIndentLevels.SelectedIndex + 1;
        ce.XmlOneAttributePerLine     = _xmlOneAttrPerLine.IsChecked == true;

        if (_currentLangId is not null)
            PersistLanguageOverrides(_currentLangId, ce.PerLanguageOverrides);
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private void OnChanged(object? sender, RoutedEventArgs e)
    {
        if (_loading) return;
        if (_currentLangId is not null && _settings is not null)
            PersistLanguageOverrides(_currentLangId, _settings.CodeEditorDefaults.PerLanguageOverrides);
        Changed?.Invoke(this, EventArgs.Empty);
        _preview?.Refresh(BuildOverrides());
    }

    private void OnLanguageComboChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_languageCombo.SelectedItem is not ComboBoxItem { Tag: LanguageDefinition lang }) return;
        ApplyLanguageOverrides(lang.Id);
    }

    private void OnResetClicked(object sender, RoutedEventArgs e)
    {
        if (_currentLangId is null || _settings is null) return;
        _settings.CodeEditorDefaults.PerLanguageOverrides.Remove(_currentLangId);
        _loading = true;
        try
        {
            _trimTrailing.IsChecked         = null;
            _insertFinalNewline.IsChecked   = null;
            _spaceAfterKeywords.IsChecked   = null;
            _spaceAroundOperators.IsChecked = null;
            _spaceAfterComma.IsChecked      = null;
            _indentCaseLabels.IsChecked     = null;
            _organizeImports.IsChecked      = null;
        }
        finally { _loading = false; }
        Changed?.Invoke(this, EventArgs.Empty);
        _preview?.Refresh(BuildOverrides());
    }

    private void OnResetAllClicked(object sender, RoutedEventArgs e)
    {
        if (_settings is null) return;
        _settings.CodeEditorDefaults.PerLanguageOverrides.Clear();
        _loading = true;
        try
        {
            _trimTrailing.IsChecked         = null;
            _insertFinalNewline.IsChecked   = null;
            _spaceAfterKeywords.IsChecked   = null;
            _spaceAroundOperators.IsChecked = null;
            _spaceAfterComma.IsChecked      = null;
            _indentCaseLabels.IsChecked     = null;
            _organizeImports.IsChecked      = null;
        }
        finally { _loading = false; }
        Changed?.Invoke(this, EventArgs.Empty);
        _preview?.Refresh(BuildOverrides());
    }

    /// <summary>
    /// Pre-selects <paramref name="langId"/> in the language combo so the page
    /// opens on the active editor's language. Call before or after <see cref="Load"/>.
    /// </summary>
    public void SelectLanguage(string langId)
    {
        for (int i = 0; i < _languageCombo.Items.Count; i++)
        {
            if (_languageCombo.Items[i] is ComboBoxItem { Tag: LanguageDefinition ld } &&
                string.Equals(ld.Id, langId, StringComparison.OrdinalIgnoreCase))
            {
                _languageCombo.SelectedIndex = i;
                return;
            }
        }
    }
    /// <summary>
    /// Enables or disables each formatting control based on the language's
    /// <c>supportedRules</c> list from its whfmt file.
    /// When <c>supportedRules</c> is absent (null), all controls are enabled.
    /// Section panels get dimmed (Opacity 0.38) when all their rules are disabled.
    /// </summary>
    private void UpdateSectionAvailability(LanguageDefinition? lang)
    {
        var supported = lang?.FormattingRules?.SupportedRules;

        // Enable/disable individual controls.
        foreach (var (ruleId, control) in _ruleControls)
        {
            bool on = supported is null || supported.Contains(ruleId);
            control.IsEnabled = on;
            control.Opacity   = on ? 1.0 : 0.38;
        }

        // Dim entire section panels when none of their rules are supported.
        bool anyCode = IsAnyOn("spaceAfterKeywords", "spaceAroundBinaryOperators",
                               "spaceAfterComma", "indentCaseLabels", "organizeImports");
        _codeSectionsPanel.Opacity = anyCode ? 1.0 : 0.38;

        bool anyXml = IsAnyOn("xmlAttributeIndentLevels", "xmlOneAttributePerLine");
        _xmlSectionPanel.Opacity = anyXml ? 1.0 : 0.38;

        bool IsAnyOn(params string[] ids)
            => supported is null || ids.Any(id => supported.Contains(id));
    }

    private void ApplyLanguageOverrides(string langId)
    {
        _currentLangId = langId;
        if (_settings is null) return;

        // Sync the preview panel to the newly selected language
        LanguageDefinition? lang = null;
        if (LanguageRegistry.Instance.FindById(langId) is { } l)
        {
            lang = l;
            _preview?.SelectLanguage(lang);
        }
        UpdateSectionAvailability(lang);

        // Always sync checkboxes: use stored per-language override when available,
        // or reset to null (indeterminate = inherit .whfmt default) when none is saved.
        _settings.CodeEditorDefaults.PerLanguageOverrides.TryGetValue(langId, out var ov);
        _loading = true;
        try
        {
            _trimTrailing.IsChecked         = ov?.TrimTrailingWhitespace;
            _insertFinalNewline.IsChecked   = ov?.InsertFinalNewline;
            _spaceAfterKeywords.IsChecked   = ov?.SpaceAfterKeywords;
            _spaceAroundOperators.IsChecked = ov?.SpaceAroundBinaryOperators;
            _spaceAfterComma.IsChecked      = ov?.SpaceAfterComma;
            _indentCaseLabels.IsChecked     = ov?.IndentCaseLabels;
            _organizeImports.IsChecked      = ov?.OrganizeImports;
        }
        finally { _loading = false; }

        _preview?.Refresh(BuildOverrides());
    }

    private void PersistLanguageOverrides(string langId, Dictionary<string, LanguageFormattingOverrides> dict)
    {
        if (!dict.TryGetValue(langId, out var ov))
        {
            ov = new LanguageFormattingOverrides();
            dict[langId] = ov;
        }
        ov.TrimTrailingWhitespace     = _trimTrailing.IsChecked;
        ov.InsertFinalNewline         = _insertFinalNewline.IsChecked;
        ov.SpaceAfterKeywords         = _spaceAfterKeywords.IsChecked;
        ov.SpaceAroundBinaryOperators = _spaceAroundOperators.IsChecked;
        ov.SpaceAfterComma            = _spaceAfterComma.IsChecked;
        ov.IndentCaseLabels           = _indentCaseLabels.IsChecked;
        ov.OrganizeImports            = _organizeImports.IsChecked;
    }

    private FormattingOverrides BuildOverrides() => new()
    {
        TrimTrailingWhitespace     = _trimTrailing.IsChecked,
        InsertFinalNewline         = _insertFinalNewline.IsChecked,
        SpaceAfterKeywords         = _spaceAfterKeywords.IsChecked,
        SpaceAroundBinaryOperators = _spaceAroundOperators.IsChecked,
        SpaceAfterComma            = _spaceAfterComma.IsChecked,
        IndentCaseLabels           = _indentCaseLabels.IsChecked,
        OrganizeImports            = _organizeImports.IsChecked,
    };

    private CheckBox MakeCheckBox(string label, bool isThreeState,
                                  string? ruleId, string? ruleDisplayName)
    {
        var cb = new CheckBox
        {
            Content      = label,
            IsThreeState = isThreeState,
            Margin       = new Thickness(0, 4, 0, 4),
        };
        cb.Checked       += OnChanged;
        cb.Unchecked     += OnChanged;
        if (isThreeState)
            cb.Indeterminate += OnChanged;

        // Wire tooltip if this checkbox maps to a formatting rule
        if (ruleId is not null && ruleDisplayName is not null && _colorizer is not null)
            WireTooltip(cb, ruleId, ruleDisplayName);

        return cb;
    }

    private CheckBox MakeThreeStateCheckBox(string label, string tooltip,
                                             string ruleId, string ruleDisplayName)
    {
        var cb = new CheckBox
        {
            Content      = label,
            IsThreeState = true,
            ToolTip      = tooltip + "\nIndeterminate = use .whfmt language default.",
            Margin       = new Thickness(0, 4, 0, 4),
        };
        cb.Checked       += OnChanged;
        cb.Unchecked     += OnChanged;
        cb.Indeterminate += OnChanged;

        if (_colorizer is not null)
            WireTooltip(cb, ruleId, ruleDisplayName);

        return cb;
    }

    /// <summary>
    /// Attaches a <see cref="FormattingRuleTooltip"/> shown in a Popup on MouseEnter.
    /// The tooltip is lazy-created and refreshed with the current language on each show.
    /// </summary>
    private void WireTooltip(CheckBox cb, string ruleId, string displayName)
    {
        _ruleMap.Add((cb, ruleId, displayName));

        cb.MouseEnter += (_, _) =>
        {
            if (_colorizer is null) return;

            // Lazy-create the tooltip for this rule
            if (!_tooltips.TryGetValue(ruleId, out var tip))
            {
                tip = new FormattingRuleTooltip(ruleId, displayName);
                _tooltips[ruleId] = tip;
            }

            // Resolve the current language from the preview panel's selection
            var langId  = _preview?.SelectedLanguageId;
            var langDef = langId is not null
                ? LanguageRegistry.Instance.FindById(langId)
                : null;

            tip.Refresh(langDef, _colorizer);

            // Show in a Popup anchored to the checkbox
            cb.ToolTip = tip;
            ToolTipService.SetShowDuration(cb, 30_000);
            ToolTipService.SetInitialShowDelay(cb, 300);
        };
    }

    /// <summary>Same as <see cref="WireTooltip"/> but for a <see cref="ComboBox"/>.</summary>
    private void WireComboTooltip(ComboBox combo, string ruleId, string displayName)
    {
        combo.MouseEnter += (_, _) =>
        {
            if (_colorizer is null) return;
            if (!_tooltips.TryGetValue(ruleId, out var tip))
            {
                tip = new FormattingRuleTooltip(ruleId, displayName);
                _tooltips[ruleId] = tip;
            }
            var langId  = _preview?.SelectedLanguageId;
            var langDef = langId is not null ? LanguageRegistry.Instance.FindById(langId) : null;
            tip.Refresh(langDef, _colorizer);
            combo.ToolTip = tip;
            ToolTipService.SetShowDuration(combo, 30_000);
            ToolTipService.SetInitialShowDelay(combo, 300);
        };
    }

    private static TextBlock MakeSectionHeader(string text) => OptionsPageHelper.SectionHeader(text);

    // ── Themed button style (matches OptionsTextButtonStyle from XAML pages) ──────

    private static Style? _themedButtonStyle;

    private static Style ThemedButtonStyle => _themedButtonStyle ??= (Style)XamlReader.Parse("""
        <Style TargetType="Button"
               xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
               xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
            <Setter Property="Background"          Value="{DynamicResource Panel_ToolbarBrush}"/>
            <Setter Property="Foreground"          Value="{DynamicResource DockMenuForegroundBrush}"/>
            <Setter Property="BorderBrush"         Value="{DynamicResource DockBorderBrush}"/>
            <Setter Property="BorderThickness"     Value="1"/>
            <Setter Property="Padding"             Value="8,3"/>
            <Setter Property="Cursor"              Value="Hand"/>
            <Setter Property="FocusVisualStyle"    Value="{x:Null}"/>
            <Setter Property="SnapsToDevicePixels" Value="True"/>
            <Setter Property="Template">
                <Setter.Value>
                    <ControlTemplate TargetType="Button">
                        <Border x:Name="Bd"
                                Background="{TemplateBinding Background}"
                                BorderBrush="{TemplateBinding BorderBrush}"
                                BorderThickness="{TemplateBinding BorderThickness}"
                                CornerRadius="2"
                                Padding="{TemplateBinding Padding}"
                                SnapsToDevicePixels="True">
                            <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/>
                        </Border>
                        <ControlTemplate.Triggers>
                            <Trigger Property="IsMouseOver" Value="True">
                                <Setter TargetName="Bd" Property="Background"
                                        Value="{DynamicResource Panel_ToolbarButtonHoverBrush}"/>
                            </Trigger>
                            <Trigger Property="IsPressed" Value="True">
                                <Setter TargetName="Bd" Property="Background"
                                        Value="{DynamicResource Panel_ToolbarButtonActiveBrush}"/>
                            </Trigger>
                            <Trigger Property="IsEnabled" Value="False">
                                <Setter Property="Opacity" Value="0.4"/>
                            </Trigger>
                        </ControlTemplate.Triggers>
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
        </Style>
        """);
}
