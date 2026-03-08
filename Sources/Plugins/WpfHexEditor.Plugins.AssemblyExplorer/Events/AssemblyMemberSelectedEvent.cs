// ==========================================================
// Project: WpfHexEditor.Plugins.AssemblyExplorer
// File: Events/AssemblyMemberSelectedEvent.cs
// Author: Derek Tremblay
// Created: 2026-03-08
// Description:
//     EventBus event published when the user selects a type/member
//     node in the Assembly Explorer tree. Allows future panels
//     (e.g. IL viewer, cross-reference panel) to react.
//
// Architecture Notes:
//     Plugin-private event. Intentionally carries only primitive data
//     so cross-plugin subscribers need no reference to this assembly.
// ==========================================================

namespace WpfHexEditor.Plugins.AssemblyExplorer.Events;

/// <summary>
/// Published on <c>IPluginEventBus</c> when the user selects a node
/// in the Assembly Explorer tree.
/// </summary>
public sealed class AssemblyMemberSelectedEvent
{
    /// <summary>Display name of the selected node (e.g. "MyClass" or "MyMethod()").</summary>
    public string NodeDisplayName { get; init; } = string.Empty;

    /// <summary>
    /// ECMA-335 metadata token of the selected node, or 0 for virtual/grouping nodes.
    /// </summary>
    public int MetadataToken { get; init; }

    /// <summary>
    /// Raw PE file byte offset of the node's metadata row, or 0 if not resolved.
    /// </summary>
    public long PeOffset { get; init; }

    /// <summary>
    /// Human-readable node kind: "AssemblyRoot", "Namespace", "Type",
    /// "Method", "Field", "Property", "Event", "Reference", "Resource", "MetadataTable".
    /// </summary>
    public string NodeKind { get; init; } = string.Empty;
}
