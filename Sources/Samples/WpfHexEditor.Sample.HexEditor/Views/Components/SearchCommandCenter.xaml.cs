//////////////////////////////////////////////
// Apache 2.0  2026
// HexEditor V2 - Search Command Center (Hero Component)
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5, Claude Sonnet 4.6
//////////////////////////////////////////////

using System.Windows.Controls;
using WpfHexEditor.Sample.HexEditor.ViewModels;

namespace WpfHexEditor.Sample.HexEditor.Views.Components
{
    /// <summary>
    /// Search Command Center - The centerpiece showcasing ultra-performant V2 search
    /// </summary>
    public partial class SearchCommandCenter : UserControl
    {
        public SearchCommandCenter()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Set the ViewModel for this control
        /// </summary>
        public SearchCommandCenterViewModel ViewModel
        {
            get => DataContext as SearchCommandCenterViewModel;
            set => DataContext = value;
        }

        /// <summary>
        /// Focus the search textbox when the control loads
        /// </summary>
        private void UserControl_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            SearchTextBox.Focus();
        }
    }
}
