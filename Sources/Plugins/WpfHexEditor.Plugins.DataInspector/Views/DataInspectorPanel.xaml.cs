// ==========================================================
// Project: WpfHexEditor.Plugins.DataInspector
// File: DataInspectorPanel.xaml.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-06
// Description:
//     Code-behind for the Data Inspector panel.
//     Displays byte data interpreted in multiple numeric/string/date formats.
//
// Architecture Notes:
//     Delegates all binding logic to DataInspectorViewModel (from HexEditor).
//     Implements IDataInspectorPanel for automatic wiring by HexEditor.
// ==========================================================

using System.Windows.Controls;
using WpfHexEditor.Core.Interfaces;
using WpfHexEditor.HexEditor.ViewModels;

namespace WpfHexEditor.Plugins.DataInspector.Views;

/// <summary>
/// Panel for inspecting byte data in multiple formats.
/// Shows integers, floats, dates, network addresses, GUIDs, colors, etc.
/// </summary>
public partial class DataInspectorPanel : UserControl, IDataInspectorPanel
{
    private readonly DataInspectorViewModel _viewModel;

    public DataInspectorPanel()
    {
        InitializeComponent();
        _viewModel = new DataInspectorViewModel();
        DataContext = _viewModel;
    }

    /// <summary>
    /// Updates the inspector with new byte data from the hex editor.
    /// </summary>
    public void UpdateBytes(byte[] bytes) => _viewModel?.UpdateBytes(bytes);

    /// <summary>
    /// Clears all inspector values.
    /// </summary>
    public void Clear() => _viewModel?.UpdateBytes(null);

    /// <summary>
    /// Returns the backing ViewModel for advanced usage.
    /// </summary>
    public DataInspectorViewModel ViewModel => _viewModel;
}
