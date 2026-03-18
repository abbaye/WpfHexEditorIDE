// ==========================================================
// Project: WpfHexEditor.App
// File: Dialogs/SolutionPropertyPagesDialog.xaml.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-17
// Description:
//     VS-style "Pages de propriétés Solution" dialog host.
//     Left: collapsible page tree.  Right: active page.
//     Pages: Startup Projects, Build Dependencies, Source Files,
//            Configuration Properties.
//
// Architecture Notes:
//     Pattern: Property-page host (Strategy) — each page is a UserControl
//     with an Apply() method.  The host calls Apply() on all pages on OK,
//     or on the current page on "Appliquer".
// ==========================================================

using System.Windows;
using System.Windows.Controls;
using WpfHexEditor.App.Dialogs.SolutionPropertyPages;
using WpfHexEditor.BuildSystem;
using WpfHexEditor.Editor.Core;

namespace WpfHexEditor.App.Dialogs;

/// <summary>
/// VS-style Solution Property Pages dialog.
/// Created from <see cref="MainWindow.OpenSolutionPropertyPages"/>.
/// </summary>
public partial class SolutionPropertyPagesDialog : WpfHexEditor.Editor.Core.Views.ThemedDialog
{
    // ── Pages ─────────────────────────────────────────────────────────────────

    private readonly StartupProjectsPage        _startupPage;
    private readonly BuildDependenciesPage      _buildDepsPage;
    private readonly SourceFilesPage            _sourceFilesPage;
    private readonly ConfigurationPropertiesPage _configPropsPage;

    private UserControl? _activePage;

    // ── Constructor ───────────────────────────────────────────────────────────

    /// <summary>
    /// Initialises the dialog.
    /// </summary>
    /// <param name="solutionManager">Solution manager — provides project/solution data.</param>
    /// <param name="configManager">Configuration manager — provides build configs.</param>
    /// <param name="initialPage">
    ///   Page to pre-select: "startup", "deps", "source", "config" (default "startup").
    /// </param>
    public SolutionPropertyPagesDialog(
        ISolutionManager   solutionManager,
        ConfigurationManager? configManager,
        string             initialPage = "startup")
    {
        InitializeComponent();

        // Build pages
        _startupPage     = new StartupProjectsPage(solutionManager);
        _buildDepsPage   = new BuildDependenciesPage(solutionManager);
        _sourceFilesPage = new SourceFilesPage();
        _configPropsPage = new ConfigurationPropertiesPage(solutionManager, configManager);

        // Dialog title includes solution name
        if (solutionManager.CurrentSolution is { } sol)
            Title = $"Pages de propriétés Solution '{sol.Name}'";

        // Populate header combos
        foreach (var c in new[] { "Non applicable", "Active(Debug)", "Debug", "Release" })
            CbHeaderConfig.Items.Add(c);
        foreach (var p in new[] { "Non applicable", "Active(Any CPU)", "Any CPU", "x64", "x86" })
            CbHeaderPlatform.Items.Add(p);

        CbHeaderConfig.SelectedIndex   = 0;
        CbHeaderPlatform.SelectedIndex = 0;

        Loaded += (_, _) => SelectInitialPage(initialPage);
    }

    // ── Page selection ────────────────────────────────────────────────────────

    private void SelectInitialPage(string key)
    {
        TreeViewItem? target = key switch
        {
            "deps"   => TviBuildDeps,
            "source" => TviSourceFiles,
            "config" => TviConfig,
            _        => TviStartup
        };
        target.IsSelected = true;
        target.BringIntoView();
    }

    private void OnPageTreeSelectionChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is not TreeViewItem item) return;

        UserControl? page = null;

        if (item == TviStartup)      page = _startupPage;
        else if (item == TviBuildDeps)    page = _buildDepsPage;
        else if (item == TviSourceFiles)  page = _sourceFilesPage;
        else if (item == TviConfig)       page = _configPropsPage;
        else return;   // group header — do nothing

        _activePage    = page;
        PageHost.Content = page;
    }

    // ── Footer handlers ───────────────────────────────────────────────────────

    private void OnAccept(object sender, RoutedEventArgs e)
    {
        ApplyAll();
        DialogResult = true;
        Close();
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void OnApply(object sender, RoutedEventArgs e)
    {
        // Apply only the currently visible page
        ApplyCurrentPage();
    }

    // ── Apply helpers ─────────────────────────────────────────────────────────

    private void ApplyAll()
    {
        _startupPage.Apply();
        _buildDepsPage.Apply();
        _sourceFilesPage.Apply();
        _configPropsPage.Apply();
    }

    private void ApplyCurrentPage()
    {
        if (_activePage == _startupPage)     _startupPage.Apply();
        else if (_activePage == _buildDepsPage)   _buildDepsPage.Apply();
        else if (_activePage == _sourceFilesPage) _sourceFilesPage.Apply();
        else if (_activePage == _configPropsPage) _configPropsPage.Apply();
    }
}
