//////////////////////////////////////////////
// GNU Affero General Public License v3.0  2026
// HexEditor V2 - Search Command Center ViewModel
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5, Claude Sonnet 4.6
//////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using WpfHexEditor.Core.Bytes;
using WpfHexEditor.Core.Search.Models;
using WpfHexEditor.Core.ViewModels;

namespace WpfHexEditor.Sample.HexEditor.ViewModels
{
    /// <summary>
    /// ViewModel for Search Command Center - showcasing ultra-performant V2 search
    /// </summary>
    public class SearchCommandCenterViewModel : ViewModelBase
    {
        #region Fields

        private ByteProvider _byteProvider;
        private string _searchPattern = "";
        private SearchMode _selectedSearchMode = SearchMode.Text;
        private bool _isSearching;
        private List<SearchMatch> _searchResults = new List<SearchMatch>();
        private int _currentMatchIndex = -1;
        private double _searchDurationMs;
        private CancellationTokenSource _cancellationTokenSource;

        #endregion

        #region Events

        public event EventHandler<SearchMatch> OnMatchFound;

        #endregion

        #region Properties

        public ByteProvider ByteProvider
        {
            get => _byteProvider;
            set
            {
                _byteProvider = value;
                OnPropertyChanged(nameof(ByteProvider));
                OnPropertyChanged(nameof(CanSearch));
            }
        }

        public string SearchPattern
        {
            get => _searchPattern;
            set
            {
                _searchPattern = value;
                OnPropertyChanged(nameof(SearchPattern));
                OnPropertyChanged(nameof(HasSearchPattern));
                OnPropertyChanged(nameof(CanSearch));
            }
        }

        public SearchMode SelectedSearchMode
        {
            get => _selectedSearchMode;
            set
            {
                _selectedSearchMode = value;
                OnPropertyChanged(nameof(SelectedSearchMode));
                OnPropertyChanged(nameof(IsTextMode));
                OnPropertyChanged(nameof(IsHexMode));
                OnPropertyChanged(nameof(IsRegexMode));
                OnPropertyChanged(nameof(IsWildcardMode));
            }
        }

        public bool IsTextMode
        {
            get => SelectedSearchMode == SearchMode.Text;
            set { if (value) SelectedSearchMode = SearchMode.Text; }
        }

        public bool IsHexMode
        {
            get => SelectedSearchMode == SearchMode.Hex;
            set { if (value) SelectedSearchMode = SearchMode.Hex; }
        }

        public bool IsRegexMode
        {
            get => SelectedSearchMode == SearchMode.Regex;
            set { if (value) SelectedSearchMode = SearchMode.Regex; }
        }

        public bool IsWildcardMode
        {
            get => SelectedSearchMode == SearchMode.Wildcard;
            set { if (value) SelectedSearchMode = SearchMode.Wildcard; }
        }

        public bool HasSearchPattern => !string.IsNullOrWhiteSpace(SearchPattern);

        public bool CanSearch => ByteProvider != null && HasSearchPattern && !IsSearching;

        public bool IsSearching
        {
            get => _isSearching;
            set
            {
                _isSearching = value;
                OnPropertyChanged(nameof(IsSearching));
                OnPropertyChanged(nameof(CanSearch));
            }
        }

        public int ResultCount => _searchResults?.Count ?? 0;

        public bool HasResults => ResultCount > 0;

        public bool HasNoResults => !IsSearching && HasSearchPattern && ResultCount == 0;

        public double SearchDurationMs
        {
            get => _searchDurationMs;
            set
            {
                _searchDurationMs = value;
                OnPropertyChanged(nameof(SearchDurationMs));
                OnPropertyChanged(nameof(SearchSpeedMBps));
            }
        }

        public double SearchSpeedMBps
        {
            get
            {
                if (ByteProvider == null || SearchDurationMs == 0) return 0;
                var fileSizeMB = ByteProvider.Length / (1024.0 * 1024.0);
                return fileSizeMB / (SearchDurationMs / 1000.0);
            }
        }

        #endregion

        #region Commands

        public ICommand SearchCommand { get; }
        public ICommand FindNextCommand { get; }
        public ICommand FindPreviousCommand { get; }
        public ICommand FindAllCommand { get; }
        public ICommand ClearSearchCommand { get; }

        #endregion

        #region Constructor

        public SearchCommandCenterViewModel()
        {
            SearchCommand = new RelayCommand(async () => await FindAllAsync(), () => CanSearch);
            FindNextCommand = new RelayCommand(FindNext, () => HasResults);
            FindPreviousCommand = new RelayCommand(FindPrevious, () => HasResults);
            FindAllCommand = new RelayCommand(async () => await FindAllAsync(), () => CanSearch);
            ClearSearchCommand = new RelayCommand(ClearSearch, () => HasSearchPattern);
        }

        #endregion

        #region Methods

        private async Task FindAllAsync()
        {
            if (!CanSearch) return;

            IsSearching = true;
            _searchResults.Clear();
            _currentMatchIndex = -1;
            OnPropertyChanged(nameof(ResultCount));
            OnPropertyChanged(nameof(HasResults));
            OnPropertyChanged(nameof(HasNoResults));

            try
            {
                _cancellationTokenSource?.Cancel();
                _cancellationTokenSource = new CancellationTokenSource();

                var stopwatch = Stopwatch.StartNew();

                var options = BuildSearchOptions();
                var result = await Task.Run(() => ByteProvider.Search(options, _cancellationTokenSource.Token));

                stopwatch.Stop();
                SearchDurationMs = stopwatch.Elapsed.TotalMilliseconds;

                if (result.Success && result.Matches.Count > 0)
                {
                    _searchResults = result.Matches.ToList();
                    _currentMatchIndex = 0;

                    // Navigate to first match
                    OnMatchFound?.Invoke(this, _searchResults[0]);
                }

                OnPropertyChanged(nameof(ResultCount));
                OnPropertyChanged(nameof(HasResults));
                OnPropertyChanged(nameof(HasNoResults));
            }
            catch (OperationCanceledException)
            {
                // Search cancelled - ignore
            }
            finally
            {
                IsSearching = false;
            }
        }

        private void FindNext()
        {
            if (!HasResults) return;

            _currentMatchIndex = (_currentMatchIndex + 1) % _searchResults.Count;
            OnMatchFound?.Invoke(this, _searchResults[_currentMatchIndex]);
        }

        private void FindPrevious()
        {
            if (!HasResults) return;

            _currentMatchIndex = (_currentMatchIndex - 1 + _searchResults.Count) % _searchResults.Count;
            OnMatchFound?.Invoke(this, _searchResults[_currentMatchIndex]);
        }

        private void ClearSearch()
        {
            SearchPattern = "";
            _searchResults.Clear();
            _currentMatchIndex = -1;
            SearchDurationMs = 0;

            OnPropertyChanged(nameof(ResultCount));
            OnPropertyChanged(nameof(HasResults));
            OnPropertyChanged(nameof(HasNoResults));
        }

        private SearchOptions BuildSearchOptions()
        {
            var options = new SearchOptions
            {
                StartPosition = 0,
                UseParallelSearch = true,
                MaxResults = 0 // Find all
            };

            switch (SelectedSearchMode)
            {
                case SearchMode.Text:
                    options.Pattern = Encoding.UTF8.GetBytes(SearchPattern);
                    break;

                case SearchMode.Hex:
                    options.Pattern = ParseHexPattern(SearchPattern);
                    break;

                case SearchMode.Regex:
                    // For regex, we need to search the entire file as text and convert matches
                    // This is a simplified implementation - full regex would require more work
                    options.Pattern = Encoding.UTF8.GetBytes(SearchPattern);
                    break;

                case SearchMode.Wildcard:
                    options.Pattern = ParseHexPattern(SearchPattern);
                    options.UseWildcard = true;
                    break;
            }

            return options;
        }

        private byte[] ParseHexPattern(string hexString)
        {
            // Remove spaces and validate
            hexString = Regex.Replace(hexString, @"\s+", "");

            // Support wildcard ?? notation
            var bytes = new List<byte>();
            for (int i = 0; i < hexString.Length; i += 2)
            {
                if (i + 1 < hexString.Length)
                {
                    var byteStr = hexString.Substring(i, 2);
                    if (byteStr == "??")
                    {
                        bytes.Add(0xFF); // Wildcard marker (will be handled by SearchEngine)
                    }
                    else
                    {
                        bytes.Add(Convert.ToByte(byteStr, 16));
                    }
                }
            }

            return bytes.ToArray();
        }


        #endregion
    }

    /// <summary>
    /// Search mode enumeration
    /// </summary>
    public enum SearchMode
    {
        Text,
        Hex,
        Regex,
        Wildcard
    }

    /// <summary>
    /// Simple RelayCommand implementation
    /// </summary>
    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool> _canExecute;

        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public RelayCommand(Action execute, Func<bool> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public bool CanExecute(object parameter) => _canExecute?.Invoke() ?? true;

        public void Execute(object parameter) => _execute();
    }
}
