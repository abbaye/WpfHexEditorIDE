// ==========================================================
// Project: WpfHexEditor.App
// File: Dialogs/SolutionPropertyPages/StartupProjectsPage.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-17
// Description:
//     "Configurer des projets de démarrage" property page.
//     VS-identical layout: single-project radio + ComboBox,
//     multiple-project radio + DataGrid with project/action columns.
// ==========================================================

using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WpfHexEditor.Editor.Core;

namespace WpfHexEditor.App.Dialogs.SolutionPropertyPages;

/// <summary>Launch action for a project in multi-startup mode.</summary>
public enum StartupAction { None, Start, StartWithoutDebugging }

/// <summary>Row model for the multiple-startup DataGrid.</summary>
public sealed class StartupProfileEntry
{
    public string        ProjectName { get; set; } = string.Empty;
    public StartupAction Action      { get; set; } = StartupAction.Start;
}

/// <summary>
/// Property page: "Configurer des projets de démarrage".
/// Implements single-project selection (fully functional) and multiple-project
/// grid (UI only — not persisted to .sln in Phase 1).
/// </summary>
internal sealed class StartupProjectsPage : UserControl
{
    // ── Fields ───────────────────────────────────────────────────────────────

    private readonly ISolutionManager _solutionManager;

    private RadioButton _rbCurrentSelection  = null!;
    private RadioButton _rbSingle            = null!;
    private RadioButton _rbMultiple          = null!;
    private ComboBox    _cbSingleProject     = null!;
    private DataGrid    _dgMultiple          = null!;

    private readonly ObservableCollection<StartupProfileEntry> _profiles = [];

    // ── Constructor ──────────────────────────────────────────────────────────

    internal StartupProjectsPage(ISolutionManager solutionManager)
    {
        _solutionManager = solutionManager;
        BuildUI();
        PopulateData();
    }

    // ── Public API ───────────────────────────────────────────────────────────

    internal void Apply()
    {
        if (_rbSingle.IsChecked == true && _cbSingleProject.SelectedItem is string name)
        {
            var sol = _solutionManager.CurrentSolution;
            if (sol is null) return;
            var project = sol.Projects.FirstOrDefault(
                p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (project is not null)
                _solutionManager.SetStartupProject(project.Id);
        }
        // Multiple mode: stored in _profiles — persistence deferred to Phase 2
    }

    // ── Data ─────────────────────────────────────────────────────────────────

    private void PopulateData()
    {
        var sol = _solutionManager.CurrentSolution;
        if (sol is null) return;

        // Populate single-project ComboBox (launchable projects only)
        foreach (var p in sol.Projects)
            if (IsLaunchable(p))
                _cbSingleProject.Items.Add(p.Name);

        _cbSingleProject.SelectedItem = sol.StartupProject?.Name;

        // Populate multiple-startup DataGrid
        _profiles.Clear();
        foreach (var p in sol.Projects)
            if (IsLaunchable(p))
                _profiles.Add(new StartupProfileEntry
                {
                    ProjectName = p.Name,
                    Action      = p == sol.StartupProject
                                  ? StartupAction.Start
                                  : StartupAction.None
                });

        _dgMultiple.ItemsSource = _profiles;
    }

    private static bool IsLaunchable(IProject p)
    {
        if (p is not IProjectWithReferences vp) return true;
        return vp.OutputType.Equals("Exe",    StringComparison.OrdinalIgnoreCase)
            || vp.OutputType.Equals("WinExe", StringComparison.OrdinalIgnoreCase);
    }

    // ── UI Construction ──────────────────────────────────────────────────────

    private void BuildUI()
    {
        SetResourceReference(BackgroundProperty, "DockWindowBackgroundBrush");

        var root = new StackPanel { Margin = new Thickness(12) };

        // "Sélection actuelle" (not implemented — grayed out)
        _rbCurrentSelection = new RadioButton
        {
            Content   = "Sélection actuelle",
            IsEnabled = false,
            Margin    = new Thickness(0, 0, 0, 8)
        };
        _rbCurrentSelection.SetResourceReference(ForegroundProperty, "DockMenuForegroundBrush");

        // "Projet de démarrage unique"
        _rbSingle = new RadioButton
        {
            Content   = "Projet de démarrage unique :",
            IsChecked = true,
            Margin    = new Thickness(0, 0, 0, 4)
        };
        _rbSingle.SetResourceReference(ForegroundProperty, "DockMenuForegroundBrush");
        _rbSingle.Checked += (_, _) => UpdateMode();

        _cbSingleProject = new ComboBox
        {
            Margin = new Thickness(16, 0, 0, 12),
            Height = 24
        };
        _cbSingleProject.SetResourceReference(ComboBox.BackgroundProperty, "DockWindowBackgroundBrush");
        _cbSingleProject.SetResourceReference(ComboBox.ForegroundProperty, "DockMenuForegroundBrush");
        _cbSingleProject.SetResourceReference(ComboBox.BorderBrushProperty, "DockBorderBrush");

        // "Plusieurs projets de démarrage"
        _rbMultiple = new RadioButton
        {
            Content = "Plusieurs projets de démarrage :",
            Margin  = new Thickness(0, 0, 0, 4)
        };
        _rbMultiple.SetResourceReference(ForegroundProperty, "DockMenuForegroundBrush");
        _rbMultiple.Checked += (_, _) => UpdateMode();

        // Profils toolbar
        var profilsLabel = new TextBlock
        {
            Text   = "Profils de lancement",
            Margin = new Thickness(16, 0, 0, 4)
        };
        profilsLabel.SetResourceReference(TextBlock.ForegroundProperty, "DockMenuForegroundBrush");

        var profilsToolbar = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin      = new Thickness(16, 0, 0, 4)
        };
        foreach (var (icon, tip) in new[] {
            ("\uE710", "Add"), ("\uE8C8", "Duplicate"),
            ("\uE74D", "Remove"), ("\uE74B", "Move Up") })
        {
            var btn = new Button
            {
                ToolTip   = tip,
                Padding   = new Thickness(4),
                Margin    = new Thickness(0, 0, 2, 0),
                IsEnabled = false     // Phase 1: display-only
            };
            var tb = new TextBlock
            {
                Text       = icon,
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize   = 12
            };
            tb.SetResourceReference(TextBlock.ForegroundProperty, "DockMenuForegroundBrush");
            btn.Content = tb;
            btn.SetResourceReference(Button.BackgroundProperty, "DockWindowBackgroundBrush");
            profilsToolbar.Children.Add(btn);
        }

        // Multiple-project DataGrid
        _dgMultiple = new DataGrid
        {
            Margin                  = new Thickness(16, 0, 0, 0),
            Height                  = 180,
            AutoGenerateColumns     = false,
            CanUserAddRows          = false,
            CanUserDeleteRows       = false,
            HeadersVisibility       = DataGridHeadersVisibility.Column,
            SelectionMode           = DataGridSelectionMode.Single,
            GridLinesVisibility     = DataGridGridLinesVisibility.Horizontal,
            IsEnabled               = false     // Phase 1: UI-only
        };
        _dgMultiple.SetResourceReference(DataGrid.BackgroundProperty,           "DockWindowBackgroundBrush");
        _dgMultiple.SetResourceReference(DataGrid.ForegroundProperty,           "DockMenuForegroundBrush");
        _dgMultiple.SetResourceReference(DataGrid.BorderBrushProperty,          "DockBorderBrush");
        _dgMultiple.SetResourceReference(DataGrid.RowBackgroundProperty,        "DockWindowBackgroundBrush");
        _dgMultiple.SetResourceReference(DataGrid.AlternatingRowBackgroundProperty, "DockMenuBackgroundBrush");

        var colProject = new DataGridTextColumn
        {
            Header   = "Projet",
            Binding  = new System.Windows.Data.Binding(nameof(StartupProfileEntry.ProjectName)),
            IsReadOnly = true,
            Width    = new DataGridLength(1, DataGridLengthUnitType.Star)
        };
        var colAction = new DataGridComboBoxColumn
        {
            Header             = "Action",
            SelectedItemBinding = new System.Windows.Data.Binding(nameof(StartupProfileEntry.Action)),
            Width              = new DataGridLength(140)
        };
        colAction.ItemsSource = Enum.GetValues<StartupAction>();

        _dgMultiple.Columns.Add(colProject);
        _dgMultiple.Columns.Add(colAction);

        root.Children.Add(_rbCurrentSelection);
        root.Children.Add(_rbSingle);
        root.Children.Add(_cbSingleProject);
        root.Children.Add(_rbMultiple);
        root.Children.Add(profilsLabel);
        root.Children.Add(profilsToolbar);
        root.Children.Add(_dgMultiple);

        Content = root;
        UpdateMode();
    }

    private void UpdateMode()
    {
        bool single = _rbSingle.IsChecked == true;
        _cbSingleProject.IsEnabled = single;
        // DataGrid stays disabled in Phase 1 regardless
    }
}
