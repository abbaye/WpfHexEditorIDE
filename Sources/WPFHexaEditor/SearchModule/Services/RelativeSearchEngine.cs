/*
    Apache 2.0  2026
    Author : Derek Tremblay (derektremblay666@gmail.com)
    Contributors: Claude Sonnet 4.5
*/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WpfHexaEditor.Core;
using WpfHexaEditor.Core.Bytes;
using WpfHexaEditor.Core.CharacterTable;
using WpfHexaEditor.SearchModule.Models;

namespace WpfHexaEditor.SearchModule.Services
{
    /// <summary>
    /// Search engine for discovering character encodings using relative distance patterns.
    /// Uses Boyer-Moore-Horspool algorithm via SearchEngine for fast pattern matching.
    /// </summary>
    public class RelativeSearchEngine
    {
        private readonly ByteProvider _byteProvider;
        private readonly SearchEngine _searchEngine;
        private readonly TblStream _currentTbl;  // Optional: for validation scoring

        /// <summary>
        /// Initializes a new instance of the RelativeSearchEngine.
        /// </summary>
        /// <param name="byteProvider">The byte provider to search in.</param>
        /// <param name="currentTbl">Optional: Currently loaded TBL for validation scoring.</param>
        public RelativeSearchEngine(ByteProvider byteProvider, TblStream currentTbl = null)
        {
            _byteProvider = byteProvider ?? throw new ArgumentNullException(nameof(byteProvider));
            _searchEngine = new SearchEngine(byteProvider);
            _currentTbl = currentTbl;  // Can be null if no TBL loaded
        }

        /// <summary>
        /// Performs relative search to discover character encoding.
        /// </summary>
        /// <param name="options">Search options.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Search result with encoding proposals sorted by score.</returns>
        public RelativeSearchResult Search(RelativeSearchOptions options, CancellationToken cancellationToken)
        {
            if (options == null)
                throw new ArgumentNullException(nameof(options));

            if (!options.IsValid())
                return RelativeSearchResult.CreateError("Invalid search options", options);

            if (!_byteProvider.IsOpen || _byteProvider.Length == 0)
                return RelativeSearchResult.CreateError("Byte provider is not open or empty", options);

            var stopwatch = Stopwatch.StartNew();

            try
            {
                // Step 1: Convert search text to relative pattern
                var relativePattern = ConvertToRelativePattern(options.SearchText, options.CaseSensitive);
                if (relativePattern == null || relativePattern.Length == 0)
                    return RelativeSearchResult.CreateError("Failed to convert search text to relative pattern", options);

                // Step 2: Determine search range
                long startPos = options.StartPosition;
                long endPos = options.EndPosition == -1 ? _byteProvider.Length : Math.Min(options.EndPosition, _byteProvider.Length);
                long bytesSearched = endPos - startPos;

                // Step 3: Test all 256 offsets (parallel or sequential)
                List<EncodingProposal> proposals;
                if (options.UseParallelSearch)
                {
                    proposals = TestOffsetsParallel(options, relativePattern, cancellationToken);
                }
                else
                {
                    proposals = TestOffsetsSequential(options, relativePattern, cancellationToken);
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    stopwatch.Stop();
                    return RelativeSearchResult.CreateCancelled(stopwatch.ElapsedMilliseconds, options);
                }

                // Step 4: Filter proposals by minimum matches
                var validProposals = proposals
                    .Where(p => p.MatchCount >= options.MinMatchesRequired)
                    .OrderByDescending(p => p.Score)
                    .Take(options.MaxProposals)
                    .ToList();

                // Step 5: Calculate statistics
                int totalMatches = proposals.Sum(p => p.MatchCount);

                stopwatch.Stop();

                return RelativeSearchResult.CreateSuccess(
                    validProposals,
                    stopwatch.ElapsedMilliseconds,
                    bytesSearched,
                    options.SearchText,
                    relativePattern,
                    options,
                    offsetsTested: 256,
                    totalMatchesFound: totalMatches);
            }
            catch (OperationCanceledException)
            {
                stopwatch.Stop();
                return RelativeSearchResult.CreateCancelled(stopwatch.ElapsedMilliseconds, options);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                return RelativeSearchResult.CreateError($"Search failed: {ex.Message}", options);
            }
        }

        /// <summary>
        /// Converts search text to relative distance pattern.
        /// Example: "ABC" → [1, 1] (A→B distance=1, B→C distance=1)
        /// </summary>
        private int[] ConvertToRelativePattern(string text, bool caseSensitive)
        {
            if (string.IsNullOrEmpty(text) || text.Length < 2)
                return null;

            // Normalize case if needed
            string normalizedText = caseSensitive ? text : text.ToUpperInvariant();

            // Calculate distances between consecutive characters
            var pattern = new int[normalizedText.Length - 1];
            for (int i = 0; i < normalizedText.Length - 1; i++)
            {
                int distance = (normalizedText[i + 1] - normalizedText[i] + 256) % 256;
                pattern[i] = distance;
            }

            return pattern;
        }

        /// <summary>
        /// Converts relative pattern to byte pattern for a specific offset.
        /// </summary>
        private byte[] ConvertPatternToBytes(int[] relativePattern, byte offset, char firstChar)
        {
            if (relativePattern == null || relativePattern.Length == 0)
                return null;

            var bytes = new byte[relativePattern.Length + 1];
            bytes[0] = (byte)((firstChar + offset) % 256);

            for (int i = 0; i < relativePattern.Length; i++)
            {
                bytes[i + 1] = (byte)((bytes[i] + relativePattern[i]) % 256);
            }

            return bytes;
        }

        /// <summary>
        /// Tests all 256 offsets in parallel.
        /// </summary>
        private List<EncodingProposal> TestOffsetsParallel(
            RelativeSearchOptions options,
            int[] relativePattern,
            CancellationToken cancellationToken)
        {
            var proposals = new List<EncodingProposal>(256);
            var lockObject = new object();

            Parallel.For(0, 256, new ParallelOptions
            {
                CancellationToken = cancellationToken,
                MaxDegreeOfParallelism = Environment.ProcessorCount
            },
            offset =>
            {
                var proposal = TestSingleOffset((byte)offset, options, relativePattern, cancellationToken);
                if (proposal != null)
                {
                    lock (lockObject)
                    {
                        proposals.Add(proposal);
                    }
                }
            });

            return proposals;
        }

        /// <summary>
        /// Tests all 256 offsets sequentially (for debugging or comparison).
        /// </summary>
        private List<EncodingProposal> TestOffsetsSequential(
            RelativeSearchOptions options,
            int[] relativePattern,
            CancellationToken cancellationToken)
        {
            var proposals = new List<EncodingProposal>(256);

            for (int offset = 0; offset < 256; offset++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var proposal = TestSingleOffset((byte)offset, options, relativePattern, cancellationToken);
                if (proposal != null)
                {
                    proposals.Add(proposal);
                }
            }

            return proposals;
        }

        /// <summary>
        /// Tests a single offset by converting pattern to bytes and searching.
        /// </summary>
        private EncodingProposal TestSingleOffset(
            byte offset,
            RelativeSearchOptions options,
            int[] relativePattern,
            CancellationToken cancellationToken)
        {
            // Get first character (normalized if case-insensitive)
            char firstChar = options.CaseSensitive ? options.SearchText[0] : char.ToUpperInvariant(options.SearchText[0]);

            // Convert relative pattern to byte pattern
            var bytePattern = ConvertPatternToBytes(relativePattern, offset, firstChar);
            if (bytePattern == null)
                return null;

            // Search for this byte pattern using existing SearchEngine
            var searchOptions = new SearchOptions
            {
                Pattern = bytePattern,
                StartPosition = options.StartPosition,
                EndPosition = options.EndPosition,
                SearchBackward = false,
                MaxResults = options.MaxMatchesPerOffset
            };

            var searchResult = _searchEngine.Search(searchOptions, cancellationToken);

            // If no matches, return null
            if (!searchResult.Success || searchResult.Matches.Count == 0)
                return null;

            // Create encoding proposal
            var proposal = new EncodingProposal
            {
                Offset = offset,
                MatchCount = searchResult.Matches.Count,
                MatchPositions = searchResult.Matches.Select(m => m.Position).ToList(),
                CharacterMapping = BuildCharacterMapping(options.SearchText, offset, options.CaseSensitive)
            };

            // Generate sample and preview text
            if (proposal.MatchPositions.Count > 0)
            {
                long firstMatchPos = proposal.MatchPositions[0];
                proposal.SampleText = GenerateSampleText(firstMatchPos, proposal.CharacterMapping, options.SampleLength);
                proposal.PreviewText = GenerateSampleText(firstMatchPos, proposal.CharacterMapping, options.PreviewLength);
            }

            // Calculate clustering metric
            if (proposal.MatchPositions.Count > 1)
            {
                var sortedPositions = proposal.MatchPositions.OrderBy(p => p).ToList();
                var distances = new List<long>();
                for (int i = 1; i < sortedPositions.Count; i++)
                {
                    distances.Add(sortedPositions[i] - sortedPositions[i - 1]);
                }
                proposal.AverageMatchDistance = distances.Average();
            }

            // Score the proposal
            ScoreProposal(proposal, options);

            return proposal;
        }

        /// <summary>
        /// Builds character mapping for an encoding proposal.
        /// </summary>
        private Dictionary<int, (byte actualByte, char character)> BuildCharacterMapping(
            string searchText,
            byte offset,
            bool caseSensitive)
        {
            var mapping = new Dictionary<int, (byte, char)>();

            // Normalize text if case-insensitive
            string normalizedText = caseSensitive ? searchText : searchText.ToUpperInvariant();

            // Build mapping for search text characters
            for (int i = 0; i < normalizedText.Length; i++)
            {
                char character = normalizedText[i];
                byte actualByte = (byte)((character + offset) % 256);

                // Store mapping (key = character offset from 'A')
                int charOffset = character - 'A';
                if (!mapping.ContainsKey(charOffset))
                {
                    mapping[charOffset] = (actualByte, character);
                }
            }

            // Extend mapping to full ASCII printable range (A-Z, a-z, 0-9)
            for (char c = 'A'; c <= 'Z'; c++)
            {
                int charOffset = c - 'A';
                if (!mapping.ContainsKey(charOffset))
                {
                    byte actualByte = (byte)((c + offset) % 256);
                    mapping[charOffset] = (actualByte, c);
                }
            }

            return mapping;
        }

        /// <summary>
        /// Generates sample decoded text from a position using character mapping.
        /// </summary>
        private string GenerateSampleText(
            long position,
            Dictionary<int, (byte actualByte, char character)> mapping,
            int maxLength)
        {
            if (position < 0 || position >= _byteProvider.Length)
                return string.Empty;

            var sb = new StringBuilder();
            int length = (int)Math.Min(maxLength, _byteProvider.Length - position);

            // Read bytes in bulk for better performance
            byte[] bytes = _byteProvider.GetBytes(position, length);
            if (bytes == null || bytes.Length == 0)
                return string.Empty;

            for (int i = 0; i < bytes.Length; i++)
            {
                byte b = bytes[i];

                // Try to find character for this byte
                var matchingEntry = mapping.Values.FirstOrDefault(v => v.actualByte == b);
                if (matchingEntry.character != '\0')
                {
                    sb.Append(matchingEntry.character);
                }
                else
                {
                    // Not in mapping, try to decode as printable ASCII
                    char c = (char)b;
                    if (c >= 32 && c <= 126)
                        sb.Append(c);
                    else
                        sb.Append('▒');  // Non-printable placeholder
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Scores an encoding proposal using multiple factors.
        /// </summary>
        private void ScoreProposal(EncodingProposal proposal, RelativeSearchOptions options)
        {
            double score = 0;

            // Factor 1: Match count (0-30 points)
            // More matches = higher confidence
            score += Math.Min(30, proposal.MatchCount * 1.5);

            // Factor 2: Printable character percentage (0-25 points)
            // Decoded text should be 80-95% printable
            if (!string.IsNullOrEmpty(proposal.PreviewText))
            {
                int printableCount = proposal.PreviewText.Count(c => (c >= 32 && c <= 126) || c == '\n' || c == '\r' || c == '\t');
                proposal.PrintableCharPercentage = (printableCount * 100.0) / proposal.PreviewText.Length;
                score += (proposal.PrintableCharPercentage / 100.0) * 25;
            }

            // Factor 3: Match clustering (0-15 points)
            // Matches close together = text sections
            if (proposal.MatchPositions.Count > 1)
            {
                var sortedPositions = proposal.MatchPositions.OrderBy(p => p).ToList();
                var distances = new List<long>();
                for (int i = 1; i < sortedPositions.Count; i++)
                {
                    distances.Add(sortedPositions[i] - sortedPositions[i - 1]);
                }

                double avgDistance = distances.Average();
                double variance = distances.Average(d => Math.Pow(d - avgDistance, 2));
                double stdDev = Math.Sqrt(variance);

                // Lower standard deviation = better clustering
                double clusteringScore = Math.Max(0, 15 - (stdDev / 1000));
                score += clusteringScore;
            }

            // Factor 4: Search text validation (0-10 points)
            // Preview should contain original search text
            if (!string.IsNullOrEmpty(proposal.SampleText) && !string.IsNullOrEmpty(options.SearchText))
            {
                StringComparison comparison = options.CaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
                if (proposal.SampleText.Contains(options.SearchText, comparison))
                {
                    score += 10;
                }
            }

            // Factor 5: TBL VALIDATION (0-20 points)
            // If a TBL is already loaded, check if proposal matches existing entries
            if (_currentTbl != null && _currentTbl.Length > 0)
            {
                int matchingEntries = 0;
                int checkedEntries = 0;

                foreach (var mapping in proposal.CharacterMapping)
                {
                    checkedEntries++;
                    var hexKey = ByteConverters.ByteToHex(mapping.Value.actualByte).ToUpperInvariant();
                    var (text, type) = _currentTbl.FindMatch(hexKey, showSpecialValue: false);

                    // If TBL has an entry for this byte and it matches our proposal
                    if (text != "#" && text == mapping.Value.character.ToString())
                    {
                        matchingEntries++;
                    }
                }

                if (checkedEntries > 0)
                {
                    double matchPercentage = (matchingEntries * 100.0) / checkedEntries;
                    score += (matchPercentage / 100.0) * 20; // Up to 20 bonus points
                }
            }

            proposal.Score = Math.Min(100, score);
        }

        /// <summary>
        /// Exports an encoding proposal to a TBL file.
        /// </summary>
        /// <param name="proposal">The encoding proposal to export.</param>
        /// <returns>A new TBLStream with the character mappings.</returns>
        public TblStream ExportToTbl(EncodingProposal proposal)
        {
            if (proposal == null)
                throw new ArgumentNullException(nameof(proposal));

            var tbl = new TblStream(); // Create NEW TBL, doesn't modify existing

            // Add discovered mappings
            foreach (var kvp in proposal.CharacterMapping.OrderBy(k => k.Key))
            {
                byte actualByte = kvp.Value.actualByte;
                char character = kvp.Value.character;

                string entry = ByteConverters.ByteToHex(actualByte).ToUpperInvariant();
                string value = character.ToString();

                // Use existing DTE class (no new types needed)
                var dteType = entry.Length == 2 ? DteType.Ascii : DteType.DualTitleEncoding;
                var dte = new Dte(entry, value, dteType);
                tbl.Add(dte);
            }

            return tbl; // Returns NEW TBL, existing TBL unchanged
        }
    }
}
