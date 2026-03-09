// ==========================================================
// Project: WpfHexEditor.Plugins.DataInspector
// File: DataInspectorOptionsPage.xaml.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-06
// Description:
//     Code-behind for DataInspectorOptionsPage.
//     Loads from and saves to DataInspectorOptions.Instance.
// ==========================================================

using System.Windows.Controls;

namespace WpfHexEditor.Plugins.DataInspector.Options;

public partial class DataInspectorOptionsPage : UserControl
{
    public DataInspectorOptionsPage()
    {
        // Application.LoadComponent() (called by InitializeComponent()) throws NRE internally
        // when the plugin is loaded in a custom AssemblyLoadContext and the pack URI resource
        // can't be resolved. The throw happens inside WPF code BEFORE any catch block runs,
        // so VS "break when thrown" fires even though the exception would be caught upstream.
        //
        // Fix: pre-check via GetResourceStream(), which returns null without throwing when
        // the resource is unavailable. Skip InitializeComponent() entirely in that case;
        // Load()'s null guard (if AutoRefreshCheckBox is null) handles the empty-control case.
        var uri = new System.Uri(
            "/WpfHexEditor.Plugins.DataInspector;component/options/datainspectoroptionspage.xaml",
            System.UriKind.Relative);

        if (Application.GetResourceStream(uri) is not null)
        {
            try { InitializeComponent(); }
            catch { /* Unexpected BAML failure — fields stay null; Load() guard handles it */ }
        }
    }

    /// <summary>Populates the page controls from current options.</summary>
    public void Load()
    {
        // Guard: named fields may be null if InitializeComponent() failed to resolve
        // the BAML resource (e.g., custom AssemblyLoadContext in the plugin host).
        if (AutoRefreshCheckBox is null) return;

        var opts = DataInspectorOptions.Instance;
        AutoRefreshCheckBox.IsChecked   = opts.AutoRefresh;
        LittleEndianCheckBox.IsChecked  = opts.DefaultLittleEndian;
        ShowByteChartCheckBox.IsChecked = opts.ShowByteChart;
        MaxStringBytesSlider.Value      = opts.MaxStringBytes;
        SyncChartPositionCombo(opts.ChartPosition);
    }

    /// <summary>Persists page control values back to DataInspectorOptions and saves.</summary>
    public void Save()
    {
        var opts = DataInspectorOptions.Instance;
        opts.AutoRefresh         = AutoRefreshCheckBox.IsChecked == true;
        opts.DefaultLittleEndian = LittleEndianCheckBox.IsChecked == true;
        opts.ShowByteChart       = ShowByteChartCheckBox.IsChecked == true;
        opts.MaxStringBytes      = (int)MaxStringBytesSlider.Value;
        opts.ChartPosition       = SelectedChartPosition();
        opts.Save();
    }

    private void SyncChartPositionCombo(ChartPosition pos)
    {
        foreach (System.Windows.Controls.ComboBoxItem item in ChartPositionComboBox.Items)
        {
            if (item.Tag?.ToString() == pos.ToString())
            {
                ChartPositionComboBox.SelectedItem = item;
                return;
            }
        }
    }

    private ChartPosition SelectedChartPosition()
    {
        if (ChartPositionComboBox.SelectedItem is System.Windows.Controls.ComboBoxItem item &&
            Enum.TryParse<ChartPosition>(item.Tag?.ToString(), out var pos))
            return pos;
        return ChartPosition.Left;
    }
}
