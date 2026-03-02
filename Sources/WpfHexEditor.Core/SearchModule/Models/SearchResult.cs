//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System;
using System.Collections.Generic;

namespace WpfHexEditor.Core.Search.Models
{
    /// <summary>
    /// Represents a single search result occurrence.
    /// </summary>
    public class SearchMatch
    {
        /// <summary>
        /// Gets or sets the position where the match was found.
        /// </summary>
        public long Position { get; set; }

        /// <summary>
        /// Gets or sets the length of the matched pattern.
        /// </summary>
        public int Length { get; set; }

        /// <summary>
        /// Gets or sets the matched bytes (useful for wildcard searches).
        /// </summary>
        public byte[] MatchedBytes { get; set; }

        /// <summary>
        /// Gets or sets the context bytes before the match (up to 8 bytes).
        /// Null if at the beginning of the file.
        /// </summary>
        public byte[] ContextBefore { get; set; }

        /// <summary>
        /// Gets or sets the context bytes after the match (up to 8 bytes).
        /// Null if at the end of the file.
        /// </summary>
        public byte[] ContextAfter { get; set; }

        public override string ToString()
        {
            return $"Match at position 0x{Position:X8} (length: {Length})";
        }
    }

    /// <summary>
    /// Represents the complete search operation result.
    /// </summary>
    public class SearchResult
    {
        /// <summary>
        /// Gets or sets whether the search was successful (at least one match found).
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Gets or sets the list of all matches found.
        /// </summary>
        public List<SearchMatch> Matches { get; set; } = new List<SearchMatch>();

        /// <summary>
        /// Gets the total number of matches found.
        /// </summary>
        public int Count => Matches?.Count ?? 0;

        /// <summary>
        /// Gets or sets the search duration in milliseconds.
        /// </summary>
        public long DurationMs { get; set; }

        /// <summary>
        /// Gets or sets the total bytes searched.
        /// </summary>
        public long BytesSearched { get; set; }

        /// <summary>
        /// Gets or sets any error message if the search failed.
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// Gets or sets whether the search was cancelled by the user.
        /// </summary>
        public bool WasCancelled { get; set; }

        /// <summary>
        /// Gets or sets the search options that were used.
        /// </summary>
        public SearchOptions Options { get; set; }

        /// <summary>
        /// Gets the search speed in MB/s.
        /// </summary>
        public double SpeedMBps
        {
            get
            {
                if (DurationMs == 0) return 0;
                return (BytesSearched / (1024.0 * 1024.0)) / (DurationMs / 1000.0);
            }
        }

        /// <summary>
        /// Creates a successful search result with matches.
        /// </summary>
        public static SearchResult CreateSuccess(List<SearchMatch> matches, long durationMs, long bytesSearched)
        {
            return new SearchResult
            {
                Success = matches.Count > 0,
                Matches = matches,
                DurationMs = durationMs,
                BytesSearched = bytesSearched
            };
        }

        /// <summary>
        /// Creates a failed search result with an error message.
        /// </summary>
        public static SearchResult CreateError(string errorMessage)
        {
            return new SearchResult
            {
                Success = false,
                ErrorMessage = errorMessage
            };
        }

        /// <summary>
        /// Creates a cancelled search result.
        /// </summary>
        public static SearchResult CreateCancelled()
        {
            return new SearchResult
            {
                Success = false,
                WasCancelled = true
            };
        }

        public override string ToString()
        {
            if (!Success)
            {
                if (WasCancelled)
                    return "Search cancelled";
                if (!string.IsNullOrEmpty(ErrorMessage))
                    return $"Search failed: {ErrorMessage}";
                return "No matches found";
            }

            return $"Found {Count} match(es) in {DurationMs}ms ({SpeedMBps:F2} MB/s)";
        }
    }
}
