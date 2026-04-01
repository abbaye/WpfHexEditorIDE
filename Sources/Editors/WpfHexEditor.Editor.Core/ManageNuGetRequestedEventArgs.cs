// ==========================================================
// Project: WpfHexEditor.Editor.Core
// File: ManageNuGetRequestedEventArgs.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     Shared event-args records used by both ProjectPropertiesViewModel
//     (raises ManageNuGetRequested), ISolutionExplorerPanel project node
//     (raises ManageNuGetPackagesRequested) and the solution node
//     (raises ManageSolutionNuGetPackagesRequested) to carry the target
//     IProject or ISolution to the MainWindow host.
//
// Architecture Notes:
//     Placed in WpfHexEditor.Editor.Core so it is accessible to all
//     layers (ProjectSystem, Panels.IDE, App) without creating a
//     cross-assembly dependency.
// ==========================================================

namespace WpfHexEditor.Editor.Core;

/// <summary>
/// Event args for "Manage NuGet Packages" requests raised from
/// the Project Properties document or the Solution Explorer project-node context menu.
/// </summary>
public sealed class ManageNuGetRequestedEventArgs : EventArgs
{
    /// <summary>
    /// The project for which the NuGet Manager should be opened.
    /// </summary>
    public IProject Project { get; init; } = null!;
}

/// <summary>
/// Event args for "Manage NuGet Packages for Solution" requests raised from
/// the Solution Explorer solution-node context menu.
/// </summary>
public sealed class ManageSolutionNuGetRequestedEventArgs : EventArgs
{
    /// <summary>
    /// The solution for which the solution-level NuGet Manager should be opened.
    /// </summary>
    public ISolution Solution { get; init; } = null!;
}

/// <summary>
/// Event args for "Add Reference…" requests raised from the References container context menu.
/// </summary>
public sealed class AddReferenceRequestedEventArgs : EventArgs
{
    /// <summary>The project to which a reference should be added.</summary>
    public IProject? Project { get; init; }
}

/// <summary>
/// Event args for "Remove Unused References…" requests raised from the References container context menu.
/// </summary>
public sealed class RemoveUnusedReferencesRequestedEventArgs : EventArgs
{
    /// <summary>The project from which unused references should be cleaned up.</summary>
    public IProject? Project { get; init; }
}
