//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5
//////////////////////////////////////////////

using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using WpfHexaEditor.Core;

namespace WpfHexaEditor.Models
{
    /// <summary>
    /// Represents a single line in the hex editor display
    /// </summary>
    public class HexLine
    {
        /// <summary>
        /// Line number (0-based)
        /// </summary>
        public long LineNumber { get; set; }

        /// <summary>
        /// Virtual position of the first byte in this line
        /// </summary>
        public VirtualPosition StartPosition { get; set; }

        /// <summary>
        /// Bytes in this line (max BytePerLine)
        /// </summary>
        public List<ByteData> Bytes { get; set; } = new();

        /// <summary>
        /// Offset label for this line (e.g., "0x0000")
        /// </summary>
        public string OffsetLabel { get; set; } = string.Empty;
    }

    /// <summary>
    /// Represents a single byte with its metadata
    /// </summary>
    public class ByteData : INotifyPropertyChanged
    {
        private bool _isSelected;
        private bool _isHighlighted;
        private bool _isCursor;

        /// <summary>
        /// Virtual position of this byte
        /// </summary>
        public VirtualPosition VirtualPos { get; set; }

        /// <summary>
        /// Physical position (if different from virtual)
        /// </summary>
        public PhysicalPosition? PhysicalPos { get; set; }

        /// <summary>
        /// Byte value
        /// </summary>
        public byte Value { get; set; }

        /// <summary>
        /// Byte action (Added, Modified, Deleted, Nothing)
        /// </summary>
        public ByteAction Action { get; set; } = ByteAction.Nothing;

        /// <summary>
        /// Is this byte selected?
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

        /// <summary>
        /// Is this byte highlighted (search result)?
        /// </summary>
        public bool IsHighlighted
        {
            get => _isHighlighted;
            set
            {
                if (_isHighlighted != value)
                {
                    _isHighlighted = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Is this byte the current cursor position?
        /// </summary>
        public bool IsCursor
        {
            get => _isCursor;
            set
            {
                if (_isCursor != value)
                {
                    _isCursor = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Hex representation (e.g., "FF")
        /// </summary>
        public string HexString => Value.ToString("X2");

        /// <summary>
        /// ASCII representation (printable char or '.')
        /// </summary>
        public char AsciiChar => Value >= 32 && Value <= 126 ? (char)Value : '.';

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }
}
