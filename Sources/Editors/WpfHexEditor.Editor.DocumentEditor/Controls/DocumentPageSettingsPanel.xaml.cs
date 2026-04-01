// ==========================================================
// Project: WpfHexEditor.Editor.DocumentEditor
// File: Controls/DocumentPageSettingsPanel.xaml.cs
// Description:
//     Code-behind for the 4-tab page settings flyout panel.
//     Reads/writes DocumentPageSettings and fires SettingsApplied
//     when the user clicks Apply.
// Architecture:
//     Pure UI logic — no ViewModel. Populates controls from a
//     DocumentPageSettings instance and reads them back via BuildSettings().
// ==========================================================

using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using WpfHexEditor.Editor.DocumentEditor.Core.Options;

namespace WpfHexEditor.Editor.DocumentEditor.Controls;

public partial class DocumentPageSettingsPanel : UserControl
{
    // ── Events ──────────────────────────────────────────────────────────────

    public event EventHandler<DocumentPageSettings>? SettingsApplied;
    public event EventHandler?                        Cancelled;

    // ── Constructor ─────────────────────────────────────────────────────────

    public DocumentPageSettingsPanel()
    {
        InitializeComponent();
        AttachNumericValidation();
    }

    // ── Public API ───────────────────────────────────────────────────────────

    public void LoadSettings(DocumentPageSettings s)
    {
        // ── Page tab ──────────────────────────────────────────────────────
        SelectPageSizeCombo(s.PageSize);
        PART_PortraitRadio.IsChecked   = s.Orientation == DocumentPageOrientation.Portrait;
        PART_LandscapeRadio.IsChecked  = s.Orientation == DocumentPageOrientation.Landscape;
        PART_CustomWidthBox.Text       = s.CustomWidth .ToString(CultureInfo.InvariantCulture);
        PART_CustomHeightBox.Text      = s.CustomHeight.ToString(CultureInfo.InvariantCulture);
        PART_CustomSizeRow.Visibility  = s.PageSize == DocumentPageSize.Custom
                                         ? Visibility.Visible : Visibility.Collapsed;

        PART_MarginTopBox.Text         = s.MarginTop   .ToString(CultureInfo.InvariantCulture);
        PART_MarginBottomBox.Text      = s.MarginBottom.ToString(CultureInfo.InvariantCulture);
        PART_MarginLeftBox.Text        = s.MarginLeft  .ToString(CultureInfo.InvariantCulture);
        PART_MarginRightBox.Text       = s.MarginRight .ToString(CultureInfo.InvariantCulture);
        PART_MarginGutterBox.Text      = s.MarginGutter.ToString(CultureInfo.InvariantCulture);
        PART_MirrorMarginsCheck.IsChecked = s.MirrorMargins;
        ApplyMirrorMarginLabels(s.MirrorMargins);

        // ── Columns tab ───────────────────────────────────────────────────
        SetColumnCount(s.ColumnCount);
        PART_EqualColWidthsCheck.IsChecked = s.EqualColumnWidths;
        PART_ColGapBox.Text                = s.ColumnGapPx.ToString(CultureInfo.InvariantCulture);
        PART_ColSeparatorCheck.IsChecked   = s.ShowColumnSeparatorLine;

        // ── Header/Footer tab ─────────────────────────────────────────────
        PART_HeaderEnabledCheck.IsChecked  = s.HeaderEnabled;
        PART_HeaderOptionsRow.IsEnabled    = s.HeaderEnabled;
        PART_HeaderHeightBox.Text          = s.HeaderHeightPx.ToString(CultureInfo.InvariantCulture);
        PART_HeaderMarginBox.Text          = s.HeaderMarginPx.ToString(CultureInfo.InvariantCulture);
        PART_HeaderDiffFirstCheck.IsEnabled  = s.HeaderEnabled;
        PART_HeaderSameLRCheck.IsEnabled     = s.HeaderEnabled;
        PART_HeaderDiffFirstCheck.IsChecked  = s.HeaderDifferentFirstPage;
        PART_HeaderSameLRCheck.IsChecked     = s.HeaderSameLeftRight;

        PART_FooterEnabledCheck.IsChecked  = s.FooterEnabled;
        PART_FooterOptionsRow.IsEnabled    = s.FooterEnabled;
        PART_FooterHeightBox.Text          = s.FooterHeightPx.ToString(CultureInfo.InvariantCulture);
        PART_FooterMarginBox.Text          = s.FooterMarginPx.ToString(CultureInfo.InvariantCulture);
        PART_FooterDiffFirstCheck.IsEnabled  = s.FooterEnabled;
        PART_FooterSameLRCheck.IsEnabled     = s.FooterEnabled;
        PART_FooterDiffFirstCheck.IsChecked  = s.FooterDifferentFirstPage;
        PART_FooterSameLRCheck.IsChecked     = s.FooterSameLeftRight;

        // ── Border tab ────────────────────────────────────────────────────
        PART_BorderNoneRadio.IsChecked     = s.BorderStyle == DocumentPageBorderStyle.None;
        PART_BorderBoxRadio.IsChecked      = s.BorderStyle == DocumentPageBorderStyle.Box;
        PART_BorderShadowRadio.IsChecked   = s.BorderStyle == DocumentPageBorderStyle.Shadow;
        PART_BorderColorBox.Text           = s.BorderColor;
        PART_BorderWidthBox.Text           = s.BorderWidthPx  .ToString(CultureInfo.InvariantCulture);
        PART_BorderPaddingBox.Text         = s.BorderPaddingPx.ToString(CultureInfo.InvariantCulture);
    }

    // ── Button handlers ──────────────────────────────────────────────────────

    private void OnApplyClicked(object sender, RoutedEventArgs e)
        => SettingsApplied?.Invoke(this, BuildSettings());

    private void OnCancelClicked(object sender, RoutedEventArgs e)
        => Cancelled?.Invoke(this, EventArgs.Empty);

    // ── Page tab handlers ────────────────────────────────────────────────────

    private void OnPageSizeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PART_CustomSizeRow is null) return;
        var tag = (PART_PageSizeCombo.SelectedItem as ComboBoxItem)?.Tag as string;
        PART_CustomSizeRow.Visibility = tag == "Custom"
            ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnMirrorMarginsChanged(object sender, RoutedEventArgs e)
        => ApplyMirrorMarginLabels(PART_MirrorMarginsCheck.IsChecked == true);

    // ── Columns tab handlers ─────────────────────────────────────────────────

    private void OnColumnCountClicked(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton clicked) return;

        // Radio-like exclusion: uncheck all, then check the clicked one
        foreach (var btn in new[] { PART_Col1Btn, PART_Col2Btn, PART_Col3Btn,
                                     PART_Col4Btn, PART_Col5Btn })
        {
            btn.IsChecked = ReferenceEquals(btn, clicked);
        }
    }

    // ── Header / Footer handlers ─────────────────────────────────────────────

    private void OnHeaderEnabledChanged(object sender, RoutedEventArgs e)
    {
        bool on = PART_HeaderEnabledCheck.IsChecked == true;
        PART_HeaderOptionsRow.IsEnabled      = on;
        PART_HeaderDiffFirstCheck.IsEnabled  = on;
        PART_HeaderSameLRCheck.IsEnabled     = on;
    }

    private void OnFooterEnabledChanged(object sender, RoutedEventArgs e)
    {
        bool on = PART_FooterEnabledCheck.IsChecked == true;
        PART_FooterOptionsRow.IsEnabled      = on;
        PART_FooterDiffFirstCheck.IsEnabled  = on;
        PART_FooterSameLRCheck.IsEnabled     = on;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private DocumentPageSettings BuildSettings() => new()
    {
        PageSize    = ParsePageSize(),
        Orientation = PART_LandscapeRadio.IsChecked == true
                      ? DocumentPageOrientation.Landscape
                      : DocumentPageOrientation.Portrait,
        CustomWidth  = ParseDouble(PART_CustomWidthBox.Text,  794),
        CustomHeight = ParseDouble(PART_CustomHeightBox.Text, 1122),

        MarginTop    = ParseDouble(PART_MarginTopBox.Text,    40),
        MarginBottom = ParseDouble(PART_MarginBottomBox.Text, 56),
        MarginLeft   = ParseDouble(PART_MarginLeftBox.Text,   56),
        MarginRight  = ParseDouble(PART_MarginRightBox.Text,  56),
        MarginGutter = ParseDouble(PART_MarginGutterBox.Text,  0),
        MirrorMargins = PART_MirrorMarginsCheck.IsChecked == true,

        ColumnCount           = GetColumnCount(),
        EqualColumnWidths     = PART_EqualColWidthsCheck.IsChecked == true,
        ColumnGapPx           = ParseDouble(PART_ColGapBox.Text, 20),
        ShowColumnSeparatorLine = PART_ColSeparatorCheck.IsChecked == true,

        HeaderEnabled            = PART_HeaderEnabledCheck.IsChecked == true,
        HeaderHeightPx           = ParseDouble(PART_HeaderHeightBox.Text, 38),
        HeaderMarginPx           = ParseDouble(PART_HeaderMarginBox.Text, 10),
        HeaderDifferentFirstPage = PART_HeaderDiffFirstCheck.IsChecked == true,
        HeaderSameLeftRight      = PART_HeaderSameLRCheck.IsChecked == true,

        FooterEnabled            = PART_FooterEnabledCheck.IsChecked == true,
        FooterHeightPx           = ParseDouble(PART_FooterHeightBox.Text, 38),
        FooterMarginPx           = ParseDouble(PART_FooterMarginBox.Text, 10),
        FooterDifferentFirstPage = PART_FooterDiffFirstCheck.IsChecked == true,
        FooterSameLeftRight      = PART_FooterSameLRCheck.IsChecked == true,

        BorderStyle    = ParseBorderStyle(),
        BorderColor    = string.IsNullOrWhiteSpace(PART_BorderColorBox.Text)
                         ? "#000000" : PART_BorderColorBox.Text.Trim(),
        BorderWidthPx  = ParseDouble(PART_BorderWidthBox.Text,   1),
        BorderPaddingPx= ParseDouble(PART_BorderPaddingBox.Text,  8),
    };

    private DocumentPageSize ParsePageSize()
    {
        var tag = (PART_PageSizeCombo.SelectedItem as ComboBoxItem)?.Tag as string;
        return tag switch
        {
            "A3"     => DocumentPageSize.A3,
            "A5"     => DocumentPageSize.A5,
            "Letter" => DocumentPageSize.Letter,
            "Legal"  => DocumentPageSize.Legal,
            "Custom" => DocumentPageSize.Custom,
            _        => DocumentPageSize.A4,
        };
    }

    private DocumentPageBorderStyle ParseBorderStyle()
    {
        if (PART_BorderBoxRadio.IsChecked    == true) return DocumentPageBorderStyle.Box;
        if (PART_BorderShadowRadio.IsChecked == true) return DocumentPageBorderStyle.Shadow;
        return DocumentPageBorderStyle.None;
    }

    private int GetColumnCount()
    {
        if (PART_Col2Btn.IsChecked == true) return 2;
        if (PART_Col3Btn.IsChecked == true) return 3;
        if (PART_Col4Btn.IsChecked == true) return 4;
        if (PART_Col5Btn.IsChecked == true) return 5;
        return 1;
    }

    private void SetColumnCount(int count)
    {
        PART_Col1Btn.IsChecked = count == 1;
        PART_Col2Btn.IsChecked = count == 2;
        PART_Col3Btn.IsChecked = count == 3;
        PART_Col4Btn.IsChecked = count == 4;
        PART_Col5Btn.IsChecked = count == 5;
    }

    private void SelectPageSizeCombo(DocumentPageSize size)
    {
        var tag = size switch
        {
            DocumentPageSize.A3     => "A3",
            DocumentPageSize.A5     => "A5",
            DocumentPageSize.Letter => "Letter",
            DocumentPageSize.Legal  => "Legal",
            DocumentPageSize.Custom => "Custom",
            _                       => "A4",
        };
        foreach (ComboBoxItem item in PART_PageSizeCombo.Items)
        {
            if (item.Tag as string == tag)
            {
                PART_PageSizeCombo.SelectedItem = item;
                return;
            }
        }
        PART_PageSizeCombo.SelectedIndex = 0;
    }

    private void ApplyMirrorMarginLabels(bool mirror)
    {
        PART_MarginLeftLabel.Text  = mirror ? "Inside:"  : "Left:";
        PART_MarginRightLabel.Text = mirror ? "Outside:" : "Right:";
    }

    private static double ParseDouble(string? text, double fallback)
        => double.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var v)
           ? Math.Max(0, v)
           : fallback;

    private void AttachNumericValidation()
    {
        foreach (var box in new[]
        {
            PART_CustomWidthBox, PART_CustomHeightBox,
            PART_MarginTopBox, PART_MarginBottomBox,
            PART_MarginLeftBox, PART_MarginRightBox, PART_MarginGutterBox,
            PART_ColGapBox,
            PART_HeaderHeightBox, PART_HeaderMarginBox,
            PART_FooterHeightBox, PART_FooterMarginBox,
            PART_BorderWidthBox, PART_BorderPaddingBox
        })
        {
            box.PreviewTextInput += OnNumericPreviewTextInput;
        }
    }

    private static void OnNumericPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        // Allow digits and at most one decimal point
        var box = (TextBox)sender;
        bool hasDot = box.Text.Contains('.');
        e.Handled = !e.Text.All(c => char.IsDigit(c) || (c == '.' && !hasDot));
    }
}
