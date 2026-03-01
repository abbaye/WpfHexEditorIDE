//////////////////////////////////////////////
// Apache 2.0  - 2026
// Data Inspector Panel - Code-behind
// Author : Claude Sonnet 4.5
// Contributors: Derek Tremblay (derektremblay666@gmail.com)
//////////////////////////////////////////////

using System.Windows.Controls;
using WpfHexEditor.Core.Interfaces;
using WpfHexEditor.HexEditor.ViewModels;

namespace WpfHexEditor.Panels.BinaryAnalysis
{
    /// <summary>
    /// Panel for inspecting byte data in multiple formats
    /// Shows integers, floats, dates, network addresses, GUIDs, colors, etc.
    /// </summary>
    public partial class DataInspectorPanel : UserControl, IDataInspectorPanel
    {
        private DataInspectorViewModel _viewModel;

        public DataInspectorPanel()
        {
            InitializeComponent();

            _viewModel = new DataInspectorViewModel();
            DataContext = _viewModel;
        }

        /// <summary>
        /// Update the inspector with new byte data from the hex editor
        /// </summary>
        /// <param name="bytes">Byte array to inspect</param>
        public void UpdateBytes(byte[] bytes)
        {
            _viewModel?.UpdateBytes(bytes);
        }

        /// <summary>
        /// Clear all inspector values
        /// </summary>
        public void Clear()
        {
            _viewModel?.UpdateBytes(null);
        }

        /// <summary>
        /// Get the ViewModel for advanced usage
        /// </summary>
        public DataInspectorViewModel ViewModel => _viewModel;
    }
}
