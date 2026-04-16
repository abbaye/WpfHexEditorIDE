// ==========================================================
// Project: WpfHexEditor.Shell.Panels
// File: Panels/WhfmtBrowserPanel.xaml.cs
// Description: Code-behind for the Format Browser tool window.
//              Routes UI events to WhfmtBrowserViewModel.
//              Raises surface-level events for MainWindow to handle
//              (open in StructureEditor, export, view JSON).
// ==========================================================

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using WpfHexEditor.Core.Interfaces;
using WpfHexEditor.Core.Contracts;
using WpfHexEditor.Core.Options;
using WpfHexEditor.Editor.Core;
using WpfHexEditor.Shell.Panels.Services;
using WpfHexEditor.Shell.Panels.ViewModels;

namespace WpfHexEditor.Shell.Panels.Panels;

public partial class WhfmtBrowserPanel : UserControl
{
    private readonly WhfmtBrowserViewModel _vm;

    public WhfmtBrowserPanel()
    {
        _vm = new WhfmtBrowserViewModel();
        _vm.OpenFormatRequested  += OnVmOpenFormatRequested;
        _vm.ExportFormatRequested += OnVmExportFormatRequested;
        _vm.ViewJsonRequested    += OnVmViewJsonRequested;

        DataContext = _vm;
        InitializeComponent();
    }

    // ------------------------------------------------------------------
    // Public API
    // ------------------------------------------------------------------

    /// <summary>
    /// Raised when the user opens a format for editing.
    /// Args: key-or-path, open mode, source.
    /// </summary>
    public event EventHandler<FormatOpenRequest>? OpenFormatRequested;

    /// <summary>Raised when the user requests exporting a built-in format to disk.</summary>
    public event EventHandler<string>? ExportFormatRequested;

    /// <summary>Raised when the user requests viewing a format's raw JSON.</summary>
    public event EventHandler<string>? ViewJsonRequested;

    /// <summary>
    /// Wires up catalog data sources and applies initial settings.
    /// Call once, on the UI thread, before the panel is made visible.
    /// </summary>
    public void SetCatalog(
        IEmbeddedFormatCatalog  embCatalog,
        IFormatCatalogService   catalogSvc,
        WhfmtAdHocFormatService adHocSvc,
        WhfmtExplorerSettings   settings)
    {
        _vm.Initialize(embCatalog, catalogSvc, adHocSvc, settings,
                       a => Dispatcher.BeginInvoke(a));
    }

    // ------------------------------------------------------------------
    // TreeView events
    // ------------------------------------------------------------------

    private void OnTreeSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is WhfmtFormatItemVm item)
            _vm.OnItemSelected(item);
    }

    private void OnTreeDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (GetSelectedFormatItem() is { } item)
            _vm.RequestOpenFormat(item, FormatOpenMode.Editable);
    }

    // ------------------------------------------------------------------
    // ListView events
    // ------------------------------------------------------------------

    private void OnListSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (FormatList.SelectedItem is WhfmtFormatItemVm item)
            _vm.OnItemSelected(item);
    }

    private void OnListDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (FormatList.SelectedItem is WhfmtFormatItemVm item)
            _vm.RequestOpenFormat(item, FormatOpenMode.Editable);
    }

    // ------------------------------------------------------------------
    // VM event relay
    // ------------------------------------------------------------------

    private void OnVmOpenFormatRequested(object? sender, FormatOpenRequest req)
    {
        if (req.Mode == FormatOpenMode.AddUserFormat)
        {
            // Handle file picker here (requires UI thread)
            var dlg = new OpenFileDialog
            {
                Title            = "Add Format Definition",
                Filter           = "Whfmt definitions (*.whfmt)|*.whfmt",
                Multiselect      = false,
                CheckFileExists  = true
            };
            if (dlg.ShowDialog(Window.GetWindow(this)) == true)
            {
                var err = _vm.AddFormatFromPath(dlg.FileName);
                if (err is not null)
                    MessageBox.Show(Window.GetWindow(this), err, "Add Format",
                                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            return;
        }

        if (req.Mode == FormatOpenMode.RevealFolder)
        {
            if (req.KeyOrPath is not null)
                System.Diagnostics.Process.Start("explorer.exe", $"\"{req.KeyOrPath}\"");
            return;
        }

        OpenFormatRequested?.Invoke(this, req);
    }

    private void OnVmExportFormatRequested(object? sender, string keyOrPath)
        => ExportFormatRequested?.Invoke(this, keyOrPath);

    private void OnVmViewJsonRequested(object? sender, string keyOrPath)
        => ViewJsonRequested?.Invoke(this, keyOrPath);

    // ------------------------------------------------------------------
    // Toolbar toggle helpers (XAML can't invert bool bindings without converter)
    // ------------------------------------------------------------------

    private void OnFlatViewToggleClick(object sender, RoutedEventArgs e)
        => _vm.IsTreeView = false;

    private void OnShowFailuresToggleClick(object sender, RoutedEventArgs e) { }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private WhfmtFormatItemVm? GetSelectedFormatItem()
    {
        // For TreeView, SelectedItem is the leaf WhfmtFormatItemVm when a format is selected
        return FormatTree.SelectedItem as WhfmtFormatItemVm;
    }
}
