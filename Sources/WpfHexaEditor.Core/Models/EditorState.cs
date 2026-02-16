//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5
//////////////////////////////////////////////

using System;
using System.Collections.Generic;
using WpfHexaEditor.Core.Platform.Media;
using System.Xml.Serialization;

namespace WpfHexaEditor.Models
{
    /// <summary>
    /// Represents the persistent state of the hex editor.
    /// Can be saved to and loaded from XML.
    /// V1 compatible feature - Phase 7.5 (State Persistence).
    /// </summary>
    [Serializable]
    public class EditorState
    {
        /// <summary>
        /// File name (path) of the opened file.
        /// </summary>
        public string FileName { get; set; }

        /// <summary>
        /// Current cursor position (virtual).
        /// </summary>
        public long Position { get; set; }

        /// <summary>
        /// Selection start position (virtual).
        /// </summary>
        public long SelectionStart { get; set; }

        /// <summary>
        /// Selection stop position (virtual).
        /// </summary>
        public long SelectionStop { get; set; }

        /// <summary>
        /// Bookmarks (virtual positions).
        /// </summary>
        public List<long> Bookmarks { get; set; } = new List<long>();

        /// <summary>
        /// Zoom scale (1.0 = 100%).
        /// </summary>
        public double ZoomScale { get; set; } = 1.0;

        /// <summary>
        /// Bytes per line.
        /// </summary>
        public int BytesPerLine { get; set; } = 16;

        /// <summary>
        /// Edit mode (Overwrite or Insert).
        /// </summary>
        public bool IsInsertMode { get; set; } = false;

        /// <summary>
        /// Show offset column.
        /// </summary>
        public bool ShowOffset { get; set; } = true;

        /// <summary>
        /// Show ASCII column.
        /// </summary>
        public bool ShowAscii { get; set; } = true;

        /// <summary>
        /// Show header row.
        /// </summary>
        public bool ShowHeader { get; set; } = true;

        /// <summary>
        /// Show status bar.
        /// </summary>
        public bool ShowStatusBar { get; set; } = true;

        /// <summary>
        /// Show line numbers.
        /// </summary>
        public bool ShowLineNumbers { get; set; } = true;

        /// <summary>
        /// Character table type (Ascii, TBL, etc.).
        /// </summary>
        public string CharacterTableType { get; set; } = "Ascii";

        /// <summary>
        /// TBL file path (if using TBL).
        /// </summary>
        public string TblFilePath { get; set; }

        #region Colors (serialized as ARGB strings)

        /// <summary>
        /// Selection first color (ARGB hex string).
        /// </summary>
        public string SelectionFirstColor { get; set; } = "#FF0078D4";

        /// <summary>
        /// Selection second color (ARGB hex string).
        /// </summary>
        public string SelectionSecondColor { get; set; } = "#FF004D87";

        /// <summary>
        /// Byte modified color (ARGB hex string).
        /// </summary>
        public string ByteModifiedColor { get; set; } = "#FFFFF59D";

        /// <summary>
        /// Byte deleted color (ARGB hex string).
        /// </summary>
        public string ByteDeletedColor { get; set; } = "#FFFFCCCC";

        /// <summary>
        /// Foreground color (ARGB hex string).
        /// </summary>
        public string ForegroundColor { get; set; } = "#FF000000";

        /// <summary>
        /// Background color (ARGB hex string).
        /// </summary>
        public string BackgroundColor { get; set; } = "#FFFFFFFF";

        /// <summary>
        /// Mouse over color (ARGB hex string).
        /// </summary>
        public string MouseOverColor { get; set; } = "#FFE5F3FB";

        /// <summary>
        /// Bar chart color (ARGB hex string).
        /// </summary>
        public string BarChartColor { get; set; } = "#FF0078D4";

        #endregion

        /// <summary>
        /// Timestamp when state was saved.
        /// </summary>
        public DateTime SavedAt { get; set; } = DateTime.Now;

        /// <summary>
        /// Version of the editor that saved this state.
        /// </summary>
        public string EditorVersion { get; set; } = "2.2.0";

        /// <summary>
        /// Helper method to convert PlatformColor to ARGB hex string.
        /// </summary>
        public static string ColorToString(PlatformColor color)
        {
            return $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";
        }

        /// <summary>
        /// Helper method to convert ARGB hex string to PlatformColor.
        /// </summary>
        public static PlatformColor StringToColor(string argb)
        {
            try
            {
                return (PlatformColor)ColorConverter.ConvertFromString(argb);
            }
            catch
            {
                return Colors.Black;
            }
        }
    }
}
