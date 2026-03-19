// ==========================================================
// Project: WpfHexEditor.ProjectSystem
// File: Documents/NuGet/SolutionPackageViewModel.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-18
// Description:
//     View-model for one unique NuGet package row in the solution-level
//     NuGet Manager. Aggregates the install state across all VS projects
//     of the loaded solution.
//
// Architecture Notes:
//     Immutable after construction by NuGetSolutionManagerViewModel.
//     StatusBrush and display strings are derived properties — no
//     INotifyPropertyChanged needed; rows are replaced on refresh.
// ==========================================================

using System.Collections.ObjectModel;
using System.Windows.Media;

namespace WpfHexEditor.ProjectSystem.Documents.NuGet;

/// <summary>
/// Display model for one unique package row in the solution-level NuGet Manager.
/// </summary>
public sealed class SolutionPackageViewModel
{
    // ── Identity ──────────────────────────────────────────────────────────────

    /// <summary>NuGet package id (e.g. "Newtonsoft.Json").</summary>
    public string Id { get; init; } = "";

    /// <summary>
    /// The common installed version when all projects that reference this package
    /// share the same version string, otherwise "Multiple".
    /// </summary>
    public string DisplayVersion { get; init; } = "";

    /// <summary>
    /// The latest version returned by nuget.org (empty until checked).
    /// </summary>
    public string LatestVersion { get; set; } = "";

    // ── Project breakdown ─────────────────────────────────────────────────────

    /// <summary>
    /// One entry per VS project in the solution (checked or unchecked for install ops).
    /// </summary>
    public ObservableCollection<ProjectSelectionViewModel> Projects { get; init; } = [];

    // ── Derived flags ─────────────────────────────────────────────────────────

    /// <summary>
    /// Number of VS projects in the solution that reference this package.
    /// </summary>
    public int InstalledProjectCount => Projects.Count(p => p.HasPackage);

    /// <summary>Total number of VS projects in the solution.</summary>
    public int TotalProjectCount => Projects.Count;

    /// <summary>
    /// "X / Y projects" label shown next to the package name in the list.
    /// </summary>
    public string ProjectCountLabel => $"{InstalledProjectCount} / {TotalProjectCount} projects";

    /// <summary>
    /// <see langword="true"/> when ≥ 2 projects reference this package with different versions.
    /// Drives the "Consolidate" tab filter.
    /// </summary>
    public bool HasConflict { get; init; }

    /// <summary>
    /// <see langword="true"/> when <see cref="LatestVersion"/> is non-empty and differs
    /// from every installed version (i.e., an update is available).
    /// </summary>
    public bool HasUpdate =>
        !string.IsNullOrEmpty(LatestVersion) &&
        Projects.Any(p => p.HasPackage &&
                          !string.Equals(p.InstalledVersion, LatestVersion, StringComparison.OrdinalIgnoreCase));

    // ── Browse metadata (populated from nuget.org search results) ────────────

    /// <summary>Package description returned by the NuGet search API (empty for Installed/Updates tabs).</summary>
    public string Description { get; init; } = "";

    /// <summary>Authors returned by the NuGet search API.</summary>
    public string Authors { get; init; } = "";

    /// <summary>Total download count returned by the NuGet search API.</summary>
    public long TotalDownloads { get; init; }

    // ── Visuals ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Color dot: amber = conflict or update, teal = fully installed + up-to-date, gray = partial/none.
    /// </summary>
    public Brush StatusBrush =>
        HasConflict || HasUpdate
            ? new SolidColorBrush(Color.FromRgb(0xD6, 0x89, 0x10))   // amber
            : InstalledProjectCount > 0
                ? new SolidColorBrush(Color.FromRgb(0x4E, 0xC9, 0xB0)) // teal
                : new SolidColorBrush(Color.FromRgb(0x60, 0x60, 0x60)); // gray
}
