// ==========================================================
// Project: WpfHexEditor.Plugins.ResxLocalization
// File: ViewModels/LocaleBrowserViewModel.cs
// Description:
//     ViewModel for the Locale Browser panel.
//     Holds the tree of locale variants for the currently active
//     .resx file.  Refreshed when ResxLocaleDiscoveredEvent arrives
//     on the IDE event bus.
// ==========================================================

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using WpfHexEditor.Core.ViewModels;

namespace WpfHexEditor.Plugins.ResxLocalization.ViewModels;

/// <summary>Represents one locale row in the Locale Browser tree.</summary>
public sealed class LocaleRowViewModel : ViewModelBase
{
    private bool _isActive;

    public string  CultureCode  { get; init; } = string.Empty;
    public string  FilePath     { get; init; } = string.Empty;
    public string  DisplayName  { get; init; } = string.Empty;
    public bool    IsBase       { get; init; }

    /// <summary>True when this locale file is currently open in the editor.</summary>
    public bool IsActive
    {
        get => _isActive;
        set { _isActive = value; OnPropertyChanged(); }
    }

}

/// <summary>
/// Backing ViewModel for <see cref="Panels.LocaleBrowserPanel"/>.
/// </summary>
public sealed class LocaleBrowserViewModel : ViewModelBase
{
    private string _basePath = string.Empty;
    private string _statusText = "No .resx file active";

    public ObservableCollection<LocaleRowViewModel> Locales { get; } = [];

    public string BasePath
    {
        get => _basePath;
        set { _basePath = value; OnPropertyChanged(); }
    }

    public string StatusText
    {
        get => _statusText;
        set { _statusText = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// Rebuilds the locale list from a <see cref="ResxLocaleDiscoveredEvent"/> payload.
    /// </summary>
    public void Refresh(string basePath, string[] variantPaths)
    {
        BasePath = basePath;
        Locales.Clear();

        // Base file (neutral culture) first
        Locales.Add(new LocaleRowViewModel
        {
            CultureCode = "Base",
            FilePath    = basePath,
            DisplayName = $"(Base) {System.IO.Path.GetFileName(basePath)}",
            IsBase      = true
        });

        foreach (var path in variantPaths)
        {
            var fileName = System.IO.Path.GetFileName(path);
            // Extract culture code: Resources.fr-CA.resx â†’ fr-CA
            var parts = fileName.Split('.');
            var culture = parts.Length >= 3 ? parts[^2] : fileName;

            string displayName;
            try
            {
                var ci = new System.Globalization.CultureInfo(culture);
                displayName = $"{culture}  â€”  {ci.DisplayName}";
            }
            catch
            {
                displayName = culture;
            }

            Locales.Add(new LocaleRowViewModel
            {
                CultureCode = culture,
                FilePath    = path,
                DisplayName = displayName,
                IsBase      = false
            });
        }

        StatusText = $"{Locales.Count} locale(s) found";
    }

    /// <summary>Marks the row matching <paramref name="openFilePath"/> as active.</summary>
    public void SetActiveFile(string openFilePath)
    {
        foreach (var row in Locales)
            row.IsActive = string.Equals(row.FilePath, openFilePath,
                StringComparison.OrdinalIgnoreCase);
    }

}
