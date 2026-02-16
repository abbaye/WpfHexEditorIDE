////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5
//////////////////////////////////////////////

using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using WpfHexaEditor.Core.Bytes;
using WpfHexaEditor.Properties;
using WpfHexaEditor.SearchModule.Models;

namespace WpfHexaEditor.SearchModule.ViewModels
{
    /// <summary>
    /// ViewModel for search operations with full MVVM support.
    /// Provides bindings for search UI controls and manages search state.
    /// </summary>
    public class SearchViewModel : INotifyPropertyChanged
    {
        #region Fields

        private ByteProvider _byteProvider;
        private CancellationTokenSource _cancellationTokenSource;

        private string _searchText = string.Empty;
        private string _searchHex = string.Empty;
        private SearchMode _selectedSearchMode = SearchMode.Text;
        private Encoding _selectedEncoding = Encoding.UTF8;
        private bool _caseSensitive = true;
        private bool _useWildcard = false;
        private bool _searchBackward = false;
        private bool _wrapAround = true;
        private bool _isSearching = false;
        private int _currentMatchIndex = -1;
        private string _statusMessage = string.Empty;
        private SearchResult _lastSearchResult;

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets the ByteProvider instance for searching.
        /// </summary>
        public ByteProvider ByteProvider
        {
            get => _byteProvider;
            set
            {
                if (_byteProvider != value)
                {
                    _byteProvider = value;
                    OnPropertyChanged();
                    UpdateCommandStates();
                }
            }
        }

        /// <summary>
        /// Gets or sets the search text (for Text mode).
        /// </summary>
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (_searchText != value)
                {
                    _searchText = value;
                    OnPropertyChanged();
                    UpdateCommandStates();
                }
            }
        }

        /// <summary>
        /// Gets or sets the search hex pattern (for Hex mode).
        /// </summary>
        public string SearchHex
        {
            get => _searchHex;
            set
            {
                if (_searchHex != value)
                {
                    _searchHex = value;
                    OnPropertyChanged();
                    UpdateCommandStates();
                }
            }
        }

        /// <summary>
        /// Gets or sets the selected search mode (Text or Hex).
        /// </summary>
        public SearchMode SelectedSearchMode
        {
            get => _selectedSearchMode;
            set
            {
                if (_selectedSearchMode != value)
                {
                    _selectedSearchMode = value;
                    OnPropertyChanged();
                    UpdateCommandStates();
                }
            }
        }

        /// <summary>
        /// Gets or sets the selected text encoding (for Text mode).
        /// </summary>
        public Encoding SelectedEncoding
        {
            get => _selectedEncoding;
            set
            {
                if (_selectedEncoding != value)
                {
                    _selectedEncoding = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Gets available text encodings.
        /// </summary>
        public ObservableCollection<EncodingInfo> AvailableEncodings { get; } = new ObservableCollection<EncodingInfo>
        {
            new EncodingInfo { Name = "UTF-8", Encoding = Encoding.UTF8 },
            new EncodingInfo { Name = "UTF-16 (Unicode)", Encoding = Encoding.Unicode },
            new EncodingInfo { Name = "UTF-16 BE", Encoding = Encoding.BigEndianUnicode },
            new EncodingInfo { Name = "UTF-32", Encoding = Encoding.UTF32 },
            new EncodingInfo { Name = "ASCII", Encoding = Encoding.ASCII },
            new EncodingInfo { Name = "Latin-1 (ISO-8859-1)", Encoding = Encoding.GetEncoding(28591) }
        };

        /// <summary>
        /// Gets or sets whether the search is case-sensitive (Text mode only).
        /// </summary>
        public bool CaseSensitive
        {
            get => _caseSensitive;
            set
            {
                if (_caseSensitive != value)
                {
                    _caseSensitive = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Gets or sets whether wildcard matching is enabled (Hex mode only).
        /// </summary>
        public bool UseWildcard
        {
            get => _useWildcard;
            set
            {
                if (_useWildcard != value)
                {
                    _useWildcard = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Gets or sets whether to search backward.
        /// </summary>
        public bool SearchBackward
        {
            get => _searchBackward;
            set
            {
                if (_searchBackward != value)
                {
                    _searchBackward = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Gets or sets whether to wrap around at end/start of file.
        /// </summary>
        public bool WrapAround
        {
            get => _wrapAround;
            set
            {
                if (_wrapAround != value)
                {
                    _wrapAround = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Gets whether a search operation is currently running.
        /// </summary>
        public bool IsSearching
        {
            get => _isSearching;
            private set
            {
                if (_isSearching != value)
                {
                    _isSearching = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(CanSearch));
                    UpdateCommandStates();
                }
            }
        }

        /// <summary>
        /// Gets whether search can be performed.
        /// </summary>
        public bool CanSearch => !IsSearching && _byteProvider != null && _byteProvider.IsOpen &&
                                 (SelectedSearchMode == SearchMode.Text ? !string.IsNullOrEmpty(SearchText) : !string.IsNullOrEmpty(SearchHex));

        /// <summary>
        /// Gets the search results collection.
        /// </summary>
        public ObservableCollection<SearchMatch> SearchResults { get; } = new ObservableCollection<SearchMatch>();

        /// <summary>
        /// Gets or sets the current match index in the results.
        /// </summary>
        public int CurrentMatchIndex
        {
            get => _currentMatchIndex;
            set
            {
                if (_currentMatchIndex != value)
                {
                    _currentMatchIndex = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(CurrentMatchText));
                    UpdateCommandStates();
                }
            }
        }

        /// <summary>
        /// Gets the current match text (e.g., "3 of 15").
        /// </summary>
        public string CurrentMatchText
        {
            get
            {
                if (SearchResults.Count == 0)
                    return Resources.SearchNoMatchesString;

                if (CurrentMatchIndex >= 0 && CurrentMatchIndex < SearchResults.Count)
                    return $"{CurrentMatchIndex + 1} of {SearchResults.Count}";

                return $"{SearchResults.Count} matches";
            }
        }

        /// <summary>
        /// Gets or sets the status message.
        /// </summary>
        public string StatusMessage
        {
            get => _statusMessage;
            set
            {
                if (_statusMessage != value)
                {
                    _statusMessage = value;
                    OnPropertyChanged();
                }
            }
        }

        #endregion

        #region Commands

        public ICommand FindAllCommand { get; }
        public ICommand FindNextCommand { get; }
        public ICommand FindPreviousCommand { get; }
        public ICommand CancelSearchCommand { get; }
        public ICommand ClearResultsCommand { get; }
        public ICommand NavigateToMatchCommand { get; }

        #endregion

        #region Constructor

        public SearchViewModel()
        {
            FindAllCommand = new RelayCommand(async () => await FindAllAsync(), () => CanSearch);
            FindNextCommand = new RelayCommand(async () => await FindNextAsync(), () => CanSearch);
            FindPreviousCommand = new RelayCommand(async () => await FindPreviousAsync(), () => CanSearch);
            CancelSearchCommand = new RelayCommand(CancelSearch, () => IsSearching);
            ClearResultsCommand = new RelayCommand(ClearResults, () => SearchResults.Count > 0);
            NavigateToMatchCommand = new RelayCommand<int>(NavigateToMatch);
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Performs a "Find All" search operation.
        /// </summary>
        public async Task FindAllAsync()
        {
            if (!CanSearch) return;

            IsSearching = true;
            StatusMessage = Properties.Resources.StatusSearching;
            SearchResults.Clear();
            CurrentMatchIndex = -1;

            _cancellationTokenSource = new CancellationTokenSource();

            try
            {
                var options = BuildSearchOptions();
                options.StartPosition = 0;

                var result = await Task.Run(() => _byteProvider.Search(options, _cancellationTokenSource.Token));

                _lastSearchResult = result;

                if (result.Success && result.Matches.Count > 0)
                {
                    foreach (var match in result.Matches)
                    {
                        SearchResults.Add(match);
                    }

                    CurrentMatchIndex = 0;
                    StatusMessage = string.Format(Properties.Resources.StatusFoundMatchesWithSpeedFormat, result.Count, result.DurationMs, result.SpeedMBps);

                    // Navigate to first match
                    OnMatchFound?.Invoke(this, SearchResults[0]);
                }
                else if (result.WasCancelled)
                {
                    StatusMessage = Properties.Resources.StatusSearchCancelled;
                }
                else if (!string.IsNullOrEmpty(result.ErrorMessage))
                {
                    StatusMessage = string.Format(Properties.Resources.StatusError, result.ErrorMessage);
                }
                else
                {
                    StatusMessage = Properties.Resources.SearchNoMatchesString;
                }
            }
            catch (Exception ex)
            {
                StatusMessage = string.Format(Properties.Resources.StatusError, ex.Message);
            }
            finally
            {
                IsSearching = false;
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
            }
        }

        /// <summary>
        /// Finds the next occurrence from current position.
        /// </summary>
        public async Task FindNextAsync(long? startPosition = null)
        {
            if (!CanSearch) return;

            IsSearching = true;
            StatusMessage = Properties.Resources.StatusSearching;

            _cancellationTokenSource = new CancellationTokenSource();

            try
            {
                var options = BuildSearchOptions();
                options.StartPosition = startPosition ?? GetCurrentSearchPosition(forward: true);
                options.MaxResults = 1;
                options.SearchBackward = false;

                var result = await Task.Run(() => _byteProvider.Search(options, _cancellationTokenSource.Token));

                if (result.Success && result.Matches.Count > 0)
                {
                    var match = result.Matches[0];

                    // Update results if in "Find All" mode
                    if (SearchResults.Count > 0)
                    {
                        var index = SearchResults.Select((m, i) => new { Match = m, Index = i })
                                                 .FirstOrDefault(x => x.Match.Position == match.Position)?.Index ?? -1;

                        if (index >= 0)
                            CurrentMatchIndex = index;
                    }

                    StatusMessage = string.Format(Properties.Resources.StatusMatchFoundAtFormat, match.Position);
                    OnMatchFound?.Invoke(this, match);
                }
                else if (WrapAround && startPosition.HasValue && startPosition.Value > 0)
                {
                    // Wrap around to beginning
                    await FindNextAsync(0);
                    return;
                }
                else
                {
                    StatusMessage = Properties.Resources.StatusNoMoreMatches;
                }
            }
            catch (Exception ex)
            {
                StatusMessage = string.Format(Properties.Resources.StatusError, ex.Message);
            }
            finally
            {
                IsSearching = false;
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
            }
        }

        /// <summary>
        /// Finds the previous occurrence from current position.
        /// </summary>
        public async Task FindPreviousAsync()
        {
            if (!CanSearch) return;

            IsSearching = true;
            StatusMessage = Properties.Resources.StatusSearching;

            _cancellationTokenSource = new CancellationTokenSource();

            try
            {
                var options = BuildSearchOptions();
                options.StartPosition = 0;
                options.EndPosition = GetCurrentSearchPosition(forward: false);
                options.MaxResults = 1;
                options.SearchBackward = true;

                var result = await Task.Run(() => _byteProvider.Search(options, _cancellationTokenSource.Token));

                if (result.Success && result.Matches.Count > 0)
                {
                    var match = result.Matches[0];

                    // Update results if in "Find All" mode
                    if (SearchResults.Count > 0)
                    {
                        var index = SearchResults.Select((m, i) => new { Match = m, Index = i })
                                                 .FirstOrDefault(x => x.Match.Position == match.Position)?.Index ?? -1;

                        if (index >= 0)
                            CurrentMatchIndex = index;
                    }

                    StatusMessage = string.Format(Properties.Resources.StatusMatchFoundAtFormat, match.Position);
                    OnMatchFound?.Invoke(this, match);
                }
                else if (WrapAround)
                {
                    // Wrap around to end
                    var wrapOptions = BuildSearchOptions();
                    wrapOptions.StartPosition = 0;
                    wrapOptions.MaxResults = 1;
                    wrapOptions.SearchBackward = true;

                    var wrapResult = await Task.Run(() => _byteProvider.Search(wrapOptions, _cancellationTokenSource.Token));

                    if (wrapResult.Success && wrapResult.Matches.Count > 0)
                    {
                        var match = wrapResult.Matches[0];
                        StatusMessage = string.Format(Properties.Resources.StatusMatchFoundAtFormat, match.Position);
                        OnMatchFound?.Invoke(this, match);
                    }
                    else
                    {
                        StatusMessage = Properties.Resources.SearchNoMatchesString;
                    }
                }
                else
                {
                    StatusMessage = Properties.Resources.StatusNoMoreMatches;
                }
            }
            catch (Exception ex)
            {
                StatusMessage = string.Format(Properties.Resources.StatusError, ex.Message);
            }
            finally
            {
                IsSearching = false;
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
            }
        }

        /// <summary>
        /// Cancels the current search operation.
        /// </summary>
        public void CancelSearch()
        {
            _cancellationTokenSource?.Cancel();
            StatusMessage = Properties.Resources.StatusSearchCancelled;
        }

        /// <summary>
        /// Clears all search results.
        /// </summary>
        public void ClearResults()
        {
            SearchResults.Clear();
            CurrentMatchIndex = -1;
            StatusMessage = Properties.Resources.ReadyString;
            _lastSearchResult = null;
        }

        /// <summary>
        /// Navigates to a specific match by index.
        /// </summary>
        public void NavigateToMatch(int index)
        {
            if (index >= 0 && index < SearchResults.Count)
            {
                CurrentMatchIndex = index;
                var match = SearchResults[index];
                StatusMessage = $"Match {index + 1} of {SearchResults.Count} at position 0x{match.Position:X8}";
                OnMatchFound?.Invoke(this, match);
            }
        }

        #endregion

        #region Events

        /// <summary>
        /// Event raised when a match is found (for navigation purposes).
        /// </summary>
        public event EventHandler<SearchMatch> OnMatchFound;

        #endregion

        #region Private Methods

        private SearchOptions BuildSearchOptions()
        {
            byte[] pattern;

            if (SelectedSearchMode == SearchMode.Text)
            {
                var text = CaseSensitive ? SearchText : SearchText.ToLowerInvariant();
                pattern = SelectedEncoding.GetBytes(text);
            }
            else // Hex mode
            {
                var (hexPattern, hasWildcards) = ParseHexPattern(SearchHex);
                pattern = hexPattern;
                UseWildcard = hasWildcards;
            }

            return new SearchOptions
            {
                Pattern = pattern,
                CaseSensitive = CaseSensitive,
                SearchBackward = SearchBackward,
                UseWildcard = UseWildcard,
                WildcardByte = 0xFF,
                WrapAround = WrapAround,
                UseParallelSearch = true
            };
        }

        private (byte[] pattern, bool hasWildcards) ParseHexPattern(string hexPattern)
        {
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
                    break;

                string hexByte = hexPattern.Substring(i, 2);

                if (hexByte == "??" || hexByte == "**")
                {
                    bytes.Add(0xFF);
                    hasWildcards = true;
                }
                else
                {
                    bytes.Add(Convert.ToByte(hexByte, 16));
                }
            }

            return (bytes.ToArray(), hasWildcards);
        }

        private long GetCurrentSearchPosition(bool forward)
        {
            // Default to start/end if no current position available
            if (CurrentMatchIndex < 0 || CurrentMatchIndex >= SearchResults.Count)
                return forward ? 0 : (_byteProvider?.VirtualLength ?? 0);

            var currentMatch = SearchResults[CurrentMatchIndex];
            return forward ? currentMatch.Position + currentMatch.Length : currentMatch.Position;
        }

        private void UpdateCommandStates()
        {
            (FindAllCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (FindNextCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (FindPreviousCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (CancelSearchCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (ClearResultsCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }

    #region Supporting Types

    /// <summary>
    /// Search mode enumeration.
    /// </summary>
    public enum SearchMode
    {
        Text,
        Hex
    }

    /// <summary>
    /// Encoding information for UI binding.
    /// </summary>
    public class EncodingInfo
    {
        public string Name { get; set; }
        public Encoding Encoding { get; set; }

        public override string ToString() => Name;
    }

    /// <summary>
    /// Simple RelayCommand implementation for MVVM.
    /// </summary>
    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool> _canExecute;

        public RelayCommand(Action execute, Func<bool> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public event EventHandler CanExecuteChanged;

        public bool CanExecute(object parameter) => _canExecute?.Invoke() ?? true;

        public void Execute(object parameter) => _execute();

        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Generic RelayCommand with parameter support.
    /// </summary>
    public class RelayCommand<T> : ICommand
    {
        private readonly Action<T> _execute;
        private readonly Func<T, bool> _canExecute;

        public RelayCommand(Action<T> execute, Func<T, bool> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public event EventHandler CanExecuteChanged;

        public bool CanExecute(object parameter)
        {
            if (parameter is T typedParam)
                return _canExecute?.Invoke(typedParam) ?? true;
            return false;
        }

        public void Execute(object parameter)
        {
            if (parameter is T typedParam)
                _execute(typedParam);
        }

        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }

    #endregion
}
