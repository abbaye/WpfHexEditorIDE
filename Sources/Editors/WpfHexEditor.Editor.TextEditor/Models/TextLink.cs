// ==========================================================
// Project: WpfHexEditor.Editor.TextEditor
// File: Models/TextLink.cs
// Author: Derek Tremblay
// Created: 2026-03-16
// License: GNU Affero General Public License v3.0 (AGPL-3.0)
// Description:
//     Immutable model representing a clickable text range in the TextEditor.
//     Rendered as an underline by TextLinkAdorner; clicking invokes OnClick.
//     Used by SetContentWithLinks() for goto-definition navigation.
//
// Architecture Notes:
//     Pattern: Immutable data model (record).
// ==========================================================

namespace WpfHexEditor.Editor.TextEditor.Models;

/// <summary>
/// A clickable text range in the code editor.
/// Rendered with an underline by <see cref="Services.TextLinkAdorner"/>.
/// </summary>
public sealed record TextLink(
    /// <summary>Zero-based start character offset within the editor text.</summary>
    int StartOffset,

    /// <summary>Zero-based exclusive end character offset (StartOffset + length).</summary>
    int EndOffset,

    /// <summary>The identifier text of this link (for tooltip / hit-test).</summary>
    string DisplayText,

    /// <summary>Invoked when the user Ctrl+Clicks the link.</summary>
    Action OnClick);
