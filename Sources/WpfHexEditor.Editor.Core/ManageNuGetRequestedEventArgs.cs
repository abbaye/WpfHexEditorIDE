// ==========================================================
// Project: WpfHexEditor.Editor.Core
// File: ManageNuGetRequestedEventArgs.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     Shared event-args record used by both ProjectPropertiesViewModel
//     (raises ManageNuGetRequested) and ISolutionExplorerPanel
//     (raises ManageNuGetPackagesRequested) to carry the target IProject
//     to the MainWindow host.
//
// Architecture Notes:
//     Placed in WpfHexEditor.Editor.Core so it is accessible to all
//     layers (ProjectSystem, Panels.IDE, App) without creating a
//     cross-assembly dependency.
// ==========================================================

namespace WpfHexEditor.Editor.Core;

/// <summary>
/// Event args for "Manage NuGet Packages" requests raised from
/// the Project Properties document or the Solution Explorer context menu.
/// </summary>
public sealed class ManageNuGetRequestedEventArgs : EventArgs
{
    /// <summary>
    /// The project for which the NuGet Manager should be opened.
    /// </summary>
    public IProject Project { get; init; } = null!;
}
