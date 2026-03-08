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
        InitializeComponent();
    }

    /// <summary>Populates the page controls from current options.</summary>
    public void Load()
    {
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
