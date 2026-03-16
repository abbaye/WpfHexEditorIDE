// ==========================================================
// Project: WpfHexEditor.App
// File: Build/ConfigurationManagerDialog.xaml.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     Phase 3K — VS-Like Configuration Manager dialog.
//     Allows the user to change the active build configuration/platform
//     and to see/edit per-project build flags.
//
// Architecture Notes:
//     Pattern: Presenter — reads ConfigurationManager state on open,
//     writes back (configuration switch) on Apply/Close.
//     DataGrid rows are ProjectConfigRow view-models; never stored long-term.
// ==========================================================

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using WpfHexEditor.BuildSystem;
using WpfHexEditor.Editor.Core;

namespace WpfHexEditor.App.Build;

/// <summary>
/// Configuration Manager dialog — switch active build configuration and platform.
/// </summary>
public partial class ConfigurationManagerDialog : Window
{
    private readonly ConfigurationManager _configManager;
    private readonly ISolutionManager     _solutionManager;
    private bool                          _loading;

    public ConfigurationManagerDialog(
        ConfigurationManager configManager,
        ISolutionManager     solutionManager)
    {
        InitializeComponent();
        _configManager   = configManager;
        _solutionManager = solutionManager;
        Populate();
    }

    // -----------------------------------------------------------------------
    // Population
    // -----------------------------------------------------------------------

    private void Populate()
    {
        _loading = true;
        try
        {
            // Populate active configuration drop-down.
            CbActiveConfig.ItemsSource  = _configManager.Configurations.Select(c => c.Name).ToList();
            CbActiveConfig.SelectedItem = _configManager.ActiveConfiguration.Name;

            // Populate platform drop-down.
            CbActivePlatform.ItemsSource  = new[] { "AnyCPU", "x64", "x86" };
            CbActivePlatform.SelectedItem = _configManager.ActivePlatform;

            // Populate per-project rows.
            PopulateProjectGrid();
        }
        finally
        {
            _loading = false;
        }
    }

    private void PopulateProjectGrid()
    {
        var solution = _solutionManager.CurrentSolution;
        if (solution is null)
        {
            ProjectGrid.ItemsSource = Array.Empty<ProjectConfigRow>();
            return;
        }

        var rows = solution.Projects
            .Select(p => new ProjectConfigRow
            {
                ProjectName   = p.Name,
                Configuration = _configManager.ActiveConfiguration.Name,
                Platform      = _configManager.ActivePlatform,
                Build         = true,
            })
            .ToList();

        ProjectGrid.ItemsSource = rows;
    }

    // -----------------------------------------------------------------------
    // Handlers
    // -----------------------------------------------------------------------

    private void OnActiveConfigChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (_loading) return;
        if (CbActiveConfig.SelectedItem is not string name) return;

        var cfg = _configManager.Configurations
            .FirstOrDefault(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (cfg is not null)
        {
            _configManager.ActiveConfiguration = cfg;
            PopulateProjectGrid();
        }
    }

    private void OnActivePlatformChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (_loading) return;
        if (CbActivePlatform.SelectedItem is string platform)
        {
            _configManager.ActivePlatform = platform;
            PopulateProjectGrid();
        }
    }

    private void OnApply(object sender, RoutedEventArgs e)
    {
        // Configuration is already applied immediately on selection change.
        // Apply just re-syncs to make the visual state consistent.
        Populate();
    }

    // -----------------------------------------------------------------------
    // Row ViewModel
    // -----------------------------------------------------------------------

    private sealed class ProjectConfigRow : INotifyPropertyChanged
    {
        private string _configuration = "Debug";
        private string _platform      = "AnyCPU";
        private bool   _build         = true;

        public string ProjectName    { get; init; } = string.Empty;

        public string Configuration
        {
            get => _configuration;
            set { _configuration = value; OnPropertyChanged(); }
        }

        public string Platform
        {
            get => _platform;
            set { _platform = value; OnPropertyChanged(); }
        }

        public bool Build
        {
            get => _build;
            set { _build = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? p = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
    }
}
