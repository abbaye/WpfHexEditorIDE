using System;
using System.Windows;
using WpfHexaEditor.Interfaces;

namespace WpfHexaEditor
{
    public partial class HexEditor
    {
        #region ByteDistributionPanel DependencyProperty

        /// <summary>
        /// Connect an external panel that implements <see cref="IByteDistributionPanel"/>.
        /// The panel will receive byte data automatically when a file is opened or closed.
        /// </summary>
        public IByteDistributionPanel ByteDistributionPanel
        {
            get => (IByteDistributionPanel)GetValue(ByteDistributionPanelProperty);
            set => SetValue(ByteDistributionPanelProperty, value);
        }

        public static readonly DependencyProperty ByteDistributionPanelProperty =
            DependencyProperty.Register(
                nameof(ByteDistributionPanel),
                typeof(IByteDistributionPanel),
                typeof(HexEditor),
                new PropertyMetadata(null, OnByteDistributionPanelChanged));

        private static void OnByteDistributionPanelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not HexEditor editor)
                return;

            // If file already open, push data to the newly connected panel immediately
            if (e.NewValue is IByteDistributionPanel panel && editor.IsFileOrStreamLoaded)
                editor.NotifyByteDistributionPanel();
        }

        #endregion

        #region Internal methods called by FileOperations / AsyncOperations

        /// <summary>
        /// Read up to 1 MB from the current provider and push it to the connected panel.
        /// No-op when no panel is connected or no file is loaded.
        /// </summary>
        internal void NotifyByteDistributionPanel()
        {
            var panel = ByteDistributionPanel;
            if (panel == null || _viewModel?.Provider == null)
                return;

            try
            {
                var maxBytes = (int)Math.Min(_viewModel.Provider.Length, 1024 * 1024);
                var bytes = _viewModel.Provider.GetBytes(0, maxBytes);
                panel.UpdateData(bytes);
            }
            catch
            {
                // Never crash HexEditor due to a panel update failure
            }
        }

        /// <summary>
        /// Clear the connected panel when a file is closed.
        /// No-op when no panel is connected.
        /// </summary>
        internal void ClearByteDistributionPanel()
        {
            ByteDistributionPanel?.Clear();
        }

        #endregion
    }
}
