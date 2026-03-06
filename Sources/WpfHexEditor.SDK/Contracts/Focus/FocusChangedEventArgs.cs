// ==========================================================
// Project: WpfHexEditor.SDK
// File: FocusChangedEventArgs.cs
// Author: Auto
// Created: 2026-03-06
// Description:
//     Event arguments for IFocusContextService.FocusChanged events.
//     Carries both previous and new active document/panel state.
//
// Architecture Notes:
//     Immutable record for thread-safe passing across plugin boundaries.
//
// ==========================================================

namespace WpfHexEditor.SDK.Contracts.Focus;

/// <summary>
/// Event arguments raised when the active document or panel changes in the IDE.
/// </summary>
public sealed class FocusChangedEventArgs : EventArgs
{
    /// <summary>Gets the previously active document (null if none was active).</summary>
    public IDocument? PreviousDocument { get; init; }

    /// <summary>Gets the currently active document (null if no document is active).</summary>
    public IDocument? ActiveDocument { get; init; }

    /// <summary>Gets the currently active panel (null if no panel is focused).</summary>
    public IPanel? ActivePanel { get; init; }

    /// <summary>Gets the previously active panel (null if none was focused).</summary>
    public IPanel? PreviousPanel { get; init; }
}
