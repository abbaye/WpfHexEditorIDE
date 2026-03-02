//////////////////////////////////////////////
// Apache 2.0  - 2016-2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5, Claude Sonnet 4.6
//////////////////////////////////////////////

using System;
using System.IO;
using WpfHexEditor.Core;
using WpfHexEditor.Core.Bytes;
using WpfHexEditor.Core.CharacterTable;

namespace WpfHexEditor.Core.Services
{
    /// <summary>
    /// Service responsible for clipboard operations (Copy/Paste/Cut)
    /// </summary>
    /// <example>
    /// Basic usage:
    /// <code>
    /// var service = new ClipboardService();
    /// service.DefaultCopyMode = CopyPasteMode.HexaString;
    ///
    /// // Check if copy is possible
    /// if (service.CanCopy(selectionLength, provider))
    /// {
    ///     // Copy to clipboard
    ///     service.CopyToClipboard(provider, selectionStart, selectionStop);
    /// }
    ///
    /// // Copy with specific mode
    /// service.CopyToClipboard(provider, CopyPasteMode.AsciiString, start, stop);
    ///
    /// // Copy to stream
    /// using (var stream = new MemoryStream())
    /// {
    ///     service.CopyToStream(provider, stream, start, stop, copyChange: false);
    ///     var bytes = stream.ToArray();
    /// }
    /// </code>
    /// </example>
    public class ClipboardService
    {
        #region Properties

        /// <summary>
        /// Default copy to clipboard mode
        /// </summary>
        public CopyPasteMode DefaultCopyMode { get; set; } = CopyPasteMode.HexaString;

        #endregion

        #region Public Methods

        /// <summary>
        /// Check if copy operation is possible
        /// </summary>
        /// <param name="selectionLength">Length of selection in bytes</param>
        /// <param name="provider">ByteProvider instance</param>
        /// <returns>True if copy is possible</returns>
        /// <example>
        /// <code>
        /// if (clipboardService.CanCopy(selectionLength, provider))
        /// {
        ///     clipboardService.CopyToClipboard(provider, start, stop);
        ///     Console.WriteLine("Copied to clipboard!");
        /// }
        /// </code>
        /// </example>
        public bool CanCopy(long selectionLength, ByteProvider provider)
        {
            return selectionLength >= 1 && provider != null && provider.IsOpen;
        }

        /// <summary>
        /// Check if delete operation is possible
        /// </summary>
        public bool CanDelete(long selectionLength, ByteProvider provider, bool readOnlyMode, bool allowDeleteByte)
        {
            return CanCopy(selectionLength, provider) && !readOnlyMode && allowDeleteByte;
        }

        /// <summary>
        /// Copy to clipboard with default mode
        /// </summary>
        public void CopyToClipboard(ByteProvider provider, long selectionStart, long selectionStop, TblStream tbl = null)
        {
            CopyToClipboard(provider, DefaultCopyMode, selectionStart, selectionStop, true, tbl);
        }

        /// <summary>
        /// Copy to clipboard with specified mode
        /// </summary>
        public void CopyToClipboard(ByteProvider provider, CopyPasteMode mode, long selectionStart, long selectionStop, TblStream tbl = null)
        {
            CopyToClipboard(provider, mode, selectionStart, selectionStop, true, tbl);
        }

        /// <summary>
        /// Copy to clipboard with full parameters
        /// </summary>
        public void CopyToClipboard(ByteProvider provider, CopyPasteMode mode, long selectionStart, long selectionStop, bool copyChange, TblStream tbl)
        {
            if (provider == null || !provider.IsOpen) return;
            if (selectionStart < 0 || selectionStop < selectionStart) return;

            // V2: Use GetBytes directly and convert to clipboard format
            var length = (int)(selectionStop - selectionStart + 1);
            var bytes = provider.GetBytes(selectionStart, length);
            if (bytes == null || bytes.Length == 0) return;

            // Convert to appropriate clipboard format
            string clipboardData = mode switch
            {
                CopyPasteMode.HexaString => BitConverter.ToString(bytes).Replace("-", " "),
                CopyPasteMode.AsciiString => System.Text.Encoding.ASCII.GetString(bytes),
                CopyPasteMode.TblString when tbl != null => ByteConverters.BytesToString(bytes),
                _ => BitConverter.ToString(bytes).Replace("-", " ")
            };

            System.Windows.Clipboard.SetText(clipboardData);
        }

        /// <summary>
        /// Copy selection to a stream
        /// </summary>
        public void CopyToStream(ByteProvider provider, Stream output, long selectionStart, long selectionStop, bool copyChange)
        {
            if (provider == null || !provider.IsOpen) return;
            if (output == null) return;
            if (selectionStart < 0 || selectionStop < selectionStart) return;

            // V2: Use GetBytes + stream.Write instead of CopyToStream
            var length = (int)(selectionStop - selectionStart + 1);
            var bytes = provider.GetBytes(selectionStart, length);
            if (bytes != null && bytes.Length > 0)
            {
                output.Write(bytes, 0, bytes.Length);
            }
        }

        /// <summary>
        /// Get copy data as byte array
        /// </summary>
        public byte[] GetCopyData(ByteProvider provider, long selectionStart, long selectionStop, bool copyChange)
        {
            if (provider == null || !provider.IsOpen) return null;
            if (selectionStart < 0 || selectionStop < selectionStart) return null;

            // V2: Use GetBytes directly instead of GetCopyData
            var length = (int)(selectionStop - selectionStart + 1);
            return provider.GetBytes(selectionStart, length);
        }

        /// <summary>
        /// Get all bytes from provider
        /// </summary>
        public byte[] GetAllBytes(ByteProvider provider, bool copyChange = true)
        {
            if (provider == null || !provider.IsOpen) return null;

            // V2: Use GetBytes(0, VirtualLength) instead of GetAllBytes
            return provider.GetBytes(0, (int)provider.VirtualLength);
        }

        /// <summary>
        /// Fill selection with a specific byte value
        /// </summary>
        public void FillWithByte(ByteProvider provider, long startPosition, long length, byte value, bool readOnlyMode)
        {
            if (provider == null || !provider.IsOpen) return;
            if (startPosition < 0 || length <= 0) return;
            if (readOnlyMode) return;

            provider.FillWithByte(startPosition, length, value);
        }

        #endregion
    }
}
