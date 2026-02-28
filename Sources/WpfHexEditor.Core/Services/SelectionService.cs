//////////////////////////////////////////////
// Apache 2.0  - 2016-2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5
//////////////////////////////////////////////

using System;
using System.IO;
using WpfHexEditor.Core;
using WpfHexEditor.Core.Bytes;

namespace WpfHexEditor.Core.Services
{
    /// <summary>
    /// Service responsible for selection operations
    /// </summary>
    /// <example>
    /// Basic usage:
    /// <code>
    /// var service = new SelectionService();
    ///
    /// // Validate selection
    /// if (service.IsValidSelection(start, stop))
    /// {
    ///     long length = service.GetSelectionLength(start, stop);
    ///     Console.WriteLine($"Selected {length} bytes");
    /// }
    ///
    /// // Fix selection order
    /// var (fixedStart, fixedStop) = service.FixSelectionStartStop(start, stop);
    ///
    /// // Select all
    /// var (selectAllStart, selectAllStop) = service.SelectAll(provider);
    ///
    /// // Get selection as byte array
    /// byte[] selectedBytes = service.GetSelectionByteArray(provider, start, stop);
    /// </code>
    /// </example>
    public class SelectionService
    {
        #region Public Methods

        /// <summary>
        /// Check if selection is valid
        /// </summary>
        public bool IsValidSelection(long selectionStart, long selectionStop)
        {
            return selectionStart >= 0 && selectionStop >= selectionStart;
        }

        /// <summary>
        /// Get the length of byte are selected (base 1)
        /// </summary>
        public long GetSelectionLength(long selectionStart, long selectionStop)
        {
            if (selectionStart == -1 || selectionStop == -1)
                return 0;

            if (selectionStart == selectionStop)
                return 1;

            if (selectionStart > selectionStop)
                return selectionStart - selectionStop + 1;

            return selectionStop - selectionStart + 1;
        }

        /// <summary>
        /// Fix selection range (swap if start > stop)
        /// </summary>
        public (long start, long stop) FixSelectionRange(long selectionStart, long selectionStop)
        {
            if (selectionStart > selectionStop)
                return (selectionStop, selectionStart);

            return (selectionStart, selectionStop);
        }

        /// <summary>
        /// Validate and clamp selection to provider bounds
        /// </summary>
        public (long start, long stop) ValidateSelection(ByteProvider provider, long selectionStart, long selectionStop)
        {
            if (provider == null || !provider.IsOpen)
                return (-1, -1);

            var start = Math.Max(-1, selectionStart);
            var stop = Math.Max(-1, selectionStop);

            if (start >= 0 && start >= provider.VirtualLength)
                start = provider.VirtualLength - 1;

            if (stop >= 0 && stop >= provider.VirtualLength)
                stop = provider.VirtualLength - 1;

            return (start, stop);
        }

        /// <summary>
        /// Get byte array from selection
        /// </summary>
        public byte[] GetSelectionBytes(ByteProvider provider, long selectionStart, long selectionStop, bool copyChange = true)
        {
            if (provider == null || !provider.IsOpen)
                return null;

            if (!IsValidSelection(selectionStart, selectionStop))
                return null;

            // V2: Use GetBytes directly instead of CopyToStream
            var length = (int)(selectionStop - selectionStart + 1);
            return provider.GetBytes(selectionStart, length);
        }

        /// <summary>
        /// Get all bytes from provider
        /// </summary>
        public byte[] GetAllBytes(ByteProvider provider, bool copyChange = true)
        {
            if (provider == null || !provider.IsOpen)
                return null;

            // V2: Use GetBytes(0, VirtualLength) instead of GetAllBytes
            return provider.GetBytes(0, (int)provider.VirtualLength);
        }

        /// <summary>
        /// Calculate selection start position for "Select All"
        /// </summary>
        public long GetSelectAllStart(ByteProvider provider)
        {
            if (provider == null || !provider.IsOpen)
                return -1;

            return 0;
        }

        /// <summary>
        /// Calculate selection stop position for "Select All"
        /// </summary>
        public long GetSelectAllStop(ByteProvider provider)
        {
            if (provider == null || !provider.IsOpen)
                return -1;

            return provider.VirtualLength;
        }

        /// <summary>
        /// Check if entire file is selected
        /// </summary>
        public bool IsAllSelected(ByteProvider provider, long selectionStart, long selectionStop)
        {
            if (provider == null || !provider.IsOpen)
                return false;

            return selectionStart == 0 && selectionStop >= provider.VirtualLength - 1;
        }

        /// <summary>
        /// Check if any selection exists
        /// </summary>
        public bool HasSelection(long selectionStart, long selectionStop)
        {
            return selectionStart >= 0 && selectionStop >= 0;
        }

        /// <summary>
        /// Extend selection by offset
        /// </summary>
        public long ExtendSelection(ByteProvider provider, long currentPosition, long offset, long visualStart = -1, long visualStop = -1)
        {
            if (provider == null || !provider.IsOpen)
                return currentPosition;

            var newPosition = currentPosition + offset;

            // Clamp to provider bounds
            if (newPosition < 0)
                newPosition = 0;
            else if (newPosition >= provider.VirtualLength)
                newPosition = provider.VirtualLength - 1;

            // Respect visual byte address if set
            if (visualStart >= 0 && newPosition < visualStart)
                newPosition = visualStart;

            if (visualStop >= 0 && newPosition > visualStop)
                newPosition = visualStop;

            return newPosition;
        }

        /// <summary>
        /// Get selection byte at specific position
        /// </summary>
        public byte? GetSelectionByte(ByteProvider provider, long position)
        {
            if (provider == null || !provider.IsOpen)
                return null;

            if (position < 0 || position >= provider.VirtualLength)
                return null;

            // V2: Fix tuple usage - GetByte returns (byte value, bool success)
            var (value, success) = provider.GetByte(position);
            return success ? value : (byte?)null;
        }

        #endregion
    }
}
