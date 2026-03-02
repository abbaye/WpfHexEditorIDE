//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.Windows;
using WpfHexEditor.HexEditor.Search.Views;

namespace WpfHexEditor.HexEditor
{
    /// <summary>
    /// HexEditor partial class — Inline Quick Search Bar (Ctrl+F).
    /// The QuickSearchBar is a VS Code-style overlay that appears at the top-right
    /// of the content area. For advanced options the user can click "⋯" which
    /// delegates to <see cref="ShowAdvancedSearchDialog"/>.
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
                QuickSearchBarOverlay.ViewModel?.UpdateCommandStates();
                QuickSearchBarOverlay.SearchInput?.Focus();
                QuickSearchBarOverlay.SearchInput?.SelectAll();
                return;
            }

            // Refresh ByteProvider in case file was reloaded
            QuickSearchBarOverlay.BindToHexEditor(this);

            // Wire AdvancedSearch button (once — guard via detach/re-bind pattern)
            QuickSearchBarOverlay.OnCloseRequested -= QuickSearchBar_CloseRequested;
            QuickSearchBarOverlay.OnCloseRequested += QuickSearchBar_CloseRequested;

            QuickSearchBarOverlay.OnAdvancedSearchRequested -= QuickSearchBar_AdvancedSearchRequested;
            QuickSearchBarOverlay.OnAdvancedSearchRequested += QuickSearchBar_AdvancedSearchRequested;

            QuickSearchBarOverlay.Visibility = Visibility.Visible;
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
            var currentSearch = QuickSearchBarOverlay.ViewModel?.SearchText;

            // Hide the inline bar first so the two UIs don't overlap
            HideQuickSearchBar();
            ShowAdvancedSearchDialog(Window.GetWindow(this), currentSearch);
        }

        #endregion
    }
}
