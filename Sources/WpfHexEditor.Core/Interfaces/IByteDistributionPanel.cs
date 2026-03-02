//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

namespace WpfHexEditor.Core.Interfaces
{
    /// <summary>
    /// Interface for an external panel that displays byte frequency distribution.
    /// Implement this interface to connect a custom visualization panel to HexEditor
    /// via the <see cref="WpfHexEditor.Core.HexEditor.ByteDistributionPanel"/> dependency property.
    /// </summary>
    public interface IByteDistributionPanel
    {
        /// <summary>
        /// Update the panel with the byte content of the currently open file.
        /// Called automatically when a file is opened.
        /// </summary>
        /// <param name="data">Raw bytes to analyze (up to 1 MB sampled).</param>
        void UpdateData(byte[] data);

        /// <summary>
        /// Clear the panel when the file is closed.
        /// </summary>
        void Clear();
    }
}
