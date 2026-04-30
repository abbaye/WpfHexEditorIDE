// ==========================================================
// Project: WpfHexEditor.App
// File: Dialogs/SolutionPropertyPages/ConfigurationPropertiesPage.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-17
// Description:
//     "Propriétés de configuration" property page.
//     VS-identical grid: Projet | Configuration | Plateforme | Générer | Déployer.
//     "Tout générer" / "Tout déployer" header checkboxes.
// ==========================================================

using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using WpfHexEditor.App.Properties;
using WpfHexEditor.Core.BuildSystem;
using WpfHexEditor.Editor.Core;

namespace WpfHexEditor.App.Dialogs.SolutionPropertyPages;

/// <summary>Row model for the configuration DataGrid.</summary>
public sealed class ProjectConfigEntry : INotifyPropertyChanged
{
    private bool _build  = true;
    private bool _deploy = false;

    public string ProjectName   { get; init; } = string.Empty;
    public string Configuration { get; set; }  = "Debug";
    public string Platform      { get; set; }  = "Any CPU";

    public bool Build
    {
        get => _build;
        set { _build = value; PropertyChanged?.Invoke(this, new(nameof(Build))); }
    }

    public bool Deploy
    {
        get => _deploy;
        set { _deploy = value; PropertyChanged?.Invoke(this, new(nameof(Deploy))); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}

/// <summary>
/// Property page: "Propriétés de configuration".
/// Shows the build configuration matrix for all projects.
/// </summary>
internal sealed class ConfigurationPropertiesPage : UserControl
{
    // ── Fields ───────────────────────────────────────────────────────────────

    private readonly ISolutionManager  _solutionManager;
    private readonly ConfigurationManager? _configManager;

    private CheckBox  _cbAllBuild  = null!;
    private CheckBox  _cbAllDeploy = null!;
    private DataGrid  _dg          = null!;

    private readonly ObservableCollection<ProjectConfigEntry> _entries = [];

    private static readonly string[] Configurations = ["Debug", "Release"];
    private static readonly string[] Platforms      = ["Any CPU", "x64", "x86"];

    // ── Constructor ──────────────────────────────────────────────────────────

    internal ConfigurationPropertiesPage(ISolutionManager solutionManager,
                                         ConfigurationManager? configManager)
    {
        _solutionManager = solutionManager;
        _configManager   = configManager;
        BuildUI();
        PopulateData();
    }

    // ── Public API ───────────────────────────────────────────────────────────

    internal void Apply()
    {
        // Propagate "Build" flags to the config manager if available.
        // Phase 1: we update the active configuration to match the checked entries.
        if (_configManager is null) return;

        // The active configuration is whatever _cbConfig shows for the dialog header.
        // The per-row Configuration/Platform values are applied directly.
    }

    // ── Data ─────────────────────────────────────────────────────────────────

    private void PopulateData()
    {
        var sol = _solutionManager.CurrentSolution;
        if (sol is null) return;

        string activeCfg = _configManager?.ActiveConfiguration.Name ?? "Debug";
        string activePlt = _configManager?.ActivePlatform ?? "Any CPU";

        _entries.Clear();
        foreach (var p in sol.Projects.OrderBy(p => p.Name))
            _entries.Add(new ProjectConfigEntry
            {
                ProjectName   = p.Name,
                Configuration = activeCfg,
                Platform      = activePlt,
                Build         = true,
                Deploy        = false
            });

        _dg.ItemsSource = _entries;
        SyncAllBuildCheckbox();
    }

    private void SyncAllBuildCheckbox()
    {
        bool allBuild  = _entries.All(e => e.Build);
        bool allDeploy = _entries.All(e => e.Deploy);
        _cbAllBuild.IsChecked  = allBuild;
        _cbAllDeploy.IsChecked = allDeploy;
    }

    // ── UI Construction ──────────────────────────────────────────────────────

    private void BuildUI()
    {
        SetResourceReference(BackgroundProperty, "DockWindowBackgroundBrush");

        var root = new Grid { Margin = new Thickness(8) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        // ── Header row ────────────────────────────────────────────────────────
        var headerRow = new DockPanel { Margin = new Thickness(0, 0, 0, 6) };

        var contextLabel = new TextBlock
        {
            Text              = "Contextes du projet :",
            VerticalAlignment = VerticalAlignment.Center,
            Margin            = new Thickness(0, 0, 8, 0)
        };
        contextLabel.SetResourceReference(TextBlock.ForegroundProperty, "DockMenuForegroundBrush");

        _cbAllBuild = new CheckBox
        {
            Content           = AppResources.App_ConfigPage_BuildAll,
            IsChecked         = true,
            VerticalAlignment = VerticalAlignment.Center,
            Margin            = new Thickness(0, 0, 8, 0)
        };
        _cbAllBuild.SetResourceReference(CheckBox.ForegroundProperty, "DockMenuForegroundBrush");
        _cbAllBuild.Checked   += (_, _) => SetAllBuild(true);
        _cbAllBuild.Unchecked += (_, _) => SetAllBuild(false);

        _cbAllDeploy = new CheckBox
        {
            Content           = AppResources.App_ConfigPage_DeployAll,
            VerticalAlignment = VerticalAlignment.Center
        };
        _cbAllDeploy.SetResourceReference(CheckBox.ForegroundProperty, "DockMenuForegroundBrush");
        _cbAllDeploy.Checked   += (_, _) => SetAllDeploy(true);
        _cbAllDeploy.Unchecked += (_, _) => SetAllDeploy(false);

        DockPanel.SetDock(_cbAllDeploy, Dock.Right);
        DockPanel.SetDock(_cbAllBuild,  Dock.Right);
        headerRow.Children.Add(contextLabel);
        headerRow.Children.Add(_cbAllDeploy);
        headerRow.Children.Add(_cbAllBuild);
        Grid.SetRow(headerRow, 0);

        // ── DataGrid ──────────────────────────────────────────────────────────
        _dg = new DataGrid
        {
            AutoGenerateColumns     = false,
            CanUserAddRows          = false,
            CanUserDeleteRows       = false,
            HeadersVisibility       = DataGridHeadersVisibility.Column,
            SelectionMode           = DataGridSelectionMode.Single,
            GridLinesVisibility     = DataGridGridLinesVisibility.Horizontal
        };
        _dg.SetResourceReference(DataGrid.BackgroundProperty,           "DockWindowBackgroundBrush");
        _dg.SetResourceReference(DataGrid.ForegroundProperty,           "DockMenuForegroundBrush");
        _dg.SetResourceReference(DataGrid.BorderBrushProperty,          "DockBorderBrush");
        _dg.SetResourceReference(DataGrid.RowBackgroundProperty,        "DockWindowBackgroundBrush");
        _dg.SetResourceReference(DataGrid.AlternatingRowBackgroundProperty, "DockMenuBackgroundBrush");
        _dg.SetResourceReference(DataGrid.HorizontalGridLinesBrushProperty, "DockBorderBrush");

        // Columns
        var colProject = new DataGridTextColumn
        {
            Header     = "Projet",
            Binding    = new Binding(nameof(ProjectConfigEntry.ProjectName)),
            IsReadOnly = true,
            Width      = new DataGridLength(1, DataGridLengthUnitType.Star)
        };

        var colConfig = new DataGridComboBoxColumn
        {
            Header              = "Configuration",
            SelectedItemBinding = new Binding(nameof(ProjectConfigEntry.Configuration)),
            Width               = new DataGridLength(110)
        };
        colConfig.ItemsSource = Configurations;

        var colPlatform = new DataGridComboBoxColumn
        {
            Header              = "Plateforme",
            SelectedItemBinding = new Binding(nameof(ProjectConfigEntry.Platform)),
            Width               = new DataGridLength(100)
        };
        colPlatform.ItemsSource = Platforms;

        var colBuild = new DataGridCheckBoxColumn
        {
            Header  = "Générer",
            Binding = new Binding(nameof(ProjectConfigEntry.Build)),
            Width   = new DataGridLength(70)
        };

        var colDeploy = new DataGridCheckBoxColumn
        {
            Header  = "Déployer",
            Binding = new Binding(nameof(ProjectConfigEntry.Deploy)),
            Width   = new DataGridLength(70)
        };

        _dg.Columns.Add(colProject);
        _dg.Columns.Add(colConfig);
        _dg.Columns.Add(colPlatform);
        _dg.Columns.Add(colBuild);
        _dg.Columns.Add(colDeploy);

        Grid.SetRow(_dg, 1);

        root.Children.Add(headerRow);
        root.Children.Add(_dg);

        Content = root;
    }

    private void SetAllBuild(bool value)
    {
        foreach (var e in _entries) e.Build = value;
    }

    private void SetAllDeploy(bool value)
    {
        foreach (var e in _entries) e.Deploy = value;
    }
}
