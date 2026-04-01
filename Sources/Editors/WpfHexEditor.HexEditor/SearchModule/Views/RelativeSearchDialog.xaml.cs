// ==========================================================
// Project: WpfHexEditor.HexEditor
// File: RelativeSearchDialog.xaml.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude (Anthropic)
// Created: 2026-03-06
// Description:
//     Code-behind for the RelativeSearchDialog — a themed dialog for performing
//     searches relative to the current cursor position or a reference offset.
//     Binds to RelativeSearchViewModel.
//
// Architecture Notes:
//     Extends ThemedDialog from WpfHexEditor.Editor.Core.Views for consistent theming.
//
// ==========================================================

using System.Windows;
using WpfHexEditor.HexEditor.Search.ViewModels;

namespace WpfHexEditor.HexEditor.Search.Views
{
    /// <summary>
    /// Interaction logic for RelativeSearchDialog.xaml
    /// </summary>
    public partial class RelativeSearchDialog : WpfHexEditor.Editor.Core.Views.ThemedDialog
    {
        public RelativeSearchDialog()
        {
            InitializeComponent();

            // Set default DataContext if not provided
            if (DataContext == null)
            {
                DataContext = new RelativeSearchViewModel();
            }
        }

        /// <summary>
        /// Gets or sets the ViewModel for this dialog.
        /// </summary>
        public RelativeSearchViewModel ViewModel
        {
            get => DataContext as RelativeSearchViewModel;
            set => DataContext = value;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
