////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5
//////////////////////////////////////////////

using System.Windows;
using WpfHexEditor.HexEditor.Search.ViewModels;

namespace WpfHexEditor.HexEditor.Search.Views
{
    /// <summary>
    /// Interaction logic for FindReplaceDialog.xaml
    /// </summary>
    public partial class FindReplaceDialog : Window
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
