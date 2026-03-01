//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5, Claude Sonnet 4.6
//////////////////////////////////////////////

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
