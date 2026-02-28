//////////////////////////////////////////////
// Apache 2.0  - 2016-2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5
//////////////////////////////////////////////

using System.Linq;
using WpfHexEditor.Core;
using WpfHexEditor.Core.Bytes;

namespace WpfHexEditor.Core.Services
{
    /// <summary>
    /// Service responsible for position calculations and conversions
    /// </summary>
    /// <example>
    /// Basic usage:
    /// <code>
    /// var service = new PositionService();
    /// var provider = hexEditor.Provider;
    ///
    /// // Line and column calculations
    /// long position = 256;
    /// long line = service.GetLineNumber(position, byteShiftLeft: 0, hideByteDeleted: false,
    ///                                    bytePerLine: 16, byteSizeRatio: 1, provider);
    /// long column = service.GetColumnNumber(position, hideByteDeleted: false,
    ///                                       allowVisualByteAddress: false, visualByteAdressStart: 0,
    ///                                       byteShiftLeft: 0, bytePerLine: 16, provider);
    /// Console.WriteLine($"Position {position} is at line {line}, column {column}");
    ///
    /// // Hex conversion
    /// var (success, pos) = service.HexLiteralToLong("0x100");  // Parse "0x100" → 256
    /// if (success)
    /// {
    ///     string hexStr = service.LongToHex(pos);  // Convert back → "100"
    ///     Console.WriteLine($"Parsed position: {pos}, Hex: {hexStr}");
    /// }
    ///
    /// // Position validation
    /// long fileLength = 1024;
    /// bool isValid = service.IsPositionValid(position, fileLength);
    /// long clamped = service.ClampPosition(2000, minPosition: 0, maxPosition: fileLength);
    ///
    /// // Visibility checking
    /// long firstVisible = service.GetFirstVisibleBytePosition(scrollValue: 10, bytePerLine: 16,
    ///                                                          byteShiftLeft: 0, byteSizeRatio: 1,
    ///                                                          hideByteDeleted: false,
    ///                                                          allowVisualByteAddress: false,
    ///                                                          visualByteAdressStart: 0, provider);
    /// long lastVisible = firstVisible + (16 * 20); // 20 visible lines
    /// bool visible = service.IsBytePositionVisible(position, firstVisible, lastVisible);
    /// </code>
    /// </example>
    public class PositionService
    {
        #region Line/Column Calculations

        /// <summary>
        /// Calculate the line number for a given byte position
        /// </summary>
        /// <param name="position">Byte position in stream</param>
        /// <param name="byteShiftLeft">Byte shift offset</param>
        /// <param name="hideByteDeleted">Whether deleted bytes are hidden</param>
        /// <param name="bytePerLine">Number of bytes per line</param>
        /// <param name="byteSizeRatio">Byte size ratio</param>
        /// <param name="provider">Byte provider for deleted byte count</param>
        /// <returns>Line number</returns>
        public long GetLineNumber(long position, long byteShiftLeft, bool hideByteDeleted,
            int bytePerLine, int byteSizeRatio, ByteProvider provider) =>
            (position - byteShiftLeft - (hideByteDeleted
                ? GetCountOfByteDeletedBeforePosition(position, provider)
                : 0)
            ) / (bytePerLine * byteSizeRatio);

        /// <summary>
        /// Calculate the column number for a given byte position
        /// </summary>
        /// <param name="position">Byte position in stream</param>
        /// <param name="hideByteDeleted">Whether deleted bytes are hidden</param>
        /// <param name="allowVisualByteAddress">Whether visual byte addressing is enabled</param>
        /// <param name="visualByteAdressStart">Start position for visual addressing</param>
        /// <param name="byteShiftLeft">Byte shift offset</param>
        /// <param name="bytePerLine">Number of bytes per line</param>
        /// <param name="provider">Byte provider for deleted byte count</param>
        /// <returns>Column number (0-based)</returns>
        public long GetColumnNumber(long position, bool hideByteDeleted, bool allowVisualByteAddress,
            long visualByteAdressStart, long byteShiftLeft, int bytePerLine, ByteProvider provider)
        {
            var correcter = hideByteDeleted
                ? GetCountOfByteDeletedBeforePosition(position, provider)
                : 0;

            return allowVisualByteAddress
                ? (position - visualByteAdressStart - byteShiftLeft - correcter) % bytePerLine
                : (position - byteShiftLeft - correcter) % bytePerLine;
        }

        /// <summary>
        /// Get the count of deleted bytes before a given position
        /// </summary>
        /// <param name="position">Byte position in stream</param>
        /// <param name="provider">Byte provider</param>
        /// <returns>Count of deleted bytes before position</returns>
        public long GetCountOfByteDeletedBeforePosition(long position, ByteProvider provider) =>
            provider == null
                ? 0
                : provider.GetByteModifieds(ByteAction.Deleted)
                          .Count(b => b.Value.BytePositionInStream < position);

        #endregion

        #region Position Validation

        /// <summary>
        /// Calculate a valid position with correction for deleted bytes
        /// </summary>
        /// <param name="position">Start position</param>
        /// <param name="positionCorrection">Position offset (positive or negative)</param>
        /// <param name="provider">Byte provider for deleted byte checking</param>
        /// <returns>Corrected valid position, or -1 if provider is null</returns>
        public long GetValidPositionFrom(long position, long positionCorrection, ByteProvider provider)
        {
            if (provider == null)
                return -1;

            var validPosition = position;
            var gap = positionCorrection >= 0 ? positionCorrection : -positionCorrection;

            long cnt = 0;
            for (long i = 0; i < gap; i++)
            {
                cnt++;

                if (provider.CheckIfIsByteModified(position + (positionCorrection > 0 ? cnt : -cnt), ByteAction.Deleted).success)
                {
                    validPosition += positionCorrection > 0 ? 1 : -1;
                    i--;
                }
                else
                    validPosition += positionCorrection > 0 ? 1 : -1;
            }

            return validPosition >= 0 ? validPosition : -1;
        }

        #endregion

        #region Hex Conversion

        /// <summary>
        /// Convert hex literal string to long position
        /// </summary>
        /// <param name="hexLiteral">Hex string (e.g., "0x10", "FF", "10h")</param>
        /// <returns>Tuple of (success, position). Returns (-1, false) on error</returns>
        public (bool success, long position) HexLiteralToLong(string hexLiteral) =>
            ByteConverters.HexLiteralToLong(hexLiteral);

        /// <summary>
        /// Convert long position to hex string
        /// </summary>
        /// <param name="position">Position value</param>
        /// <param name="offsetWidth">Width formatting option</param>
        /// <returns>Hex string representation</returns>
        public string LongToHex(long position, OffSetPanelFixedWidth offsetWidth = OffSetPanelFixedWidth.Dynamic) =>
            ByteConverters.LongToHex(position, offsetWidth);

        #endregion

        #region Visibility Calculations

        /// <summary>
        /// Calculate first visible byte position based on scroll position
        /// </summary>
        /// <param name="scrollValue">Vertical scroll bar value</param>
        /// <param name="bytePerLine">Bytes per line</param>
        /// <param name="byteShiftLeft">Byte shift offset</param>
        /// <param name="byteSizeRatio">Byte size ratio</param>
        /// <param name="hideByteDeleted">Whether deleted bytes are hidden</param>
        /// <param name="allowVisualByteAddress">Whether visual addressing is enabled</param>
        /// <param name="visualByteAdressStart">Visual address start position</param>
        /// <param name="provider">Byte provider for deleted count</param>
        /// <returns>First visible byte position</returns>
        public long GetFirstVisibleBytePosition(long scrollValue, int bytePerLine, long byteShiftLeft,
            int byteSizeRatio, bool hideByteDeleted, bool allowVisualByteAddress,
            long visualByteAdressStart, ByteProvider provider)
        {
            // Compute the targeted position for the first visible byte
            var targetedPosition = allowVisualByteAddress
                ? scrollValue * (bytePerLine + byteShiftLeft + visualByteAdressStart) * byteSizeRatio
                : scrollValue * (bytePerLine + byteShiftLeft) * byteSizeRatio;

            // Count the deleted bytes before the targeted position
            return hideByteDeleted
                ? targetedPosition + GetCountOfByteDeletedBeforePosition(targetedPosition, provider)
                : targetedPosition;
        }

        /// <summary>
        /// Check if byte position is visible in viewport
        /// </summary>
        /// <param name="bytePosition">Position to check</param>
        /// <param name="firstVisiblePosition">First visible position</param>
        /// <param name="lastVisiblePosition">Last visible position</param>
        /// <returns>True if position is visible</returns>
        public bool IsBytePositionVisible(long bytePosition, long firstVisiblePosition, long lastVisiblePosition) =>
            bytePosition >= firstVisiblePosition && bytePosition <= lastVisiblePosition;

        #endregion

        #region Validation Helpers

        /// <summary>
        /// Validate that a position is within valid range
        /// </summary>
        /// <param name="position">Position to validate</param>
        /// <param name="maxLength">Maximum valid length</param>
        /// <returns>True if position is valid</returns>
        public bool IsPositionValid(long position, long maxLength) =>
            position >= 0 && position < maxLength;

        /// <summary>
        /// Clamp position to valid range
        /// </summary>
        /// <param name="position">Position to clamp</param>
        /// <param name="minPosition">Minimum allowed position</param>
        /// <param name="maxPosition">Maximum allowed position</param>
        /// <returns>Clamped position</returns>
        public long ClampPosition(long position, long minPosition, long maxPosition)
        {
            if (position < minPosition)
                return minPosition;

            if (position > maxPosition)
                return maxPosition;

            return position;
        }

        #endregion
    }
}
