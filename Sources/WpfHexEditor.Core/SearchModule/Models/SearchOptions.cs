using System;

namespace WpfHexaEditor.SearchModule.Models
{
    /// <summary>
    /// Represents search configuration options for the hex editor.
    /// </summary>
    public class SearchOptions
    {
        /// <summary>
        /// Gets or sets the search pattern (byte array for binary search).
        /// </summary>
        public byte[] Pattern { get; set; }

        /// <summary>
        /// Gets or sets whether the search should be case-sensitive (for text mode).
        /// </summary>
        public bool CaseSensitive { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to search in reverse direction (from end to start).
        /// </summary>
        public bool SearchBackward { get; set; } = false;

        /// <summary>
        /// Gets or sets the starting position for the search.
        /// </summary>
        public long StartPosition { get; set; } = 0;

        /// <summary>
        /// Gets or sets the ending position for the search (use -1 for end of file).
        /// </summary>
        public long EndPosition { get; set; } = -1;

        /// <summary>
        /// Gets or sets whether to use wildcard matching (* for any byte).
        /// </summary>
        public bool UseWildcard { get; set; } = false;

        /// <summary>
        /// Gets or sets the wildcard byte value (default: 0xFF represents wildcard).
        /// </summary>
        public byte WildcardByte { get; set; } = 0xFF;

        /// <summary>
        /// Gets or sets whether to use parallel search for large files (>10MB).
        /// </summary>
        public bool UseParallelSearch { get; set; } = true;

        /// <summary>
        /// Gets or sets the chunk size for parallel search (default: 1MB).
        /// </summary>
        public int ParallelChunkSize { get; set; } = 1024 * 1024;

        /// <summary>
        /// Gets or sets the maximum number of results to return (0 = unlimited).
        /// </summary>
        public int MaxResults { get; set; } = 0;

        /// <summary>
        /// Gets or sets whether to wrap around when reaching end/start of file.
        /// </summary>
        public bool WrapAround { get; set; } = false;

        /// <summary>
        /// Gets or sets the number of context bytes to capture before and after each match.
        /// Default is 8 bytes. Set to 0 to disable context capture.
        /// </summary>
        public int ContextRadius { get; set; } = 8;

        /// <summary>
        /// Validates the search options.
        /// </summary>
        public bool IsValid()
        {
            if (Pattern == null || Pattern.Length == 0)
                return false;

            if (StartPosition < 0)
                return false;

            return true;
        }

        /// <summary>
        /// Creates a copy of the current search options.
        /// </summary>
        public SearchOptions Clone()
        {
            return new SearchOptions
            {
                Pattern = (byte[])Pattern?.Clone(),
                CaseSensitive = CaseSensitive,
                SearchBackward = SearchBackward,
                StartPosition = StartPosition,
                EndPosition = EndPosition,
                UseWildcard = UseWildcard,
                WildcardByte = WildcardByte,
                UseParallelSearch = UseParallelSearch,
                ParallelChunkSize = ParallelChunkSize,
                MaxResults = MaxResults,
                WrapAround = WrapAround
            };
        }
    }
}
