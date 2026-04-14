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
    /// Represents a single encoding proposal (one tested offset).
    /// </summary>
    public class EncodingProposal
    {
        /// <summary>
        /// Gets or sets the byte offset (0-255) that was tested.
        /// This is the value added to ASCII 'A' (0x41) to get the ROM's character encoding.
        /// </summary>
        public byte Offset { get; set; }

        /// <summary>
        /// Gets or sets the number of matches found for this encoding offset.
        /// Higher count usually indicates correct encoding.
        /// </summary>
        public int MatchCount { get; set; }

        /// <summary>
        /// Gets or sets the quality score (0-100) for this encoding proposal.
        /// Calculated based on: match count, printable characters, clustering, text validation, and TBL validation.
        /// </summary>
        public double Score { get; set; }

        /// <summary>
        /// Gets or sets the list of positions where matches were found.
        /// </summary>
        public List<long> MatchPositions { get; set; } = new List<long>();

        /// <summary>
        /// Gets or sets a short sample of decoded text (~100 chars) for quick preview.
        /// </summary>
        public string SampleText { get; set; }

        /// <summary>
        /// Gets or sets a longer preview of decoded text (~500 chars) for validation.
        /// Used to verify if the encoding is correct by looking for readable text.
        /// </summary>
        public string PreviewText { get; set; }

        /// <summary>
        /// Gets or sets the character mapping for this encoding.
        /// Key: Character offset (0-255), Value: (actual ROM byte value, decoded character)
        /// </summary>
        public Dictionary<int, (byte actualByte, char character)> CharacterMapping { get; set; } = new Dictionary<int, (byte, char)>();

        /// <summary>
        /// Gets or sets the percentage of printable characters in the decoded preview (0-100).
        /// Higher percentage usually indicates correct encoding.
        /// </summary>
        public double PrintableCharPercentage { get; set; }

        /// <summary>
        /// Gets or sets the average distance between matches (clustering metric).
        /// Lower values indicate matches are clustered together (text sections).
        /// </summary>
        public double AverageMatchDistance { get; set; }

        /// <summary>
        /// Gets a human-readable string representation of this proposal.
        /// </summary>
        public override string ToString()
        {
            return $"Offset +{Offset:D3} | Score: {Score:F1} | Matches: {MatchCount} | Sample: {(string.IsNullOrEmpty(SampleText) ? "(empty)" : SampleText.Substring(0, Math.Min(30, SampleText.Length)))}...";
        }

        /// <summary>
        /// Gets the first match position, or -1 if no matches.
        /// </summary>
        public long FirstMatchPosition => MatchPositions.Count > 0 ? MatchPositions.First() : -1;
    }
}
