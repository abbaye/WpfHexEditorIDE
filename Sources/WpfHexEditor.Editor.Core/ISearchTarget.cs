////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System;
using System.Windows;

namespace WpfHexEditor.Editor.Core
{
    /// <summary>
    /// Declares which features the QuickSearchBar should expose for a given editor.
    /// The bar adapts its visible controls to the capabilities of the bound target.
    /// </summary>
    [Flags]
    public enum SearchBarCapabilities
    {
        None           = 0,
        CaseSensitive  = 1 << 0,   // toggle "Ab"
        Wildcard       = 1 << 1,   // toggle ".*"
        HexMode        = 1 << 2,   // toggle "0x"       — HexEditor only
        Replace        = 1 << 3,   // expand/collapse replace row (→1 / →∀)
        AdvancedSearch = 1 << 4,   // button "⋯" → advanced dialog — HexEditor only
        CustomFilters  = 1 << 5,   // custom filter slot injected by the editor (TblEditor)
    }

    /// <summary>
    /// Options passed to <see cref="ISearchTarget.Find"/> from the search bar.
    /// </summary>
    [Flags]
    public enum SearchTargetOptions
    {
        None            = 0,
        CaseSensitive   = 1 << 0,
        UseWildcard     = 1 << 1,
        HexadecimalMode = 1 << 2,
    }

    /// <summary>
    /// Contract implemented by editors that support inline search.
    /// The <see cref="QuickSearchBar"/> UserControl binds to this interface via
    /// <c>BindToTarget(ISearchTarget)</c> and adjusts its visible features
    /// according to <see cref="Capabilities"/>.
    /// </summary>
    public interface ISearchTarget
    {
        // -- Capabilities (what the bar should show) ----------------------
        SearchBarCapabilities Capabilities { get; }

        // -- Current state ------------------------------------------------
        int MatchCount        { get; }
        int CurrentMatchIndex { get; }

        // -- Operations ---------------------------------------------------

        /// <summary>
        /// Starts a new search. The target executes the search (possibly async)
        /// and fires <see cref="SearchResultsChanged"/> when results are ready.
        /// Navigation to the first result is the target's responsibility.
        /// </summary>
        void Find(string query, SearchTargetOptions options = default);

        /// <summary>Navigates to the next cached result (wraps around).</summary>
        void FindNext();

        /// <summary>Navigates to the previous cached result (wraps around).</summary>
        void FindPrevious();

        /// <summary>Clears highlights / cached results.</summary>
        void ClearSearch();

        // -- Replace (only called when Capabilities has Replace) -----------
        void Replace(string replacement);
        void ReplaceAll(string replacement);

        // -- Custom filters (only called when Capabilities has CustomFilters)
        /// <summary>
        /// Returns a <see cref="UIElement"/> injected into the bar's custom-filter
        /// slot (between the toggles and the Prev/Next buttons).
        /// Return <c>null</c> when not applicable.
        /// </summary>
        UIElement? GetCustomFiltersContent();

        /// <summary>
        /// Raised by the target when the match list changes so the bar can
        /// refresh its match counter.
        /// </summary>
        event EventHandler? SearchResultsChanged;
    }
}
