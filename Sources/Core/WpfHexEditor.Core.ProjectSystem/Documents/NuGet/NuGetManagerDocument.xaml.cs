// ==========================================================
// Project: WpfHexEditor.ProjectSystem
// File: Documents/NuGet/NuGetManagerDocument.xaml.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     Code-behind for the VS-Like NuGet Package Manager document tab.
//     Minimal: only triggers the initial data load on Loaded.
//     All logic lives in NuGetManagerViewModel.
//
// Architecture Notes:
//     Theme:    DockMenuBackgroundBrush (see NuGetManagerDocument.xaml)
//     Pattern:  MVVM — DataContext set by the host (MainWindow)
// ==========================================================

using System.Windows.Controls;

namespace WpfHexEditor.Core.ProjectSystem.Documents.NuGet;

/// <summary>
/// VS-Like NuGet Package Manager document tab.
/// </summary>
public partial class NuGetManagerDocument : UserControl
{
    public NuGetManagerDocument()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is NuGetManagerViewModel vm)
            await vm.LoadAsync();
    }
}
