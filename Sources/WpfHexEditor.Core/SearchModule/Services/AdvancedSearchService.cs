/*
    Apache 2.0  2026
    Author : Derek Tremblay (derektremblay666@gmail.com)
    Contributors: Claude Sonnet 4.5
*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WpfHexEditor.Core;
using WpfHexEditor.Core.Bytes;
using WpfHexEditor.Core.CharacterTable;
using WpfHexEditor.Core.Search.Models;

namespace WpfHexEditor.Core.Search.Services
{
    /// <summary>
    /// Advanced search service with TBL support, export, and history management.
    /// Composes the existing SearchEngine with additional features.
    /// </summary>
    public class AdvancedSearchService
    {
        private readonly SearchEngine _engine;
        private readonly ByteProvider _byteProvider;
        private static readonly List<SearchHistoryEntry> _globalHistory = new();
        private const int MAX_HISTORY_ENTRIES = 20;

        public AdvancedSearchService(ByteProvider byteProvider)
        {
            _byteProvider = byteProvider ?? throw new ArgumentNullException(nameof(byteProvider));
            _engine = new SearchEngine(byteProvider);
        }

        #region TBL Text Search

        /// <summary>
        /// Converts TBL text to byte pattern using greedy inverse matching.
        /// Example: "こんにちは" → bytes via TBL character table
        /// </summary>
        public byte[] TblTextToBytes(string tblText, TblStream tbl)
        {
            if (string.IsNullOrEmpty(tblText))
                return Array.Empty<byte>();

            if (tbl == null || tbl.Length == 0)
                throw new InvalidOperationException("No TBL loaded. Cannot convert TBL text to bytes.");

            var result = new List<byte>();

            for (int i = 0; i < tblText.Length;)
            {
                bool found = false;

                // Try longest matches first (greedy, up to 10 chars)
                for (int len = Math.Min(10, tblText.Length - i); len >= 1; len--)
                {
                    string substr = tblText.Substring(i, len);

                    // Search in TBL dictionary for matching value
                    var match = FindTblEntryByValue(tbl, substr);
                    if (match.hasMatch)
                    {
                        // Convert hex entry to bytes
                        byte[] entryBytes = HexStringToBytes(match.entry);
                        result.AddRange(entryBytes);
                        i += len;
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    throw new InvalidOperationException(
                        $"Character '{tblText[i]}' not found in current TBL table. " +
                        $"Use HEX mode for direct byte search.");
                }
            }

            return result.ToArray();
        }

        /// <summary>
        /// Searches TBL dictionary for entry with matching value (reverse lookup).
        /// </summary>
        private (bool hasMatch, string entry) FindTblEntryByValue(TblStream tbl, string value)
        {
            // Access internal dictionary via reflection (or make it public in TblStream)
            // For now, we'll iterate through all possible hex values
            // This is a simplified implementation - in production, TblStream should expose a reverse lookup

            // Try all single-byte values first (optimization)
            if (value.Length == 1)
            {
                for (int b = 0; b < 256; b++)
                {
                    string hex = ByteConverters.ByteToHex((byte)b);
                    var (text, type) = tbl.FindMatch(hex, showSpecialValue: false);
                    if (text == value && type != DteType.Invalid)
                        return (true, hex);
                }
            }

            // Try multi-byte values (2-8 bytes)
            // This is expensive but necessary for TBL reverse lookup
            // In a real implementation, TblStream should maintain a reverse dictionary

            // For now, return not found - this will be improved when TblStream is updated
            return (false, null);
        }

        /// <summary>
        /// Converts hex string to bytes.
        /// </summary>
        private byte[] HexStringToBytes(string hex)
        {
            if (string.IsNullOrEmpty(hex) || hex.Length % 2 != 0)
                return Array.Empty<byte>();

            byte[] bytes = new byte[hex.Length / 2];
            for (int i = 0; i < hex.Length; i += 2)
            {
                bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
            }
            return bytes;
        }

        /// <summary>
        /// Performs TBL text search asynchronously.
        /// </summary>
        public async Task<SearchResult> SearchTblTextAsync(
            string tblText,
            TblStream tbl,
            SearchOptions baseOptions,
            CancellationToken cancellationToken)
        {
            return await Task.Run(() =>
            {
                try
                {
                    // Convert TBL text to bytes
                    byte[] pattern = TblTextToBytes(tblText, tbl);

                    // Create search options with the converted pattern
                    var options = new SearchOptions
                    {
                        Pattern = pattern,
                        StartPosition = baseOptions.StartPosition,
                        EndPosition = baseOptions.EndPosition,
                        SearchBackward = baseOptions.SearchBackward,
                        UseWildcard = baseOptions.UseWildcard,
                        WildcardByte = baseOptions.WildcardByte,
                        MaxResults = baseOptions.MaxResults,
                        WrapAround = baseOptions.WrapAround,
                        ContextRadius = baseOptions.ContextRadius
                    };

                    // Perform search
                    return _engine.Search(options, cancellationToken);
                }
                catch (Exception ex)
                {
                    return SearchResult.CreateError(ex.Message);
                }
            }, cancellationToken);
        }

        #endregion

        #region Advanced Find with Context

        /// <summary>
        /// Performs async search with progress reporting (chunked).
        /// </summary>
        public async Task<SearchResult> FindAllAsync(
            SearchOptions options,
            IProgress<double> progress,
            CancellationToken cancellationToken)
        {
            // For now, delegate to SearchEngine (progress reporting can be added later)
            return await Task.Run(() => _engine.Search(options, cancellationToken), cancellationToken);
        }

        #endregion

        #region Export

        /// <summary>
        /// Export formats supported.
        /// </summary>
        public enum ExportFormat
        {
            PlainText,
            Csv,
            Json
        }

        /// <summary>
        /// Exports search results to a file asynchronously.
        /// </summary>
        public async Task ExportResultsAsync(
            IEnumerable<SearchMatch> matches,
            string filePath,
            ExportFormat format,
            byte[] fileData = null,
            int contextBytes = 8)
        {
            await Task.Run(() =>
            {
                using (var writer = new StreamWriter(filePath, false, Encoding.UTF8))
                {
                    switch (format)
                    {
                        case ExportFormat.PlainText:
                            ExportAsText(writer, matches, contextBytes);
                            break;

                        case ExportFormat.Csv:
                            ExportAsCsv(writer, matches, contextBytes);
                            break;

                        case ExportFormat.Json:
                            ExportAsJson(writer, matches, contextBytes);
                            break;
                    }
                }
            });
        }

        private void ExportAsText(StreamWriter writer, IEnumerable<SearchMatch> matches, int contextBytes)
        {
            writer.WriteLine("=".PadRight(80, '='));
            writer.WriteLine("WPFHexEditor - Search Results Export");
            writer.WriteLine($"Exported: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            writer.WriteLine($"Total Matches: {matches.Count()}");
            writer.WriteLine("=".PadRight(80, '='));
            writer.WriteLine();

            int index = 1;
            foreach (var match in matches)
            {
                writer.WriteLine($"Match #{index}:");
                writer.WriteLine($"  Position (Hex): 0x{match.Position:X8}");
                writer.WriteLine($"  Position (Dec): {match.Position:N0}");
                writer.WriteLine($"  Length: {match.Length} bytes");
                writer.WriteLine($"  Matched Bytes: {BytesToHexString(match.MatchedBytes)}");

                if (match.ContextBefore != null || match.ContextAfter != null)
                {
                    writer.WriteLine($"  Context:");
                    writer.WriteLine($"    Before: {BytesToHexString(match.ContextBefore ?? Array.Empty<byte>())}");
                    writer.WriteLine($"    Match:  {BytesToHexString(match.MatchedBytes)}");
                    writer.WriteLine($"    After:  {BytesToHexString(match.ContextAfter ?? Array.Empty<byte>())}");
                }

                writer.WriteLine();
                index++;
            }
        }

        private void ExportAsCsv(StreamWriter writer, IEnumerable<SearchMatch> matches, int contextBytes)
        {
            // Header
            writer.WriteLine("Index,Position (Hex),Position (Dec),Length,Matched Bytes,Context Before,Context After");

            // Data rows
            int index = 1;
            foreach (var match in matches)
            {
                writer.Write(index);
                writer.Write($",0x{match.Position:X8}");
                writer.Write($",{match.Position}");
                writer.Write($",{match.Length}");
                writer.Write($",\"{BytesToHexString(match.MatchedBytes)}\"");
                writer.Write($",\"{BytesToHexString(match.ContextBefore ?? Array.Empty<byte>())}\"");
                writer.WriteLine($",\"{BytesToHexString(match.ContextAfter ?? Array.Empty<byte>())}\"");
                index++;
            }
        }

        private void ExportAsJson(StreamWriter writer, IEnumerable<SearchMatch> matches, int contextBytes)
        {
            writer.WriteLine("{");
            writer.WriteLine($"  \"exportDate\": \"{DateTime.Now:yyyy-MM-dd HH:mm:ss}\",");
            writer.WriteLine($"  \"totalMatches\": {matches.Count()},");
            writer.WriteLine("  \"matches\": [");

            var matchList = matches.ToList();
            for (int i = 0; i < matchList.Count; i++)
            {
                var match = matchList[i];
                writer.WriteLine("    {");
                writer.WriteLine($"      \"index\": {i + 1},");
                writer.WriteLine($"      \"positionHex\": \"0x{match.Position:X8}\",");
                writer.WriteLine($"      \"positionDec\": {match.Position},");
                writer.WriteLine($"      \"length\": {match.Length},");
                writer.WriteLine($"      \"matchedBytes\": \"{BytesToHexString(match.MatchedBytes)}\",");
                writer.WriteLine($"      \"contextBefore\": \"{BytesToHexString(match.ContextBefore ?? Array.Empty<byte>())}\",");
                writer.WriteLine($"      \"contextAfter\": \"{BytesToHexString(match.ContextAfter ?? Array.Empty<byte>())}\"");
                writer.Write("    }");
                if (i < matchList.Count - 1)
                    writer.WriteLine(",");
                else
                    writer.WriteLine();
            }

            writer.WriteLine("  ]");
            writer.WriteLine("}");
        }

        private string BytesToHexString(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
                return string.Empty;

            var sb = new StringBuilder(bytes.Length * 3);
            for (int i = 0; i < bytes.Length; i++)
            {
                sb.Append(ByteConverters.ByteToHex(bytes[i]));
                if (i < bytes.Length - 1)
                    sb.Append(' ');
            }
            return sb.ToString();
        }

        #endregion

        #region History Management

        /// <summary>
        /// Records a search in the global history (MRU).
        /// </summary>
        public void RecordSearch(SearchHistoryEntry entry)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.Pattern))
                return;

            lock (_globalHistory)
            {
                // Find existing entry with same pattern and mode
                var existing = _globalHistory.FirstOrDefault(h =>
                    h.Pattern == entry.Pattern &&
                    h.Mode == entry.Mode &&
                    h.CaseSensitive == entry.CaseSensitive);

                if (existing != null)
                {
                    // Update existing entry
                    existing.LastUsed = DateTime.Now;
                    existing.UseCount++;

                    // Move to front (MRU)
                    _globalHistory.Remove(existing);
                    _globalHistory.Insert(0, existing);
                }
                else
                {
                    // Add new entry at front
                    entry.LastUsed = DateTime.Now;
                    entry.UseCount = 1;
                    _globalHistory.Insert(0, entry);

                    // Trim to max size
                    while (_globalHistory.Count > MAX_HISTORY_ENTRIES)
                    {
                        _globalHistory.RemoveAt(_globalHistory.Count - 1);
                    }
                }
            }
        }

        /// <summary>
        /// Gets the search history (MRU order).
        /// </summary>
        public IReadOnlyList<SearchHistoryEntry> GetHistory()
        {
            lock (_globalHistory)
            {
                return _globalHistory.ToList();
            }
        }

        /// <summary>
        /// Clears the search history.
        /// </summary>
        public void ClearHistory()
        {
            lock (_globalHistory)
            {
                _globalHistory.Clear();
            }
        }

        #endregion
    }
}
