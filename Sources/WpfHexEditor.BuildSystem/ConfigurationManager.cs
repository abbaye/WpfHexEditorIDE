// ==========================================================
// Project: WpfHexEditor.BuildSystem
// File: ConfigurationManager.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     Manages the set of available build configurations (Debug/Release/custom)
//     and the currently active configuration per solution.
//     Bound to the Build Toolbar ComboBox in MainWindow.
//
// Architecture Notes:
//     Pattern: Registry + Observable
//     - ConfigurationChanged event drives ComboBox updates in the UI.
// ==========================================================

using WpfHexEditor.Editor.Core;

namespace WpfHexEditor.BuildSystem;

/// <summary>
/// Manages available <see cref="IBuildConfiguration"/> objects and the currently active one.
/// </summary>
public sealed class ConfigurationManager
{
    private readonly List<BuildConfiguration> _configurations = [BuildConfiguration.Debug, BuildConfiguration.Release];
    private BuildConfiguration _active;

    public ConfigurationManager()
    {
        _active = _configurations[0]; // Debug by default
    }

    // -----------------------------------------------------------------------
    // Configurations
    // -----------------------------------------------------------------------

    /// <summary>All available build configurations.</summary>
    public IReadOnlyList<BuildConfiguration> Configurations => _configurations;

    /// <summary>Currently active configuration.</summary>
    public BuildConfiguration ActiveConfiguration
    {
        get => _active;
        set
        {
            if (_active == value) return;
            _active = value;
            ConfigurationChanged?.Invoke(this, value);
        }
    }

    /// <summary>Currently active platform (e.g. <c>"AnyCPU"</c>).</summary>
    public string ActivePlatform
    {
        get => _active.Platform;
        set
        {
            if (_active.Platform == value) return;
            _active.Platform = value;
            ConfigurationChanged?.Invoke(this, _active);
        }
    }

    /// <summary>All distinct platform names across all configurations.</summary>
    public IReadOnlyList<string> AvailablePlatforms
        => [.. _configurations.Select(c => c.Platform).Distinct()];

    // -----------------------------------------------------------------------
    // Mutation
    // -----------------------------------------------------------------------

    /// <summary>Adds a new custom configuration.</summary>
    public void Add(BuildConfiguration config)
    {
        if (!_configurations.Any(c => c.Name.Equals(config.Name, StringComparison.OrdinalIgnoreCase)))
            _configurations.Add(config);
    }

    /// <summary>Removes a configuration by name.</summary>
    public void Remove(string name)
    {
        var found = _configurations.FirstOrDefault(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (found is not null && found != _active)
            _configurations.Remove(found);
    }

    // -----------------------------------------------------------------------
    // Events
    // -----------------------------------------------------------------------

    /// <summary>Raised when the active configuration or platform changes.</summary>
    public event EventHandler<BuildConfiguration>? ConfigurationChanged;
}
