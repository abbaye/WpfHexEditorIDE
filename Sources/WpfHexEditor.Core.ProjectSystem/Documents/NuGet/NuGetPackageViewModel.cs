// ==========================================================
// Project: WpfHexEditor.ProjectSystem
// File: Documents/NuGet/NuGetPackageViewModel.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     View-model for a single package row in the NuGet Manager list.
//     Exposes install state, update availability, and display metadata.
//
// Architecture Notes:
//     Immutable-ish: all properties are set once at construction time
//     by NuGetManagerViewModel; StatusBrush is derived.
//     Does NOT implement INotifyPropertyChanged — rows are replaced
//     entirely when the list is refreshed.
// ==========================================================

using System.Windows.Media;

namespace WpfHexEditor.Core.ProjectSystem.Documents.NuGet;

/// <summary>
/// Display model for one NuGet package row.
/// </summary>
public sealed class NuGetPackageViewModel
{
    public string  Id               { get; init; } = "";
    public string  LatestVersion    { get; init; } = "";
    public string? InstalledVersion { get; init; }
    public string  Authors          { get; init; } = "";
    public string  Description      { get; init; } = "";
    public long    TotalDownloads   { get; init; }

    /// <summary>
    /// True when the package is referenced in the project's .csproj.
    /// </summary>
    public bool IsInstalled => InstalledVersion is not null;

    /// <summary>
    /// True when a newer version is available on NuGet.org compared to the installed one.
    /// Uses simple string comparison; NuGet's semver ordering is not required here.
    /// </summary>
    public bool HasUpdate =>
        InstalledVersion is not null &&
        !string.Equals(InstalledVersion, LatestVersion, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Color dot shown next to the package name:
    /// teal  = installed and up-to-date,
    /// amber = update available,
    /// gray  = not installed.
    /// </summary>
    public Brush StatusBrush => HasUpdate
        ? new SolidColorBrush(Color.FromRgb(0xD6, 0x89, 0x10))   // amber
        : IsInstalled
            ? new SolidColorBrush(Color.FromRgb(0x4E, 0xC9, 0xB0)) // teal
            : new SolidColorBrush(Color.FromRgb(0x60, 0x60, 0x60)); // gray
}
