// ==========================================================
// Project: WpfHexEditor.HexEditor
// File: HexEditor.QuickSearch.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude (Anthropic)
// Created: 2026-03-06
// Description:
//     Partial class implementing the inline Ctrl+F quick search bar for the HexEditor.
//     Hosts a QuickSearchBar UserControl in a transparent Canvas overlay and manages
//     its show/hide lifecycle, position, and delegation to the full search panel.
//
// Architecture Notes:
//     QuickSearchBar floats in a Canvas over the HexViewport. Position is persisted
//     via DependencyProperties. Ctrl+F toggles visibility; Escape dismisses it.
//
// ==========================================================

using System.Windows;

namespace WpfHexEditor.HexEditor
{
    /// <summary>
    /// HexEditor partial class — Inline Quick Search Bar (Ctrl+F).
    /// The <see cref="WpfHexEditor.Editor.Core.Views.QuickSearchBar"/> is hosted in a
    /// transparent Canvas overlay so it can be dragged anywhere within the editor area.
    /// For advanced options the user clicks "⋯" which delegates to
    /// <see cref="ShowAdvancedSearchDialog"/>.
    /// </summary>
    public partial class HexEditor
    {
        #region Quick Search Bar — Show / Hide

        /// <summary>
        /// Shows the inline quick search bar and binds it to this HexEditor.
        /// If already visible, just refocuses the search input.
        /// </summary>
        public void ShowQuickSearchBar()
        {
            if (QuickSearchBarOverlay == null) return;

            if (QuickSearchBarOverlay.Visibility == Visibility.Visible)
            {
                // Already open — refocus so the user can type immediately
                QuickSearchBarOverlay.FocusSearchInput();
                return;
            }

            // Bind to this HexEditor (implements ISearchTarget)
            QuickSearchBarOverlay.BindToTarget(this);

            // Wire host-level events (guard: remove first to avoid duplicate subscriptions)
            QuickSearchBarOverlay.OnCloseRequested          -= QuickSearchBar_CloseRequested;
            QuickSearchBarOverlay.OnCloseRequested          += QuickSearchBar_CloseRequested;
            QuickSearchBarOverlay.OnAdvancedSearchRequested -= QuickSearchBar_AdvancedSearchRequested;
            QuickSearchBarOverlay.OnAdvancedSearchRequested += QuickSearchBar_AdvancedSearchRequested;

            QuickSearchBarOverlay.Visibility = Visibility.Visible;

            // Position the bar in the top-right corner unless the user has moved it
            if (SearchBarCanvas != null)
                QuickSearchBarOverlay.EnsureDefaultPosition(SearchBarCanvas);
        }

        /// <summary>
        /// Hides the inline quick search bar and clears search highlights.
        /// </summary>
        public void HideQuickSearchBar()
        {
            if (QuickSearchBarOverlay == null) return;

            QuickSearchBarOverlay.Visibility = Visibility.Collapsed;
            QuickSearchBarOverlay.Detach();

            // Clear search result highlights from the viewport
            ClearCustomBackgroundBlockByTag("QuickSearchResult");
        }

        #endregion

        #region Event Handlers

        private void QuickSearchBar_CloseRequested(object sender, System.EventArgs e)
            => HideQuickSearchBar();

        private void QuickSearchBar_AdvancedSearchRequested(object sender, System.EventArgs e)
        {
            // Transfer the current search text so the dialog opens pre-filled
            var currentSearch = QuickSearchBarOverlay?.SearchText;

            // Hide the inline bar first so the two UIs don't overlap
            HideQuickSearchBar();
            ShowAdvancedSearchDialog(Window.GetWindow(this), currentSearch);
        }

        #endregion
    }
}
