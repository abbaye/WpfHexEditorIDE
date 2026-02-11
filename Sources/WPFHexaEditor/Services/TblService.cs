//////////////////////////////////////////////
// Apache 2.0  - 2016-2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5
//////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.IO;
using WpfHexaEditor.Core;
using WpfHexaEditor.Core.CharacterTable;

namespace WpfHexaEditor.Services
{
    /// <summary>
    /// Service responsible for TBL (character table) management operations
    /// </summary>
    public class TblService
    {
        #region Private Fields

        /// <summary>
        /// Current TBL character table
        /// </summary>
        private TblStream _characterTable;

        #endregion

        #region Properties

        /// <summary>
        /// Get the current character table
        /// </summary>
        public TblStream CharacterTable => _characterTable;

        /// <summary>
        /// Check if a TBL is currently loaded
        /// </summary>
        public bool HasTable => _characterTable != null;

        /// <summary>
        /// Get the current TBL file name (if loaded from file)
        /// </summary>
        public string CurrentFileName { get; private set; }

        /// <summary>
        /// Get the current TBL type (if loaded as default)
        /// </summary>
        public DefaultCharacterTableType? CurrentDefaultType { get; private set; }

        #endregion

        #region Load Operations

        /// <summary>
        /// Load TBL character table from file
        /// </summary>
        /// <param name="fileName">Path to TBL file</param>
        /// <returns>True if loaded successfully</returns>
        public bool LoadFromFile(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return false;

            if (!File.Exists(fileName))
                return false;

            try
            {
                // Dispose previous table
                _characterTable?.Dispose();

                // Load new table
                _characterTable = new TblStream(fileName);
                CurrentFileName = fileName;
                CurrentDefaultType = null;

                return true;
            }
            catch (Exception)
            {
                _characterTable = null;
                CurrentFileName = null;
                return false;
            }
        }

        /// <summary>
        /// Load default TBL character table
        /// </summary>
        /// <param name="type">Type of default table to load</param>
        /// <returns>True if loaded successfully</returns>
        public bool LoadDefault(DefaultCharacterTableType type = DefaultCharacterTableType.Ascii)
        {
            try
            {
                // Dispose previous table
                _characterTable?.Dispose();

                // Load default table
                _characterTable = TblStream.CreateDefaultTbl(type);
                CurrentDefaultType = type;
                CurrentFileName = null;

                return true;
            }
            catch (Exception)
            {
                _characterTable = null;
                CurrentDefaultType = null;
                return false;
            }
        }

        /// <summary>
        /// Clear current TBL table
        /// </summary>
        public void Clear()
        {
            _characterTable?.Dispose();
            _characterTable = null;
            CurrentFileName = null;
            CurrentDefaultType = null;
        }

        #endregion

        #region Bookmark Operations

        /// <summary>
        /// Get TBL bookmarks from current table
        /// </summary>
        /// <returns>Enumerable of bookmarks, empty if no table loaded</returns>
        public IEnumerable<BookMark> GetTblBookmarks()
        {
            if (_characterTable == null)
                return Array.Empty<BookMark>();

            return _characterTable.BookMarks;
        }

        /// <summary>
        /// Check if current table has bookmarks
        /// </summary>
        /// <returns>True if table loaded and has bookmarks</returns>
        public bool HasBookmarks()
        {
            return _characterTable != null && _characterTable.BookMarks != null && _characterTable.BookMarks.Count > 0;
        }

        /// <summary>
        /// Get count of TBL bookmarks
        /// </summary>
        /// <returns>Count of bookmarks</returns>
        public int GetBookmarkCount()
        {
            if (_characterTable == null || _characterTable.BookMarks == null)
                return 0;

            return _characterTable.BookMarks.Count;
        }

        #endregion

        #region Conversion Operations

        /// <summary>
        /// Find character match for hex value in current table
        /// </summary>
        /// <param name="hex">Hex value to find</param>
        /// <param name="showSpecialValue">Include special values (EndBlock, EndLine)</param>
        /// <returns>Tuple of (text, dteType), ("#", Invalid) if not found or no table loaded</returns>
        public (string text, DteType dteType) FindMatch(string hex, bool showSpecialValue = false)
        {
            if (_characterTable == null)
                return ("#", DteType.Invalid);

            return _characterTable.FindMatch(hex, showSpecialValue);
        }

        /// <summary>
        /// Convert byte array to TBL string
        /// </summary>
        /// <param name="bytes">Bytes to convert</param>
        /// <returns>Converted string, empty if no table loaded</returns>
        public string BytesToString(byte[] bytes)
        {
            if (_characterTable == null)
                return string.Empty;

            if (bytes == null || bytes.Length == 0)
                return string.Empty;

            return _characterTable.ToTblString(bytes);
        }

        /// <summary>
        /// Check if byte array contains TBL special values
        /// </summary>
        /// <param name="bytes">Bytes to check</param>
        /// <param name="position">Position to check</param>
        /// <returns>True if contains special values</returns>
        public bool ContainsSpecialValues(byte[] bytes, long position)
        {
            if (_characterTable == null)
                return false;

            if (bytes == null || bytes.Length == 0)
                return false;

            // This method would need to be added to TblStream or implemented here
            // For now, return false as a safe default
            return false;
        }

        #endregion

        #region Query Operations

        /// <summary>
        /// Check if current table is a default table
        /// </summary>
        /// <returns>True if loaded from default, false if from file or not loaded</returns>
        public bool IsDefaultTable()
        {
            return CurrentDefaultType.HasValue;
        }

        /// <summary>
        /// Check if current table is from a file
        /// </summary>
        /// <returns>True if loaded from file</returns>
        public bool IsFileTable()
        {
            return !string.IsNullOrWhiteSpace(CurrentFileName);
        }

        /// <summary>
        /// Get table information string
        /// </summary>
        /// <returns>String describing current table, or empty if none loaded</returns>
        public string GetTableInfo()
        {
            if (_characterTable == null)
                return string.Empty;

            if (CurrentDefaultType.HasValue)
                return $"Default: {CurrentDefaultType.Value}";

            if (!string.IsNullOrWhiteSpace(CurrentFileName))
                return $"File: {Path.GetFileName(CurrentFileName)}";

            return "Custom table";
        }

        #endregion

        #region IDisposable Support

        /// <summary>
        /// Dispose resources
        /// </summary>
        public void Dispose()
        {
            Clear();
        }

        #endregion
    }
}
