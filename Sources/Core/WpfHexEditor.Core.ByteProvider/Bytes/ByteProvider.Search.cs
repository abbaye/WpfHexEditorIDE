// ==========================================================
// Project: WpfHexEditor.Core
// File: ByteProvider.Search.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude (Anthropic)
// Created: 2026-03-06
// Description:
//     Partial class of ByteProvider implementing the advanced search operations.
//     Integrates the SearchEngine (Boyer-Moore-Horspool algorithm) for ultra-fast
//     pattern matching with full cancellation support via CancellationToken.
//
// Architecture Notes:
//     Partial class pattern — exposes SearchEngine as lazy-initialized field.
//     Delegates all search logic to SearchEngine; this class is the integration
//     point between ByteProvider's data layer and the SearchModule services.
//
// ==========================================================

using System;
using System.Threading;
using WpfHexEditor.Core.Search.Models;
using WpfHexEditor.Core.Search.Services;

namespace WpfHexEditor.Core.Bytes
{
    /// <summary>
    /// ByteProvider partial class - Advanced Search Operations (V2)
    /// Integrates ultra-performant SearchEngine with Boyer-Moore-Horspool algorithm
    /// </summary>
    public sealed partial class ByteProvider
    {
        private SearchEngine _searchEngine;

        /// <summary>
        /// Gets the search engine instance (lazy initialization).
        /// </summary>
        private SearchEngine GetSearchEngine()
        {
            if (_searchEngine == null)
            {
                _searchEngine = new SearchEngine(this);
            }
            return _searchEngine;
        }

        /// <summary>
        /// Performs an advanced search with the specified options.
        /// This is the V2 ultra-performant search method with support for:
        /// - Boyer-Moore-Horspool algorithm (99% faster than naive search)
        /// - Parallel search for large files (>10MB)
        /// - Wildcard matching
        /// - Search direction control
        /// - Result limits
        /// - Cancellation support
        /// </summary>
        /// <param name="options">Search configuration options</param>
        /// <param name="cancellationToken">Cancellation token for long-running searches</param>
        /// <returns>SearchResult containing all matches and performance metrics</returns>
        public SearchResult Search(SearchOptions options, CancellationToken cancellationToken = default)
        {
            if (!IsOpen)
                return SearchResult.CreateError("No file is open");

            var engine = GetSearchEngine();
            return engine.Search(options, cancellationToken);
        }

        /// <summary>
        /// Finds the next occurrence of a pattern starting from the specified position.
        /// V2 method with advanced options support.
        /// </summary>
        /// <param name="pattern">Byte pattern to search for</param>
        /// <param name="startPosition">Position to start search from</param>
        /// <param name="forward">True to search forward, false to search backward</param>
        /// <param name="useWildcard">Enable wildcard matching (* for any byte)</param>
        /// <param name="wildcardByte">Byte value representing wildcard (default: 0xFF)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>SearchMatch with position and details, or null if not found</returns>
        public SearchMatch FindNextAdvanced(byte[] pattern, long startPosition, bool forward = true,
            bool useWildcard = false, byte wildcardByte = 0xFF, CancellationToken cancellationToken = default)
        {
            if (!IsOpen)
                return null;

            var engine = GetSearchEngine();
            return engine.FindNext(pattern, startPosition, forward, useWildcard, wildcardByte, cancellationToken);
        }

        /// <summary>
        /// Finds all occurrences of a pattern in the file.
        /// V2 method with advanced options support.
        /// </summary>
        /// <param name="pattern">Byte pattern to search for</param>
        /// <param name="useWildcard">Enable wildcard matching (* for any byte)</param>
        /// <param name="wildcardByte">Byte value representing wildcard (default: 0xFF)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>List of all matches with positions and details</returns>
        public System.Collections.Generic.List<SearchMatch> FindAllAdvanced(byte[] pattern,
            bool useWildcard = false, byte wildcardByte = 0xFF, CancellationToken cancellationToken = default)
        {
            if (!IsOpen)
                return new System.Collections.Generic.List<SearchMatch>();

            var engine = GetSearchEngine();
            return engine.FindAll(pattern, useWildcard, wildcardByte, cancellationToken);
        }

        /// <summary>
        /// Searches for a text string in the file.
        /// Converts text to bytes using specified encoding and performs search.
        /// </summary>
        /// <param name="text">Text to search for</param>
        /// <param name="encoding">Text encoding (default: UTF-8)</param>
        /// <param name="caseSensitive">Whether search is case-sensitive</param>
        /// <param name="startPosition">Position to start search from</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>SearchResult containing all matches</returns>
        public SearchResult SearchText(string text, System.Text.Encoding encoding = null,
            bool caseSensitive = true, long startPosition = 0, CancellationToken cancellationToken = default)
        {
            if (!IsOpen)
                return SearchResult.CreateError("No file is open");

            if (string.IsNullOrEmpty(text))
                return SearchResult.CreateError("Search text cannot be empty");

            // Default to UTF-8 if no encoding specified
            encoding = encoding ?? System.Text.Encoding.UTF8;

            // Convert text to bytes
            byte[] pattern = encoding.GetBytes(caseSensitive ? text : text.ToLowerInvariant());

            // Create search options
            var options = new SearchOptions
            {
                Pattern = pattern,
                StartPosition = startPosition,
                CaseSensitive = caseSensitive
            };

            var engine = GetSearchEngine();
            return engine.Search(options, cancellationToken);
        }

        /// <summary>
        /// Searches for a hex string pattern (e.g., "48 65 6C 6C 6F" or "48656C6C6F").
        /// Supports wildcards using "??" or "*" (e.g., "48 ?? 6C 6C 6F" matches "48 XX 6C 6C 6F").
        /// </summary>
        /// <param name="hexPattern">Hex string pattern with optional wildcards</param>
        /// <param name="startPosition">Position to start search from</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>SearchResult containing all matches</returns>
        public SearchResult SearchHex(string hexPattern, long startPosition = 0, CancellationToken cancellationToken = default)
        {
            if (!IsOpen)
                return SearchResult.CreateError("No file is open");

            if (string.IsNullOrWhiteSpace(hexPattern))
                return SearchResult.CreateError("Hex pattern cannot be empty");

            try
            {
                // Parse hex pattern with wildcard support
                var (pattern, hasWildcards) = ParseHexPattern(hexPattern);

                if (pattern == null || pattern.Length == 0)
                    return SearchResult.CreateError("Invalid hex pattern format");

                // Create search options
                var options = new SearchOptions
                {
                    Pattern = pattern,
                    StartPosition = startPosition,
                    UseWildcard = hasWildcards,
                    WildcardByte = 0xFF
                };

                var engine = GetSearchEngine();
                return engine.Search(options, cancellationToken);
            }
            catch (Exception ex)
            {
                return SearchResult.CreateError($"Failed to parse hex pattern: {ex.Message}");
            }
        }

        /// <summary>
        /// Parses a hex string pattern with optional wildcards.
        /// Supports formats: "48656C6C6F", "48 65 6C 6C 6F", "48 ?? 6C 6C 6F"
        /// </summary>
        private (byte[] pattern, bool hasWildcards) ParseHexPattern(string hexPattern)
        {
            // Remove spaces and common separators
            hexPattern = hexPattern.Replace(" ", "")
                                   .Replace("-", "")
                                   .Replace(":", "")
                                   .Replace("0x", "")
                                   .ToUpperInvariant();

            bool hasWildcards = false;
            var bytes = new System.Collections.Generic.List<byte>();

            for (int i = 0; i < hexPattern.Length; i += 2)
            {
                if (i + 1 >= hexPattern.Length)
                    throw new FormatException("Hex pattern must have an even number of characters");

                string hexByte = hexPattern.Substring(i, 2);

                // Check for wildcard patterns
                if (hexByte == "??" || hexByte == "**")
                {
                    bytes.Add(0xFF); // Use 0xFF as wildcard marker
                    hasWildcards = true;
                }
                else
                {
                    bytes.Add(Convert.ToByte(hexByte, 16));
                }
            }

            return (bytes.ToArray(), hasWildcards);
        }
    }
}
