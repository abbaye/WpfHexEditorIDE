// ==========================================================
// Project: WpfHexEditor.ProjectSystem
// File: Documents/ProjectPropertiesDocument.xaml.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     Code-behind for the VS-Like Project Properties document tab.
//     Handles left-nav section switching and populates read-only
//     fields from the ViewModel.
//
// Architecture Notes:
//     Pattern: MVVM with code-behind for section visibility toggling
//     and Win32 browse dialog integration.
// ==========================================================

using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace WpfHexEditor.ProjectSystem.Documents;

/// <summary>
/// VS-Like project properties opened as a document tab.
/// DataContext must be a <see cref="ProjectPropertiesViewModel"/>.
/// </summary>
public partial class ProjectPropertiesDocument : UserControl
{
    // All section panels in display order (must match SectionId mapping)
    private readonly Dictionary<string, StackPanel> _sections = new();

    public ProjectPropertiesDocument()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Loaded             += OnLoaded;
    }

    // -----------------------------------------------------------------------
    // Initialisation
    // -----------------------------------------------------------------------

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Map section IDs to their panel UIElements
        _sections["app-general"]    = SectionAppGeneral;
        _sections["build"]          = SectionBuild;
        _sections["app-dependencies"] = SectionDependencies;
        _sections["items"]          = SectionItems;
        _sections["references"]     = SectionReferences;
        _sections["package"]        = SectionPackage;
        _sections["debug"]          = SectionDebug;
        _sections["code-analysis"]  = SectionCodeAnalysis;

        // Bind data that requires code-behind
        if (DataContext is ProjectPropertiesViewModel vm)
            BindViewModel(vm);
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is ProjectPropertiesViewModel vm)
            BindViewModel(vm);
    }

    private void BindViewModel(ProjectPropertiesViewModel vm)
    {
        // Title
        TitleText.Text = $"{vm.ProjectName} — Propriétés";
        vm.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is nameof(vm.ProjectName))
                TitleText.Text = $"{vm.ProjectName} — Propriétés";
        };

        // Populate read-only lists
        ItemsListView.ItemsSource = vm.Items;
        ItemCountLabel.Text       = vm.ItemCountText;
        RefsListView.ItemsSource  = vm.References;
        DepsListView.ItemsSource  = vm.References;

        // Disable nav headers (non-selectable group labels)
        NavListBox.Loaded += (_, _) =>
        {
            foreach (NavItem item in vm.NavigationItems.Where(n => n.IsHeader))
            {
                var container = NavListBox.ItemContainerGenerator
                    .ContainerFromItem(item) as ListBoxItem;
                if (container != null)
                {
                    container.IsEnabled  = false;
                    container.Focusable  = false;
                }
            }
        };

        // Show first leaf section
        ShowSection(vm.ActiveSection);
    }

    // -----------------------------------------------------------------------
    // Navigation
    // -----------------------------------------------------------------------

    private void OnNavSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (NavListBox.SelectedItem is NavItem { IsHeader: false } item)
            ShowSection(item.SectionId);
        else if (NavListBox.SelectedItem is NavItem { IsHeader: true })
            NavListBox.SelectedItem = null; // Prevent header selection
    }

    private void ShowSection(string sectionId)
    {
        // Hide all, then show the requested one
        foreach (var panel in _sections.Values)
            panel.Visibility = Visibility.Collapsed;

        if (_sections.TryGetValue(sectionId, out var target))
            target.Visibility = Visibility.Visible;
    }

    // -----------------------------------------------------------------------
    // Output path browse
    // -----------------------------------------------------------------------

    private void OnBrowseOutputPath(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog
        {
            Title = "Sélectionner le répertoire de sortie"
        };

        if (dlg.ShowDialog() == true && DataContext is ProjectPropertiesViewModel vm)
            vm.OutputPath = dlg.FolderName;
    }
}
