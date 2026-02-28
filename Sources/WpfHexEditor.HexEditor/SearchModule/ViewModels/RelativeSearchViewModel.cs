/*
    Apache 2.0  2026
    Author : Derek Tremblay (derektremblay666@gmail.com)
    Contributors: Claude Sonnet 4.5
*/

using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Win32;
using WpfHexEditor.Core;
using WpfHexEditor.Core.Bytes;
using WpfHexEditor.Core.CharacterTable;
using WpfHexEditor.Core.Search.Models;
using WpfHexEditor.Core.Search.Services;

namespace WpfHexEditor.HexEditor.Search.ViewModels
{
    /// <summary>
    /// ViewModel for Relative Search dialog.
    /// Implements MVVM pattern with data binding and commands.
    /// </summary>
    public class RelativeSearchViewModel : INotifyPropertyChanged
    {
        private ByteProvider _byteProvider;
        private TblStream _currentTbl;
        private RelativeSearchEngine _engine;
        private CancellationTokenSource _cancellationTokenSource;

        private string _searchText;
        private bool _isSearching;
        private string _statusMessage;
        private EncodingProposal _selectedProposal;
        private bool _useParallelSearch = true;
        private int _minMatches = 1;
        private int _maxProposals = 20;

        public RelativeSearchViewModel()
        {
            Proposals = new ObservableCollection<EncodingProposal>();

            // Initialize commands
            SearchCommand = new RelayCommand(async () => await SearchAsync(), () => CanSearch());
            CancelSearchCommand = new RelayCommand(CancelSearch, () => IsSearching);
            ExportToTblCommand = new RelayCommand(ExportSelectedToTbl, () => SelectedProposal != null && !IsSearching);
            NavigateToMatchCommand = new RelayCommand(NavigateToFirstMatch, () => SelectedProposal != null && SelectedProposal.MatchPositions.Count > 0);
            ClearResultsCommand = new RelayCommand(ClearResults, () => Proposals.Count > 0 && !IsSearching);
        }

        #region Properties

        /// <summary>
        /// Gets or sets the byte provider to search in.
        /// </summary>
        public ByteProvider ByteProvider
        {
            get => _byteProvider;
            set
            {
                _byteProvider = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Gets or sets the currently loaded TBL (optional, for validation scoring).
        /// </summary>
        public TblStream CurrentTbl
        {
            get => _currentTbl;
            set
            {
                _currentTbl = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasTblLoaded));
                OnPropertyChanged(nameof(TblStatusMessage));
            }
        }

        /// <summary>
        /// Gets whether a TBL is loaded.
        /// </summary>
        public bool HasTblLoaded => _currentTbl != null && _currentTbl.Length > 0;

        /// <summary>
        /// Gets the TBL status message (LOCALIZED).
        /// </summary>
        public string TblStatusMessage
        {
            get
            {
                if (HasTblLoaded)
                    return string.Format(WpfHexEditor.Core.Properties.Resources.RelativeSearchTblLoadedString, _currentTbl.Length);
                else
                    return WpfHexEditor.Core.Properties.Resources.RelativeSearchNoTblString;
            }
        }

        /// <summary>
        /// Gets or sets the search text (known text from ROM).
        /// </summary>
        public string SearchText
        {
            get => _searchText;
            set
            {
                _searchText = value;
                OnPropertyChanged();
                (SearchCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }

        /// <summary>
        /// Gets or sets whether a search is currently running.
        /// </summary>
        public bool IsSearching
        {
            get => _isSearching;
            set
            {
                _isSearching = value;
                OnPropertyChanged();
                (SearchCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (CancelSearchCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (ExportToTblCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (NavigateToMatchCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (ClearResultsCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }

        /// <summary>
        /// Gets or sets the status message (LOCALIZED in code).
        /// </summary>
        public string StatusMessage
        {
            get => _statusMessage;
            set
            {
                _statusMessage = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Gets or sets the selected encoding proposal.
        /// </summary>
        public EncodingProposal SelectedProposal
        {
            get => _selectedProposal;
            set
            {
                _selectedProposal = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(PreviewText));
                (ExportToTblCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (NavigateToMatchCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }

        /// <summary>
        /// Gets the preview text for the selected proposal.
        /// </summary>
        public string PreviewText => SelectedProposal?.PreviewText ?? string.Empty;

        /// <summary>
        /// Gets or sets whether to use parallel search.
        /// </summary>
        public bool UseParallelSearch
        {
            get => _useParallelSearch;
            set
            {
                _useParallelSearch = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Gets or sets the minimum number of matches required.
        /// </summary>
        public int MinMatches
        {
            get => _minMatches;
            set
            {
                _minMatches = Math.Max(1, value);
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Gets or sets the maximum number of proposals to show.
        /// </summary>
        public int MaxProposals
        {
            get => _maxProposals;
            set
            {
                _maxProposals = Math.Max(1, Math.Min(100, value));
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Gets the collection of encoding proposals.
        /// </summary>
        public ObservableCollection<EncodingProposal> Proposals { get; }

        #endregion

        #region Commands

        /// <summary>
        /// Command to start search.
        /// </summary>
        public ICommand SearchCommand { get; }

        /// <summary>
        /// Command to cancel search.
        /// </summary>
        public ICommand CancelSearchCommand { get; }

        /// <summary>
        /// Command to export selected proposal to TBL file.
        /// </summary>
        public ICommand ExportToTblCommand { get; }

        /// <summary>
        /// Command to navigate to first match position.
        /// </summary>
        public ICommand NavigateToMatchCommand { get; }

        /// <summary>
        /// Command to clear search results.
        /// </summary>
        public ICommand ClearResultsCommand { get; }

        #endregion

        #region Command Implementations

        private bool CanSearch()
        {
            return !string.IsNullOrWhiteSpace(SearchText) &&
                   SearchText.Length >= 2 &&
                   _byteProvider != null &&
                   _byteProvider.IsOpen &&
                   !IsSearching;
        }

        /// <summary>
        /// Performs the search asynchronously (LOCALIZED status messages).
        /// </summary>
        public async Task SearchAsync()
        {
            IsSearching = true;
            Proposals.Clear();

            // Create new cancellation token
            _cancellationTokenSource = new CancellationTokenSource();

            // Show different message based on TBL status (LOCALIZED)
            if (HasTblLoaded)
                StatusMessage = string.Format(WpfHexEditor.Core.Properties.Resources.RelativeSearchSearchingWithTblString, _currentTbl.Length);
            else
                StatusMessage = WpfHexEditor.Core.Properties.Resources.RelativeSearchSearchingString;

            var options = new RelativeSearchOptions
            {
                SearchText = SearchText,
                StartPosition = 0,
                EndPosition = -1,
                MinMatchesRequired = MinMatches,
                MaxProposals = MaxProposals,
                UseParallelSearch = UseParallelSearch,
                CaseSensitive = false
            };

            // Create engine with current TBL for validation scoring
            _engine = new RelativeSearchEngine(_byteProvider, _currentTbl);

            RelativeSearchResult result = null;

            try
            {
                result = await Task.Run(() => _engine.Search(options, _cancellationTokenSource.Token));
            }
            catch (OperationCanceledException)
            {
                StatusMessage = WpfHexEditor.Core.Properties.Resources.RelativeSearchCancelledString;
                IsSearching = false;
                return;
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
                IsSearching = false;
                return;
            }

            // Process results (LOCALIZED)
            if (result.Success && result.Proposals.Count > 0)
            {
                foreach (var proposal in result.Proposals)
                {
                    Proposals.Add(proposal);
                }

                SelectedProposal = Proposals[0];

                // Show TBL validation in status if applicable (LOCALIZED)
                if (HasTblLoaded && Proposals[0].Score > 80)
                {
                    StatusMessage = string.Format(
                        WpfHexEditor.Core.Properties.Resources.RelativeSearchFoundWithValidationString,
                        result.Count,
                        result.DurationMs);
                }
                else
                {
                    StatusMessage = string.Format(
                        WpfHexEditor.Core.Properties.Resources.RelativeSearchFoundProposalsString,
                        result.Count,
                        result.DurationMs);
                }
            }
            else if (result.WasCancelled)
            {
                StatusMessage = WpfHexEditor.Core.Properties.Resources.RelativeSearchCancelledString;
            }
            else
            {
                StatusMessage = WpfHexEditor.Core.Properties.Resources.RelativeSearchNoProposalsString;
            }

            IsSearching = false;
        }

        private void CancelSearch()
        {
            _cancellationTokenSource?.Cancel();
        }

        private void ExportSelectedToTbl()
        {
            if (SelectedProposal == null || _engine == null)
                return;

            var dialog = new SaveFileDialog
            {
                Filter = "TBL Files (*.tbl)|*.tbl|All Files (*.*)|*.*",
                DefaultExt = ".tbl",
                FileName = $"discovered_encoding_offset_{SelectedProposal.Offset}.tbl"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var tbl = _engine.ExportToTbl(SelectedProposal);
                    // TODO: TblStream.FileName has an internal setter; need InternalsVisibleTo or a Save(string) overload
                    // tbl.FileName = dialog.FileName;
                    // tbl.Save();
                    // Workaround: write TBL content manually
                    var sb = new System.Text.StringBuilder();
                    foreach (var entry in tbl.GetAllEntries())
                    {
                        var value = entry.Value.Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");
                        sb.AppendLine($"{entry.Entry}={value}");
                    }
                    System.IO.File.WriteAllText(dialog.FileName, sb.ToString(), System.Text.Encoding.UTF8);

                    StatusMessage = $"TBL exported to: {dialog.FileName}";
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Export failed: {ex.Message}";
                }
            }
        }

        private void NavigateToFirstMatch()
        {
            if (SelectedProposal != null && SelectedProposal.MatchPositions.Count > 0)
            {
                long position = SelectedProposal.MatchPositions[0];
                OnNavigateToPosition?.Invoke(this, position);
            }
        }

        private void ClearResults()
        {
            Proposals.Clear();
            SelectedProposal = null;
            StatusMessage = string.Empty;
        }

        #endregion

        #region Events

        /// <summary>
        /// Event raised when navigation to a position is requested.
        /// </summary>
        public event EventHandler<long> OnNavigateToPosition;

        /// <summary>
        /// Property changed event for INotifyPropertyChanged.
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }
}
