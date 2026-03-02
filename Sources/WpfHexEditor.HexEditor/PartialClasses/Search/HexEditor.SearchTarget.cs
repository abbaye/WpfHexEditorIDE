////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System;
using System.ComponentModel;
using System.Windows;
using WpfHexEditor.Core.Search.Models;
using WpfHexEditor.Editor.Core;
using WpfHexEditor.HexEditor.Search.ViewModels;

namespace WpfHexEditor.HexEditor
{
    /// <summary>
    /// HexEditor partial class — implements <see cref="ISearchTarget"/> so that the
    /// shared <see cref="WpfHexEditor.Editor.Core.Views.QuickSearchBar"/> can drive
    /// search/replace operations without any direct reference to <see cref="HexEditor"/>.
    /// </summary>
    public partial class HexEditor : ISearchTarget
    {
        #region Fields

        private ReplaceViewModel? _searchTargetVm;
        private int _searchTargetMatchCount;
        private int _searchTargetCurrentIndex = -1;

        #endregion

        #region ISearchTarget — Capabilities

        /// <inheritdoc/>
        public SearchBarCapabilities Capabilities =>
            SearchBarCapabilities.CaseSensitive  |
            SearchBarCapabilities.Wildcard        |
            SearchBarCapabilities.HexMode         |
            SearchBarCapabilities.Replace         |
            SearchBarCapabilities.AdvancedSearch;

        #endregion

        #region ISearchTarget — State

        /// <inheritdoc/>
        public int MatchCount        => _searchTargetMatchCount;

        /// <inheritdoc/>
        public int CurrentMatchIndex => _searchTargetCurrentIndex;

        #endregion

        #region ISearchTarget — Operations

        /// <inheritdoc/>
        public void Find(string query, SearchTargetOptions options = default)
        {
            var vm = EnsureSearchVm();

            // Refresh the byte provider in case the file was reloaded
            vm.ByteProvider = GetByteProvider();

            // Push options
            vm.SearchText   = query;
            vm.CaseSensitive = options.HasFlag(SearchTargetOptions.CaseSensitive);
            vm.UseWildcard   = options.HasFlag(SearchTargetOptions.UseWildcard);
            vm.IsHexMode     = options.HasFlag(SearchTargetOptions.HexadecimalMode);

            // Fire-and-forget: results come back via OnMatchFound / PropertyChanged
            _ = vm.FindAllAsync();
        }

        /// <inheritdoc/>
        public void FindNext()
        {
            var vm = EnsureSearchVm();
            _ = vm.FindNextAsync();
        }

        /// <inheritdoc/>
        public void FindPrevious()
        {
            var vm = EnsureSearchVm();
            _ = vm.FindPreviousAsync();
        }

        /// <inheritdoc/>
        public void ClearSearch()
        {
            _searchTargetVm?.ClearResults();
            ClearCustomBackgroundBlockByTag("QuickSearchResult");

            _searchTargetMatchCount  = 0;
            _searchTargetCurrentIndex = -1;
            SearchResultsChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <inheritdoc/>
        public void Replace(string replacement)
        {
            var vm = EnsureSearchVm();
            vm.ReplaceText = replacement;
            _ = vm.ReplaceNextAsync();
        }

        /// <inheritdoc/>
        public void ReplaceAll(string replacement)
        {
            var vm = EnsureSearchVm();
            vm.ReplaceText = replacement;
            _ = vm.ReplaceAllAsync();
        }

        /// <inheritdoc/>
        public UIElement? GetCustomFiltersContent() => null;

        #endregion

        #region ISearchTarget — Event

        /// <inheritdoc/>
        public event EventHandler? SearchResultsChanged;

        #endregion

        #region Private Helpers

        private ReplaceViewModel EnsureSearchVm()
        {
            if (_searchTargetVm != null) return _searchTargetVm;

            _searchTargetVm = new ReplaceViewModel();

            // Navigate to each found match
            _searchTargetVm.OnMatchFound += OnSearchTargetMatchFound;

            // Detect when a search completes with zero results (OnMatchFound won't fire)
            _searchTargetVm.PropertyChanged += OnSearchTargetVmPropertyChanged;

            return _searchTargetVm;
        }

        private void OnSearchTargetMatchFound(object? sender, SearchMatch match)
        {
            _searchTargetMatchCount  = _searchTargetVm?.SearchResults.Count ?? 0;
            _searchTargetCurrentIndex = _searchTargetVm?.CurrentMatchIndex   ?? -1;

            // Navigate the HexEditor viewport to the found match
            FindSelect(match.Position, match.Length);
            SetPosition(match.Position);

            SearchResultsChanged?.Invoke(this, EventArgs.Empty);
        }

        private void OnSearchTargetVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // When the async search finishes (IsSearching → false) with no results,
            // OnMatchFound was never called — we still need to refresh the counter.
            if (e.PropertyName != nameof(SearchViewModel.IsSearching)) return;
            if (_searchTargetVm == null || _searchTargetVm.IsSearching) return;

            _searchTargetMatchCount  = _searchTargetVm.SearchResults.Count;
            _searchTargetCurrentIndex = _searchTargetVm.CurrentMatchIndex;

            // Always fire so the bar refreshes its "Aucun résultat" label
            SearchResultsChanged?.Invoke(this, EventArgs.Empty);
        }

        #endregion
    }
}
