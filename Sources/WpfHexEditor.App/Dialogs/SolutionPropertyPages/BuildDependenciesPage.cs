// ==========================================================
// Project: WpfHexEditor.App
// File: Dialogs/SolutionPropertyPages/BuildDependenciesPage.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-17
// Description:
//     "Dépendances de build de projet" property page.
//     Project selector + checklist of build dependencies, populated from
//     IProjectWithReferences.ProjectReferences.  Read-only display in Phase 1.
// ==========================================================

using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using WpfHexEditor.Editor.Core;

namespace WpfHexEditor.App.Dialogs.SolutionPropertyPages;

/// <summary>
/// Property page: "Dépendances de build de projet".
/// Shows per-project build dependencies.  In Phase 1 dependencies are read
/// from IProjectWithReferences.ProjectReferences (display-only).
/// </summary>
internal sealed class BuildDependenciesPage : UserControl
{
    // ── Fields ───────────────────────────────────────────────────────────────

    private readonly ISolutionManager _solutionManager;
    private ComboBox  _cbProject  = null!;
    private ListBox   _lbDepends  = null!;

    // ── Constructor ──────────────────────────────────────────────────────────

    internal BuildDependenciesPage(ISolutionManager solutionManager)
    {
        _solutionManager = solutionManager;
        BuildUI();
        PopulateProjectList();
    }

    // ── Public API ───────────────────────────────────────────────────────────

    internal void Apply()
    {
        // Phase 1: display only — no persistence to .sln / .whsln yet.
    }

    // ── Data ─────────────────────────────────────────────────────────────────

    private void PopulateProjectList()
    {
        var sol = _solutionManager.CurrentSolution;
        if (sol is null) return;

        foreach (var p in sol.Projects.OrderBy(p => p.Name))
            _cbProject.Items.Add(p.Name);

        if (_cbProject.Items.Count > 0)
            _cbProject.SelectedIndex = 0;
    }

    private void RefreshDependsList(string? selectedProjectName)
    {
        _lbDepends.Items.Clear();
        var sol = _solutionManager.CurrentSolution;
        if (sol is null || string.IsNullOrEmpty(selectedProjectName)) return;

        var selectedProject = sol.Projects.FirstOrDefault(
            p => p.Name.Equals(selectedProjectName, StringComparison.OrdinalIgnoreCase));

        // Collect this project's dependency project names from its references.
        var dependencyNames = selectedProject is IProjectWithReferences vp
            ? vp.ProjectReferences
                 .Select(refPath => sol.Projects.FirstOrDefault(p =>
                     string.Equals(p.ProjectFilePath, refPath, StringComparison.OrdinalIgnoreCase)
                     || string.Equals(Path.GetFileNameWithoutExtension(p.ProjectFilePath),
                                      Path.GetFileNameWithoutExtension(refPath),
                                      StringComparison.OrdinalIgnoreCase))?.Name)
                 .Where(n => n is not null)
                 .ToHashSet(StringComparer.OrdinalIgnoreCase)
            : new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var p in sol.Projects.OrderBy(p => p.Name))
        {
            if (p.Name.Equals(selectedProjectName, StringComparison.OrdinalIgnoreCase))
                continue;   // skip self

            var cb = new CheckBox
            {
                Content   = p.Name,
                IsChecked = dependencyNames.Contains(p.Name),
                IsEnabled = false,   // Phase 1: read-only
                Margin    = new Thickness(4, 2, 4, 2)
            };
            cb.SetResourceReference(CheckBox.ForegroundProperty, "DockMenuForegroundBrush");
            _lbDepends.Items.Add(cb);
        }
    }

    // ── UI Construction ──────────────────────────────────────────────────────

    private void BuildUI()
    {
        SetResourceReference(BackgroundProperty, "DockWindowBackgroundBrush");

        var root = new Grid { Margin = new Thickness(12) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var projectLabel = new TextBlock
        {
            Text   = "Projets :",
            Margin = new Thickness(0, 0, 0, 4)
        };
        projectLabel.SetResourceReference(TextBlock.ForegroundProperty, "DockMenuForegroundBrush");
        Grid.SetRow(projectLabel, 0);

        _cbProject = new ComboBox { Margin = new Thickness(0, 0, 0, 12), Height = 24 };
        _cbProject.SetResourceReference(ComboBox.BackgroundProperty, "DockWindowBackgroundBrush");
        _cbProject.SetResourceReference(ComboBox.ForegroundProperty, "DockMenuForegroundBrush");
        _cbProject.SetResourceReference(ComboBox.BorderBrushProperty, "DockBorderBrush");
        _cbProject.SelectionChanged += (_, _) =>
            RefreshDependsList(_cbProject.SelectedItem as string);
        Grid.SetRow(_cbProject, 1);

        var dependsLabel = new TextBlock
        {
            Text   = "Dépend de :",
            Margin = new Thickness(0, 0, 0, 4)
        };
        dependsLabel.SetResourceReference(TextBlock.ForegroundProperty, "DockMenuForegroundBrush");
        Grid.SetRow(dependsLabel, 2);

        _lbDepends = new ListBox
        {
            BorderThickness = new Thickness(1)
        };
        _lbDepends.SetResourceReference(ListBox.BackgroundProperty, "DockWindowBackgroundBrush");
        _lbDepends.SetResourceReference(ListBox.ForegroundProperty, "DockMenuForegroundBrush");
        _lbDepends.SetResourceReference(ListBox.BorderBrushProperty, "DockBorderBrush");
        Grid.SetRow(_lbDepends, 3);

        root.Children.Add(projectLabel);
        root.Children.Add(_cbProject);
        root.Children.Add(dependsLabel);
        root.Children.Add(_lbDepends);

        Content = root;
    }
}
