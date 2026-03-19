// ==========================================================
// Project: WpfHexEditor.Editor.XamlDesigner
// File: LiveTreeNode.cs
// Author: Derek Tremblay
// Created: 2026-03-17
// Description:
//     ViewModel wrapping a live WPF DependencyObject for display in the
//     Visual Tree mode of the XamlOutlinePanel.
//     Shows the runtime type name, optional x:Name / Name, and the actual
//     dimensions if the object is a FrameworkElement.
//
// Architecture Notes:
//     INPC. IsSelected / IsExpanded support two-way TreeView binding.
//     Children populated by LiveVisualTreeService.
// ==========================================================

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;

namespace WpfHexEditor.Editor.XamlDesigner.ViewModels;

/// <summary>
/// View model for a single node in the live visual tree.
/// </summary>
public sealed class LiveTreeNode : INotifyPropertyChanged
{
    private bool _isSelected;
    private bool _isExpanded;

    // ── Constructor ───────────────────────────────────────────────────────────

    public LiveTreeNode(DependencyObject source)
    {
        Source      = source;
        TypeName    = source.GetType().Name;
        ElementName = (source as FrameworkElement)?.Name;

        if (source is FrameworkElement fe && fe.ActualWidth > 0)
            DimensionsLabel = $"{fe.ActualWidth:F0}×{fe.ActualHeight:F0}";

        DisplayLabel = BuildLabel();
    }

    // ── Properties ────────────────────────────────────────────────────────────

    public DependencyObject Source          { get; }
    public string           TypeName        { get; }
    public string?          ElementName     { get; }
    public string?          DimensionsLabel { get; }
    public string           DisplayLabel    { get; }

    public ObservableCollection<LiveTreeNode> Children { get; } = new();

    public bool IsSelected
    {
        get => _isSelected;
        set { if (_isSelected == value) return; _isSelected = value; OnPropertyChanged(); }
    }

    public bool IsExpanded
    {
        get => _isExpanded;
        set { if (_isExpanded == value) return; _isExpanded = value; OnPropertyChanged(); }
    }

    // ── INPC ──────────────────────────────────────────────────────────────────

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    // ── Private ───────────────────────────────────────────────────────────────

    private string BuildLabel()
    {
        var label = TypeName;
        if (!string.IsNullOrEmpty(ElementName))
            label += $" [{ElementName}]";
        if (!string.IsNullOrEmpty(DimensionsLabel))
            label += $" ({DimensionsLabel})";
        return label;
    }
}
