//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;

namespace WpfHexEditor.Core.Search.Models
{
    /// <summary>
    /// Represents the complete result of a relative search operation.
    /// </summary>
    public class RelativeSearchResult
    {
        /// <summary>
        /// Gets or sets whether the search was successful (at least one proposal found).
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Gets or sets the list of encoding proposals found, sorted by score (highest first).
        /// </summary>
        public List<EncodingProposal> Proposals { get; set; } = new List<EncodingProposal>();

        /// <summary>
        /// Gets the total number of proposals found.
        /// </summary>
        public int Count => Proposals?.Count ?? 0;

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
        /// Gets or sets the search text that was used.
        /// </summary>
        public string SearchText { get; set; }

        /// <summary>
        /// Gets or sets the relative pattern that was generated from the search text.
        /// Array of distances between consecutive characters.
        /// </summary>
        public int[] RelativePattern { get; set; }

        /// <summary>
        /// Gets or sets the search options that were used.
        /// </summary>
        public RelativeSearchOptions Options { get; set; }

        /// <summary>
        /// Gets or sets the number of offsets tested (typically 256).
        /// </summary>
        public int OffsetsTested { get; set; }

        /// <summary>
        /// Gets or sets the total number of matches found across all offsets.
        /// </summary>
        public int TotalMatchesFound { get; set; }

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
        /// Gets the best proposal (highest score), or null if no proposals found.
        /// </summary>
        public EncodingProposal BestProposal => Proposals?.FirstOrDefault();

        /// <summary>
        /// Creates a successful search result with proposals.
        /// </summary>
        public static RelativeSearchResult CreateSuccess(
            List<EncodingProposal> proposals,
            long durationMs,
            long bytesSearched,
            string searchText,
            int[] relativePattern,
            RelativeSearchOptions options,
            int offsetsTested = 256,
            int totalMatchesFound = 0)
        {
            // Sort proposals by score (descending)
            var sortedProposals = proposals?.OrderByDescending(p => p.Score).ToList() ?? new List<EncodingProposal>();

            return new RelativeSearchResult
            {
                Success = sortedProposals.Count > 0,
                Proposals = sortedProposals,
                DurationMs = durationMs,
                BytesSearched = bytesSearched,
                SearchText = searchText,
                RelativePattern = relativePattern,
                Options = options,
                OffsetsTested = offsetsTested,
                TotalMatchesFound = totalMatchesFound
            };
        }

        /// <summary>
        /// Creates a failed search result with an error message.
        /// </summary>
        public static RelativeSearchResult CreateError(string errorMessage, RelativeSearchOptions options = null)
        {
            return new RelativeSearchResult
            {
                Success = false,
                ErrorMessage = errorMessage,
                Options = options
            };
        }

        /// <summary>
        /// Creates a cancelled search result.
        /// </summary>
        public static RelativeSearchResult CreateCancelled(long durationMs = 0, RelativeSearchOptions options = null)
        {
            return new RelativeSearchResult
            {
                Success = false,
                WasCancelled = true,
                DurationMs = durationMs,
                Options = options
            };
        }

        /// <summary>
        /// Gets a human-readable string representation of this result.
        /// </summary>
        public override string ToString()
        {
            if (!Success)
            {
                if (WasCancelled)
                    return "Search cancelled";
                if (!string.IsNullOrEmpty(ErrorMessage))
                    return $"Search failed: {ErrorMessage}";
                return "No encoding proposals found";
            }

            if (Count == 0)
                return "Search completed but no proposals found";

            var best = BestProposal;
            return $"Found {Count} proposal(s) in {DurationMs}ms ({SpeedMBps:F2} MB/s) | Best: Offset +{best.Offset} (Score: {best.Score:F1}, Matches: {best.MatchCount})";
        }

        /// <summary>
        /// Gets a detailed summary of the search results.
        /// </summary>
        public string GetDetailedSummary()
        {
            if (!Success)
                return ToString();

            var summary = new System.Text.StringBuilder();
            summary.AppendLine($"Relative Search Results for \"{SearchText}\"");
            summary.AppendLine($"Pattern: [{string.Join(", ", RelativePattern ?? new int[0])}]");
            summary.AppendLine($"Duration: {DurationMs}ms | Speed: {SpeedMBps:F2} MB/s");
            summary.AppendLine($"Bytes Searched: {BytesSearched:N0} | Offsets Tested: {OffsetsTested}");
            summary.AppendLine($"Total Matches: {TotalMatchesFound:N0} | Proposals: {Count}");
            summary.AppendLine();

            summary.AppendLine("Top Proposals:");
            for (int i = 0; i < Math.Min(5, Count); i++)
            {
                var proposal = Proposals[i];
                summary.AppendLine($"  {i + 1}. {proposal}");
            }

            return summary.ToString();
        }
    }
}
