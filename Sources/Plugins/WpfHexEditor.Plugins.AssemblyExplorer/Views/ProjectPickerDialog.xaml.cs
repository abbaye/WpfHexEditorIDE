// ==========================================================
// Project: WpfHexEditor.Plugins.AssemblyExplorer
// File: Views/ProjectPickerDialog.xaml.cs
// Author: Derek Tremblay
// Created: 2026-03-16
// Description:
//     Code-behind for the project-picker modal dialog.
//     Presents a list of WH native projects from the active solution.
//     Sets SelectedProject on OK; null on Cancel or no selection.
//
// Architecture Notes:
//     Pattern: Dialog — no MVVM (single-purpose, display-only).
//     Inherits ThemedDialog for VS2022-style custom chrome.
// ==========================================================

using System.Windows;
using System.Windows.Controls;
using WpfHexEditor.Editor.Core;
using WpfHexEditor.Editor.Core.Views;

namespace WpfHexEditor.Plugins.AssemblyExplorer.Views;

/// <summary>
/// Type alias for the ThemedDialog base so XAML can reference it without the full namespace.
/// </summary>
public class ThemedDialogBase : ThemedDialog { }

/// <summary>
/// Modal dialog that lets the user pick a WH native project to extract decompiled code into.
/// </summary>
public partial class ProjectPickerDialog : ThemedDialogBase
{
    /// <summary>The project selected by the user, or <see langword="null"/> if cancelled.</summary>
    public IProject? SelectedProject { get; private set; }

    public ProjectPickerDialog(IEnumerable<IProject> projects)
    {
        InitializeComponent();
        ProjectListBox.ItemsSource = projects.ToList();
    }

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        => OkButton.IsEnabled = ProjectListBox.SelectedItem is IProject;

    private void OnListDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (ProjectListBox.SelectedItem is IProject)
            CommitSelection();
    }

    private void OnOkClick(object sender, RoutedEventArgs e)
        => CommitSelection();

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        SelectedProject = null;
        DialogResult = false;
    }

    private void CommitSelection()
    {
        SelectedProject = ProjectListBox.SelectedItem as IProject;
        DialogResult    = SelectedProject is not null;
    }
}
