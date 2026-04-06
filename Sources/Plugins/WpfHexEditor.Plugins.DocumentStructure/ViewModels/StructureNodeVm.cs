// ==========================================================
// Project: WpfHexEditor.Plugins.DocumentStructure
// File: ViewModels/StructureNodeVm.cs
// Created: 2026-04-05
// Description:
//     ViewModel for a single node in the Document Structure tree.
//     Provides INPC for IsExpanded/IsSelected/IsHighlighted/Visibility,
//     and a static icon mapping from Kind string to Segoe MDL2 glyph.
//
// Architecture Notes:
//     Immutable data (Name, Kind, etc.) set at construction.
//     Mutable UI state (IsExpanded, IsSelected, IsHighlighted, Visibility)
//     supports two-way binding from the TreeView.
// ==========================================================

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using WpfHexEditor.SDK.ExtensionPoints.DocumentStructure;

namespace WpfHexEditor.Plugins.DocumentStructure.ViewModels;

/// <summary>
/// ViewModel wrapping a <see cref="DocumentStructureNode"/> for the tree panel.
/// </summary>
public sealed class StructureNodeVm : INotifyPropertyChanged
{
    private bool _isExpanded;
    private bool _isSelected;
    private bool _isHighlighted;
    private Visibility _visibility = Visibility.Visible;

    public string Name { get; }
    public string Kind { get; }
    public string? Detail { get; }
    public string IconGlyph { get; }
    public int StartLine { get; }
    public int StartColumn { get; }
    public int EndLine { get; }
    public long ByteOffset { get; }
    public long ByteLength { get; }
    public int IndentLevel { get; init; }
    public ObservableCollection<StructureNodeVm> Children { get; } = [];

    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetField(ref _isExpanded, value);
    }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetField(ref _isSelected, value);
    }

    public bool IsHighlighted
    {
        get => _isHighlighted;
        set => SetField(ref _isHighlighted, value);
    }

    public Visibility Visibility
    {
        get => _visibility;
        set => SetField(ref _visibility, value);
    }

    public StructureNodeVm(DocumentStructureNode node)
    {
        Name = node.Name;
        Kind = node.Kind;
        Detail = node.Detail;
        IconGlyph = ResolveIcon(node.Kind, node.IconGlyph);
        StartLine = node.StartLine;
        StartColumn = node.StartColumn;
        EndLine = node.EndLine;
        ByteOffset = node.ByteOffset;
        ByteLength = node.ByteLength;

        foreach (var child in node.Children)
            Children.Add(new StructureNodeVm(child));
    }

    /// <summary>
    /// Creates a flat-mode clone with an explicit indent level (no children).
    /// </summary>
    public StructureNodeVm(StructureNodeVm source, int indentLevel)
    {
        Name = source.Name;
        Kind = source.Kind;
        Detail = source.Detail;
        IconGlyph = source.IconGlyph;
        StartLine = source.StartLine;
        StartColumn = source.StartColumn;
        EndLine = source.EndLine;
        ByteOffset = source.ByteOffset;
        ByteLength = source.ByteLength;
        IndentLevel = indentLevel;
    }

    // ── Icon Resolution ─────────────────────────────────────────────────────

    private static string ResolveIcon(string kind, string? explicitGlyph)
    {
        if (!string.IsNullOrEmpty(explicitGlyph)) return explicitGlyph!;
        return KindToIcon.TryGetValue(kind, out var glyph) ? glyph : "\uE946"; // Help
    }

    private static readonly Dictionary<string, string> KindToIcon = new(StringComparer.OrdinalIgnoreCase)
    {
        ["class"]          = "\uE8A5",  // Page
        ["struct"]         = "\uE8A5",
        ["record"]         = "\uE8A5",
        ["interface"]      = "\uE8C0",  // CalendarReply
        ["method"]         = "\uEA8C",  // Code
        ["function"]       = "\uEA8C",
        ["constructor"]    = "\uEA8C",
        ["property"]       = "\uE8FC",  // Repair
        ["field"]          = "\uEA86",
        ["variable"]       = "\uEA86",
        ["enum"]           = "\uE8EF",  // List
        ["enummember"]     = "\uE8EF",
        ["event"]          = "\uEA8F",
        ["namespace"]      = "\uE8F1",  // Library
        ["module"]         = "\uE8F1",
        ["heading"]        = "\uE8AC",  // FontSize
        ["section"]        = "\uE7C1",  // GlobalNav
        ["element"]        = "\uE8A0",  // Tag
        ["key"]            = "\uE8D7",  // Permissions
        ["block"]          = "\uE8FD",  // AllApps
        ["array"]          = "\uE8EF",  // List
        ["object"]         = "\uE8A5",  // Page
        ["region"]         = "\uE8A4",  // Trim
        ["constant"]       = "\uE8A7",
        ["typeparameter"]  = "\uE8A5",
        ["file"]           = "\uE8A5",
    };

    // ── INPC ────────────────────────────────────────────────────────────────

    public event PropertyChangedEventHandler? PropertyChanged;

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
