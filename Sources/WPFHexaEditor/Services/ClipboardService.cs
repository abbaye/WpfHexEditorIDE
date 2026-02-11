//////////////////////////////////////////////
// Apache 2.0  - 2016-2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5
//////////////////////////////////////////////

using System;
using System.IO;
using WpfHexaEditor.Core;
using WpfHexaEditor.Core.Bytes;
using WpfHexaEditor.Core.CharacterTable;

namespace WpfHexaEditor.Services
{
    /// <summary>
    /// Service responsible for clipboard operations (Copy/Paste/Cut)
    /// </summary>
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

            provider.CopyToClipboard(mode, selectionStart, selectionStop, copyChange, tbl);
        }

        /// <summary>
        /// Copy selection to a stream
        /// </summary>
        public void CopyToStream(ByteProvider provider, Stream output, long selectionStart, long selectionStop, bool copyChange)
        {
            if (provider == null || !provider.IsOpen) return;
            if (output == null) return;
            if (selectionStart < 0 || selectionStop < selectionStart) return;

            provider.CopyToStream(output, selectionStart, selectionStop, copyChange);
        }

        /// <summary>
        /// Get copy data as byte array
        /// </summary>
        public byte[] GetCopyData(ByteProvider provider, long selectionStart, long selectionStop, bool copyChange)
        {
            if (provider == null || !provider.IsOpen) return null;
            if (selectionStart < 0 || selectionStop < selectionStart) return null;

            return provider.GetCopyData(selectionStart, selectionStop, copyChange);
        }

        /// <summary>
        /// Get all bytes from provider
        /// </summary>
        public byte[] GetAllBytes(ByteProvider provider, bool copyChange = true)
        {
            if (provider == null || !provider.IsOpen) return null;

            return provider.GetAllBytes(copyChange);
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
