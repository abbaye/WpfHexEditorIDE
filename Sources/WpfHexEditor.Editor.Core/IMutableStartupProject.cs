// ==========================================================
// Project: WpfHexEditor.Editor.Core
// File: IMutableStartupProject.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-17
// Description:
//     Opt-in interface for ISolution implementations that allow
//     the active startup project to be changed at runtime.
//     Implemented by both Solution (.whsln) and VsSolution (.sln)
//     so SolutionManager can update the in-memory model without
//     depending on concrete types from plugin assemblies.
// ==========================================================

namespace WpfHexEditor.Editor.Core;

/// <summary>
/// Marks an <see cref="ISolution"/> whose startup project can be mutated
/// after initial construction (e.g. via "Set as Startup Project").
/// </summary>
public interface IMutableStartupProject
{
    /// <summary>
    /// Updates the in-memory startup project to <paramref name="project"/>.
    /// Pass <see langword="null"/> to clear the current selection.
    /// </summary>
    void ChangeStartupProject(IProject? project);
}
