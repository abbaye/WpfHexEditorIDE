//////////////////////////////////////////////
// Apache 2.0  2026
// HexEditor V2 - Settings Panel
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5, Claude Sonnet 4.6
//////////////////////////////////////////////

using System.Windows.Controls;
using WpfHexEditor.Sample.HexEditor.ViewModels;

namespace WpfHexEditor.Sample.HexEditor.Views.Components
{
    /// <summary>
    /// Settings Panel - comprehensive settings for theme, language, search, and display options
    /// </summary>
    public partial class SettingsPanel : UserControl
    {
        public SettingsPanel()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Set the ViewModel for this control
        /// </summary>
        public SettingsPanelViewModel ViewModel
        {
            get => DataContext as SettingsPanelViewModel;
            set => DataContext = value;
        }
    }
}
