// ==========================================================
// Project: WpfHexEditor.HexEditor
// File: FindReplaceDialog.xaml.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude (Anthropic)
// Created: 2026-03-06
// Description:
//     Code-behind for the FindReplaceDialog — a themed dialog combining find
//     and replace functionality for byte sequences. Binds to ReplaceViewModel
//     and supports single-occurrence and replace-all modes.
//
// Architecture Notes:
//     Extends ThemedDialog from WpfHexEditor.Editor.Core.Views for consistent theming.
//     ReplaceViewModel is set as DataContext and exposes commands for both operations.
//
// ==========================================================

using System.Windows;
using WpfHexEditor.HexEditor.Search.ViewModels;

namespace WpfHexEditor.HexEditor.Search.Views
{
    /// <summary>
    /// Interaction logic for FindReplaceDialog.xaml
    /// </summary>
    public partial class FindReplaceDialog : WpfHexEditor.Editor.Core.Views.ThemedDialog
    {
        public FindReplaceDialog()
        {
            InitializeComponent();

            // Set default DataContext if not set
            if (DataContext == null)
            {
                DataContext = new ReplaceViewModel();
            }

            // Handle F3 for Find Next
            PreviewKeyDown += (s, e) =>
            {
                if (e.Key == System.Windows.Input.Key.F3)
                {
                    if ((System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Shift) == System.Windows.Input.ModifierKeys.Shift)
                    {
                        ViewModel?.FindPreviousCommand?.Execute(null);
                    }
                    else
                    {
                        ViewModel?.FindNextCommand?.Execute(null);
                    }
                    e.Handled = true;
                }
            };
        }

        /// <summary>
        /// Gets or sets the ReplaceViewModel.
        /// </summary>
        public ReplaceViewModel ViewModel
        {
            get => DataContext as ReplaceViewModel;
            set => DataContext = value;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
