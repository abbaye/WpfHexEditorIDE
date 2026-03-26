// ==========================================================
// Project: WpfHexEditor.Plugins.FormatStructure
// File: Views/FormatStructurePanel.xaml.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude (Anthropic)
// Created: 2026-03-26
// Description:
//     Panel showing the full hierarchical structure tree of all
//     parsed blocks from the active .whfmt format definition.
//     Like 010 Editor "Template Results" — click to navigate.
//
// Architecture Notes:
//     Converts BlockDefinition hierarchy → StructureFieldNode tree.
//     TreeView with HierarchicalDataTemplate for expand/collapse.
//     FieldNavigateRequested event drives hex editor navigation.
// ==========================================================

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WpfHexEditor.Core.FormatDetection;

namespace WpfHexEditor.Plugins.FormatStructure.Views;

/// <summary>
/// A node in the format structure tree.
/// Leaf nodes represent individual fields; group nodes represent repeating/nested/conditional containers.
/// </summary>
public sealed class StructureFieldNode : INotifyPropertyChanged
{
    private bool _isExpanded;

    public string Name       { get; init; } = string.Empty;
    public long   Offset     { get; init; }
    public long   Length     { get; init; }
    public string BlockType  { get; init; } = "field";
    public Color  SwatchColor { get; init; } = Colors.Gray;
    public bool   IsGroup    { get; init; }

    public string OffsetHex  => Offset >= 0 ? $"0x{Offset:X}" : "";
    public string LengthText => Length > 0 ? (Length >= 1024 ? $"{Length / 1024}K" : $"{Length}") : "";
    public string Tooltip    => IsGroup
        ? $"{Name} ({Children.Count} items)"
        : $"{Name}\nOffset: 0x{Offset:X}\nLength: {Length} bytes\nType: {BlockType}";

    public ObservableCollection<StructureFieldNode> Children { get; } = [];

    public bool IsExpanded
    {
        get => _isExpanded;
        set { _isExpanded = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>
/// Format Structure Tree panel (D1).
/// Shows all parsed blocks from the active .whfmt format definition in a hierarchical tree.
/// </summary>
public partial class FormatStructurePanel : UserControl
{
    private readonly ObservableCollection<StructureFieldNode> _rootNodes = [];

    public event EventHandler<StructureFieldNode>? FieldNavigateRequested;

    public FormatStructurePanel()
    {
        InitializeComponent();
        StructureTree.ItemsSource = _rootNodes;
    }

    /// <summary>
    /// Rebuild the tree from a detected format's block definitions.
    /// </summary>
    public void LoadFormat(string formatName, List<BlockDefinition>? blocks)
    {
        _rootNodes.Clear();
        FormatNameText.Text = formatName;

        if (blocks == null || blocks.Count == 0)
        {
            BlockCountText.Text = "0 fields";
            return;
        }

        int fieldCount = 0;
        foreach (var block in blocks)
        {
            var node = BuildNode(block, ref fieldCount);
            if (node != null)
                _rootNodes.Add(node);
        }

        BlockCountText.Text = $"{fieldCount} fields";
    }

    /// <summary>Clear the tree (e.g., when switching editors).</summary>
    public void Clear()
    {
        _rootNodes.Clear();
        FormatNameText.Text = "No format detected";
        BlockCountText.Text = "";
    }

    private StructureFieldNode? BuildNode(BlockDefinition block, ref int fieldCount)
    {
        if (block == null) return null;

        var type = block.Type?.ToLowerInvariant() ?? "field";
        var color = ParseColor(block.Color);

        switch (type)
        {
            case "repeating":
            {
                var group = new StructureFieldNode
                {
                    Name        = block.Name ?? "Repeating",
                    Offset      = -1,
                    Length      = 0,
                    BlockType   = "repeating",
                    SwatchColor = color,
                    IsGroup     = true,
                    IsExpanded  = true
                };
                if (block.Fields != null)
                    foreach (var f in block.Fields)
                    {
                        var child = BuildNode(f, ref fieldCount);
                        if (child != null) group.Children.Add(child);
                    }
                return group;
            }

            case "conditional":
            {
                var group = new StructureFieldNode
                {
                    Name        = block.Name ?? "Conditional",
                    Offset      = -1,
                    Length      = 0,
                    BlockType   = "conditional",
                    SwatchColor = color,
                    IsGroup     = true,
                    IsExpanded  = true
                };
                if (block.Then != null)
                    foreach (var t in block.Then)
                    {
                        var child = BuildNode(t, ref fieldCount);
                        if (child != null) group.Children.Add(child);
                    }
                if (block.Else != null)
                    foreach (var e in block.Else)
                    {
                        var child = BuildNode(e, ref fieldCount);
                        if (child != null) group.Children.Add(child);
                    }
                return group;
            }

            case "loop":
            {
                var group = new StructureFieldNode
                {
                    Name        = block.Name ?? "Loop",
                    Offset      = -1,
                    Length      = 0,
                    BlockType   = "loop",
                    SwatchColor = color,
                    IsGroup     = true,
                    IsExpanded  = false
                };
                if (block.Body != null)
                    foreach (var b in block.Body)
                    {
                        var child = BuildNode(b, ref fieldCount);
                        if (child != null) group.Children.Add(child);
                    }
                return group;
            }

            case "nested":
            {
                return new StructureFieldNode
                {
                    Name        = block.Name ?? block.StructRef ?? "Nested",
                    Offset      = -1,
                    Length      = 0,
                    BlockType   = "nested",
                    SwatchColor = color,
                    IsGroup     = true
                };
            }

            case "pointer":
            {
                fieldCount++;
                return new StructureFieldNode
                {
                    Name        = block.Label ?? block.Name ?? $"-> {block.TargetVar}",
                    Offset      = -1,
                    Length      = 0,
                    BlockType   = "pointer",
                    SwatchColor = Colors.MediumSlateBlue
                };
            }

            case "action":
            case "computefromvariables":
                // Internal blocks — skip in tree
                return null;

            case "metadata":
                fieldCount++;
                return new StructureFieldNode
                {
                    Name        = block.Name ?? block.Variable ?? "Metadata",
                    Offset      = -1,
                    Length      = 0,
                    BlockType   = "metadata",
                    SwatchColor = Colors.DimGray
                };

            default: // "field", "signature"
            {
                fieldCount++;
                long offset = ResolveOffset(block);
                long length = ResolveLength(block);

                return new StructureFieldNode
                {
                    Name        = block.Name ?? block.Description ?? "Field",
                    Offset      = offset,
                    Length      = length,
                    BlockType   = block.ValueType ?? type,
                    SwatchColor = color
                };
            }
        }
    }

    private static long ResolveOffset(BlockDefinition block)
    {
        if (block.Offset is int i) return i;
        if (block.Offset is long l) return l;
        if (block.Offset is string s && long.TryParse(s, out var p)) return p;
        return -1;
    }

    private static long ResolveLength(BlockDefinition block)
    {
        if (block.Length is int i) return i;
        if (block.Length is long l) return l;
        if (block.Length is string s && long.TryParse(s, out var p)) return p;
        return 0;
    }

    private static Color ParseColor(string? hex)
    {
        if (string.IsNullOrEmpty(hex) || hex.Length < 7 || hex[0] != '#')
            return Colors.Gray;

        try
        {
            byte r = Convert.ToByte(hex.Substring(1, 2), 16);
            byte g = Convert.ToByte(hex.Substring(3, 2), 16);
            byte b = Convert.ToByte(hex.Substring(5, 2), 16);
            return Color.FromRgb(r, g, b);
        }
        catch
        {
            return Colors.Gray;
        }
    }

    private void OnTreeSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is StructureFieldNode node && node.Offset >= 0)
            FieldNavigateRequested?.Invoke(this, node);
    }
}
