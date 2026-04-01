// ==========================================================
// Project: WpfHexEditor.Core
// File: IEditorNavigationCallback.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude (Anthropic)
// Created: 2026-03-26
// Description:
//     Optional callback interface for editors to receive navigation
//     and selection requests from the format parsing service.
//     Editors implement only the methods that make sense for them.
//
// Architecture Notes:
//     Default interface methods — editors can override selectively.
//     HexEditor implements all; a text editor might only implement NavigateTo.
// ==========================================================

namespace WpfHexEditor.Core.Interfaces
{
    /// <summary>
    /// Optional interface for <see cref="IBinaryDataSource"/> implementations
    /// that can also receive navigation, selection, and bookmark requests
    /// from the format parsing panel.
    /// </summary>
    public interface IEditorNavigationCallback
    {
        /// <summary>
        /// Scroll the editor to make <paramref name="offset"/> visible and select one byte.
        /// </summary>
        void NavigateTo(long offset) { }

        /// <summary>
        /// Highlight the byte range [<paramref name="start"/>, <paramref name="end"/>] (inclusive).
        /// </summary>
        void SetSelection(long start, long end) { }

        /// <summary>
        /// Register a persistent bookmark at <paramref name="offset"/>.
        /// </summary>
        void SetBookmark(long offset) { }

        /// <summary>
        /// Remove a previously registered bookmark at <paramref name="offset"/>.
        /// </summary>
        void RemoveBookmark(long offset) { }

        /// <summary>
        /// Add a custom background block overlay to the editor viewport.
        /// </summary>
        void AddCustomBackgroundBlock(CustomBackgroundBlock block) { }

        /// <summary>
        /// Remove all custom background blocks from the editor viewport.
        /// </summary>
        void ClearCustomBackgroundBlocks() { }
    }
}
