// ==========================================================
// Project: WpfHexEditor.Plugins.XamlDesigner
// File: Options/XamlDesignerOptionsPage.xaml.cs
// Author: Derek Tremblay
// Created: 2026-03-16
// Description:
//     Code-behind for the XAML Designer options page.
//     Loads current option values into controls on Load(),
//     reads them back into the options model on Save().
// ==========================================================

using System.Windows.Controls;

namespace WpfHexEditor.Plugins.XamlDesigner.Options;

/// <summary>
/// Interaction logic for XamlDesignerOptionsPage.xaml
/// </summary>
public partial class XamlDesignerOptionsPage : UserControl
{
    public XamlDesignerOptionsPage()
    {
        InitializeComponent();
    }

    // ── Load ─────────────────────────────────────────────────────────────────

    public void Load()
    {
        var opts = XamlDesignerOptions.Instance;

        ChkAutoPreview.IsChecked    = opts.AutoPreviewEnabled;
        PreviewDelaySlider.Value    = opts.AutoPreviewDelayMs;
        PreviewDelayLabel.Text      = $"{opts.AutoPreviewDelayMs} ms";

        // Sync view mode combo selection.
        foreach (ComboBoxItem item in ViewModeCombo.Items)
        {
            if ((string?)item.Tag == opts.DefaultViewMode)
            {
                ViewModeCombo.SelectedItem = item;
                break;
            }
        }

        ChkSnapToGrid.IsChecked   = opts.SnapToGrid;
        TxtSnapSize.Text          = opts.GridSnapSize.ToString();
        ChkShowDefaultProps.IsChecked = opts.ShowDefaultProperties;
    }

    // ── Save ─────────────────────────────────────────────────────────────────

    public void Save()
    {
        var opts = XamlDesignerOptions.Instance;

        opts.AutoPreviewEnabled  = ChkAutoPreview.IsChecked == true;
        opts.AutoPreviewDelayMs  = (int)PreviewDelaySlider.Value;
        opts.DefaultViewMode     = ((ComboBoxItem?)ViewModeCombo.SelectedItem)?.Tag as string ?? "Split";
        opts.SnapToGrid          = ChkSnapToGrid.IsChecked == true;
        opts.ShowDefaultProperties = ChkShowDefaultProps.IsChecked == true;

        if (int.TryParse(TxtSnapSize.Text, out var snap) && snap is >= 1 and <= 128)
            opts.GridSnapSize = snap;

        opts.Save();
    }

    // ── Handlers ─────────────────────────────────────────────────────────────

    private void OnPreviewDelayChanged(object sender, System.Windows.RoutedPropertyChangedEventArgs<double> e)
    {
        if (PreviewDelayLabel is not null)
            PreviewDelayLabel.Text = $"{(int)e.NewValue} ms";
    }
}
