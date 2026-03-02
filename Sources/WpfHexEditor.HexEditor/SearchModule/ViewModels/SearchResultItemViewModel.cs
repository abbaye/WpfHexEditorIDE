//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using WpfHexEditor.Core;
using WpfHexEditor.Core.Bytes;
using WpfHexEditor.Core.CharacterTable;

namespace WpfHexEditor.HexEditor.Search.ViewModels
{
    /// <summary>
    /// ViewModel for a single search result item in the results list.
    /// Provides formatted display strings for position, context, etc.
    /// </summary>
    public class SearchResultItemViewModel : INotifyPropertyChanged
    {
        private bool _isSelected;

        #region Raw Data

        /// <summary>
        /// Gets the absolute position of the match in the file.
        /// </summary>
        public long Position { get; }

        /// <summary>
        /// Gets the length of the matched pattern in bytes.
        /// </summary>
        public int Length { get; }

        /// <summary>
        /// Gets the actual bytes that were matched.
        /// </summary>
        public byte[] MatchedBytes { get; }

        /// <summary>
        /// Gets the 8 bytes before the match (null if at start of file).
        /// </summary>
        public byte[] ContextBefore { get; }

        /// <summary>
        /// Gets the 8 bytes after the match (null if at end of file).
        /// </summary>
        public byte[] ContextAfter { get; }

        /// <summary>
        /// Gets the 1-based index in the results list.
        /// </summary>
        public int Index { get; }

        #endregion

        #region Display Properties

        /// <summary>
        /// Gets the position formatted as hex (e.g., "0x00041A80").
        /// </summary>
        public string PositionHex => $"0x{Position:X8}";

        /// <summary>
        /// Gets the position formatted as decimal with thousand separators (e.g., "269,440").
        /// </summary>
        public string PositionDec => Position.ToString("N0");

        /// <summary>
        /// Gets the hex representation of the context with match highlighted.
        /// Format: "4D 5A [90 00] 03" (brackets around match)
        /// </summary>
        public string HexContext
        {
            get
            {
                var sb = new StringBuilder();

                // Context before (max 8 bytes)
                if (ContextBefore != null && ContextBefore.Length > 0)
                {
                    for (int i = 0; i < Math.Min(4, ContextBefore.Length); i++)
                        sb.Append(ByteConverters.ByteToHex(ContextBefore[i])).Append(' ');
                }

                // Matched bytes (highlighted with brackets)
                sb.Append('[');
                for (int i = 0; i < MatchedBytes.Length && i < 8; i++)
                {
                    sb.Append(ByteConverters.ByteToHex(MatchedBytes[i]));
                    if (i < MatchedBytes.Length - 1 && i < 7)
                        sb.Append(' ');
                }
                sb.Append(']');

                // Context after (max 8 bytes)
                if (ContextAfter != null && ContextAfter.Length > 0)
                {
                    sb.Append(' ');
                    for (int i = 0; i < Math.Min(4, ContextAfter.Length); i++)
                        sb.Append(ByteConverters.ByteToHex(ContextAfter[i])).Append(' ');
                }

                return sb.ToString().TrimEnd();
            }
        }

        /// <summary>
        /// Gets the ASCII representation of the context (printable chars only).
        /// </summary>
        public string AsciiContext
        {
            get
            {
                var sb = new StringBuilder();

                // Context before
                if (ContextBefore != null)
                {
                    foreach (byte b in ContextBefore)
                        sb.Append(ByteConverters.ByteToChar(b));
                }

                // Matched bytes
                foreach (byte b in MatchedBytes)
                    sb.Append(ByteConverters.ByteToChar(b));

                // Context after
                if (ContextAfter != null)
                {
                    foreach (byte b in ContextAfter)
                        sb.Append(ByteConverters.ByteToChar(b));
                }

                return sb.ToString();
            }
        }

        /// <summary>
        /// Gets or sets whether this result is currently selected.
        /// </summary>
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged();
                }
            }
        }

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes a new instance of SearchResultItemViewModel.
        /// </summary>
        public SearchResultItemViewModel(
            long position,
            int length,
            byte[] matchedBytes,
            byte[] contextBefore,
            byte[] contextAfter,
            int index)
        {
            Position = position;
            Length = length;
            MatchedBytes = matchedBytes ?? Array.Empty<byte>();
            ContextBefore = contextBefore;
            ContextAfter = contextAfter;
            Index = index;
        }

        #endregion

        #region TBL Context Support

        /// <summary>
        /// Gets the TBL representation of the context if a TBL is provided.
        /// </summary>
        /// <param name="tbl">The TBL stream to use for conversion.</param>
        /// <returns>Decoded TBL string or empty if no TBL.</returns>
        public string GetTblContext(TblStream tbl)
        {
            if (tbl == null)
                return string.Empty;

            var sb = new StringBuilder();

            // Combine all bytes for TBL conversion
            var allBytes = new byte[(ContextBefore?.Length ?? 0) + MatchedBytes.Length + (ContextAfter?.Length ?? 0)];
            int offset = 0;

            if (ContextBefore != null)
            {
                Array.Copy(ContextBefore, 0, allBytes, offset, ContextBefore.Length);
                offset += ContextBefore.Length;
            }

            Array.Copy(MatchedBytes, 0, allBytes, offset, MatchedBytes.Length);
            offset += MatchedBytes.Length;

            if (ContextAfter != null)
            {
                Array.Copy(ContextAfter, 0, allBytes, offset, ContextAfter.Length);
            }

            // Convert using TBL
            return tbl.ToTblString(allBytes);
        }

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion

        public override string ToString()
        {
            return $"Result #{Index}: {PositionHex} ({Length} bytes)";
        }
    }
}
