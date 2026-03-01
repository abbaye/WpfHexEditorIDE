//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System;

namespace WpfHexEditor.Core.Search.Models
{
    /// <summary>
    /// Configuration options for relative search (encoding discovery).
    /// </summary>
    public class RelativeSearchOptions
    {
        /// <summary>
        /// Gets or sets the known text to search for (e.g., "World", "Start", "Hero").
        /// This text will be converted to a relative distance pattern.
        /// </summary>
        public string SearchText { get; set; }

        /// <summary>
        /// Gets or sets the starting position for the search (default: 0).
        /// </summary>
        public long StartPosition { get; set; } = 0;

        /// <summary>
        /// Gets or sets the ending position for the search (default: -1 = end of file).
        /// </summary>
        public long EndPosition { get; set; } = -1;

        /// <summary>
        /// Gets or sets the minimum number of matches required for a proposal to be included (default: 1).
        /// Lower values may produce more false positives but catch rare encodings.
        /// </summary>
        public int MinMatchesRequired { get; set; } = 1;

        /// <summary>
        /// Gets or sets the maximum number of proposals to return (default: 20).
        /// Only the top-scoring proposals will be returned.
        /// </summary>
        public int MaxProposals { get; set; } = 20;

        /// <summary>
        /// Gets or sets whether the search is case-sensitive (default: false).
        /// When false, both 'A' and 'a' will be considered equivalent.
        /// </summary>
        public bool CaseSensitive { get; set; } = false;

        /// <summary>
        /// Gets or sets the length of sample text to generate for preview (default: 100 chars).
        /// </summary>
        public int SampleLength { get; set; } = 100;

        /// <summary>
        /// Gets or sets the length of preview text to generate for validation (default: 500 chars).
        /// </summary>
        public int PreviewLength { get; set; } = 500;

        /// <summary>
        /// Gets or sets whether to use parallel search (default: true).
        /// Parallel search tests all 256 offsets simultaneously (8-16x faster).
        /// </summary>
        public bool UseParallelSearch { get; set; } = true;

        /// <summary>
        /// Gets or sets the maximum number of matches to find per offset (default: 100).
        /// Limits memory usage for very common patterns.
        /// </summary>
        public int MaxMatchesPerOffset { get; set; } = 100;

        /// <summary>
        /// Gets or sets the timeout in milliseconds (default: 30000 = 30 seconds).
        /// Search will be cancelled if it exceeds this time.
        /// </summary>
        public int TimeoutMs { get; set; } = 30000;

        /// <summary>
        /// Gets or sets whether to include detailed statistics in results (default: true).
        /// </summary>
        public bool IncludeStatistics { get; set; } = true;

        /// <summary>
        /// Validates the search options.
        /// </summary>
        /// <returns>True if options are valid, false otherwise.</returns>
        public bool IsValid()
        {
            // Search text must be at least 2 characters
            if (string.IsNullOrWhiteSpace(SearchText) || SearchText.Length < 2)
                return false;

            // Start position must be non-negative
            if (StartPosition < 0)
                return false;

            // End position must be -1 (unlimited) or >= start position
            if (EndPosition != -1 && EndPosition < StartPosition)
                return false;

            // Min matches must be positive
            if (MinMatchesRequired < 1)
                return false;

            // Max proposals must be positive
            if (MaxProposals < 1)
                return false;

            // Lengths must be positive
            if (SampleLength < 1 || PreviewLength < 1)
                return false;

            // Max matches per offset must be positive
            if (MaxMatchesPerOffset < 1)
                return false;

            // Timeout must be positive
            if (TimeoutMs < 100)  // At least 100ms
                return false;

            return true;
        }

        /// <summary>
        /// Creates a copy of the current search options.
        /// </summary>
        public RelativeSearchOptions Clone()
        {
            return new RelativeSearchOptions
            {
                SearchText = SearchText,
                StartPosition = StartPosition,
                EndPosition = EndPosition,
                MinMatchesRequired = MinMatchesRequired,
                MaxProposals = MaxProposals,
                CaseSensitive = CaseSensitive,
                SampleLength = SampleLength,
                PreviewLength = PreviewLength,
                UseParallelSearch = UseParallelSearch,
                MaxMatchesPerOffset = MaxMatchesPerOffset,
                TimeoutMs = TimeoutMs,
                IncludeStatistics = IncludeStatistics
            };
        }

        /// <summary>
        /// Gets a human-readable string representation of these options.
        /// </summary>
        public override string ToString()
        {
            return $"RelativeSearch(\"{SearchText}\", Range={StartPosition}-{(EndPosition == -1 ? "EOF" : EndPosition.ToString())}, MinMatches={MinMatchesRequired}, MaxProposals={MaxProposals}, Parallel={UseParallelSearch})";
        }
    }
}
