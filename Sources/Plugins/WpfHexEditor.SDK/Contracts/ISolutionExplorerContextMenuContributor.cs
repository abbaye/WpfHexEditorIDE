// ==========================================================
// Project: WpfHexEditor.SDK
// File: Contracts/ISolutionExplorerContextMenuContributor.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-19
// Description:
//     Extension point that lets plugins inject items into the
//     Solution Explorer right-click context menu.
//
// Architecture Notes:
//     Pattern: Strategy + Contributor
//     Register via IUIRegistry.RegisterContextMenuContributor().
//     GetContextMenuItems() is called on the UI thread every time
//     the context menu opens — keep it fast (no I/O).
// ==========================================================

namespace WpfHexEditor.SDK.Contracts;

/// <summary>
/// Implemented by plugins that want to inject items into the
/// Solution Explorer context menu.
/// </summary>
public interface ISolutionExplorerContextMenuContributor
{
    /// <summary>
    /// Returns the menu items (and optional separators) to inject for the
    /// node that was right-clicked.
    /// </summary>
    /// <param name="nodeKind">
    /// Type of the node: "File", "Project", "Solution", "Folder", "SolutionFolder".
    /// </param>
    /// <param name="nodePath">
    /// Absolute path associated with the node (file path, project file path,
    /// folder path, or solution file path). <see langword="null"/> for nodes
    /// that have no path (e.g. virtual solution folders).
    /// </param>
    /// <returns>
    /// Zero or more <see cref="SolutionContextMenuItem"/> entries to append
    /// after a plugin separator. Return empty to contribute nothing for this node.
    /// </returns>
    IReadOnlyList<SolutionContextMenuItem> GetContextMenuItems(string nodeKind, string? nodePath);
}
