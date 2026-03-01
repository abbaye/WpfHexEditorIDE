//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Win32;
using WpfHexEditor.HexEditor.Commands;
using WpfHexEditor.Core;
using WpfHexEditor.Core.Bytes;
using WpfHexEditor.Core.CharacterTable;
using WpfHexEditor.Core.Search.Models;
using WpfHexEditor.Core.Search.Services;

namespace WpfHexEditor.HexEditor.Search.ViewModels
{
    /// <summary>
    /// Advanced search ViewModel - always bound to parent HexEditor.
    /// Supports 5 modes: TEXT, HEX, WILDCARD, TBL TEXT, RELATIVE.
    /// </summary>
    public class AdvancedSearchViewModel : INotifyPropertyChanged, IDisposable
    {
        #region Fields

        // === BINDING SUR HEXEDITOR PARENT ===
        private ByteProvider _byteProvider;
        private TblStream _tblStream;
        private HexEditor _parentEditor;

        // Services
        private AdvancedSearchService _advancedService;
        private RelativeSearchEngine _relativeEngine;
        private CancellationTokenSource _cancellationTokenSource;

        // Input State
        private string _searchInput = string.Empty;
        private string _replaceInput = string.Empty;
        private SearchMode _selectedMode = SearchMode.Text;
        private Encoding _selectedEncoding = Encoding.UTF8;
        private bool _caseSensitive = true;
        private bool _useWildcard = false;
        private byte _wildcardByte = 0xFF;
        private bool _searchBackward = false;
        private bool _wrapAround = true;
        private bool _searchInSelectionOnly = false;
        private int _maxResults = 10000;
        private bool _highlightAllResults = true;
        private bool _realTimeSearch = false;
        private bool _matchLength = false;

        // TBL Support
        private bool _showTblContext = true;

        // Relative Search Support
        private int _relativeMinMatches = 1;
        private int _relativeMaxProposals = 20;
        private int _relativeSampleLength = 100;

        // Results State
        private object _selectedResult;
        private bool _isResultsTruncated = false;

        // Performance Metrics
        private double _searchDurationMs = 0;
        private double _searchSpeedMBps = 0;
        private long _bytesSearched = 0;

        // Progress
        private double _searchProgress = 0;
        private bool _isIndeterminate = true;
        private bool _isSearching = false;

        // Parent HexEditor State
        private long _selectionStart = 0;
        private long _selectionEnd = 0;
        private string _fileName = string.Empty;

        // Incremental navigation index (-1 = no selection yet)
        private int _currentResultIndex = -1;

        #endregion

        #region Properties

        /// <summary>
        /// Gets whether a TBL is loaded in the parent HexEditor.
        /// </summary>
        public bool IsTblLoaded => _tblStream != null && _tblStream.Length > 0;

        /// <summary>
        /// Gets TBL information string.
        /// </summary>
        public string TblInfo
        {
            get
            {
                if (!IsTblLoaded) return string.Empty;
                return $"{_tblStream.FileName} ({_tblStream.Length} entries)";
            }
        }

        /// <summary>
        /// Gets the file length from parent ByteProvider.
        /// </summary>
        public long FileLength => _byteProvider?.VirtualLength ?? 0;

        /// <summary>
        /// Gets the file name from parent HexEditor.
        /// </summary>
        public string FileName
        {
            get => _fileName;
            private set
            {
                if (_fileName != value)
                {
                    _fileName = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Gets or sets selection start (synced from parent).
        /// </summary>
        public long SelectionStart
        {
            get => _selectionStart;
            private set
            {
                if (_selectionStart != value)
                {
                    _selectionStart = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Gets or sets selection end (synced from parent).
        /// </summary>
        public long SelectionEnd
        {
            get => _selectionEnd;
            private set
            {
                if (_selectionEnd != value)
                {
                    _selectionEnd = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Gets or sets the search input string.
        /// </summary>
        public string SearchInput
        {
            get => _searchInput;
            set
            {
                if (_searchInput != value)
                {
                    _searchInput = value;
                    OnPropertyChanged();
                    UpdateCommandStates();
                }
            }
        }

        /// <summary>
        /// Gets or sets the replace input string.
        /// </summary>
        public string ReplaceInput
        {
            get => _replaceInput;
            set
            {
                if (_replaceInput != value)
                {
                    _replaceInput = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Gets or sets the selected search mode.
        /// </summary>
        public SearchMode SelectedMode
        {
            get => _selectedMode;
            set
            {
                if (_selectedMode != value)
                {
                    _selectedMode = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsRelativeMode));
                    OnPropertyChanged(nameof(ShowReplaceControls));
                    UpdateCommandStates();
                }
            }
        }

        /// <summary>
        /// Gets whether currently in RELATIVE mode.
        /// </summary>
        public bool IsRelativeMode => _selectedMode == SearchMode.Relative;

        /// <summary>
        /// Gets whether to show replace controls (hidden in RELATIVE mode).
        /// </summary>
        public bool ShowReplaceControls => _selectedMode != SearchMode.Relative;

        /// <summary>
        /// Gets or sets the selected encoding.
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
        /// Gets available encodings.
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

        public byte WildcardByte
        {
            get => _wildcardByte;
            set
            {
                if (_wildcardByte != value)
                {
                    _wildcardByte = value;
                    OnPropertyChanged();
                }
            }
        }

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

        public bool SearchInSelectionOnly
        {
            get => _searchInSelectionOnly;
            set
            {
                if (_searchInSelectionOnly != value)
                {
                    _searchInSelectionOnly = value;
                    OnPropertyChanged();
                }
            }
        }

        public int MaxResults
        {
            get => _maxResults;
            set
            {
                if (_maxResults != value)
                {
                    _maxResults = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool HighlightAllResults
        {
            get => _highlightAllResults;
            set
            {
                if (_highlightAllResults != value)
                {
                    _highlightAllResults = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool RealTimeSearch
        {
            get => _realTimeSearch;
            set
            {
                if (_realTimeSearch != value)
                {
                    _realTimeSearch = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool MatchLength
        {
            get => _matchLength;
            set
            {
                if (_matchLength != value)
                {
                    _matchLength = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool ShowTblContext
        {
            get => _showTblContext;
            set
            {
                if (_showTblContext != value)
                {
                    _showTblContext = value;
                    OnPropertyChanged();
                }
            }
        }

        public int RelativeMinMatches
        {
            get => _relativeMinMatches;
            set
            {
                if (_relativeMinMatches != value)
                {
                    _relativeMinMatches = value;
                    OnPropertyChanged();
                }
            }
        }

        public int RelativeMaxProposals
        {
            get => _relativeMaxProposals;
            set
            {
                if (_relativeMaxProposals != value)
                {
                    _relativeMaxProposals = value;
                    OnPropertyChanged();
                }
            }
        }

        public int RelativeSampleLength
        {
            get => _relativeSampleLength;
            set
            {
                if (_relativeSampleLength != value)
                {
                    _relativeSampleLength = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Standard search results.
        /// </summary>
        public ObservableCollection<SearchResultItemViewModel> StandardResults { get; } = new ObservableCollection<SearchResultItemViewModel>();

        /// <summary>
        /// Relative search encoding proposals.
        /// </summary>
        public ObservableCollection<EncodingProposal> RelativeResults { get; } = new ObservableCollection<EncodingProposal>();

        /// <summary>
        /// Gets or sets selected result (polymorphic: SearchResultItemViewModel or EncodingProposal).
        /// </summary>
        public object SelectedResult
        {
            get => _selectedResult;
            set
            {
                if (_selectedResult != value)
                {
                    _selectedResult = value;
                    OnPropertyChanged();
                    UpdateCommandStates();

                    // Trigger navigation
                    if (value is SearchResultItemViewModel stdResult)
                    {
                        ResultNavigationRequested?.Invoke(this, stdResult);
                    }
                }
            }
        }

        public int TotalResultCount => IsRelativeMode ? RelativeResults.Count : StandardResults.Count;

        public bool IsResultsTruncated
        {
            get => _isResultsTruncated;
            private set
            {
                if (_isResultsTruncated != value)
                {
                    _isResultsTruncated = value;
                    OnPropertyChanged();
                }
            }
        }

        public double SearchDurationMs
        {
            get => _searchDurationMs;
            private set
            {
                if (Math.Abs(_searchDurationMs - value) > 0.01)
                {
                    _searchDurationMs = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(PerformanceLabel));
                }
            }
        }

        public double SearchSpeedMBps
        {
            get => _searchSpeedMBps;
            private set
            {
                if (Math.Abs(_searchSpeedMBps - value) > 0.01)
                {
                    _searchSpeedMBps = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(PerformanceLabel));
                }
            }
        }

        public long BytesSearched
        {
            get => _bytesSearched;
            private set
            {
                if (_bytesSearched != value)
                {
                    _bytesSearched = value;
                    OnPropertyChanged();
                }
            }
        }

        public string PerformanceLabel
        {
            get
            {
                if (TotalResultCount == 0)
                    return "No matches";

                if (SearchDurationMs > 0)
                    return $"{TotalResultCount:N0} matches in {SearchDurationMs:F0}ms at {SearchSpeedMBps:F2} MB/s";

                return $"{TotalResultCount:N0} matches";
            }
        }

        public double SearchProgress
        {
            get => _searchProgress;
            private set
            {
                if (Math.Abs(_searchProgress - value) > 0.01)
                {
                    _searchProgress = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsIndeterminate
        {
            get => _isIndeterminate;
            private set
            {
                if (_isIndeterminate != value)
                {
                    _isIndeterminate = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsSearching
        {
            get => _isSearching;
            private set
            {
                if (_isSearching != value)
                {
                    _isSearching = value;
                    OnPropertyChanged();
                    UpdateCommandStates();
                }
            }
        }

        /// <summary>
        /// Search history (MRU).
        /// </summary>
        public ObservableCollection<SearchHistoryEntry> SearchHistory { get; } = new ObservableCollection<SearchHistoryEntry>();

        #endregion

        #region Commands

        public ICommand FindAllCommand { get; }
        public ICommand FindNextCommand { get; }
        public ICommand FindPreviousCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand ClearResultsCommand { get; }
        public ICommand NavigateToResultCommand { get; }
        public ICommand ReplaceSelectedCommand { get; }
        public ICommand ReplaceAllCommand { get; }
        public ICommand ToggleHighlightAllCommand { get; }
        public ICommand ExportResultsCommand { get; }
        public ICommand ClearHistoryCommand { get; }
        public ICommand ExportProposalToTblCommand { get; }
        public ICommand ApplyEncodingCommand { get; }
        public ICommand LoadTblCommand { get; }
        public ICommand CloseTblCommand { get; }
        public ICommand CloseCommand { get; }

        #endregion

        #region Events

        public event EventHandler<SearchResultItemViewModel> ResultNavigationRequested;
        public event EventHandler<IEnumerable<SearchMatch>> HighlightResultsRequested;
        public event EventHandler HighlightClearRequested;
        public event EventHandler<TblStream> TblLoadRequested;
        public event EventHandler TblCloseRequested;

        #endregion

        #region Constructor

        public AdvancedSearchViewModel()
        {
            // Initialize commands
            FindAllCommand = new RelayCommand(async () => await FindAllAsync(), () => CanSearch());
            FindNextCommand = new RelayCommand(FindNext, () => CanNavigate());
            FindPreviousCommand = new RelayCommand(FindPrevious, () => CanNavigate());
            CancelCommand = new RelayCommand(Cancel, () => IsSearching);
            ClearResultsCommand = new RelayCommand(ClearResults, () => TotalResultCount > 0);
            NavigateToResultCommand = new RelayCommand<SearchResultItemViewModel>(NavigateToResult, r => r != null);
            ReplaceSelectedCommand = new RelayCommand(ReplaceSelected, () => CanReplace());
            ReplaceAllCommand = new RelayCommand(async () => await ReplaceAllAsync(), () => CanReplace());
            ToggleHighlightAllCommand = new RelayCommand(ToggleHighlightAll);
            ExportResultsCommand = new RelayCommand(async () => await ExportResultsAsync(), () => TotalResultCount > 0);
            ClearHistoryCommand = new RelayCommand(ClearHistory, () => SearchHistory.Count > 0);
            ExportProposalToTblCommand = new RelayCommand<EncodingProposal>(ExportProposalToTbl, p => p != null);
            ApplyEncodingCommand = new RelayCommand<EncodingProposal>(ApplyEncoding, p => p != null);
            LoadTblCommand = new RelayCommand(LoadTbl);
            CloseTblCommand = new RelayCommand(CloseTbl, () => IsTblLoaded);
            CloseCommand = new RelayCommand(Close);
        }

        #endregion

        #region Initialization (CRITICAL: Called by HexEditor parent)

        /// <summary>
        /// Binds this ViewModel to a parent HexEditor.
        /// MUST be called before any operations!
        /// </summary>
        public void BindToHexEditor(HexEditor editor)
        {
            if (editor == null)
                throw new ArgumentNullException(nameof(editor));

            _parentEditor = editor;
            _byteProvider = editor.GetByteProvider();
            _tblStream = editor.TBL;
            FileName = editor.FileName ?? string.Empty;

            // Initialize services
            _advancedService = new AdvancedSearchService(_byteProvider);
            _relativeEngine = new RelativeSearchEngine(_byteProvider, _tblStream);

            // Subscribe to parent changes
            editor.SelectionChanged += OnParentSelectionChanged;

            // Update initial state
            UpdateFromParent();
            LoadSearchHistory();

            // Force command state update after binding
            UpdateCommandStates();

            OnPropertyChanged(nameof(IsTblLoaded));
            OnPropertyChanged(nameof(TblInfo));
            OnPropertyChanged(nameof(FileLength));
            OnPropertyChanged(nameof(FileName));
        }

        private void OnParentSelectionChanged(object sender, EventArgs e)
        {
            if (_parentEditor != null)
            {
                SelectionStart = _parentEditor.SelectionStart;
                SelectionEnd = _parentEditor.SelectionStop;
            }
        }

        private void UpdateFromParent()
        {
            if (_parentEditor != null)
            {
                SelectionStart = _parentEditor.SelectionStart;
                SelectionEnd = _parentEditor.SelectionStop;
            }
        }

        private void LoadSearchHistory()
        {
            SearchHistory.Clear();
            var history = _advancedService.GetHistory();
            foreach (var entry in history)
            {
                SearchHistory.Add(entry);
            }
        }

        #endregion

        #region Command Implementations

        private bool CanSearch()
        {
            // ByteProvider check is done before dialog opens, so we only check SearchInput here
            return !IsSearching && !string.IsNullOrEmpty(SearchInput);
        }

        private bool CanNavigate()
        {
            return !IsSearching && StandardResults.Count > 0;
        }

        private bool CanReplace()
        {
            return !IsSearching && StandardResults.Count > 0 && !IsRelativeMode;
        }

        private async Task FindAllAsync()
        {
            if (!CanSearch()) return;

            IsSearching = true;
            IsIndeterminate = true;
            SearchProgress = 0;

            ClearResults();

            _cancellationTokenSource = new CancellationTokenSource();

            try
            {
                if (IsRelativeMode)
                {
                    // RELATIVE SEARCH
                    await PerformRelativeSearchAsync();
                }
                else
                {
                    // STANDARD SEARCH (TEXT, HEX, WILDCARD, TBL TEXT)
                    await PerformStandardSearchAsync();
                }

                // Record in history
                RecordCurrentSearch();
            }
            catch (OperationCanceledException)
            {
                // User cancelled
            }
            catch (Exception ex)
            {
                // Error handling
                System.Windows.MessageBox.Show($"Search error: {ex.Message}", "Error",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
            finally
            {
                IsSearching = false;
                IsIndeterminate = false;
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
            }
        }

        private async Task PerformStandardSearchAsync()
        {
            SearchResult result;

            if (SelectedMode == SearchMode.TblText)
            {
                // TBL TEXT mode
                if (!IsTblLoaded)
                {
                    System.Windows.MessageBox.Show("No TBL loaded. Cannot search TBL text.", "TBL Required",
                        System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                    return;
                }

                var baseOptions = BuildSearchOptions();
                result = await _advancedService.SearchTblTextAsync(SearchInput, _tblStream, baseOptions, _cancellationTokenSource.Token);
            }
            else
            {
                // TEXT, HEX, WILDCARD modes
                var options = BuildSearchOptions();
                result = await Task.Run(() => new SearchEngine(_byteProvider).Search(options, _cancellationTokenSource.Token));
            }

            // Populate results
            if (result.Success && result.Matches != null)
            {
                int index = 1;
                foreach (var match in result.Matches.Take(MaxResults))
                {
                    var itemVm = new SearchResultItemViewModel(
                        match.Position,
                        match.Length,
                        match.MatchedBytes,
                        match.ContextBefore,
                        match.ContextAfter,
                        index++);

                    StandardResults.Add(itemVm);
                }

                IsResultsTruncated = result.Matches.Count > MaxResults;
            }

            // Update metrics
            SearchDurationMs = result.DurationMs;
            SearchSpeedMBps = result.SpeedMBps;
            BytesSearched = result.BytesSearched;

            OnPropertyChanged(nameof(TotalResultCount));
            OnPropertyChanged(nameof(PerformanceLabel));

            // Highlight if enabled
            if (HighlightAllResults && result.Success)
            {
                HighlightResultsRequested?.Invoke(this, result.Matches);
            }
        }

        private async Task PerformRelativeSearchAsync()
        {
            var options = new RelativeSearchOptions
            {
                SearchText = SearchInput,
                MinMatchesRequired = RelativeMinMatches,
                MaxProposals = RelativeMaxProposals,
                CaseSensitive = CaseSensitive,
                SampleLength = RelativeSampleLength,
                UseParallelSearch = true
            };

            var result = await Task.Run(() => _relativeEngine.Search(options, _cancellationTokenSource.Token));

            if (result.Success && result.Proposals != null)
            {
                foreach (var proposal in result.Proposals.Take(RelativeMaxProposals))
                {
                    RelativeResults.Add(proposal);
                }
            }

            SearchDurationMs = result.DurationMs;
            SearchSpeedMBps = result.SpeedMBps;

            OnPropertyChanged(nameof(TotalResultCount));
            OnPropertyChanged(nameof(PerformanceLabel));
        }

        private SearchOptions BuildSearchOptions()
        {
            byte[] pattern = ConvertInputToPattern();

            var options = new SearchOptions
            {
                Pattern = pattern,
                StartPosition = SearchInSelectionOnly ? SelectionStart : 0,
                EndPosition = SearchInSelectionOnly ? SelectionEnd : -1,
                SearchBackward = SearchBackward,
                UseWildcard = UseWildcard,
                WildcardByte = WildcardByte,
                MaxResults = MaxResults,
                WrapAround = WrapAround,
                ContextRadius = 8
            };

            return options;
        }

        private byte[] ConvertInputToPattern()
        {
            switch (SelectedMode)
            {
                case SearchMode.Text:
                    var bytes = SelectedEncoding.GetBytes(SearchInput);
                    if (!CaseSensitive)
                    {
                        // Convert to uppercase for case-insensitive search
                        var upperText = SearchInput.ToUpperInvariant();
                        bytes = SelectedEncoding.GetBytes(upperText);
                    }
                    return bytes;

                case SearchMode.Hex:
                case SearchMode.Wildcard:
                    return ByteConverters.HexToByte(SearchInput);

                case SearchMode.TblText:
                    // Handled separately in PerformStandardSearchAsync
                    return Array.Empty<byte>();

                default:
                    return Array.Empty<byte>();
            }
        }

        private void FindNext()
        {
            if (StandardResults.Count == 0) return;
            _currentResultIndex = (_currentResultIndex + 1) % StandardResults.Count;
            var result = StandardResults[_currentResultIndex];
            _selectedResult = result;         // bypass setter to avoid double-fire
            OnPropertyChanged(nameof(SelectedResult));
            NavigateToResult(result);
            UpdateCommandStates();
        }

        private void FindPrevious()
        {
            if (StandardResults.Count == 0) return;
            _currentResultIndex = (_currentResultIndex - 1 + StandardResults.Count) % StandardResults.Count;
            var result = StandardResults[_currentResultIndex];
            _selectedResult = result;         // bypass setter to avoid double-fire
            OnPropertyChanged(nameof(SelectedResult));
            NavigateToResult(result);
            UpdateCommandStates();
        }

        private void Cancel()
        {
            _cancellationTokenSource?.Cancel();
        }

        private void ClearResults()
        {
            StandardResults.Clear();
            RelativeResults.Clear();
            IsResultsTruncated = false;
            SearchDurationMs = 0;
            SearchSpeedMBps = 0;
            BytesSearched = 0;
            _currentResultIndex = -1;

            OnPropertyChanged(nameof(TotalResultCount));
            OnPropertyChanged(nameof(PerformanceLabel));

            HighlightClearRequested?.Invoke(this, EventArgs.Empty);
        }

        private void NavigateToResult(SearchResultItemViewModel result)
        {
            if (result != null)
            {
                ResultNavigationRequested?.Invoke(this, result);
            }
        }

        /// <summary>
        /// Converts the Replace input string to raw bytes using the current mode/encoding.
        /// Returns null and shows an error message on failure.
        /// </summary>
        private byte[] ConvertReplaceInputToBytes()
        {
            try
            {
                switch (SelectedMode)
                {
                    case SearchMode.Text:
                        return SelectedEncoding.GetBytes(ReplaceInput);

                    case SearchMode.Hex:
                    case SearchMode.Wildcard:
                        return ByteConverters.HexToByte(ReplaceInput);

                    case SearchMode.TblText:
                        // TBL replacement: encode text via TblStream character table
                        if (!IsTblLoaded || _tblStream == null)
                        {
                            System.Windows.MessageBox.Show(
                                "No TBL loaded. Cannot encode replacement bytes in TBL TEXT mode.",
                                "TBL Required",
                                System.Windows.MessageBoxButton.OK,
                                System.Windows.MessageBoxImage.Warning);
                            return null;
                        }
                        return SelectedEncoding.GetBytes(ReplaceInput);

                    default:
                        return Array.Empty<byte>();
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Invalid replacement value: {ex.Message}",
                    "Replace Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
                return null;
            }
        }

        private void ReplaceSelected()
        {
            if (!(SelectedResult is SearchResultItemViewModel result)) return;
            if (_byteProvider == null) return;

            var replaceBytes = ConvertReplaceInputToBytes();
            if (replaceBytes == null) return;

            try
            {
                // Remove the found bytes, then insert the replacement at the same position
                _byteProvider.DeleteBytes(result.Position, result.Length);
                if (replaceBytes.Length > 0)
                    _byteProvider.InsertBytes(result.Position, replaceBytes);

                // Remove this result and advance the index
                StandardResults.Remove(result);
                _currentResultIndex = Math.Min(_currentResultIndex, StandardResults.Count - 1);

                OnPropertyChanged(nameof(TotalResultCount));
                OnPropertyChanged(nameof(PerformanceLabel));
                UpdateCommandStates();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Replace error: {ex.Message}", "Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        private async Task ReplaceAllAsync()
        {
            if (StandardResults.Count == 0) return;
            if (_byteProvider == null) return;

            var replaceBytes = ConvertReplaceInputToBytes();
            if (replaceBytes == null) return;

            var count = StandardResults.Count;
            var confirm = System.Windows.MessageBox.Show(
                $"Replace all {count:N0} occurrence(s)?",
                "Replace All",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Question);

            if (confirm != System.Windows.MessageBoxResult.Yes) return;

            IsSearching = true;
            UpdateCommandStates();

            try
            {
                // Yield once so the UI refreshes the busy state
                await Task.Yield();

                // Process in REVERSE position order — replacing in forward order would shift
                // all subsequent offsets when the replacement length differs from the match length.
                var sortedResults = StandardResults.OrderByDescending(r => r.Position).ToList();

                foreach (var r in sortedResults)
                {
                    _byteProvider.DeleteBytes(r.Position, r.Length);
                    if (replaceBytes.Length > 0)
                        _byteProvider.InsertBytes(r.Position, replaceBytes);
                }

                ClearResults();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Replace All error: {ex.Message}", "Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
            finally
            {
                IsSearching = false;
                UpdateCommandStates();
            }
        }

        private void ToggleHighlightAll()
        {
            HighlightAllResults = !HighlightAllResults;

            if (HighlightAllResults && StandardResults.Count > 0)
            {
                var matches = StandardResults.Select(r => new SearchMatch
                {
                    Position = r.Position,
                    Length = r.Length,
                    MatchedBytes = r.MatchedBytes
                }).ToList();

                HighlightResultsRequested?.Invoke(this, matches);
            }
            else
            {
                HighlightClearRequested?.Invoke(this, EventArgs.Empty);
            }
        }

        private async Task ExportResultsAsync()
        {
            var saveDialog = new SaveFileDialog
            {
                Filter = "CSV Files (*.csv)|*.csv|Text Files (*.txt)|*.txt|JSON Files (*.json)|*.json",
                DefaultExt = "csv",
                FileName = $"search_results_{DateTime.Now:yyyyMMdd_HHmmss}"
            };

            if (saveDialog.ShowDialog() == true)
            {
                var format = saveDialog.FilterIndex switch
                {
                    1 => AdvancedSearchService.ExportFormat.Csv,
                    2 => AdvancedSearchService.ExportFormat.PlainText,
                    3 => AdvancedSearchService.ExportFormat.Json,
                    _ => AdvancedSearchService.ExportFormat.Csv
                };

                var matches = StandardResults.Select(r => new SearchMatch
                {
                    Position = r.Position,
                    Length = r.Length,
                    MatchedBytes = r.MatchedBytes,
                    ContextBefore = r.ContextBefore,
                    ContextAfter = r.ContextAfter
                }).ToList();

                await _advancedService.ExportResultsAsync(matches, saveDialog.FileName, format);
            }
        }

        private void ClearHistory()
        {
            _advancedService.ClearHistory();
            SearchHistory.Clear();
        }

        private void ExportProposalToTbl(EncodingProposal proposal)
        {
            if (proposal == null) return;

            var newTbl = _relativeEngine.ExportToTbl(proposal);

            var saveDialog = new SaveFileDialog
            {
                Title   = "Export TBL File",
                Filter  = "TBL Files (*.tbl)|*.tbl|All Files (*.*)|*.*",
                DefaultExt = "tbl",
                FileName = $"encoding_offset{proposal.Offset}.tbl"
            };

            if (saveDialog.ShowDialog() != true) return;

            try
            {
                newTbl.SaveAs(saveDialog.FileName);
                System.Windows.MessageBox.Show(
                    $"TBL exported successfully!\n{saveDialog.FileName}\nEntries: {newTbl.Length}",
                    "Export Successful",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to export TBL:\n{ex.Message}",
                    "Export Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        private void ApplyEncoding(EncodingProposal proposal)
        {
            if (proposal == null) return;

            var newTbl = _relativeEngine.ExportToTbl(proposal);
            TblLoadRequested?.Invoke(this, newTbl);

            System.Windows.MessageBox.Show($"Encoding applied!\nOffset: {proposal.Offset}\nYou can now use TBL TEXT mode.",
                "Encoding Applied", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
        }

        private void LoadTbl()
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Select a TBL (Character Table) file",
                Filter = "TBL Files (*.tbl)|*.tbl|All Files (*.*)|*.*",
                CheckFileExists = true,
                Multiselect = false
            };

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    var tbl = new Core.CharacterTable.TblStream(openFileDialog.FileName);
                    TblLoadRequested?.Invoke(this, tbl);

                    // Update TBL reference
                    _tblStream = tbl;
                    OnPropertyChanged(nameof(IsTblLoaded));
                    OnPropertyChanged(nameof(TblInfo));

                    System.Windows.MessageBox.Show(
                        $"TBL loaded successfully!\n\n{tbl.FileName}\nEntries: {tbl.Length}\n\nYou can now use TBL TEXT mode.",
                        "TBL Loaded",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show(
                        $"Failed to load TBL file:\n{ex.Message}",
                        "Error Loading TBL",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Error);
                }
            }
        }

        private void CloseTbl()
        {
            if (!IsTblLoaded) return;

            var result = System.Windows.MessageBox.Show(
                "Close the current TBL file?\n\nTBL TEXT mode will be disabled.",
                "Close TBL",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Question);

            if (result == System.Windows.MessageBoxResult.Yes)
            {
                TblCloseRequested?.Invoke(this, EventArgs.Empty);

                _tblStream = null;
                OnPropertyChanged(nameof(IsTblLoaded));
                OnPropertyChanged(nameof(TblInfo));
            }
        }

        private void Close()
        {
            // Close dialog - handled by View
            System.Windows.Application.Current.Windows
                .OfType<Search.Views.AdvancedSearchDialog>()
                .FirstOrDefault(w => w.DataContext == this)?.Close();
        }

        private void RecordCurrentSearch()
        {
            var entry = new SearchHistoryEntry
            {
                Pattern = SearchInput,
                Mode = SelectedMode,
                Encoding = SelectedEncoding,
                CaseSensitive = CaseSensitive,
                UseWildcard = UseWildcard
            };

            _advancedService.RecordSearch(entry);
            LoadSearchHistory();
        }

        private void UpdateCommandStates()
        {
            CommandManager.InvalidateRequerySuggested();
        }

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            // Unsubscribe events
            if (_parentEditor != null)
            {
                _parentEditor.SelectionChanged -= OnParentSelectionChanged;
            }

            // Cancel any ongoing search
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();

            // Clear references
            _byteProvider = null;
            _tblStream = null;
            _parentEditor = null;
            _advancedService = null;
            _relativeEngine = null;

            // Clear collections
            StandardResults?.Clear();
            RelativeResults?.Clear();
            SearchHistory?.Clear();
        }

        #endregion
    }
}
