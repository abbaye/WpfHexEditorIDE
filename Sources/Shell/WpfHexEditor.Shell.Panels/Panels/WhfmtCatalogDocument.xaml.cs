// ==========================================================
// Project: WpfHexEditor.Shell.Panels
// File: Panels/WhfmtCatalogDocument.xaml.cs
// Description: Code-behind for the Format Catalog virtual document tab.
//              Thin shell — routes DataGrid selection and double-click to the VM.
// ==========================================================

using System;
using System.Collections.Generic;
using System.Linq;
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

public partial class WhfmtCatalogDocument : UserControl
{
    private readonly WhfmtCatalogViewModel _vm;

    public WhfmtCatalogDocument()
    {
        _vm = new WhfmtCatalogViewModel();
        _vm.OpenFormatsRequested   += (s, e) => OpenFormatsRequested?.Invoke(this, e);
        _vm.ExportFormatsRequested += (s, e) => ExportFormatsRequested?.Invoke(this, e);
        _vm.AddFormatRequested     += OnVmAddFormatRequested;

        DataContext = _vm;
        InitializeComponent();
    }

    // ------------------------------------------------------------------
    // Public API
    // ------------------------------------------------------------------

    /// <summary>Raised when the user requests opening one or more formats.</summary>
    public event EventHandler<IReadOnlyList<string>>? OpenFormatsRequested;

    /// <summary>Raised when the user requests exporting one or more formats.</summary>
    public event EventHandler<IReadOnlyList<string>>? ExportFormatsRequested;

    public void SetCatalog(
        IEmbeddedFormatCatalog  embCatalog,
        IFormatCatalogService   catalogSvc,
        WhfmtAdHocFormatService adHocSvc,
        WhfmtExplorerSettings   settings)
    {
        _vm.Initialize(embCatalog, catalogSvc, adHocSvc, settings);
    }

    // ------------------------------------------------------------------
    // DataGrid events
    // ------------------------------------------------------------------

    private void OnGridSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var selected = CatalogGrid.SelectedItems
            .OfType<WhfmtFormatItemVm>()
            .ToList();
        _vm.SetMultiSelection(selected);
    }

    private void OnGridDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (CatalogGrid.SelectedItem is WhfmtFormatItemVm item)
            OpenFormatsRequested?.Invoke(this, [item.ResourceKey ?? item.FilePath ?? item.Name]);
    }

    // ------------------------------------------------------------------
    // VM events
    // ------------------------------------------------------------------

    private void OnVmAddFormatRequested(object? sender, EventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title           = "Add Format Definition",
            Filter          = "Whfmt definitions (*.whfmt)|*.whfmt",
            Multiselect     = false,
            CheckFileExists = true
        };
        if (dlg.ShowDialog() == true)
            OpenFormatsRequested?.Invoke(this, [dlg.FileName]);
    }
}
