/*
    Apache 2.0  2026
    Author : Derek Tremblay (derektremblay666@gmail.com)
    Contributors: Claude Sonnet 4.5
*/

using System.Windows;
using WpfHexaEditor.SearchModule.ViewModels;

namespace WpfHexaEditor.SearchModule.Views
{
    /// <summary>
    /// Interaction logic for RelativeSearchDialog.xaml
    /// </summary>
    public partial class RelativeSearchDialog : Window
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
