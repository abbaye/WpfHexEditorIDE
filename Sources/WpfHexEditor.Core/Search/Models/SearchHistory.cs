//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System;
using System.Text;

namespace WpfHexEditor.Core.Search.Models
{
    /// <summary>
    /// Represents a single entry in the search history.
    /// Used for MRU (Most Recently Used) search pattern tracking.
    /// </summary>
    public class SearchHistoryEntry
    {
        /// <summary>
        /// Gets or sets the raw search pattern entered by the user.
        /// </summary>
        public string Pattern { get; set; }

        /// <summary>
        /// Gets or sets the search mode used (Text, Hex, Wildcard, TblText, Relative).
        /// </summary>
        public SearchMode Mode { get; set; }

        /// <summary>
        /// Gets or sets the encoding used (only for Text mode).
        /// </summary>
        public Encoding Encoding { get; set; }

        /// <summary>
        /// Gets or sets whether the search was case-sensitive.
        /// </summary>
        public bool CaseSensitive { get; set; }

        /// <summary>
        /// Gets or sets whether wildcards were used.
        /// </summary>
        public bool UseWildcard { get; set; }

        /// <summary>
        /// Gets or sets the last time this pattern was used.
        /// </summary>
        public DateTime LastUsed { get; set; }

        /// <summary>
        /// Gets or sets the number of times this pattern was used.
        /// </summary>
        public int UseCount { get; set; }

        /// <summary>
        /// Gets a display-friendly description of this history entry.
        /// </summary>
        public string DisplayText
        {
            get
            {
                var mode = Mode switch
                {
                    SearchMode.Text => "TXT",
                    SearchMode.Hex => "HEX",
                    SearchMode.Wildcard => "WLD",
                    SearchMode.TblText => "TBL",
                    SearchMode.Relative => "REL",
                    _ => "???"
                };

                var pattern = Pattern?.Length > 30 ? Pattern.Substring(0, 27) + "..." : Pattern;
                return $"[{mode}] {pattern}";
            }
        }

        /// <summary>
        /// Creates a copy of this history entry.
        /// </summary>
        public SearchHistoryEntry Clone()
        {
            return new SearchHistoryEntry
            {
                Pattern = Pattern,
                Mode = Mode,
                Encoding = Encoding,
                CaseSensitive = CaseSensitive,
                UseWildcard = UseWildcard,
                LastUsed = LastUsed,
                UseCount = UseCount
            };
        }

        public override string ToString()
        {
            return DisplayText;
        }
    }
}
