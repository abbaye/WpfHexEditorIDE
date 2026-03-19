// ==========================================================
// Project: WpfHexEditor.Editor.XamlDesigner
// File: ResourceBrowserPanel.xaml.cs
// Author: Derek Tremblay
// Created: 2026-03-17
// Description:
//     Code-behind for the Resource Browser dockable panel.
//     Wires ViewModel events and exposes FindUsagesRequested for plugin wiring.
//
// Architecture Notes:
//     VS-Like Panel Pattern. Follows OnLoaded/OnUnloaded lifecycle rule.
// ==========================================================

using System.Windows;
using System.Windows.Controls;
using WpfHexEditor.Editor.XamlDesigner.ViewModels;

namespace WpfHexEditor.Editor.XamlDesigner.Panels;

/// <summary>
/// Resource Browser dockable panel — shows all application resources.
/// </summary>
public partial class ResourceBrowserPanel : UserControl
{
    private ResourceBrowserPanelViewModel _vm = new();

    public ResourceBrowserPanel()
    {
        InitializeComponent();
        DataContext = _vm;
        Loaded   += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public ResourceBrowserPanelViewModel ViewModel => _vm;

    public event EventHandler<string>? FindUsagesRequested;

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _vm.FindUsagesRequested -= OnFindUsages;
        _vm.FindUsagesRequested += OnFindUsages;

        // Auto-scan on first load.
        if (_vm.EntriesView.IsEmpty)
            _vm.Scan();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _vm.FindUsagesRequested -= OnFindUsages;
    }

    private void OnFindUsages(object? sender, string key)
        => FindUsagesRequested?.Invoke(this, key);
}
