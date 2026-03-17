// ==========================================================
// Project: WpfHexEditor.Plugins.AssemblyExplorer
// File: ViewModels/AssemblyWorkspaceEntry.cs
// Author: Derek Tremblay
// Created: 2026-03-16
// Description:
//     Holds the complete state for one assembly loaded in the
//     multi-assembly workspace managed by AssemblyExplorerViewModel.
//     Keyed by AssemblyModel.FilePath.ToLowerInvariant() in the workspace dictionary.
//
// Architecture Notes:
//     Pattern: Value holder (not a ViewModel — no INotifyPropertyChanged).
//     IsPinned setter propagates to the root node so the pin badge updates.
// ==========================================================

using WpfHexEditor.Core.AssemblyAnalysis.Models;

namespace WpfHexEditor.Plugins.AssemblyExplorer.ViewModels;

/// <summary>
/// Represents a single assembly loaded in the multi-assembly workspace.
/// </summary>
internal sealed class AssemblyWorkspaceEntry
{
    public AssemblyWorkspaceEntry(
        AssemblyModel             model,
        AssemblyRootNodeViewModel root,
        CancellationTokenSource   cts)
    {
        Model = model;
        Root  = root;
        Cts   = cts;
    }

    /// <summary>Immutable analysis result for this assembly.</summary>
    public AssemblyModel Model { get; }

    /// <summary>Root tree node displayed in the explorer panel.</summary>
    public AssemblyRootNodeViewModel Root { get; }

    /// <summary>
    /// CancellationTokenSource for the background analysis task.
    /// Cancelled and disposed when the entry is removed from the workspace.
    /// </summary>
    public CancellationTokenSource Cts { get; }

    /// <summary>
    /// Load time in milliseconds, stored for the output log message.
    /// </summary>
    public long LoadTimeMs { get; set; }

    private bool _isPinned;

    /// <summary>
    /// When true the entry is not evicted when the active editor changes
    /// (PinAssembliesAcrossFileChange = false) nor when the max-assembly limit is hit.
    /// Setting this property also propagates to the root node so the pin badge in the
    /// tree updates reactively.
    /// </summary>
    public bool IsPinned
    {
        get => _isPinned;
        set
        {
            _isPinned     = value;
            Root.IsPinned = value;
        }
    }
}
