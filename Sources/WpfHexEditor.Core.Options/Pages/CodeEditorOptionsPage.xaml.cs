//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WpfHexEditor.Core;
using ColorPickerControl = WpfHexEditor.ColorPicker.Controls.ColorPicker;

namespace WpfHexEditor.Core.Options.Pages;

public sealed partial class CodeEditorOptionsPage : UserControl, IOptionsPage
{
    public event EventHandler? Changed;
    private bool _loading;

    public CodeEditorOptionsPage()
    {
        InitializeComponent();

        // Populate wheel-speed combo with the same enum values as HexEditor.
        MouseWheelCombo.ItemsSource = Enum.GetValues<MouseWheelSpeed>();

        // Auto-check the override checkbox whenever the user picks a new color.
        WireAutoCheck(ChkBg,      CpBg);
        WireAutoCheck(ChkFg,      CpFg);
        WireAutoCheck(ChkKw,      CpKw);
        WireAutoCheck(ChkStr,     CpStr);
        WireAutoCheck(ChkCmt,     CpCmt);
        WireAutoCheck(ChkNum,     CpNum);
        WireAutoCheck(ChkType,    CpType);
        WireAutoCheck(ChkId,      CpId);
        WireAutoCheck(ChkOp,      CpOp);
        WireAutoCheck(ChkBracket, CpBracket);
        WireAutoCheck(ChkAttr,    CpAttr);
    }

    private void WireAutoCheck(CheckBox chk, ColorPickerControl cp)
    {
        cp.ColorChanged += (_, _) =>
        {
            if (!_loading) chk.IsChecked = true;
        };
    }

    // -- IOptionsPage ------------------------------------------------------

    public void Load(AppSettings s)
    {
        _loading = true;
        try
        {
            var ce = s.CodeEditorDefaults;

            SelectComboByTag(FontFamilyCombo, ce.FontFamily);
            TxtFontSize.Text   = ce.FontSize.ToString("F0");
            TxtIndentSize.Text = ce.IndentSize.ToString();
            CheckUseSpaces.IsChecked     = ce.UseSpaces;
            CheckSmartComplete.IsChecked  = ce.ShowSmartComplete;
            CheckLineNumbers.IsChecked   = ce.ShowLineNumbers;
            CheckHighlightLine.IsChecked   = ce.HighlightCurrentLine;
            CheckFoldDoubleClick.IsChecked = ce.FoldToggleOnDoubleClick;
            CheckWordWrap.IsChecked        = ce.WordWrap;
            TxtZoom.Text      = ((int)(ce.DefaultZoom * 100)).ToString();
            MouseWheelCombo.SelectedItem = ce.MouseWheelSpeed;
            CheckChangeset.IsChecked = false; // feature not yet implemented — always unchecked

            LoadColorPicker(ChkBg,      CpBg,      ce.BackgroundColor,  "TE_Background");
            LoadColorPicker(ChkFg,      CpFg,      ce.ForegroundColor,  "TE_Foreground");
            LoadColorPicker(ChkKw,      CpKw,      ce.KeywordColor,     "TE_Keyword");
            LoadColorPicker(ChkStr,     CpStr,     ce.StringColor,      "TE_String");
            LoadColorPicker(ChkCmt,     CpCmt,     ce.CommentColor,     "TE_Comment");
            LoadColorPicker(ChkNum,     CpNum,     ce.NumberColor,      "TE_Number");
            LoadColorPicker(ChkType,    CpType,    ce.TypeColor,        "CE_Type");
            LoadColorPicker(ChkId,      CpId,      ce.IdentifierColor,  "CE_Identifier");
            LoadColorPicker(ChkOp,      CpOp,      ce.OperatorColor,    "CE_Operator");
            LoadColorPicker(ChkBracket, CpBracket, ce.BracketColor,     "CE_Bracket");
            LoadColorPicker(ChkAttr,    CpAttr,    ce.AttributeColor,   "CE_Attribute");
        }
        finally { _loading = false; }
    }

    public void Flush(AppSettings s)
    {
        var ce = s.CodeEditorDefaults;

        ce.FontFamily        = ReadComboTag(FontFamilyCombo, "Consolas");
        ce.FontSize          = ParseDouble(TxtFontSize.Text, 13.0);
        ce.IndentSize        = ParseInt(TxtIndentSize.Text, 4);
        ce.UseSpaces         = CheckUseSpaces.IsChecked    == true;
        ce.ShowSmartComplete  = CheckSmartComplete.IsChecked == true;
        ce.ShowLineNumbers   = CheckLineNumbers.IsChecked  == true;
        ce.HighlightCurrentLine    = CheckHighlightLine.IsChecked   == true;
        ce.FoldToggleOnDoubleClick = CheckFoldDoubleClick.IsChecked == true;
        ce.WordWrap                = CheckWordWrap.IsChecked        == true;
        ce.DefaultZoom     = ParseDouble(TxtZoom.Text, 100.0) / 100.0;
        if (MouseWheelCombo.SelectedItem is MouseWheelSpeed mws)
            ce.MouseWheelSpeed = mws;
        ce.ChangesetEnabled = CheckChangeset.IsChecked == true;
        ce.BackgroundColor   = FlushColorPicker(ChkBg,      CpBg);
        ce.ForegroundColor   = FlushColorPicker(ChkFg,      CpFg);
        ce.KeywordColor      = FlushColorPicker(ChkKw,      CpKw);
        ce.StringColor       = FlushColorPicker(ChkStr,     CpStr);
        ce.CommentColor      = FlushColorPicker(ChkCmt,     CpCmt);
        ce.NumberColor       = FlushColorPicker(ChkNum,     CpNum);
        ce.TypeColor         = FlushColorPicker(ChkType,    CpType);
        ce.IdentifierColor   = FlushColorPicker(ChkId,      CpId);
        ce.OperatorColor     = FlushColorPicker(ChkOp,      CpOp);
        ce.BracketColor      = FlushColorPicker(ChkBracket, CpBracket);
        ce.AttributeColor    = FlushColorPicker(ChkAttr,    CpAttr);
    }

    // -- Control handlers -------------------------------------------------

    private void OnCheckChanged(object sender, RoutedEventArgs e)
    {
        if (!_loading) Changed?.Invoke(this, EventArgs.Empty);
    }

    private void OnComboChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_loading) Changed?.Invoke(this, EventArgs.Empty);
    }

    private void OnTextLostFocus(object sender, RoutedEventArgs e)
    {
        if (!_loading) Changed?.Invoke(this, EventArgs.Empty);
    }

    private void OnColorCheckChanged(object sender, RoutedEventArgs e)
    {
        if (!_loading) Changed?.Invoke(this, EventArgs.Empty);
    }

    private void OnColorPickerChanged(object sender, Color e)
    {
        if (!_loading) Changed?.Invoke(this, EventArgs.Empty);
    }

    // -- Helpers ----------------------------------------------------------

    private static void SelectComboByTag(ComboBox combo, string tag)
    {
        foreach (ComboBoxItem item in combo.Items)
        {
            if (item.Tag?.ToString() == tag)
            {
                combo.SelectedItem = item;
                return;
            }
        }
        if (combo.Items.Count > 0)
            combo.SelectedIndex = 0;
    }

    private static string ReadComboTag(ComboBox combo, string fallback)
        => combo.SelectedItem is ComboBoxItem item
            ? item.Tag?.ToString() ?? fallback
            : fallback;

    private static double ParseDouble(string text, double fallback)
        => double.TryParse(text, out double v) && v > 0 ? v : fallback;

    private static int ParseInt(string text, int fallback)
        => int.TryParse(text, out int v) && v > 0 ? v : fallback;

    // Restores CheckBox + ColorPicker from a stored hex string (empty = no override).
    // When no override is stored the swatch shows the actual theme colour for themeKey.
    private static void LoadColorPicker(CheckBox chk, ColorPickerControl cp,
                                        string value, string themeKey)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            chk.IsChecked = false;
            cp.SelectedColor = ResolveThemeColor(themeKey);
            return;
        }
        try
        {
            cp.SelectedColor = (Color)ColorConverter.ConvertFromString(value.Trim());
            chk.IsChecked = true;
        }
        catch
        {
            chk.IsChecked = false;
            cp.SelectedColor = ResolveThemeColor(themeKey);
        }
    }

    // Resolves a SolidColorBrush from the application resource dictionary by key.
    // Falls back to Transparent when the key is absent (no theme loaded).
    private static Color ResolveThemeColor(string key)
        => Application.Current.Resources[key] is SolidColorBrush b ? b.Color : Colors.Transparent;

    // Returns "#RRGGBB" when the override is active, empty string otherwise.
    private static string FlushColorPicker(CheckBox chk, ColorPickerControl cp)
    {
        if (chk.IsChecked != true) return string.Empty;
        var c = cp.SelectedColor;
        return $"#{c.R:X2}{c.G:X2}{c.B:X2}";
    }
}
