// ==========================================================
// Project: WpfHexEditor.Plugins.StructureOverlay
// File: StructureOverlayPanel.xaml.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-06
// Description:
//     Structure overlay panel migrated from Panels.BinaryAnalysis.
//     Displays structure overlays on binary data as a TreeView.
// ==========================================================

using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Microsoft.Win32;
using WpfHexEditor.Core.Interfaces;
using WpfHexEditor.Core.Models.StructureOverlay;
using WpfHexEditor.Core.Services;
using WpfHexEditor.Core.ViewModels;
using WpfHexEditor.SDK.UI;
using WpfHexEditor.Plugins.StructureOverlay.Properties;

namespace WpfHexEditor.Plugins.StructureOverlay.Views;

/// <summary>
/// Panel for managing and displaying structure overlays on binary data.
/// </summary>
public partial class StructureOverlayPanel : UserControl, IStructureOverlayPanel
{
    #region Fields

    private readonly StructureOverlayViewModel _viewModel;
    private readonly StructureOverlayService _service;
    private byte[]? _currentFileBytes;
    private ToolbarOverflowManager _overflowManager = null!;

    #endregion

    #region Events

    /// <summary>Fired when a new overlay is added.</summary>
    public event EventHandler<OverlayStructure>? OnOverlayAdded;

    /// <summary>Fired when all overlays are cleared.</summary>
    public event EventHandler? OnAllOverlaysCleared;

    /// <summary>Fired when a field is selected (for highlighting in the hex editor).</summary>
    public event EventHandler<OverlayField>? OnFieldSelectedForHighlight;

    /// <summary>Fired when a structure is selected.</summary>
    public event EventHandler<OverlayStructure>? OnStructureSelectedForHighlight;

    #endregion

    #region Constructor

    public StructureOverlayPanel()
    {
        InitializeComponent();

        _viewModel = new StructureOverlayViewModel();
        _service   = new StructureOverlayService();
        DataContext = _viewModel;

        _viewModel.OnFieldSelected     += ViewModel_OnFieldSelected;
        _viewModel.OnStructureSelected += ViewModel_OnStructureSelected;
        StructuresTreeView.SelectedItemChanged += StructuresTreeView_SelectedItemChanged;

        Loaded += (_, _) =>
        {
            _overflowManager = new ToolbarOverflowManager(
                toolbarContainer:      ToolbarBorder,
                alwaysVisiblePanel:    ToolbarRightPanel,
                overflowButton:        ToolbarOverflowButton,
                overflowMenu:          OverflowContextMenu,
                groupsInCollapseOrder: new FrameworkElement[] { TbgStructureAdd });
            Dispatcher.InvokeAsync(_overflowManager.CaptureNaturalWidths, DispatcherPriority.Loaded);
        };
    }

    #endregion

    #region Public properties

    /// <summary>Gets the underlying ViewModel.</summary>
    public StructureOverlayViewModel ViewModel => _viewModel;

    #endregion

    #region IStructureOverlayPanel

    /// <inheritdoc/>
    public void UpdateFileBytes(byte[] fileBytes) => _currentFileBytes = fileBytes;

    /// <summary>
    /// Clears the cached file bytes to allow the GC to reclaim the buffer.
    /// Called automatically after overlay creation and can be called manually
    /// when the panel knows the bytes are no longer needed.
    /// </summary>
    public void ReleaseFileBytes() => _currentFileBytes = null;

    /// <inheritdoc/>
    public void AddOverlayFromFormat(JsonObject formatDefinition)
    {
        if (_currentFileBytes is null || formatDefinition is null) return;

        var overlay = _service.CreateOverlayFromFormat(formatDefinition, _currentFileBytes);

        // Release the byte buffer immediately after parsing — the overlay itself stores
        // only the parsed field offsets/lengths, not the raw bytes.
        ReleaseFileBytes();

        if (overlay != null)
        {
            _viewModel.AddStructure(overlay);
            OnOverlayAdded?.Invoke(this, overlay);
        }
    }

    /// <inheritdoc/>
    public void AddCustomOverlay(string name, List<(string name, string type, int length)> fields, long startOffset = 0)
    {
        var overlay = _service.CreateCustomOverlay(name, fields, startOffset);
        if (overlay != null)
        {
            _viewModel.AddStructure(overlay);
            OnOverlayAdded?.Invoke(this, overlay);
        }
    }

    /// <inheritdoc/>
    public void ClearAllOverlays()
    {
        _viewModel.ClearAll();
        OnAllOverlaysCleared?.Invoke(this, EventArgs.Empty);
    }

    #endregion

    #region Event handlers — toolbar

    private void AddStructureButton_Click(object sender, RoutedEventArgs e)
    {
        var fields = new List<(string, string, int)>
        {
            ("Header",      "uint32", 4),
            ("Version",     "uint16", 2),
            ("Flags",       "uint16", 2),
            ("Data Length", "uint32", 4)
        };
        AddCustomOverlay("Custom Structure", fields, 0);
    }

    private void LoadFormatButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Format Definitions (*.json)|*.json|All Files (*.*)|*.*",
            Title  = "Load Format Definition"
        };

        if (dialog.ShowDialog() != true) return;

        try
        {
            var json      = System.IO.File.ReadAllText(dialog.FileName);
            var formatDef = JsonNode.Parse(json)!.AsObject();
            AddOverlayFromFormat(formatDef);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"{StructureOverlayResources.StructureOverlay_Error_LoadFailed}:\n{ex.Message}",
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ClearAllButton_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.Structures.Count == 0) return;

        var result = MessageBox.Show(
            StructureOverlayResources.StructureOverlay_Confirm_RemoveAll,
            "Confirm",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
            ClearAllOverlays();
    }

    #endregion

    #region Event handlers — TreeView

    private void StructuresTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is OverlayStructure structure)
            _viewModel.SelectedStructure = structure;
        else if (e.NewValue is OverlayField field)
            _viewModel.SelectedField = field;
    }

    private void ViewModel_OnFieldSelected(object? sender, OverlayField field)
    {
        if (field != null)
            OnFieldSelectedForHighlight?.Invoke(this, field);
    }

    private void ViewModel_OnStructureSelected(object? sender, OverlayStructure structure)
    {
        if (structure != null)
            OnStructureSelectedForHighlight?.Invoke(this, structure);
    }

    #endregion

    // ── Toolbar overflow ─────────────────────────────────────────────────────

    private void OnToolbarSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (e.WidthChanged) _overflowManager?.Update();
    }

    private void OnOverflowButtonClick(object sender, RoutedEventArgs e)
    {
        OverflowContextMenu.PlacementTarget = ToolbarOverflowButton;
        OverflowContextMenu.Placement       = System.Windows.Controls.Primitives.PlacementMode.Bottom;
        OverflowContextMenu.IsOpen          = true;
    }

    private void OnOverflowMenuOpened(object sender, RoutedEventArgs e)
    {
        _overflowManager?.SyncMenuVisibility();
    }
}
