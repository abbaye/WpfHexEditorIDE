// ==========================================================
// Project: WpfHexEditor.Editor.XamlDesigner
// File: XamlToolboxPanel.xaml.cs
// Author: Derek Tremblay
// Created: 2026-03-17
// Description:
//     Code-behind for the XAML Toolbox dockable panel.
//     Manages drag-and-drop initiation when the user drags a ToolboxItem
//     from the list onto the design canvas.
//
// Architecture Notes:
//     VS-Like Panel Pattern — 26px toolbar + filtered list content area.
//     Drag initiated on PreviewMouseMove with ToolboxDropService.DragDropFormat data.
//     Follows OnLoaded/OnUnloaded lifecycle rule: never nulls the ViewModel.
// ==========================================================

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WpfHexEditor.Editor.XamlDesigner.Models;
using WpfHexEditor.Editor.XamlDesigner.Services;
using WpfHexEditor.Editor.XamlDesigner.ViewModels;

namespace WpfHexEditor.Editor.XamlDesigner.Panels;

/// <summary>
/// XAML Toolbox dockable panel — lists all available WPF controls for drag-to-canvas.
/// </summary>
public partial class XamlToolboxPanel : UserControl
{
    // ── State ─────────────────────────────────────────────────────────────────

    private XamlToolboxPanelViewModel _vm = new();
    private Point _dragStartPoint;
    private bool  _isDragStarted;

    // ── Constructor ───────────────────────────────────────────────────────────

    public XamlToolboxPanel()
    {
        InitializeComponent();
        DataContext = _vm;

        Loaded   += OnLoaded;
        Unloaded += OnUnloaded;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Exposes the ViewModel for external access.</summary>
    public XamlToolboxPanelViewModel ViewModel => _vm;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ToolboxList.PreviewMouseLeftButtonDown -= OnListMouseDown;
        ToolboxList.PreviewMouseLeftButtonDown += OnListMouseDown;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        ToolboxList.PreviewMouseLeftButtonDown -= OnListMouseDown;
    }

    // ── Drag initiation ───────────────────────────────────────────────────────

    private void OnListMouseDown(object sender, MouseButtonEventArgs e)
    {
        _dragStartPoint = e.GetPosition(null);
        _isDragStarted  = false;
    }

    private void OnListPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed) return;
        if (_isDragStarted) return;

        var current = e.GetPosition(null);
        if (Math.Abs(current.X - _dragStartPoint.X) < SystemParameters.MinimumHorizontalDragDistance
            && Math.Abs(current.Y - _dragStartPoint.Y) < SystemParameters.MinimumVerticalDragDistance)
            return;

        if (_vm.SelectedItem is not ToolboxItem item) return;

        _isDragStarted = true;

        var data = new DataObject(ToolboxDropService.DragDropFormat, item);
        DragDrop.DoDragDrop(ToolboxList, data, DragDropEffects.Copy);
    }
}
