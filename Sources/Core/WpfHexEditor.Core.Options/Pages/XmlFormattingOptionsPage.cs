// ==========================================================
// Project: WpfHexEditor.Core.Options
// File: Pages/XmlFormattingOptionsPage.cs
// Contributors: Claude Sonnet 4.6
// Created: 2026-04-06
// Description:
//     Options page for XML / XAML-specific formatting settings:
//     - XmlAttributeIndentLevels: how many indent levels continuation
//       attribute lines are offset from the element depth.
//     - XmlOneAttributePerLine: place each attribute on its own line.
//
// Architecture Notes:
//     Code-only UserControl implementing IOptionsPage.
//     Reads/writes AppSettings.CodeEditorDefaults.XmlAttributeIndentLevels
//     and XmlOneAttributePerLine.
//     Registered in OptionsPageRegistry under "Code Editor" / "XML / XAML".
// ==========================================================

using System;
using System.Windows;
using System.Windows.Controls;

namespace WpfHexEditor.Core.Options.Pages;

/// <summary>
/// IDE options page — Code Editor › XML / XAML.
/// </summary>
public sealed class XmlFormattingOptionsPage : UserControl, IOptionsPage
{
    public event EventHandler? Changed;

    private readonly ComboBox  _attrIndentLevels;
    private readonly CheckBox  _oneAttrPerLine;
    private bool _loading;

    public XmlFormattingOptionsPage()
    {
        // ── Layout ────────────────────────────────────────────────────────
        var root = new StackPanel { Margin = new Thickness(12) };

        // Section header
        root.Children.Add(new TextBlock
        {
            Text       = "XML / XAML Attribute Formatting",
            FontWeight = FontWeights.SemiBold,
            Margin     = new Thickness(0, 0, 0, 8),
        });

        // ── Attribute indent levels ───────────────────────────────────────
        var attrLevelPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin      = new Thickness(0, 0, 0, 8),
        };
        attrLevelPanel.Children.Add(new TextBlock
        {
            Text              = "Attribute continuation indent:",
            VerticalAlignment = VerticalAlignment.Center,
            Margin            = new Thickness(0, 0, 8, 0),
            Width             = 230,
        });

        _attrIndentLevels = new ComboBox { Width = 180 };
        _attrIndentLevels.Items.Add("1 level  (4 spaces at root)");
        _attrIndentLevels.Items.Add("2 levels (8 spaces at root) — VS default");
        _attrIndentLevels.Items.Add("3 levels (12 spaces at root)");
        _attrIndentLevels.SelectionChanged += (_, _) => { if (!_loading) Changed?.Invoke(this, EventArgs.Empty); };
        attrLevelPanel.Children.Add(_attrIndentLevels);
        root.Children.Add(attrLevelPanel);

        // Separator
        root.Children.Add(new Separator { Margin = new Thickness(0, 4, 0, 8) });

        // ── One attribute per line ────────────────────────────────────────
        _oneAttrPerLine = new CheckBox
        {
            Content = "Each XML/XAML attribute on its own line  (first attribute stays on tag line)",
            Margin  = new Thickness(0, 0, 0, 4),
        };
        _oneAttrPerLine.Checked   += (_, _) => { if (!_loading) Changed?.Invoke(this, EventArgs.Empty); };
        _oneAttrPerLine.Unchecked += (_, _) => { if (!_loading) Changed?.Invoke(this, EventArgs.Empty); };
        root.Children.Add(_oneAttrPerLine);

        // Description note
        root.Children.Add(new TextBlock
        {
            Text         = "When enabled, reformatting a tag such as\n  <Button Grid.Row=\"0\" Width=\"100\" Height=\"30\"/>\nbecomes:\n  <Button Grid.Row=\"0\"\n          Width=\"100\"\n          Height=\"30\"/>",
            Margin       = new Thickness(20, 4, 0, 0),
            FontStyle    = System.Windows.FontStyles.Italic,
            TextWrapping = TextWrapping.Wrap,
            Opacity      = 0.75,
        });

        Content = root;
    }

    // ── IOptionsPage ──────────────────────────────────────────────────────

    public string Category => "Code Editor";
    public string PageName  => "XML / XAML";

    public void Load(AppSettings settings)
    {
        _loading = true;
        try
        {
            var d = settings.CodeEditorDefaults;
            _attrIndentLevels.SelectedIndex = Math.Clamp(d.XmlAttributeIndentLevels - 1, 0, 2);
            _oneAttrPerLine.IsChecked       = d.XmlOneAttributePerLine;
        }
        finally { _loading = false; }
    }

    public void Flush(AppSettings settings)
    {
        var d = settings.CodeEditorDefaults;
        d.XmlAttributeIndentLevels = _attrIndentLevels.SelectedIndex + 1;
        d.XmlOneAttributePerLine   = _oneAttrPerLine.IsChecked == true;
    }
}
