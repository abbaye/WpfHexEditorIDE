// ==========================================================
// Project: WpfHexEditor.Plugins.ClassDiagram
// File: Panels/ClassHistoryPanel.xaml.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-19
// Description:
//     Code-behind for ClassHistoryPanel.
//     Instantiates ClassHistoryPanelViewModel and routes list
//     click events to ViewModel.RequestJumpTo() so the plugin can
//     perform the corresponding undo/redo operations on the host.
//
// Architecture Notes:
//     Pattern: View (MVVM).
//     Click-to-jump is handled in code-behind to calculate the
//     target index (item position in the list) and delegate to
//     the ViewModel rather than exposing an ICommand with index logic.
// ==========================================================

using System.Windows.Controls;
using System.Windows.Input;
using WpfHexEditor.Editor.ClassDiagram.ViewModels;

namespace WpfHexEditor.Plugins.ClassDiagram.Panels;

/// <summary>
/// Dockable history panel mirroring the diagram undo manager.
/// </summary>
public partial class ClassHistoryPanel : UserControl
{
    /// <summary>Gets the ViewModel backing this panel.</summary>
    public ClassHistoryPanelViewModel ViewModel { get; }

    public ClassHistoryPanel()
    {
        ViewModel   = new ClassHistoryPanelViewModel();
        DataContext = ViewModel;
        InitializeComponent();
    }

    // ── Event handlers ───────────────────────────────────────────────────────

    private void OnHistoryListClick(object sender, MouseButtonEventArgs e)
    {
        if (HistoryList.SelectedIndex < 0) return;
        // +1 because the target index is the state AFTER applying the selected entry.
        ViewModel.RequestJumpTo(HistoryList.SelectedIndex + 1);
    }
}
