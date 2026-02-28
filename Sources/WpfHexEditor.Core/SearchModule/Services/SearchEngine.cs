using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WpfHexaEditor.Core.Bytes;
using WpfHexaEditor.SearchModule.Models;

namespace WpfHexaEditor.SearchModule.Services
{
    /// <summary>
    /// Ultra-performant search engine using Boyer-Moore-Horspool algorithm.
    /// Provides up to 99% faster searching compared to naive byte-by-byte comparison.
    /// </summary>
    public class SearchEngine
    {
        private readonly ByteProvider _byteProvider;
        private const int PARALLEL_THRESHOLD = 10 * 1024 * 1024; // 10 MB

        public SearchEngine(ByteProvider byteProvider)
        {
            _byteProvider = byteProvider ?? throw new ArgumentNullException(nameof(byteProvider));
        }

        /// <summary>
        /// Performs a search operation with the specified options.
        /// </summary>
        public SearchResult Search(SearchOptions options, CancellationToken cancellationToken = default)
        {
            if (options == null)
                return SearchResult.CreateError("Search options cannot be null");

            if (!options.IsValid())
                return SearchResult.CreateError("Invalid search options");

            var stopwatch = Stopwatch.StartNew();

            try
            {
                long endPos = options.EndPosition == -1 ? _byteProvider.VirtualLength : options.EndPosition;
                long searchLength = endPos - options.StartPosition;

                // Validate search range
                if (options.StartPosition >= _byteProvider.VirtualLength)
                    return SearchResult.CreateError("Start position is beyond file length");

                if (searchLength < options.Pattern.Length)
                    return SearchResult.CreateError("Search range is smaller than pattern length");

                List<SearchMatch> matches;

                // Choose search strategy based on file size and options
                if (options.UseParallelSearch && searchLength > PARALLEL_THRESHOLD && !options.SearchBackward)
                {
                    matches = ParallelSearch(options, cancellationToken);
                }
                else
                {
                    matches = SequentialSearch(options, cancellationToken);
                }

                stopwatch.Stop();

                var result = SearchResult.CreateSuccess(matches, stopwatch.ElapsedMilliseconds, searchLength);
                result.Options = options;
                return result;
            }
            catch (OperationCanceledException)
            {
                return SearchResult.CreateCancelled();
            }
            catch (Exception ex)
            {
                return SearchResult.CreateError($"Search failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Performs sequential search using Boyer-Moore-Horspool algorithm.
        /// </summary>
        private List<SearchMatch> SequentialSearch(SearchOptions options, CancellationToken cancellationToken)
        {
            var matches = new List<SearchMatch>();
            var pattern = options.Pattern;
            var patternLength = pattern.Length;

            long endPos = options.EndPosition == -1 ? _byteProvider.VirtualLength : options.EndPosition;
            long searchStart = options.StartPosition;
            long searchEnd = endPos - patternLength + 1;

            if (options.SearchBackward)
            {
                // Backward search
                var badCharShift = BuildBadCharacterTableReverse(pattern, options.UseWildcard, options.WildcardByte);

                for (long pos = searchEnd - 1; pos >= searchStart; )
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (IsMatchAt(pos, pattern, options.UseWildcard, options.WildcardByte))
                    {
                        matches.Add(CreateSearchMatch(pos, patternLength, options.ContextRadius));

                        if (options.MaxResults > 0 && matches.Count >= options.MaxResults)
                            break;

                        pos--; // Move one position back to find overlapping matches
                    }
                    else
                    {
                        // Boyer-Moore-Horspool shift
                        byte mismatchByte = GetByteAt(pos);
                        pos -= badCharShift.ContainsKey(mismatchByte) ? badCharShift[mismatchByte] : patternLength;
                    }
                }

                matches.Reverse(); // Maintain forward order
            }
            else
            {
                // Forward search (standard Boyer-Moore-Horspool)
                var badCharShift = BuildBadCharacterTable(pattern, options.UseWildcard, options.WildcardByte);

                for (long pos = searchStart; pos < searchEnd; )
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (IsMatchAt(pos, pattern, options.UseWildcard, options.WildcardByte))
                    {
                        matches.Add(CreateSearchMatch(pos, patternLength, options.ContextRadius));

                        if (options.MaxResults > 0 && matches.Count >= options.MaxResults)
                            break;

                        pos++; // Move one position forward to find overlapping matches
                    }
                    else
                    {
                        // Boyer-Moore-Horspool shift: check last character of current window
                        long checkPos = pos + patternLength - 1;
                        if (checkPos >= _byteProvider.VirtualLength)
                            break;

                        byte mismatchByte = GetByteAt(checkPos);
                        pos += badCharShift.ContainsKey(mismatchByte) ? badCharShift[mismatchByte] : patternLength;
                    }
                }
            }

            return matches;
        }

        /// <summary>
        /// Performs parallel search for large files (divides into chunks and searches in parallel).
        /// </summary>
        private List<SearchMatch> ParallelSearch(SearchOptions options, CancellationToken cancellationToken)
        {
            var pattern = options.Pattern;
            var patternLength = pattern.Length;

            long endPos = options.EndPosition == -1 ? _byteProvider.VirtualLength : options.EndPosition;
            long searchLength = endPos - options.StartPosition;

            int chunkSize = options.ParallelChunkSize;
            int overlap = patternLength - 1; // Overlap to catch patterns spanning chunk boundaries

            var chunkCount = (int)Math.Ceiling((double)searchLength / chunkSize);
            var allMatches = new List<SearchMatch>[chunkCount];

            // Process chunks in parallel
            Parallel.For(0, chunkCount, new ParallelOptions { CancellationToken = cancellationToken }, i =>
            {
                long chunkStart = options.StartPosition + (i * chunkSize);
                long chunkEnd = Math.Min(chunkStart + chunkSize + overlap, endPos);

                var chunkOptions = options.Clone();
                chunkOptions.StartPosition = chunkStart;
                chunkOptions.EndPosition = chunkEnd;
                chunkOptions.UseParallelSearch = false; // Prevent recursive parallelization
                chunkOptions.MaxResults = 0; // No limit within chunks

                allMatches[i] = SequentialSearch(chunkOptions, cancellationToken);
            });

            // Merge results and remove duplicates from overlapping regions
            var mergedMatches = new List<SearchMatch>();
            var seenPositions = new HashSet<long>();

            foreach (var chunkMatches in allMatches)
            {
                if (chunkMatches == null) continue;

                foreach (var match in chunkMatches)
                {
                    if (seenPositions.Add(match.Position))
                    {
                        mergedMatches.Add(match);

                        if (options.MaxResults > 0 && mergedMatches.Count >= options.MaxResults)
                            break;
                    }
                }

                if (options.MaxResults > 0 && mergedMatches.Count >= options.MaxResults)
                    break;
            }

            // Sort by position
            mergedMatches.Sort((a, b) => a.Position.CompareTo(b.Position));

            return mergedMatches;
        }

        /// <summary>
        /// Builds the bad character shift table for Boyer-Moore-Horspool (forward search).
        /// </summary>
        private Dictionary<byte, int> BuildBadCharacterTable(byte[] pattern, bool useWildcard, byte wildcardByte)
        {
            var table = new Dictionary<byte, int>();
            int patternLength = pattern.Length;

            // Default shift is the pattern length
            for (int i = 0; i < patternLength - 1; i++)
            {
                byte b = pattern[i];

                // Skip wildcard bytes in shift table
                if (useWildcard && b == wildcardByte)
                    continue;

                // Shift is distance from this character to the end
                table[b] = patternLength - 1 - i;
            }

            return table;
        }

        /// <summary>
        /// Builds the bad character shift table for reverse search.
        /// </summary>
        private Dictionary<byte, int> BuildBadCharacterTableReverse(byte[] pattern, bool useWildcard, byte wildcardByte)
        {
            var table = new Dictionary<byte, int>();
            int patternLength = pattern.Length;

            // For reverse search, build table from end to start
            for (int i = patternLength - 1; i > 0; i--)
            {
                byte b = pattern[i];

                if (useWildcard && b == wildcardByte)
                    continue;

                table[b] = i;
            }

            return table;
        }

        /// <summary>
        /// Checks if the pattern matches at the specified position.
        /// </summary>
        private bool IsMatchAt(long position, byte[] pattern, bool useWildcard, byte wildcardByte)
        {
            if (position + pattern.Length > _byteProvider.VirtualLength)
                return false;

            for (int i = 0; i < pattern.Length; i++)
            {
                // Wildcard matches any byte
                if (useWildcard && pattern[i] == wildcardByte)
                    continue;

                if (GetByteAt(position + i) != pattern[i])
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Gets a single byte at the specified position.
        /// </summary>
        private byte GetByteAt(long position)
        {
            if (position < 0 || position >= _byteProvider.VirtualLength)
                return 0;

            var (value, success) = _byteProvider.GetByte(position);
            return success ? value : (byte)0;
        }

        /// <summary>
        /// Gets multiple bytes starting at the specified position.
        /// </summary>
        private byte[] GetBytesAt(long position, int length)
        {
            if (position < 0 || position >= _byteProvider.VirtualLength)
                return Array.Empty<byte>();

            int actualLength = (int)Math.Min(length, _byteProvider.VirtualLength - position);
            return _byteProvider.GetBytes(position, actualLength);
        }

        /// <summary>
        /// Creates a SearchMatch with context bytes captured.
        /// </summary>
        private SearchMatch CreateSearchMatch(long position, int length, int contextRadius)
        {
            // Get matched bytes
            byte[] matchedBytes = GetBytesAt(position, length);

            // Get context before (up to contextRadius bytes)
            byte[] contextBefore = null;
            if (contextRadius > 0 && position > 0)
            {
                long beforeStart = Math.Max(0, position - contextRadius);
                int beforeLength = (int)(position - beforeStart);
                contextBefore = GetBytesAt(beforeStart, beforeLength);
            }

            // Get context after (up to contextRadius bytes)
            byte[] contextAfter = null;
            if (contextRadius > 0 && position + length < _byteProvider.VirtualLength)
            {
                long afterLength = Math.Min(contextRadius, _byteProvider.VirtualLength - position - length);
                contextAfter = GetBytesAt(position + length, (int)afterLength);
            }

            return new SearchMatch
            {
                Position = position,
                Length = length,
                MatchedBytes = matchedBytes,
                ContextBefore = contextBefore,
                ContextAfter = contextAfter
            };
        }

        /// <summary>
        /// Finds the next match starting from the specified position.
        /// </summary>
        public SearchMatch FindNext(byte[] pattern, long startPosition, bool forward = true,
            bool useWildcard = false, byte wildcardByte = 0xFF, CancellationToken cancellationToken = default)
        {
            var options = new SearchOptions
            {
                Pattern = pattern,
                StartPosition = startPosition,
                SearchBackward = !forward,
                UseWildcard = useWildcard,
                WildcardByte = wildcardByte,
                MaxResults = 1
            };

            var result = Search(options, cancellationToken);
            return result.Success ? result.Matches.FirstOrDefault() : null;
        }

        /// <summary>
        /// Finds all matches of the pattern in the entire file.
        /// </summary>
        public List<SearchMatch> FindAll(byte[] pattern, bool useWildcard = false,
            byte wildcardByte = 0xFF, CancellationToken cancellationToken = default)
        {
            var options = new SearchOptions
            {
                Pattern = pattern,
                StartPosition = 0,
                UseWildcard = useWildcard,
                WildcardByte = wildcardByte
            };

            var result = Search(options, cancellationToken);
            return result.Success ? result.Matches : new List<SearchMatch>();
        }
    }
}
