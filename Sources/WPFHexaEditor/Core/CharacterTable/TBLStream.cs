//////////////////////////////////////////////
// Apache 2.0  - 2003-2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5 (Performance optimizations)
//////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using WpfHexaEditor.Core.Bytes;

namespace WpfHexaEditor.Core.CharacterTable
{
    /// <summary>
    /// Used to manage Thingy TBL file (entry=value)
    /// </summary>
    public sealed class TblStream : IDisposable
    {
        #region Global class variables
        /// <summary>
        /// TBL file path
        /// </summary>
        private string _fileName = string.Empty;

        /// <summary>
        /// Represente the whole TBL file
        /// </summary>
        private Dictionary<string, Dte> _dteList = new();

        /// <summary>
        /// Cached EndBlock and EndLine values for performance
        /// </summary>
        private string _endBlock = string.Empty;
        private string _endLine = string.Empty;
        #endregion

        #region Constructors
        /// <summary>
        /// Constructeur perm�tant de charg?le fichier DTE
        /// </summary>
        public TblStream(string fileName) => FileName = fileName;

        /// <summary>
        /// Constructeur perm�tant de charg�le fichier DTE
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
        /// <param name="showSpecialValue">Fin the Endblock and EndLine</param>
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

            return _dteList.TryGetValue(hex, out var dte)
                ? (dte.Value, dte.Type)
                : ("#", DteType.Invalid);
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

            // OPTIMIZED: Pre-allocate StringBuilder capacity and reuse hex conversion results
            var sb = new StringBuilder(data.Length * 2);

            for (var i = 0; i < data.Length; i++)
            {
                // Try multi-byte (MTE/DTE) match first
                if (i < data.Length - 1)
                {
                    // Build 4-char hex string (e.g., "8899")
                    var hex1 = ByteConverters.ByteToHex(data[i]);
                    var hex2 = ByteConverters.ByteToHex(data[i + 1]);
                    var mteKey = hex1 + hex2;

                    if (_dteList.TryGetValue(mteKey, out var mte))
                    {
                        sb.Append(mte.Value); // FIX: Append .Value not tuple
                        i++; // Skip next byte since we consumed it
                        continue;
                    }

                    // If no MTE match, check single byte with hex1 we already computed
                    if (_dteList.TryGetValue(hex1, out var dte1))
                        sb.Append(dte1.Value);
                    else
                        sb.Append('#'); // No match found
                }
                else
                {
                    // Last byte - single byte match only
                    var key = ByteConverters.ByteToHex(data[i]);

                    if (_dteList.TryGetValue(key, out var dte))
                        sb.Append(dte.Value);
                    else
                        sb.Append('#'); // No match found
                }
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
        /// Load the TBL file
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
                var line = rawLine.TrimEnd('\r', '\n');
                if (string.IsNullOrWhiteSpace(line))
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
        /// Try to parse a DTE entry from a line
        /// </summary>
        private void TryParseDteEntry(string line, int equalIndex)
        {
            try
            {
                var entry = line.Substring(0, equalIndex);
                var valueStart = equalIndex + 1;
                var value = valueStart < line.Length ? line.Substring(valueStart) : string.Empty;

                // Remove trailing carriage return if present
                if (value.EndsWith("\r"))
                    value = value.Substring(0, value.Length - 1);

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
                    // Determine type based on entry length
                    if (entry.Length == 2)
                        type = value.Length == 1 ? DteType.Ascii : DteType.DualTitleEncoding;
                    else if (entry.Length == 4)
                        type = DteType.MultipleTitleEncoding;
                    else
                        type = DteType.Invalid;
                }

                if (type != DteType.Invalid)
                {
                    var dte = new Dte(entry, value, type);
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
        /// Load TBL file
        /// </summary>
        public void Load()
        {
            //ouverture du fichier
            if (!File.Exists(_fileName))
            {
                var fs = File.Create(_fileName);
                fs.Close();
            }

            StreamReader tblFile;
            try
            {
                tblFile = new StreamReader(_fileName, Encoding.ASCII);
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
        /// Save tbl file
        /// </summary>
        public void Save()
        {
            var myFile = new FileStream(_fileName, FileMode.Create, FileAccess.Write);
            var tblFile = new StreamWriter(myFile, Encoding.Unicode); //ASCII

            if (tblFile.BaseStream.CanWrite)
            {
                //Save tbl set
                foreach (var dte in _dteList)
                    if (dte.Value.Type != DteType.EndBlock &&
                        dte.Value.Type != DteType.EndLine)
                        tblFile.WriteLine(dte.Value.Entry + "=" + dte.Value);
                    else
                        tblFile.WriteLine(dte.Value.Entry);

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
        }

        /// <summary>
        /// Add a DTE/MTE in TBL
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
        }

        /// <summary>
        /// Remove TBL entry
        /// </summary>
        public void Remove(Dte dte)
        {
            if (dte is null) return;

            _dteList.Remove(dte.Entry);
        }

        /// <summary>
        /// Get the string representation of the TBL
        /// </summary>
        public override string ToString()
        {
            var sb = new StringBuilder();

            //Save tbl set
            foreach (var dte in _dteList)
                if (dte.Value.Type != DteType.EndBlock &&
                    dte.Value.Type != DteType.EndLine)
                    sb.AppendLine(dte.Value.Entry + "=" + dte.Value);
                else
                    sb.AppendLine(dte.Value.Entry);

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
        /// Get of set bookmarks
        /// </summary>
        public List<BookMark> BookMarks { get; set; } = new();

        public int TotalDte => _dteList.Count(l => l.Value.Type == DteType.DualTitleEncoding);
        public int TotalMte => _dteList.Count(l => l.Value.Type == DteType.MultipleTitleEncoding);
        public int TotalAscii => _dteList.Count(l => l.Value.Type == DteType.Ascii);
        public int TotalInvalid => _dteList.Count(l => l.Value.Type == DteType.Invalid);
        public int TotalJaponais => _dteList.Count(l => l.Value.Type == DteType.Japonais);
        public int TotalEndLine => _dteList.Count(l => l.Value.Type == DteType.EndLine);
        public int TotalEndBlock => _dteList.Count(l => l.Value.Type == DteType.EndBlock);

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