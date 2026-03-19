// ==========================================================
// Project: WpfHexEditor.Plugins.ClassDiagram
// File: Panels/DiagramSearchPanel.xaml.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-19
// Description:
//     Code-behind for DiagramSearchPanel.
//     Instantiates DiagramSearchPanelViewModel and wires
//     the search-box Enter key and button click to ViewModel.Search().
//
// Architecture Notes:
//     Pattern: View (MVVM).
//     Enter key and button both delegate to ViewModel.Search().
// ==========================================================

using System.Windows.Controls;
using System.Windows.Input;
using WpfHexEditor.Editor.ClassDiagram.ViewModels;

namespace WpfHexEditor.Plugins.ClassDiagram.Panels;

/// <summary>
/// Dockable full-text search panel for the class diagram editor.
/// </summary>
public partial class DiagramSearchPanel : UserControl
{
    /// <summary>Gets the ViewModel backing this panel.</summary>
    public DiagramSearchPanelViewModel ViewModel { get; }

    public DiagramSearchPanel()
    {
        ViewModel   = new DiagramSearchPanelViewModel();
        DataContext = ViewModel;
        InitializeComponent();
    }

    // ── Event handlers ───────────────────────────────────────────────────────

    private void OnSearchBoxKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            ViewModel.Search();
            e.Handled = true;
        }
    }

    private void OnSearchClick(object sender, System.Windows.RoutedEventArgs e)
        => ViewModel.Search();
}
