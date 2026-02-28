//////////////////////////////////////////////
// Apache 2.0  - 2026
// HexEditor - Data Inspector Integration (Partial Class)
// Author : Claude Sonnet 4.5
// Contributors: Derek Tremblay (derektremblay666@gmail.com)
//////////////////////////////////////////////

using System;
using System.Linq;
using System.Windows;
using WpfHexEditor.Core.Interfaces;

namespace WpfHexEditor.HexEditor
{
    /// <summary>
    /// HexEditor partial class - Data Inspector integration
    /// Provides real-time byte interpretation in multiple formats
    /// </summary>
    public partial class HexEditor
    {
        #region Fields

        private IDataInspectorPanel _dataInspectorPanel;
        private bool _dataInspectorEnabled = false;

        #endregion

        #region Dependency Properties

        /// <summary>
        /// Dependency Property for DataInspectorVisibility
        /// </summary>
        public static readonly DependencyProperty DataInspectorVisibilityProperty =
            DependencyProperty.Register(
                nameof(DataInspectorVisibility),
                typeof(Visibility),
                typeof(HexEditor),
                new PropertyMetadata(Visibility.Collapsed, OnDataInspectorVisibilityChanged));

        /// <summary>
        /// Visibility of the Data Inspector panel
        /// </summary>
        public Visibility DataInspectorVisibility
        {
            get => (Visibility)GetValue(DataInspectorVisibilityProperty);
            set => SetValue(DataInspectorVisibilityProperty, value);
        }

        private static void OnDataInspectorVisibilityChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexEditor editor)
            {
                editor._dataInspectorEnabled = (Visibility)e.NewValue == Visibility.Visible;

                // Update inspector if visible
                if (editor._dataInspectorEnabled)
                {
                    editor.UpdateDataInspector();
                }
            }
        }

        /// <summary>
        /// Dependency Property for DataInspectorByteCount
        /// Number of bytes to show in Data Inspector (1-16)
        /// </summary>
        public static readonly DependencyProperty DataInspectorByteCountProperty =
            DependencyProperty.Register(
                nameof(DataInspectorByteCount),
                typeof(int),
                typeof(HexEditor),
                new PropertyMetadata(16, OnDataInspectorByteCountChanged, CoerceDataInspectorByteCount));

        /// <summary>
        /// Number of bytes to inspect (1-16)
        /// </summary>
        public int DataInspectorByteCount
        {
            get => (int)GetValue(DataInspectorByteCountProperty);
            set => SetValue(DataInspectorByteCountProperty, value);
        }

        private static void OnDataInspectorByteCountChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HexEditor editor)
            {
                editor.UpdateDataInspector();
            }
        }

        private static object CoerceDataInspectorByteCount(DependencyObject d, object baseValue)
        {
            var value = (int)baseValue;
            return Math.Max(1, Math.Min(16, value)); // Clamp between 1 and 16
        }

        #endregion

        #region Initialization

        /// <summary>
        /// Initialize Data Inspector panel
        /// </summary>
        private void InitializeDataInspector()
        {
            _dataInspectorPanel = this.FindName("DataInspectorPanel") as IDataInspectorPanel;

            // Hook into selection changed event
            SelectionChanged += HexEditor_SelectionChanged_DataInspector;
        }

        #endregion

        #region Event Handlers

        /// <summary>
        /// Handle selection changes to update Data Inspector
        /// </summary>
        private void HexEditor_SelectionChanged_DataInspector(object sender, EventArgs e)
        {
            if (_dataInspectorEnabled)
            {
                UpdateDataInspector();
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Update the Data Inspector with current selection
        /// </summary>
        public void UpdateDataInspector()
        {
            if (!_dataInspectorEnabled || _dataInspectorPanel == null || _viewModel?.Provider == null)
                return;

            try
            {
                var position = SelectionStart;
                var length = Math.Min(DataInspectorByteCount, _viewModel.Provider.Length - position);

                if (length <= 0)
                {
                    _dataInspectorPanel.Clear();
                    return;
                }

                // Read bytes from current position
                var bytes = _viewModel.Provider.GetBytes(position, (int)length);

                // Update inspector
                _dataInspectorPanel.UpdateBytes(bytes);
            }
            catch (Exception)
            {
                // Silently ignore errors
                _dataInspectorPanel?.Clear();
            }
        }

        /// <summary>
        /// Get reference to the Data Inspector panel
        /// </summary>
        public IDataInspectorPanel GetDataInspectorPanel()
        {
            return _dataInspectorPanel;
        }

        #endregion
    }
}
