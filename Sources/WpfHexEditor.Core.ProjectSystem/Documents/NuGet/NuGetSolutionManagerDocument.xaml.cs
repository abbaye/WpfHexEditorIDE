// ==========================================================
// Project: WpfHexEditor.ProjectSystem
// File: Documents/NuGet/NuGetSolutionManagerDocument.xaml.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-18
// Description:
//     Code-behind for the VS-Like NuGet Solution Package Manager document tab.
//     Minimal: only triggers the initial data load on Loaded.
//     All logic lives in NuGetSolutionManagerViewModel.
//
// Architecture Notes:
//     Theme:    DockMenuBackgroundBrush (see NuGetSolutionManagerDocument.xaml)
//     Pattern:  MVVM — DataContext set by the host (MainWindow)
// ==========================================================

using System.Windows.Controls;

namespace WpfHexEditor.Core.ProjectSystem.Documents.NuGet;

/// <summary>
/// VS-Like NuGet Solution Package Manager document tab.
/// Aggregates packages across all VS projects in the loaded solution.
/// </summary>
public partial class NuGetSolutionManagerDocument : UserControl
{
    public NuGetSolutionManagerDocument()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is NuGetSolutionManagerViewModel vm)
            await vm.LoadAsync();
    }
}
