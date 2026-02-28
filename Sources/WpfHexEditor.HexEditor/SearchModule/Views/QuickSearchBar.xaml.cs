////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5
//////////////////////////////////////////////

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WpfHexEditor.HexEditor.Search.ViewModels;

namespace WpfHexEditor.HexEditor.Search.Views
{
    /// <summary>
    /// Interaction logic for QuickSearchBar.xaml
    /// VSCode-style floating search bar (Ctrl+F).
    /// </summary>
    public partial class QuickSearchBar : UserControl
    {
        public QuickSearchBar()
        {
            InitializeComponent();

            // Set default DataContext if not set
            if (DataContext == null)
            {
                var vm = new SearchViewModel();
                DataContext = vm;

                // Add close command
                vm.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(SearchViewModel.StatusMessage))
                    {
                        // Auto-hide after finding/not finding
                        if (vm.StatusMessage.Contains("No") || vm.StatusMessage.Contains("not found"))
                        {
                            // Could implement auto-hide logic here
                        }
                    }
                };
            }

            // Focus search box when loaded
            Loaded += (s, e) =>
            {
                var textBox = FindName("SearchInput") as TextBox;
                textBox?.Focus();
                textBox?.SelectAll();
            };
        }

        /// <summary>
        /// Gets or sets the SearchViewModel.
        /// </summary>
        public SearchViewModel ViewModel
        {
            get => DataContext as SearchViewModel;
            set => DataContext = value;
        }

        /// <summary>
        /// Command to close the quick search bar.
        /// </summary>
        public ICommand CloseCommand
        {
            get => new RelayCommand(() => OnCloseRequested?.Invoke(this, System.EventArgs.Empty));
        }

        /// <summary>
        /// Event raised when the quick search bar should be closed.
        /// </summary>
        public event System.EventHandler OnCloseRequested;
    }
}
