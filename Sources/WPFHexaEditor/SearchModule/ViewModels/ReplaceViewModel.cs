////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5
//////////////////////////////////////////////

using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using WpfHexaEditor.Core.Bytes;
using WpfHexaEditor.Properties;
using WpfHexaEditor.SearchModule.Models;

namespace WpfHexaEditor.SearchModule.ViewModels
{
    /// <summary>
    /// ViewModel for find and replace operations.
    /// Extends SearchViewModel with replace functionality.
    /// </summary>
    public class ReplaceViewModel : SearchViewModel
    {
        #region Fields

        private string _replaceText = string.Empty;
        private string _replaceHex = string.Empty;
        private bool _truncateReplacement = false;
        private int _replaceCount = 0;

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets the replacement text (for Text mode).
        /// </summary>
        public string ReplaceText
        {
            get => _replaceText;
            set
            {
                if (_replaceText != value)
                {
                    _replaceText = value;
                    OnPropertyChanged();
                    UpdateReplaceCommandStates();
                }
            }
        }

        /// <summary>
        /// Gets or sets the replacement hex pattern (for Hex mode).
        /// </summary>
        public string ReplaceHex
        {
            get => _replaceHex;
            set
            {
                if (_replaceHex != value)
                {
                    _replaceHex = value;
                    OnPropertyChanged();
                    UpdateReplaceCommandStates();
                }
            }
        }

        /// <summary>
        /// Gets or sets whether to truncate replacement to match find pattern length.
        /// </summary>
        public bool TruncateReplacement
        {
            get => _truncateReplacement;
            set
            {
                if (_truncateReplacement != value)
                {
                    _truncateReplacement = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Gets the total number of replacements made.
        /// </summary>
        public int ReplaceCount
        {
            get => _replaceCount;
            private set
            {
                if (_replaceCount != value)
                {
                    _replaceCount = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Gets whether replace operations can be performed.
        /// </summary>
        public bool CanReplace => CanSearch && !string.IsNullOrEmpty(GetReplacePattern()) &&
                                  ByteProvider != null && !ByteProvider.IsReadOnly;

        #endregion

        #region Commands

        public ICommand ReplaceNextCommand { get; }
        public ICommand ReplaceAllCommand { get; }

        #endregion

        #region Constructor

        public ReplaceViewModel()
        {
            ReplaceNextCommand = new RelayCommand(async () => await ReplaceNextAsync(), () => CanReplace);
            ReplaceAllCommand = new RelayCommand(async () => await ReplaceAllAsync(), () => CanReplace);

            // Subscribe to property changes to update command states
            PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(CanSearch) || e.PropertyName == nameof(ByteProvider))
                {
                    OnPropertyChanged(nameof(CanReplace));
                    UpdateReplaceCommandStates();
                }
            };
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Replaces the next occurrence of the search pattern.
        /// </summary>
        public async Task ReplaceNextAsync()
        {
            if (!CanReplace) return;

            try
            {
                // First, find the next match
                await FindNextAsync();

                // If a match was found (check if we have results)
                if (CurrentMatchIndex >= 0 && CurrentMatchIndex < SearchResults.Count)
                {
                    var match = SearchResults[CurrentMatchIndex];
                    var replaceBytes = GetReplacementBytes();

                    if (replaceBytes != null && replaceBytes.Length > 0)
                    {
                        // Perform the replacement
                        PerformReplace(match.Position, match.Length, replaceBytes);
                        ReplaceCount++;

                        StatusMessage = string.Format(Properties.Resources.StatusReplacedAtFormat, match.Position, ReplaceCount);

                        // Remove this match from results since it's been replaced
                        SearchResults.RemoveAt(CurrentMatchIndex);

                        // Adjust current index
                        if (CurrentMatchIndex >= SearchResults.Count && SearchResults.Count > 0)
                            CurrentMatchIndex = SearchResults.Count - 1;
                        else if (SearchResults.Count == 0)
                            CurrentMatchIndex = -1;
                    }
                }
            }
            catch (Exception ex)
            {
                StatusMessage = string.Format(Properties.Resources.StatusReplaceError, ex.Message);
            }
        }

        /// <summary>
        /// Replaces all occurrences of the search pattern.
        /// </summary>
        public async Task ReplaceAllAsync()
        {
            if (!CanReplace) return;

            try
            {
                // First, find all matches
                await FindAllAsync();

                if (SearchResults.Count == 0)
                {
                    StatusMessage = Properties.Resources.StatusNoMatchesToReplace;
                    return;
                }

                var replaceBytes = GetReplacementBytes();
                if (replaceBytes == null || replaceBytes.Length == 0)
                {
                    StatusMessage = Properties.Resources.StatusInvalidReplacementPattern;
                    return;
                }

                int replacedCount = 0;
                long offset = 0;

                // Create a snapshot of positions since they'll change during replacement
                var matchesToReplace = new System.Collections.Generic.List<SearchMatch>(SearchResults);

                // Start batch mode to improve performance
                ByteProvider.BeginBatch();

                try
                {
                    // Replace from end to beginning to maintain position accuracy
                    for (int i = matchesToReplace.Count - 1; i >= 0; i--)
                    {
                        var match = matchesToReplace[i];
                        long adjustedPosition = match.Position + offset;

                        PerformReplace(adjustedPosition, match.Length, replaceBytes);
                        replacedCount++;

                        // Calculate offset for next replacements
                        offset += (replaceBytes.Length - match.Length);
                    }
                }
                finally
                {
                    ByteProvider.EndBatch();
                }

                ReplaceCount += replacedCount;
                SearchResults.Clear();
                CurrentMatchIndex = -1;

                StatusMessage = string.Format(Properties.Resources.StatusReplacedOccurrencesFormat, replacedCount, ReplaceCount);
            }
            catch (Exception ex)
            {
                StatusMessage = string.Format(Properties.Resources.StatusReplaceAllError, ex.Message);
            }
        }

        /// <summary>
        /// Resets the replacement counter.
        /// </summary>
        public void ResetReplaceCount()
        {
            ReplaceCount = 0;
        }

        #endregion

        #region Private Methods

        private string GetReplacePattern()
        {
            return SelectedSearchMode == SearchMode.Text ? ReplaceText : ReplaceHex;
        }

        private byte[] GetReplacementBytes()
        {
            try
            {
                if (SelectedSearchMode == SearchMode.Text)
                {
                    if (string.IsNullOrEmpty(ReplaceText))
                        return Array.Empty<byte>();

                    var text = CaseSensitive ? ReplaceText : ReplaceText.ToLowerInvariant();
                    return SelectedEncoding.GetBytes(text);
                }
                else // Hex mode
                {
                    if (string.IsNullOrEmpty(ReplaceHex))
                        return Array.Empty<byte>();

                    var (hexPattern, _) = ParseHexPattern(ReplaceHex);
                    return hexPattern;
                }
            }
            catch
            {
                return null;
            }
        }

        private (byte[] pattern, bool hasWildcards) ParseHexPattern(string hexPattern)
        {
            hexPattern = hexPattern.Replace(" ", "")
                                   .Replace("-", "")
                                   .Replace(":", "")
                                   .Replace("0x", "")
                                   .ToUpperInvariant();

            var bytes = new System.Collections.Generic.List<byte>();

            for (int i = 0; i < hexPattern.Length; i += 2)
            {
                if (i + 1 >= hexPattern.Length)
                    break;

                string hexByte = hexPattern.Substring(i, 2);
                bytes.Add(Convert.ToByte(hexByte, 16));
            }

            return (bytes.ToArray(), false);
        }

        private void PerformReplace(long position, int findLength, byte[] replaceBytes)
        {
            if (TruncateReplacement && replaceBytes.Length > findLength)
            {
                // Truncate replacement to match find length
                var truncated = new byte[findLength];
                Array.Copy(replaceBytes, truncated, findLength);
                replaceBytes = truncated;
            }

            // Delete the found pattern
            if (findLength > 0)
            {
                ByteProvider.DeleteBytes(position, findLength);
            }

            // Insert the replacement
            if (replaceBytes.Length > 0)
            {
                ByteProvider.InsertBytes(position, replaceBytes);
            }
        }

        private void UpdateReplaceCommandStates()
        {
            (ReplaceNextCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (ReplaceAllCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }

        #endregion
    }
}
