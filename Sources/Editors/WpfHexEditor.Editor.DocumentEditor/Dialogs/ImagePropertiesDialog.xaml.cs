// ==========================================================
// Project: WpfHexEditor.Editor.DocumentEditor
// File: Dialogs/ImagePropertiesDialog.xaml.cs
// Description: Modal dialog to view and edit image block properties
//     (size, alignment, wrap spacing, border, alt text, options).
//     Changes are returned via the ImageProperties result record.
// Architecture: Result record pattern — caller applies changes.
// ==========================================================

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WpfHexEditor.Editor.DocumentEditor.Core.Model;

namespace WpfHexEditor.Editor.DocumentEditor.Dialogs;

/// <summary>Result produced by <see cref="ImagePropertiesDialog"/>.</summary>
public sealed record ImageProperties(
    double  Width,
    double  Height,
    string  Alignment,
    string  WrapMode,
    double  SpaceTop,
    double  SpaceBottom,
    double  SpaceLeft,
    double  SpaceRight,
    bool    BorderEnabled,
    double  BorderWidth,
    Color   BorderColor,
    string  BorderStyle,
    double  CornerRadius,
    string  AltText,
    bool    KeepAspect,
    bool    Protect,
    bool    Printable);

public partial class ImagePropertiesDialog : Window
{
    private readonly DocumentBlock _block;
    private readonly double        _naturalW;
    private readonly double        _naturalH;
    private          double        _aspectRatio = 1.0;
    private          bool          _suppressSizeEvents;

    private Color _borderColor = Colors.Black;

    // ── Panel references ─────────────────────────────────────────────────────

    private readonly FrameworkElement[] _panels = null!;

    // ── Result ───────────────────────────────────────────────────────────────

    public ImageProperties? Result { get; private set; }

    // ── Constructor ──────────────────────────────────────────────────────────

    public ImagePropertiesDialog(DocumentBlock block, BitmapSource? preview = null)
    {
        _block = block;

        _naturalW = ReadAttr("naturalWidth",  0);
        _naturalH = ReadAttr("naturalHeight", 0);

        if (_naturalW > 0 && _naturalH > 0)
            _aspectRatio = _naturalW / _naturalH;

        InitializeComponent();

        _panels = [PART_PanelSize, PART_PanelWrap, PART_PanelBorder, PART_PanelOptions];

        PopulateFields();

        if (preview is not null)
        {
            PART_PreviewImage.Source = preview;
            if (_naturalW > 0 && _naturalH > 0)
                PART_DimensionLabel.Text = $"{(int)_naturalW} × {(int)_naturalH} px";
        }
    }

    // ── Populate ─────────────────────────────────────────────────────────────

    private void PopulateFields()
    {
        _suppressSizeEvents = true;

        PART_Width.Text  = _naturalW > 0 ? ((int)_naturalW).ToString() : "0";
        PART_Height.Text = _naturalH > 0 ? ((int)_naturalH).ToString() : "0";

        var align = ReadAttrStr("align", "left");
        PART_AlignLeft.IsChecked   = align == "left";
        PART_AlignCenter.IsChecked = align == "center";
        PART_AlignRight.IsChecked  = align == "right";

        PART_AltText.Text = ReadAttrStr("alt", string.Empty);

        PART_SpaceTop.Text    = ReadAttr("spaceTop",    8).ToString();
        PART_SpaceBottom.Text = ReadAttr("spaceBottom", 8).ToString();
        PART_SpaceLeft.Text   = ReadAttr("spaceLeft",   0).ToString();
        PART_SpaceRight.Text  = ReadAttr("spaceRight",  0).ToString();

        var wrap = ReadAttrStr("wrap", "none");
        PART_WrapNone.IsChecked  = wrap == "none";
        PART_WrapLeft.IsChecked  = wrap == "left";
        PART_WrapRight.IsChecked = wrap == "right";

        bool borderEnabled = ReadAttr("borderEnabled", 0) > 0;
        PART_BorderEnabled.IsChecked  = borderEnabled;
        PART_BorderControls.IsEnabled = borderEnabled;
        PART_BorderWidth.Text         = ReadAttr("borderWidth", 1).ToString();

        var colorHex = ReadAttrStr("borderColor", "#000000");
        try { _borderColor = (Color)ColorConverter.ConvertFromString(colorHex); }
        catch { _borderColor = Colors.Black; }
        PART_BorderColorSwatch.Background = new SolidColorBrush(_borderColor);

        var bStyle = ReadAttrStr("borderStyle", "solid");
        PART_BorderStyleCombo.SelectedIndex = bStyle switch { "dashed" => 1, "dotted" => 2, _ => 0 };

        PART_CornerRadius.Text = ReadAttr("cornerRadius", 0).ToString();

        PART_OptKeepAspect.IsChecked = ReadAttr("keepAspect",  1) > 0;
        PART_OptProtect.IsChecked    = ReadAttr("protect",     0) > 0;
        PART_OptPrintable.IsChecked  = ReadAttr("printable",   1) > 0;

        var source = ReadAttrStr("zipEntryName", string.Empty);
        if (string.IsNullOrEmpty(source))
            source = _block.RawOffset > 0 ? $"Binary @ offset 0x{_block.RawOffset:X}" : "—";
        PART_SourceLabel.Text = source;

        _suppressSizeEvents = false;
    }

    // ── Tab switching ────────────────────────────────────────────────────────

    private void OnTabClicked(object sender, RoutedEventArgs e)
    {
        if (sender is not RadioButton rb || rb.Tag is not string tagStr) return;
        if (!int.TryParse(tagStr, out int idx)) return;

        for (int i = 0; i < _panels.Length; i++)
            _panels[i].Visibility = i == idx ? Visibility.Visible : Visibility.Collapsed;
    }

    // ── Size controls ────────────────────────────────────────────────────────

    private void OnSizeChanged(object sender, TextChangedEventArgs e)
    {
        if (_suppressSizeEvents) return;
        if (PART_LockAspect.IsChecked != true) return;
        if (_aspectRatio <= 0) return;

        _suppressSizeEvents = true;

        if (sender == PART_Width && double.TryParse(PART_Width.Text, out double w) && w > 0)
        {
            PART_Height.Text = ((int)(w / _aspectRatio)).ToString();
        }
        else if (sender == PART_Height && double.TryParse(PART_Height.Text, out double h) && h > 0)
        {
            PART_Width.Text = ((int)(h * _aspectRatio)).ToString();
        }

        _suppressSizeEvents = false;
    }

    private void OnResetSizeClicked(object sender, RoutedEventArgs e)
    {
        _suppressSizeEvents = true;
        PART_Width.Text  = _naturalW > 0 ? ((int)_naturalW).ToString() : "0";
        PART_Height.Text = _naturalH > 0 ? ((int)_naturalH).ToString() : "0";
        _suppressSizeEvents = false;
    }

    // ── Border controls ──────────────────────────────────────────────────────

    private void OnBorderEnabledChanged(object sender, RoutedEventArgs e)
    {
        if (PART_BorderControls is not null)
            PART_BorderControls.IsEnabled = PART_BorderEnabled.IsChecked == true;
    }

    private void OnPickBorderColor(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        var current = $"#{_borderColor.R:X2}{_borderColor.G:X2}{_borderColor.B:X2}";
        var input   = PromptHexColor(current);
        if (input is null) return;
        try
        {
            _borderColor = (Color)ColorConverter.ConvertFromString(input);
            PART_BorderColorSwatch.Background = new SolidColorBrush(_borderColor);
        }
        catch { /* invalid hex — ignore */ }
    }

    private static string? PromptHexColor(string currentHex)
    {
        var tb = new TextBox
        {
            Text              = currentHex,
            Width             = 160,
            Margin            = new Thickness(8),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        var ok     = new Button { Content = "OK",     IsDefault = true,  Width = 70, Margin = new Thickness(4) };
        var cancel = new Button { Content = "Cancel", IsCancel  = true,  Width = 70, Margin = new Thickness(4) };
        var btns   = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(4) };
        btns.Children.Add(ok);
        btns.Children.Add(cancel);
        var panel  = new StackPanel();
        panel.Children.Add(new TextBlock { Text = "Enter hex color (#RRGGBB):", Margin = new Thickness(8, 8, 8, 2) });
        panel.Children.Add(tb);
        panel.Children.Add(btns);

        var win = new Window
        {
            Content                 = panel,
            SizeToContent           = SizeToContent.WidthAndHeight,
            ResizeMode              = ResizeMode.NoResize,
            WindowStartupLocation   = WindowStartupLocation.CenterOwner,
            ShowInTaskbar           = false,
            Title                   = "Pick color"
        };
        string? result = null;
        ok.Click     += (_, _) => { result = tb.Text.Trim(); win.DialogResult = true; };
        cancel.Click += (_, _) => win.DialogResult = false;
        win.ShowDialog();
        return result;
    }

    // ── Buttons ──────────────────────────────────────────────────────────────

    private void OnOkClicked(object sender, RoutedEventArgs e)
    {
        var align = PART_AlignCenter.IsChecked == true ? "center"
                  : PART_AlignRight.IsChecked  == true ? "right"
                  : "left";

        var wrap = PART_WrapLeft.IsChecked  == true ? "left"
                 : PART_WrapRight.IsChecked == true ? "right"
                 : "none";

        var bStyle = PART_BorderStyleCombo.SelectedIndex switch { 1 => "dashed", 2 => "dotted", _ => "solid" };

        Result = new ImageProperties(
            Width:         double.TryParse(PART_Width.Text,         out double w)   ? w   : _naturalW,
            Height:        double.TryParse(PART_Height.Text,        out double h)   ? h   : _naturalH,
            Alignment:     align,
            WrapMode:      wrap,
            SpaceTop:      double.TryParse(PART_SpaceTop.Text,      out double st)  ? st  : 8,
            SpaceBottom:   double.TryParse(PART_SpaceBottom.Text,   out double sb)  ? sb  : 8,
            SpaceLeft:     double.TryParse(PART_SpaceLeft.Text,     out double sl)  ? sl  : 0,
            SpaceRight:    double.TryParse(PART_SpaceRight.Text,    out double sr)  ? sr  : 0,
            BorderEnabled: PART_BorderEnabled.IsChecked == true,
            BorderWidth:   double.TryParse(PART_BorderWidth.Text,   out double bw)  ? bw  : 1,
            BorderColor:   _borderColor,
            BorderStyle:   bStyle,
            CornerRadius:  double.TryParse(PART_CornerRadius.Text,  out double cr)  ? cr  : 0,
            AltText:       PART_AltText.Text.Trim(),
            KeepAspect:    PART_OptKeepAspect.IsChecked == true,
            Protect:       PART_OptProtect.IsChecked    == true,
            Printable:     PART_OptPrintable.IsChecked  == true);

        DialogResult = true;
    }

    private void OnCancelClicked(object sender, RoutedEventArgs e) => DialogResult = false;

    private void OnResetClicked(object sender, RoutedEventArgs e)
    {
        _suppressSizeEvents = false;
        PopulateFields();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private double ReadAttr(string key, double fallback)
    {
        if (!_block.Attributes.TryGetValue(key, out var raw)) return fallback;
        return raw switch
        {
            double d  => d,
            string s  => double.TryParse(s, out var v) ? v : fallback,
            _         => fallback
        };
    }

    private string ReadAttrStr(string key, string fallback) =>
        _block.Attributes.TryGetValue(key, out var raw) && raw is string s ? s : fallback;
}
