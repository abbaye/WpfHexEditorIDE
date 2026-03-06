// ==========================================================
// Project: WpfHexEditor.SDK
// File: IFocusContextService.cs
// Author: Auto
// Created: 2026-03-06
// Description:
//     Focus-aware service that tracks the currently active document and panel.
//     Plugins subscribe to FocusChanged to react to user navigation without polling.
//
// Architecture Notes:
//     Singleton service; implemented by FocusContextService in PluginHost.
//     Events are raised on the UI thread (safe for WPF plugins).
//     Critical for context-aware plugins (selection, offset, document type).
//
// ==========================================================

using WpfHexEditor.SDK.Contracts.Focus;

namespace WpfHexEditor.SDK.Contracts;

/// <summary>
/// Tracks the active document and panel in the IDE.
/// Provides change notifications to plugins without requiring polling.
/// </summary>
public interface IFocusContextService
{
    /// <summary>Gets the currently active document, or null if none is active.</summary>
    IDocument? ActiveDocument { get; }

    /// <summary>Gets the currently active panel, or null if no panel is focused.</summary>
    IPanel? ActivePanel { get; }

    /// <summary>
    /// Raised when the active document or panel changes.
    /// Always raised on the UI (Dispatcher) thread.
    /// </summary>
    event EventHandler<FocusChangedEventArgs> FocusChanged;
}
