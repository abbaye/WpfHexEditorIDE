// ==========================================================
// Project: WpfHexEditor.HexEditor
// File: ProgressOverlay.xaml.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude (Anthropic)
// Created: 2026-03-06
// Description:
//     Code-behind for the ProgressOverlay UserControl displayed during long-running
//     operations such as file loading, searching, or batch edits.
//     Binds to ProgressOverlayViewModel and shows progress, message, and cancel button.
//
// Architecture Notes:
//     MVVM pattern — DataContext is set to ProgressOverlayViewModel.
//     Theme: global WPF theme applied via merged ResourceDictionaries.
//
// ==========================================================

using System.Windows.Controls;
using WpfHexEditor.HexEditor.ViewModels;

namespace WpfHexEditor.HexEditor.Controls
{
    /// <summary>
    /// Progress overlay control for displaying long-running operation progress
    /// </summary>
    public partial class ProgressOverlay : UserControl
    {
        public ProgressOverlay()
        {
            InitializeComponent();

            // Initialize ViewModel
            ViewModel = new ProgressOverlayViewModel();
            DataContext = ViewModel;
        }

        /// <summary>
        /// ViewModel for the progress overlay
        /// </summary>
        public ProgressOverlayViewModel ViewModel { get; }
    }
}
