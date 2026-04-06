////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace WpfHexEditor.Editor.Core.ViewModels
{
    /// <summary>
    /// Thin ViewModel that drives <see cref="WpfHexEditor.Editor.Core.Views.QuickSearchBar"/>.
    /// All actual search logic is delegated to the bound <see cref="ISearchTarget"/>;
    /// this class only holds UI-facing state (text, toggles, match counter text, commands).
    /// </summary>
    public sealed class SearchBarViewModel : INotifyPropertyChanged
    {
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));

        #region Fields

        private ISearchTarget? _target;
        private string _searchText  = string.Empty;
        private string _replaceText = string.Empty;
        private bool   _caseSensitive;
        private bool   _useWildcard;
        private bool   _isHexMode;

        #endregion

        #region Properties

        /// <summary>Search query â€” changes trigger a new <see cref="ISearchTarget.Find"/> call.</summary>
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (_searchText == value) return;
                _searchText = value;
                OnPropertyChanged();
                TriggerFind();
                UpdateCommandStates();
            }
        }

        /// <summary>Replacement text (forwarded to <see cref="ISearchTarget.Replace"/>).</summary>
        public string ReplaceText
        {
            get => _replaceText;
            set
            {
                if (_replaceText == value) return;
                _replaceText = value;
                OnPropertyChanged();
            }
        }

        /// <summary>Toggles case-sensitive matching and re-issues the current search.</summary>
        public bool CaseSensitive
        {
            get => _caseSensitive;
            set
            {
                if (_caseSensitive == value) return;
                _caseSensitive = value;
                OnPropertyChanged();
                TriggerFind();
            }
        }

        /// <summary>Toggles wildcard/regex matching and re-issues the current search.</summary>
        public bool UseWildcard
        {
            get => _useWildcard;
            set
            {
                if (_useWildcard == value) return;
                _useWildcard = value;
                OnPropertyChanged();
                TriggerFind();
            }
        }

        /// <summary>Toggles hexadecimal query mode and re-issues the current search.</summary>
        public bool IsHexMode
        {
            get => _isHexMode;
            set
            {
                if (_isHexMode == value) return;
                _isHexMode = value;
                OnPropertyChanged();
                TriggerFind();
            }
        }

        /// <summary>
        /// Formatted "X of Y" counter refreshed whenever the target fires
        /// <see cref="ISearchTarget.SearchResultsChanged"/>.
        /// </summary>
        public string CurrentMatchText
        {
            get
            {
                if (_target == null || _target.MatchCount == 0)
                    return string.IsNullOrEmpty(_searchText) ? string.Empty : "Aucun rÃ©sultat";

                if (_target.CurrentMatchIndex >= 0)
                    return $"{_target.CurrentMatchIndex + 1} / {_target.MatchCount}";

                return $"{_target.MatchCount} rÃ©sultats";
            }
        }

        #endregion

        #region Commands

        public ICommand FindNextCommand     { get; }
        public ICommand FindPreviousCommand { get; }
        public ICommand ReplaceNextCommand  { get; }
        public ICommand ReplaceAllCommand   { get; }

        #endregion

        #region Constructor

        public SearchBarViewModel()
        {
            FindNextCommand     = new RelayCommand(() => _target?.FindNext(),
                                      () => _target != null && !string.IsNullOrEmpty(_searchText));
            FindPreviousCommand = new RelayCommand(() => _target?.FindPrevious(),
                                      () => _target != null && !string.IsNullOrEmpty(_searchText));
            ReplaceNextCommand  = new RelayCommand(() => _target?.Replace(_replaceText),
                                      () => _target != null && !string.IsNullOrEmpty(_searchText));
            ReplaceAllCommand   = new RelayCommand(() => _target?.ReplaceAll(_replaceText),
                                      () => _target != null && !string.IsNullOrEmpty(_searchText));
        }

        #endregion

        #region Public API

        /// <summary>
        /// Binds this ViewModel to a search target; any previous binding is removed first.
        /// Immediately refreshes <see cref="CurrentMatchText"/> and command states.
        /// </summary>
        public void BindToTarget(ISearchTarget target)
        {
            if (_target != null)
                _target.SearchResultsChanged -= OnSearchResultsChanged;

            _target = target;

            if (_target != null)
                _target.SearchResultsChanged += OnSearchResultsChanged;

            OnPropertyChanged(nameof(CurrentMatchText));
            UpdateCommandStates();
        }

        /// <summary>
        /// Detaches from the current target, clears any active search highlights,
        /// and resets the match counter.
        /// </summary>
        public void Detach()
        {
            if (_target != null)
            {
                _target.ClearSearch();
                _target.SearchResultsChanged -= OnSearchResultsChanged;
                _target = null;
            }

            OnPropertyChanged(nameof(CurrentMatchText));
            UpdateCommandStates();
        }

        #endregion

        #region Private Helpers

        private void TriggerFind()
        {
            if (_target == null) return;

            if (string.IsNullOrEmpty(_searchText))
                _target.ClearSearch();
            else
                _target.Find(_searchText, BuildOptions());
        }

        private SearchTargetOptions BuildOptions()
        {
            var opts = SearchTargetOptions.None;
            if (_caseSensitive) opts |= SearchTargetOptions.CaseSensitive;
            if (_useWildcard)   opts |= SearchTargetOptions.UseWildcard;
            if (_isHexMode)     opts |= SearchTargetOptions.HexadecimalMode;
            return opts;
        }

        private void OnSearchResultsChanged(object? sender, EventArgs e)
        {
            OnPropertyChanged(nameof(CurrentMatchText));
            UpdateCommandStates();
        }

        private void UpdateCommandStates()
        {
            (FindNextCommand     as RelayCommand)?.RaiseCanExecuteChanged();
            (FindPreviousCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (ReplaceNextCommand  as RelayCommand)?.RaiseCanExecuteChanged();
            (ReplaceAllCommand   as RelayCommand)?.RaiseCanExecuteChanged();
        }

        #endregion

        #region INotifyPropertyChanged



        #endregion
    }

    // -- Internal RelayCommand â€” no external dependency on HexEditor search stack --

    internal sealed class RelayCommand : ICommand
    {
        private readonly Action       _execute;
        private readonly Func<bool>?  _canExecute;

        public RelayCommand(Action execute, Func<bool>? canExecute = null)
        {
            _execute    = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;
        public void Execute(object? parameter)     => _execute();
        public void RaiseCanExecuteChanged()       => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
