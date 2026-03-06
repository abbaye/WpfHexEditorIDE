// ==========================================================
// Project: WpfHexEditor.SDK
// File: ISolutionExplorerService.cs
// Author: Auto
// Created: 2026-03-06
// Description:
//     Plugin-facing service for accessing the IDE Solution Explorer —
//     navigating the solution tree, querying open files and projects.
//
// Architecture Notes:
//     Implemented by SolutionExplorerServiceImpl in App/Services.
//     Read-only access by default; file operations require AccessFileSystem permission.
//
// ==========================================================

namespace WpfHexEditor.SDK.Contracts.Services;

/// <summary>
/// Provides read access to the active IDE solution structure.
/// </summary>
public interface ISolutionExplorerService
{
    /// <summary>Gets whether a solution is currently open.</summary>
    bool HasActiveSolution { get; }

    /// <summary>Gets the path to the active solution file (.whsln), or null if none.</summary>
    string? ActiveSolutionPath { get; }

    /// <summary>Gets the name of the active solution, or null if none.</summary>
    string? ActiveSolutionName { get; }

    /// <summary>
    /// Gets all file paths currently open in the IDE as document tabs.
    /// </summary>
    IReadOnlyList<string> GetOpenFilePaths();

    /// <summary>
    /// Gets all file paths belonging to the active solution (across all projects).
    /// Returns empty list if no solution is open.
    /// </summary>
    IReadOnlyList<string> GetSolutionFilePaths();

    /// <summary>
    /// Raised when the active solution changes (opened, closed, reloaded).
    /// Raised on the UI thread.
    /// </summary>
    event EventHandler SolutionChanged;
}
