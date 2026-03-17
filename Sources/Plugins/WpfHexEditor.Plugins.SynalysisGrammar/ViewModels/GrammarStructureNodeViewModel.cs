// ==========================================================
// Project: WpfHexEditor.Plugins.SynalysisGrammar
// File: ViewModels/GrammarStructureNodeViewModel.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     ViewModel for a single node in the grammar structure TreeView.
//     Represents a UfwbStructure, a typed field (Number/String/Binary),
//     or a StructRef entry, building a recursive hierarchy from UfwbGrammar.
//
// Architecture Notes:
//     Pattern: Composite (tree node ViewModel)
//     - FromGrammar() is the only factory entry point.
//     - Children are built recursively so the TreeView can use
//       HierarchicalDataTemplate without any extra code-behind.
//     - Keeps all BCL types; no WPF imports needed here.
// ==========================================================

using System.Collections.ObjectModel;
using WpfHexEditor.Core.SynalysisGrammar;

namespace WpfHexEditor.Plugins.SynalysisGrammar.ViewModels;

/// <summary>
/// Represents one node in the grammar structure browser TreeView.
/// </summary>
public sealed class GrammarStructureNodeViewModel
{
    /// <summary>Display name (field/structure name or fallback to id).</summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>Short type label shown in muted colour, e.g. "struct", "uint32", "string".</summary>
    public string TypeLabel { get; init; } = string.Empty;

    /// <summary>Segoe MDL2 Assets glyph for the node kind.</summary>
    public string Icon { get; init; } = string.Empty;

    /// <summary>Optional tooltip (description text from the grammar).</summary>
    public string Tooltip { get; init; } = string.Empty;

    /// <summary>Child nodes (non-empty only for UfwbStructure nodes).</summary>
    public ObservableCollection<GrammarStructureNodeViewModel> Children { get; } = [];

    // -- Factory -----------------------------------------------------------

    /// <summary>
    /// Builds the full node tree from a <see cref="UfwbGrammar"/>.
    /// Returns an empty collection when <paramref name="grammar"/> is null.
    /// </summary>
    public static ObservableCollection<GrammarStructureNodeViewModel> FromGrammar(UfwbGrammar? grammar)
    {
        var result = new ObservableCollection<GrammarStructureNodeViewModel>();
        if (grammar is null) return result;

        foreach (var structure in grammar.Structures)
            result.Add(FromStructure(structure));

        return result;
    }

    // -- Recursive builders ------------------------------------------------

    private static GrammarStructureNodeViewModel FromStructure(UfwbStructure s)
    {
        var node = new GrammarStructureNodeViewModel
        {
            DisplayName = NameOrId(s.Name, s.Id),
            TypeLabel   = BuildStructTypeLabel(s),
            Icon        = "\uE8EF",   // GroupList
            Tooltip     = s.Description,
        };

        foreach (var child in s.Elements)
            node.Children.Add(FromElement(child));

        return node;
    }

    private static GrammarStructureNodeViewModel FromElement(UfwbElement element)
        => element switch
        {
            UfwbStructure  s   => FromStructure(s),
            UfwbNumber     n   => FromNumber(n),
            UfwbString     str => FromString(str),
            UfwbBinary     b   => FromBinary(b),
            UfwbStructRef  r   => FromStructRef(r),
            _                  => new GrammarStructureNodeViewModel
                                  {
                                      DisplayName = NameOrId(element.Name, element.Id),
                                      TypeLabel   = "unknown",
                                      Icon        = "\uE8A5",
                                      Tooltip     = element.Description,
                                  }
        };

    private static GrammarStructureNodeViewModel FromNumber(UfwbNumber n)
        => new()
        {
            DisplayName = NameOrId(n.Name, n.Id),
            TypeLabel   = BuildNumberTypeLabel(n),
            Icon        = "\uE8A4",   // Field / numeric
            Tooltip     = n.Description,
        };

    private static GrammarStructureNodeViewModel FromString(UfwbString s)
        => new()
        {
            DisplayName = NameOrId(s.Name, s.Id),
            TypeLabel   = string.IsNullOrEmpty(s.Length) ? "string" : $"string[{s.Length}]",
            Icon        = "\uE8AB",   // Font / text
            Tooltip     = s.Description,
        };

    private static GrammarStructureNodeViewModel FromBinary(UfwbBinary b)
        => new()
        {
            DisplayName = NameOrId(b.Name, b.Id),
            TypeLabel   = string.IsNullOrEmpty(b.Length) ? "binary" : $"binary[{b.Length}]",
            Icon        = "\uE8B5",   // Binary / attach
            Tooltip     = b.Description,
        };

    private static GrammarStructureNodeViewModel FromStructRef(UfwbStructRef r)
    {
        var repeat = BuildRepeatLabel(r.RepeatMin, r.RepeatMax);
        return new GrammarStructureNodeViewModel
        {
            DisplayName = NameOrId(r.Name, r.Id),
            TypeLabel   = $"ref({r.StructureRef}){repeat}",
            Icon        = "\uE8C9",   // Link / reference
            Tooltip     = r.Description,
        };
    }

    // -- Label helpers -----------------------------------------------------

    private static string NameOrId(string name, string id)
        => string.IsNullOrWhiteSpace(name) ? $"[{id}]" : name;

    private static string BuildNumberTypeLabel(UfwbNumber n)
    {
        var baseType = n.Type switch
        {
            "float"  => "float",
            "double" => "double",
            _        => string.IsNullOrEmpty(n.Signed) || n.Signed == "yes"
                            ? $"int{(string.IsNullOrEmpty(n.Length) ? "" : n.Length + "b")}"
                            : $"uint{(string.IsNullOrEmpty(n.Length) ? "" : n.Length + "b")}",
        };

        return string.IsNullOrEmpty(n.Display) ? baseType : $"{baseType} ({n.Display})";
    }

    private static string BuildStructTypeLabel(UfwbStructure s)
    {
        var parts = new List<string> { "struct" };
        if (s.VariableOrder) parts.Add("var-order");
        if (s.Floating)      parts.Add("floating");

        var repeat = BuildRepeatLabel(s.RepeatMin, s.RepeatMax);
        if (!string.IsNullOrEmpty(repeat)) parts.Add(repeat.TrimStart());

        return string.Join(" ", parts);
    }

    private static string BuildRepeatLabel(int min, int max)
    {
        if (min == 1 && max == 1) return string.Empty;
        if (max == -1)             return $" [{min}..*]";
        if (min == max)            return $" [{min}]";
        return $" [{min}..{max}]";
    }
}
