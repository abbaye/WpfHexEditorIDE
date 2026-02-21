//////////////////////////////////////////////
// Apache 2.0  - 2003-2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5 (Performance optimizations)
//////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using WpfHexaEditor.Core.Bytes;
using WpfHexaEditor.TBLEditorModule.Models;
using WpfHexaEditor.TBLEditorModule.Services;

namespace WpfHexaEditor.Core.CharacterTable
{
    /// <summary>
    /// Used to manage Thingy TBL file (entry=value) with full standard format support
    /// </summary>
    public sealed class TblStream : IDisposable
    {
        #region Global class variables
        /// <summary>
        /// TBL file path
        /// </summary>
        private string _fileName = string.Empty;

        /// <summary>
        /// Represents the whole TBL file
        /// </summary>
        private Dictionary<string, Dte> _dteList = new();

        /// <summary>
        /// Cached EndBlock and EndLine values for performance
        /// </summary>
        private string _endBlock = string.Empty;
        private string _endLine = string.Empty;

        /// <summary>
        /// Maximum byte length for multi-byte sequences (8 bytes = 16 hex chars)
        /// </summary>
        private const int MAX_BYTE_LENGTH = 8;

        /// <summary>
        /// Modification tracking
        /// </summary>
        private int _modificationCount = 0;
        private bool _isModified = false;

        /// <summary>
        /// Statistics cache
        /// </summary>
        private TblStatistics _cachedStatistics;
        private bool _statisticsDirty = true;

        #endregion

        #region Events
        /// <summary>
        /// Event raised when TBL is modified
        /// </summary>
        public event EventHandler Modified;

        /// <summary>
        /// Raise the Modified event
        /// </summary>
        private void OnModified() => Modified?.Invoke(this, EventArgs.Empty);
        #endregion

        #region Constructors
        /// <summary>
        /// Constructor to load the DTE file
        /// </summary>
        public TblStream(string fileName) => FileName = fileName;

        /// <summary>
        /// Constructor to load the DTE file
        /// </summary>
        public TblStream() { }
        #endregion

        #region Indexer

        /// <summary>
        /// Indexer to work on the DTE contained in TBL in the manner of a table.
        /// </summary>
        public Dte this[string index]
        {
            get => _dteList[index];
            set => _dteList[index] = value;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Find entry in TBL file
        /// </summary>
        /// <param name="hex">Hex value to find match</param>
        /// <param name="showSpecialValue">Find the Endblock and EndLine</param>
        public (string text, DteType dteType) FindMatch(string hex, bool showSpecialValue)
        {
            // OPTIMIZED: Use TryGetValue instead of ContainsKey+indexer to reduce Dictionary lookups by 50%
            if (showSpecialValue)
            {
                if (_dteList.TryGetValue($"/{hex}", out var endBlock))
                    return (Properties.Resources.EndTagString, DteType.EndBlock);
                if (_dteList.TryGetValue($"*{hex}", out var endLine))
                    return (Properties.Resources.LineTagString, DteType.EndLine);
            }

            // Return standard format raw hex if no match (conformity with standard)
            return _dteList.TryGetValue(hex, out var dte)
                ? (dte.Value, dte.Type)
                : ($"[${hex}]", DteType.Invalid);
        }

        /// <summary>
        /// Convert data to TBL string.
        /// </summary>
        /// <returns>
        /// Return string converted to TBL string representation.
        /// Return null on error
        /// </returns>
        public string ToTblString(byte[] data)
        {
            if (data == null || data.Length == 0) return string.Empty;

            // OPTIMIZED: Pre-allocate StringBuilder capacity
            var sb = new StringBuilder(data.Length * 2);

            // GREEDY MATCHING: Try longest matches first (8 bytes down to 1 byte)
            for (var i = 0; i < data.Length; )
            {
                bool matchFound = false;

                // Try multi-byte matches from longest to shortest (8 bytes down to 2 bytes)
                int maxLen = Math.Min(MAX_BYTE_LENGTH, data.Length - i);
                for (int len = maxLen; len >= 2; len--)
                {
                    // Build hex string for this length
                    var hexKey = new StringBuilder(len * 2);
                    for (int j = 0; j < len; j++)
                        hexKey.Append(ByteConverters.ByteToHex(data[i + j]));

                    if (_dteList.TryGetValue(hexKey.ToString(), out var dte))
                    {
                        sb.Append(dte.Value);
                        i += len; // Skip consumed bytes
                        matchFound = true;
                        break;
                    }
                }

                if (matchFound)
                    continue;

                // Fall back to single byte match
                var singleKey = ByteConverters.ByteToHex(data[i]);
                if (_dteList.TryGetValue(singleKey, out var single))
                    sb.Append(single.Value);
                else
                    sb.Append($"[${singleKey}]"); // Standard format raw hex

                i++; // Move to next byte
            }

            return sb.ToString();
        }

        /// <summary>
        /// Close the TBL and clear object
        /// </summary>
        public void Close()
        {
            _fileName = string.Empty;
            _dteList.Clear();
            _endBlock = string.Empty;
            _endLine = string.Empty;
        }

        /// <summary>
        /// Load the TBL file with full standard format support
        /// </summary>
        public void Load(string tblString)
        {
            // OPTIMIZED: Single-pass parsing with minimal allocations
            _dteList.Clear();
            BookMarks.Clear();

            if (string.IsNullOrWhiteSpace(tblString))
                return;

            var lines = tblString.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);

            // Pre-allocate dictionary capacity (most TBL files have 256-512 entries)
            if (_dteList.Count == 0)
                _dteList = new(Math.Min(lines.Length, 512));

            // Single-pass parsing (both DTEs and bookmarks)
            foreach (var rawLine in lines)
            {
                var line = rawLine.TrimEnd('\r', '\n').Trim();

                // NEW: Skip comment lines (start with #) or empty lines
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                    continue;

                // Parse bookmark lines (start with '(')
                if (line.StartsWith("("))
                {
                    TryParseBookmark(line);
                    continue;
                }

                // Parse DTE entry lines (contain '=')
                var equalIndex = line.IndexOf('=');
                if (equalIndex > 0)
                {
                    TryParseDteEntry(line, equalIndex);
                }
            }

            // Update cached EndBlock/EndLine values
            UpdateEndBlockAndEndLineCache();

            // Reset modification tracking after load
            _isModified = false;
            _modificationCount = 0;
            _statisticsDirty = true;
        }

        /// <summary>
        /// Update cached EndBlock and EndLine values
        /// </summary>
        private void UpdateEndBlockAndEndLineCache()
        {
            _endBlock = string.Empty;
            _endLine = string.Empty;

            foreach (var dte in _dteList.Values)
            {
                if (dte.Type == DteType.EndBlock)
                    _endBlock = dte.Entry;
                else if (dte.Type == DteType.EndLine)
                    _endLine = dte.Entry;

                // Early exit if both found
                if (!string.IsNullOrEmpty(_endBlock) && !string.IsNullOrEmpty(_endLine))
                    break;
            }
        }

        /// <summary>
        /// Try to parse a DTE entry from a line with enhanced validation
        /// </summary>
        private void TryParseDteEntry(string line, int equalIndex)
        {
            try
            {
                var entry = line.Substring(0, equalIndex).Trim();
                var valueStart = equalIndex + 1;
                var value = valueStart < line.Length ? line.Substring(valueStart) : string.Empty;

                // Remove trailing carriage return if present
                if (value.EndsWith("\r"))
                    value = value.Substring(0, value.Length - 1);

                // NEW: Parse inline comment (format: entry=value # comment)
                string inlineComment = string.Empty;
                int commentIndex = value.IndexOf('#');
                if (commentIndex >= 0)
                {
                    inlineComment = value.Substring(commentIndex + 1).Trim();
                    value = value.Substring(0, commentIndex).TrimEnd();
                }

                // NEW: Parse escape sequences (\n, \r, \t)
                value = value.Replace("\\n", "\n")
                             .Replace("\\r", "\r")
                             .Replace("\\t", "\t");

                // Determine DTE type based on entry length and prefix
                DteType type;
                if (entry.StartsWith("/"))
                {
                    type = DteType.EndBlock;
                    entry = entry.Substring(1); // Remove '/' prefix
                }
                else if (entry.StartsWith("*"))
                {
                    type = DteType.EndLine;
                    entry = entry.Substring(1); // Remove '*' prefix
                }
                else if (value == "=")
                {
                    // Special case: "XX==" means value is "="
                    type = DteType.DualTitleEncoding;
                }
                else
                {
                    // NEW: Strict validation
                    if (!IsValidHexEntry(entry, out string validationError))
                    {
                        Debug.WriteLine($"Skipping invalid entry: {line} - {validationError}");
                        return;
                    }

                    // Determine type based on entry length
                    // Support 1-8 bytes (2-16 hex chars)
                    if (entry.Length == 2)
                        type = value.Length == 1 ? DteType.Ascii : DteType.DualTitleEncoding;
                    else if (entry.Length % 2 == 0 && entry.Length >= 4 && entry.Length <= 16)
                        type = DteType.MultipleTitleEncoding;  // 2-8 bytes (4-16 hex chars)
                    else
                        type = DteType.Invalid;  // Reject odd-length or > 16 chars
                }

                if (type != DteType.Invalid)
                {
                    var dte = new Dte(entry, value, type);
                    // Add inline comment if found
                    if (!string.IsNullOrEmpty(inlineComment))
                        dte.Comment = inlineComment;
                    // Add to dictionary, avoiding duplicates (Issue #105)
                    if (!_dteList.ContainsKey(dte.Entry))
                        _dteList.Add(dte.Entry, dte);
                }
            }
            catch
            {
                // Silently ignore malformed entries
            }
        }

        /// <summary>
        /// Validate hex entry format (strict validation for standard conformity)
        /// </summary>
        private static bool IsValidHexEntry(string hex, out string error)
        {
            error = null;

            // Must be even length (2, 4, 6, 8, ..., 16)
            if (hex.Length % 2 != 0)
            {
                error = "Hex entry must have even number of characters";
                return false;
            }

            // Length limits: 2-16 chars (1-8 bytes)
            if (hex.Length < 2 || hex.Length > 16)
            {
                error = $"Hex entry length must be 2-16 characters (1-8 bytes), got {hex.Length}";
                return false;
            }

            // All chars must be hex digits (0-9, A-F, case-insensitive)
            if (!Regex.IsMatch(hex, "^[0-9A-Fa-f]+$"))
            {
                error = "Hex entry must contain only hex digits (0-9, A-F)";
                return false;
            }

            return true;
        }

        /// <summary>
        /// Try to parse a bookmark from a line (format: "(addressh)description")
        /// </summary>
        private void TryParseBookmark(string line)
        {
            try
            {
                if (!line.StartsWith("("))
                    return;

                var closeParenIndex = line.IndexOf(')');
                if (closeParenIndex <= 1)
                    return;

                var hIndex = line.IndexOf('h');
                if (hIndex <= 1 || hIndex >= closeParenIndex)
                    return;

                // Extract address (between '(' and 'h')
                var addressStr = line.Substring(1, hIndex - 1);
                var (_, position) = ByteConverters.HexLiteralToLong(addressStr);

                // Extract description (after ')')
                var description = closeParenIndex + 1 < line.Length
                    ? line.Substring(closeParenIndex + 1).TrimEnd('\r', '\n')
                    : string.Empty;

                var bookmark = new BookMark
                {
                    BytePositionInStream = position,
                    Description = description,
                    Marker = ScrollMarker.TblBookmark
                };

                BookMarks.Add(bookmark);
            }
            catch
            {
                // Silently ignore malformed bookmarks
            }
        }

        /// <summary>
        /// Load TBL file with UTF-8 BOM support
        /// </summary>
        public void Load()
        {
            //opening the file
            if (!File.Exists(_fileName))
            {
                var fs = File.Create(_fileName);
                fs.Close();
            }

            StreamReader tblFile;
            try
            {
                // NEW: Use UTF-8 encoding with BOM detection for standard conformity
                tblFile = new StreamReader(_fileName, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            }
            catch
            {
                return;
            }

            if (tblFile.BaseStream.CanRead)
                Load(tblFile.ReadToEnd());

            tblFile.Close();
        }

        /// <summary>
        /// Save tbl file with escape sequences and UTF-8 encoding
        /// </summary>
        public void Save()
        {
            var myFile = new FileStream(_fileName, FileMode.Create, FileAccess.Write);
            // Use UTF-8 for standard conformity
            var tblFile = new StreamWriter(myFile, Encoding.UTF8);

            if (tblFile.BaseStream.CanWrite)
            {
                //Save tbl set
                foreach (var dte in _dteList)
                {
                    if (dte.Value.Type != DteType.EndBlock && dte.Value.Type != DteType.EndLine)
                    {
                        // NEW: Escape special characters
                        string escapedValue = dte.Value.Value
                            .Replace("\n", "\\n")
                            .Replace("\r", "\\r")
                            .Replace("\t", "\\t");

                        // NEW: Add inline comment if present
                        string line = dte.Value.Entry + "=" + escapedValue;
                        if (!string.IsNullOrWhiteSpace(dte.Value.Comment))
                            line += " # " + dte.Value.Comment;

                        tblFile.WriteLine(line);
                    }
                    else
                        tblFile.WriteLine(dte.Value.Entry);
                }

                //Save bookmark
                tblFile.WriteLine();
                foreach (var mark in BookMarks)
                    tblFile.WriteLine(mark.ToString());

                //Add to line at end of file. Needed for some apps that using tbl file
                tblFile.WriteLine();
                tblFile.WriteLine();
            }

            //close file
            tblFile.Close();

            // Reset modified flag after save
            _isModified = false;
            OnModified();
        }

        /// <summary>
        /// Add a DTE/MTE in TBL with modification tracking
        /// </summary>
        public void Add(Dte dte)
        {
            if (dte is null) return;

            _dteList.Add(dte.Entry, dte);

            // Update cache if this is an EndBlock or EndLine
            if (dte.Type == DteType.EndBlock)
                _endBlock = dte.Entry;
            else if (dte.Type == DteType.EndLine)
                _endLine = dte.Entry;

            // Track modification
            _modificationCount++;
            _isModified = true;
            _statisticsDirty = true;
            OnModified();
        }

        /// <summary>
        /// Remove TBL entry with modification tracking
        /// </summary>
        public void Remove(Dte dte)
        {
            if (dte is null) return;

            _dteList.Remove(dte.Entry);

            // Track modification
            _modificationCount++;
            _isModified = true;
            _statisticsDirty = true;
            OnModified();
        }

        /// <summary>
        /// Clear all entries
        /// </summary>
        public void Clear()
        {
            _dteList.Clear();
            _endBlock = string.Empty;
            _endLine = string.Empty;
            BookMarks.Clear();
            _modificationCount++;
            _isModified = true;
            _statisticsDirty = true;
            OnModified();
        }

        /// <summary>
        /// Reset TBL to empty state without tracking modification
        /// </summary>
        public void Reset()
        {
            Clear();
            _modificationCount = 0;
            _isModified = false;
        }

        /// <summary>
        /// Reset modified flag (call after save or when accepting changes)
        /// </summary>
        public void ResetModifiedFlag()
        {
            _isModified = false;
            _modificationCount = 0;
            OnModified();
        }

        /// <summary>
        /// Get the string representation of the TBL
        /// </summary>
        public override string ToString()
        {
            var sb = new StringBuilder();

            //Save tbl set
            foreach (var dte in _dteList)
            {
                if (dte.Value.Type != DteType.EndBlock && dte.Value.Type != DteType.EndLine)
                {
                    // Escape special characters
                    string escapedValue = dte.Value.Value
                        .Replace("\n", "\\n")
                        .Replace("\r", "\\r")
                        .Replace("\t", "\\t");
                    sb.AppendLine(dte.Value.Entry + "=" + escapedValue);
                }
                else
                    sb.AppendLine(dte.Value.Entry);
            }

            //Save bookmark
            sb.AppendLine();
            foreach (var mark in BookMarks)
                sb.AppendLine(mark.ToString());

            //Add to line at end of file. Needed for some apps that using tbl file
            sb.AppendLine();
            sb.AppendLine();

            return sb.ToString();
        }

        #endregion

        #region Enumeration API for TBL Editor

        /// <summary>
        /// Get read-only dictionary of all entries
        /// </summary>
        public IReadOnlyDictionary<string, Dte> Entries => _dteList;

        /// <summary>
        /// Get all entries as enumerable
        /// </summary>
        public IEnumerable<Dte> GetAllEntries() => _dteList.Values;

        /// <summary>
        /// Get entries by type
        /// </summary>
        public IEnumerable<Dte> GetEntriesByType(DteType type) =>
            _dteList.Values.Where(d => d.Type == type);

        /// <summary>
        /// Get entries by byte length
        /// </summary>
        public IEnumerable<Dte> GetEntriesByLength(int byteLength) =>
            _dteList.Values.Where(d => d.Entry.Length == byteLength * 2);

        /// <summary>
        /// Check if entry exists in TBL
        /// </summary>
        public bool ContainsEntry(string hex) =>
            _dteList.ContainsKey(hex?.ToUpperInvariant() ?? string.Empty);

        /// <summary>
        /// Get entry by hex key
        /// </summary>
        public Dte GetEntry(string hex) =>
            _dteList.TryGetValue(hex?.ToUpperInvariant() ?? string.Empty, out var dte) ? dte : null;

        #endregion

        #region Statistics with caching

        /// <summary>
        /// Get cached statistics (recompute if dirty)
        /// </summary>
        public TblStatistics GetStatistics()
        {
            if (!_statisticsDirty && _cachedStatistics != null)
                return _cachedStatistics;

            var stats = new TblStatistics
            {
                TotalCount = _dteList.Count
            };

            foreach (var dte in _dteList.Values)
            {
                switch (dte.Type)
                {
                    case DteType.Ascii:
                        stats.AsciiCount++;
                        break;
                    case DteType.DualTitleEncoding:
                        stats.DteCount++;
                        if (dte.Entry.Length == 4) stats.Byte2Count++;
                        break;
                    case DteType.MultipleTitleEncoding:
                        stats.MteCount++;
                        int byteLen = dte.Entry.Length / 2;
                        if (byteLen == 3) stats.Byte3Count++;
                        else if (byteLen == 4) stats.Byte4Count++;
                        else stats.Byte5PlusCount++;
                        break;
                    case DteType.Japonais:
                        stats.JapaneseCount++;
                        break;
                    case DteType.EndBlock:
                        stats.EndBlockCount++;
                        break;
                    case DteType.EndLine:
                        stats.EndLineCount++;
                        break;
                }
            }

            // Calculate coverage (single-byte only)
            var singleByteEntries = _dteList.Values
                .Where(d => d.Entry.Length == 2)
                .Select(d => d.Entry)
                .Distinct()
                .Count();
            stats.CoveragePercent = (singleByteEntries / 256.0) * 100;

            _cachedStatistics = stats;
            _statisticsDirty = false;

            return stats;
        }

        /// <summary>
        /// Invalidate statistics cache (called on modification)
        /// </summary>
        private void InvalidateStatistics()
        {
            _statisticsDirty = true;
        }

        #endregion

        #region Property
        /// <summary>
        /// Get or set the File path to TBL
        /// </summary>
        public string FileName
        {
            get => _fileName;
            internal set
            {
                if (File.Exists(value))
                {
                    _fileName = value;
                    Load();
                }
                else
                    throw new FileNotFoundException();
            }
        }

        /// <summary>
        /// Get the count of DTE/MTE in the TBL
        /// </summary>
        public int Length => _dteList.Count;

        /// <summary>
        /// Get the count of DTE/MTE in the TBL (alias for Length)
        /// </summary>
        public int Count => _dteList.Count;

        /// <summary>
        /// Get of set bookmarks
        /// </summary>
        public List<BookMark> BookMarks { get; set; } = new();

        /// <summary>
        /// Get modification count
        /// </summary>
        public int ModificationCount => _modificationCount;

        /// <summary>
        /// Get whether TBL has been modified
        /// </summary>
        public bool IsModified => _isModified;

        public int TotalDte => _dteList.Count(l => l.Value.Type == DteType.DualTitleEncoding);
        public int TotalMte => _dteList.Count(l => l.Value.Type == DteType.MultipleTitleEncoding);
        public int TotalAscii => _dteList.Count(l => l.Value.Type == DteType.Ascii);
        public int TotalInvalid => _dteList.Count(l => l.Value.Type == DteType.Invalid);
        public int TotalJaponais => _dteList.Count(l => l.Value.Type == DteType.Japonais);
        public int TotalEndLine => _dteList.Count(l => l.Value.Type == DteType.EndLine);
        public int TotalEndBlock => _dteList.Count(l => l.Value.Type == DteType.EndBlock);

        // Multi-byte statistics (by byte count)
        public int Total3Byte => _dteList.Count(l => l.Value.Entry.Length == 6);  // 3 bytes = 6 hex chars
        public int Total4Byte => _dteList.Count(l => l.Value.Entry.Length == 8);  // 4 bytes = 8 hex chars
        public int Total5PlusByte => _dteList.Count(l => l.Value.Entry.Length >= 10);  // 5+ bytes = 10+ hex chars

        /// <summary>
        /// Get the end block char (cached for performance)
        /// </summary>
        public string EndBlock => _endBlock;

        /// <summary>
        /// Get the end line char (cached for performance)
        /// </summary>
        public string EndLine => _endLine;

        /// <summary>
        /// Enable/Disable Readonly on control.
        /// </summary>
        public bool AllowEdit { get; set; }

        #endregion

        #region Build default TBL

        public static TblStream CreateDefaultTbl(DefaultCharacterTableType type = DefaultCharacterTableType.Ascii)
        {
            var tbl = new TblStream();

            switch (type)
            {
                case DefaultCharacterTableType.Ascii:
                    for (byte i = 0; i < 255; i++)
                        tbl.Add(new Dte(ByteConverters.ByteToHex(i).ToUpperInvariant(), $"{ByteConverters.ByteToChar(i)}"));
                    break;
                case DefaultCharacterTableType.EbcdicWithSpecialChar:
                    tbl.Load(Properties.Resources.EBCDIC);
                    break;
                case DefaultCharacterTableType.EbcdicNoSpecialChar:
                    tbl.Load(Properties.Resources.EBCDICNoSpecialChar);
                    break;
            }

            tbl.AllowEdit = true;
            return tbl;
        }

        #endregion

        #region Multi-Format Support

        /// <summary>
        /// Load TBL from file with auto-detected format (supports .tbl, .csv, .json)
        /// </summary>
        public TblImportResult LoadFromFile(string filePath)
        {
            var importService = new TblImportService();
            var result = importService.ImportFromFile(filePath);

            if (result.Success)
            {
                // Clear existing entries
                _dteList.Clear();

                // Add imported entries
                foreach (var entry in result.Entries)
                {
                    Add(entry);
                }

                _fileName = filePath;
                _isModified = false;
                _modificationCount = 0;
            }

            return result;
        }

        /// <summary>
        /// Load TBL from CSV file
        /// </summary>
        public TblImportResult LoadFromCsv(string filePath, CsvImportOptions options = null)
        {
            var importService = new TblImportService();
            var result = importService.ImportFromCsv(filePath, options);

            if (result.Success)
            {
                _dteList.Clear();
                foreach (var entry in result.Entries)
                    Add(entry);

                _fileName = filePath;
                _isModified = false;
                _modificationCount = 0;
            }

            return result;
        }

        /// <summary>
        /// Load TBL from JSON file
        /// </summary>
        public TblImportResult LoadFromJson(string filePath, JsonImportOptions options = null)
        {
            var importService = new TblImportService();
            var result = importService.ImportFromJson(filePath, options);

            if (result.Success)
            {
                _dteList.Clear();
                foreach (var entry in result.Entries)
                    Add(entry);

                _fileName = filePath;
                _isModified = false;
                _modificationCount = 0;
            }

            return result;
        }

        /// <summary>
        /// Save TBL to file with format based on extension (supports .tbl, .csv, .json)
        /// </summary>
        public void SaveToFile(string filePath, CsvExportOptions csvOptions = null, JsonExportOptions jsonOptions = null)
        {
            var exportService = new TblExportService();
            var entries = GetAllEntries();

            exportService.ExportToFile(entries, filePath, csvOptions, jsonOptions);

            _fileName = filePath;
            _isModified = false;
        }

        /// <summary>
        /// Save TBL to CSV file
        /// </summary>
        public void SaveToCsv(string filePath, CsvExportOptions options = null)
        {
            var exportService = new TblExportService();
            var entries = GetAllEntries();

            exportService.ExportToCsvFile(entries, filePath, options);

            _fileName = filePath;
            _isModified = false;
        }

        /// <summary>
        /// Save TBL to JSON file
        /// </summary>
        public void SaveToJson(string filePath, JsonExportOptions options = null)
        {
            var exportService = new TblExportService();
            var entries = GetAllEntries();

            exportService.ExportToJsonFile(entries, filePath, options);

            _fileName = filePath;
            _isModified = false;
        }

        /// <summary>
        /// Export TBL to CSV string
        /// </summary>
        public string ExportToCsvString(CsvExportOptions options = null)
        {
            var exportService = new TblExportService();
            var entries = GetAllEntries();
            return exportService.ExportToCsv(entries, options);
        }

        /// <summary>
        /// Export TBL to JSON string
        /// </summary>
        public string ExportToJsonString(JsonExportOptions options = null)
        {
            var exportService = new TblExportService();
            var entries = GetAllEntries();
            return exportService.ExportToJson(entries, options);
        }

        /// <summary>
        /// Detect file format from extension
        /// </summary>
        public static TblFileFormat DetectFileFormat(string filePath)
        {
            var extension = Path.GetExtension(filePath)?.ToLowerInvariant();

            return extension switch
            {
                ".tbl" => TblFileFormat.Tbl,
                ".tblx" => TblFileFormat.Tblx,
                ".csv" => TblFileFormat.Csv,
                ".json" => TblFileFormat.Json,
                _ => TblFileFormat.Tbl // Default to standard TBL
            };
        }

        #endregion

        #region IDisposable Support

        private bool _disposedValue; // To detect redundant calls

        void Dispose(bool disposing)
        {
            if (_disposedValue) return;

            if (disposing) _dteList = null;

            _disposedValue = true;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}