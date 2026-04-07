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
using WpfHexEditor.Core.ViewModels;

namespace WpfHexEditor.Plugins.DocumentStructure.ViewModels;

/// <summary>
/// ViewModel wrapping a <see cref="DocumentStructureNode"/> for the tree panel.
/// </summary>
public sealed class StructureNodeVm : ViewModelBase
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
    public object? Tag { get; set; }
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
        Tag = node.Tag;

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
        Tag = source.Tag;
    }

    // ── Icon Resolution ─────────────────────────────────────────────────────

    private static string ResolveIcon(string kind, string? explicitGlyph)
    {
        if (!string.IsNullOrEmpty(explicitGlyph)) return explicitGlyph!;
        return KindToIcon.TryGetValue(kind, out var glyph) ? glyph : "\uE946"; // Help
    }

    // Letter-badge labels rendered inside a colored Border (VS Code-style outline icons).
    private static readonly Dictionary<string, string> KindToIcon = new(StringComparer.OrdinalIgnoreCase)
    {
        ["class"]          = "C",
        ["struct"]         = "S",
        ["record"]         = "R",
        ["interface"]      = "I",
        ["method"]         = "M",
        ["function"]       = "M",
        ["constructor"]    = "C",
        ["property"]       = "P",
        ["field"]          = "F",
        ["variable"]       = "V",
        ["enum"]           = "E",
        ["enummember"]     = "e",
        ["event"]          = "Ev",
        ["namespace"]      = "N",
        ["module"]         = "N",
        ["heading"]        = "H",
        ["section"]        = "§",
        ["element"]        = "X",
        ["key"]            = "K",
        ["block"]          = "B",
        ["array"]          = "[]",
        ["object"]         = "O",
        ["region"]         = "#",
        ["constant"]       = "K",
        ["typeparameter"]  = "T",
        ["file"]           = "F",
    };

    // ── INPC ────────────────────────────────────────────────────────────────


}
