// ==========================================================
// Project: WpfHexEditor.ProjectSystem
// File: Documents/NuGet/ProjectSelectionViewModel.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-18
// Description:
//     Represents one project row in the solution-level NuGet Manager's
//     detail panel (right side). The user can check/uncheck each project
//     to control whether an install or uninstall operation affects it.
//
// Architecture Notes:
//     Pattern: MVVM — INotifyPropertyChanged so the CheckBox binding
//     updates CanExecute on the Install / Uninstall commands.
// ==========================================================

using System.ComponentModel;
using System.Runtime.CompilerServices;
using WpfHexEditor.Editor.Core;

namespace WpfHexEditor.ProjectSystem.Documents.NuGet;

/// <summary>
/// Checkbox-backed row for one project in the solution-level NuGet detail panel.
/// </summary>
public sealed class ProjectSelectionViewModel : INotifyPropertyChanged
{
    private bool    _isSelected;
    private string? _installedVersion;

    // ── Core data ─────────────────────────────────────────────────────────────

    /// <summary>The underlying project (used for write-back).</summary>
    public IProject Project { get; init; } = null!;

    /// <summary>Display name shown in the project list.</summary>
    public string Name => Project.Name;

    /// <summary>
    /// Version of the package currently installed in this project.
    /// <see langword="null"/> when the package is not referenced.
    /// </summary>
    public string? InstalledVersion
    {
        get => _installedVersion;
        set { _installedVersion = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasPackage)); }
    }

    /// <summary><see langword="true"/> when this project references the package.</summary>
    public bool HasPackage => InstalledVersion is not null;

    // ── Selection ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Whether the project is checked for the next install / uninstall action.
    /// Defaults to <see langword="true"/> when the package is already installed,
    /// <see langword="false"/> otherwise.
    /// </summary>
    public bool IsSelected
    {
        get => _isSelected;
        set { _isSelected = value; OnPropertyChanged(); }
    }

    // ── INotifyPropertyChanged ────────────────────────────────────────────────

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
