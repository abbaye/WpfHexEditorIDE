// ==========================================================
// Project: WpfHexEditor.Core.Events
// File: IDEEvents/SolutionExplorerEvents.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-23
// Description:
//     IDE event records published when the Solution Explorer file tree changes.
//     Allows plugins to react to project item additions, removals, and renames
//     without coupling to ISolutionManager directly.
// ==========================================================

namespace WpfHexEditor.Core.Events.IDEEvents;

/// <summary>Published when a file or folder item is added to a project.</summary>
public sealed record ProjectItemAddedEvent : IDEEventBase
{
    /// <summary>Absolute path of the added item. Empty for virtual/folder items.</summary>
    public string FilePath  { get; init; } = string.Empty;

    /// <summary>Id of the project that owns the item.</summary>
    public string ProjectId { get; init; } = string.Empty;

    /// <summary>String representation of <c>ProjectItemType</c> (e.g. "SourceFile", "FormatDefinition").</summary>
    public string ItemType  { get; init; } = string.Empty;
}

/// <summary>Published when a file or folder item is removed from a project.</summary>
public sealed record ProjectItemRemovedEvent : IDEEventBase
{
    /// <summary>Absolute path of the removed item.</summary>
    public string FilePath  { get; init; } = string.Empty;

    /// <summary>Id of the project that owned the item.</summary>
    public string ProjectId { get; init; } = string.Empty;

    /// <summary>String representation of <c>ProjectItemType</c>.</summary>
    public string ItemType  { get; init; } = string.Empty;
}

/// <summary>Published when a project item is renamed (file or folder).</summary>
public sealed record ProjectItemRenamedEvent : IDEEventBase
{
    /// <summary>Absolute path before the rename.</summary>
    public string OldPath   { get; init; } = string.Empty;

    /// <summary>Absolute path after the rename.</summary>
    public string NewPath   { get; init; } = string.Empty;

    /// <summary>Id of the project that owns the item.</summary>
    public string ProjectId { get; init; } = string.Empty;
}
