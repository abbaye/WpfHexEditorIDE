// ==========================================================
// Project: WpfHexEditor.SDK
// File: Events/AssemblyWorkspaceChangedEvent.cs
// Author: Derek Tremblay
// Created: 2026-03-16
// Description:
//     EventBus message published by the Assembly Explorer whenever an assembly
//     is added to or removed from the multi-assembly workspace.
//     Consumers (e.g., ParsedFields, Diff panel) can react to workspace changes
//     without a direct reference to the AssemblyExplorer plugin.
//
// Architecture Notes:
//     Pattern: Observer / Pub-Sub via IPluginEventBus.
//     Publisher: AssemblyExplorerPlugin (via WorkspaceStatsChanged event on ViewModel).
//     Subscribers: any plugin interested in workspace composition changes.
// ==========================================================

namespace WpfHexEditor.SDK.Events;

/// <summary>Describes how the workspace changed.</summary>
public enum WorkspaceChangeKind
{
    /// <summary>A new assembly was added to the workspace.</summary>
    Added,

    /// <summary>An assembly was removed from the workspace (closed by user or limit eviction).</summary>
    Removed
}

/// <summary>
/// Published by the Assembly Explorer when an assembly is added or removed
/// from the multi-assembly workspace.
/// </summary>
public sealed class AssemblyWorkspaceChangedEvent
{
    /// <summary>Absolute file path of the assembly that changed.</summary>
    public string FilePath { get; init; } = string.Empty;

    /// <summary>Simple name of the assembly (e.g. "System.Runtime").</summary>
    public string AssemblyName { get; init; } = string.Empty;

    /// <summary>Whether the assembly was added or removed.</summary>
    public WorkspaceChangeKind Kind { get; init; }

    /// <summary>Total number of assemblies currently in the workspace after this change.</summary>
    public int TotalAssemblies { get; init; }

    /// <summary>Total number of types across all loaded assemblies after this change.</summary>
    public int TotalTypes { get; init; }
}
