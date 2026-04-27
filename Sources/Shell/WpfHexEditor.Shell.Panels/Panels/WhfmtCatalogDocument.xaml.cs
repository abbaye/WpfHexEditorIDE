// ==========================================================
// Project: WpfHexEditor.Shell.Panels
// File: Panels/WhfmtCatalogDocument.xaml.cs
// Description: Code-behind for the Format Catalog virtual document tab.
//              Implements IEditorToolbarContributor and IStatusBarContributor
//              so the catalog integrates with the IDE toolbar and status bar.
// Architecture: MVVM + interface adapters; no business logic here.
// ==========================================================

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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

public partial class WhfmtCatalogDocument : UserControl,
    IEditorToolbarContributor,
    IStatusBarContributor
{
    private readonly WhfmtCatalogViewModel _vm;

    private readonly StatusBarItem _sbTotal;
    private readonly StatusBarItem _sbSelected;

    public WhfmtCatalogDocument()
    {
        _vm = new WhfmtCatalogViewModel();
        _vm.OpenFormatsRequested   += (s, e) => OpenFormatsRequested?.Invoke(this, e);
        _vm.ExportFormatsRequested += (s, e) => ExportFormatsRequested?.Invoke(this, e);
        _vm.AddFormatRequested     += OnVmAddFormatRequested;
        _vm.PropertyChanged        += OnVmPropertyChanged;

        DataContext = _vm;
        InitializeComponent();

        // ── Toolbar items ──────────────────────────────────────────────────
        ToolbarItems =
        [
            new EditorToolbarItem { Icon = "\uE7AC", Tooltip = "Open selected", Command = new LambdaCmd(() => _vm.OpenSelectedCommand.Execute(null)) },
            new EditorToolbarItem { Icon = "\uE74E", Tooltip = "Export selected to file…", Command = new LambdaCmd(() => _vm.ExportSelectedCommand.Execute(null)) },
            new EditorToolbarItem { IsSeparator = true },
            new EditorToolbarItem { Icon = "\uE109", Tooltip = "Add format file…", Command = new LambdaCmd(() => _vm.AddFormatCommand.Execute(null)) },
            new EditorToolbarItem { Icon = "\uE72C", Tooltip = "Refresh catalog (F5)", Command = new LambdaCmd(() => _vm.RefreshCommand.Execute(null)) },
        ];

        // ── Status bar items ───────────────────────────────────────────────
        _sbTotal    = new StatusBarItem { Label = "0 formats" };
        _sbSelected = new StatusBarItem { Label = string.Empty, IsVisible = false };

        StatusBarItems = [ _sbTotal, _sbSelected ];
    }

    // ------------------------------------------------------------------
    // IEditorToolbarContributor
    // ------------------------------------------------------------------

    public ObservableCollection<EditorToolbarItem> ToolbarItems { get; }

    // ------------------------------------------------------------------
    // IStatusBarContributor
    // ------------------------------------------------------------------

    public ObservableCollection<StatusBarItem> StatusBarItems { get; }

    public void RefreshStatusBarItems()
    {
        _sbTotal.Label     = $"{_vm.TotalCount} formats";
        _sbSelected.Label  = _vm.SelectedCount > 0 ? $"{_vm.SelectedCount} selected" : string.Empty;
        _sbSelected.IsVisible = _vm.SelectedCount > 0;
    }

    // ------------------------------------------------------------------
    // Public API
    // ------------------------------------------------------------------

    public event EventHandler<IReadOnlyList<string>>? OpenFormatsRequested;
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

    private void OnVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(WhfmtCatalogViewModel.SelectedCount)
                           or nameof(WhfmtCatalogViewModel.TotalCount))
            RefreshStatusBarItems();
    }
}

file sealed class LambdaCmd(Action execute) : ICommand
{
    public event EventHandler? CanExecuteChanged { add { } remove { } }
    public bool CanExecute(object? parameter) => true;
    public void Execute(object? parameter) => execute();
}
