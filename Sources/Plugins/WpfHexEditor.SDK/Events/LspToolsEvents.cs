// ==========================================================
// Project: WpfHexEditor.SDK
// File: Events/LspToolsEvents.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-30
// Description:
//     IDE-level plugin event bus events for LSP hierarchy panels.
//     Published by the host (MainWindow) on CallHierarchyDockRequested /
//     TypeHierarchyDockRequested from CodeEditor; consumed by LSPTools plugin.
// ==========================================================

using WpfHexEditor.Editor.Core.LSP;

namespace WpfHexEditor.SDK.Events;

/// <summary>
/// Published when the IDE has received LSP call hierarchy items for the caret symbol.
/// The LspTools plugin subscribes to this event to populate and show CallHierarchyPanel.
/// </summary>
public sealed class CallHierarchyReadyEvent
{
    /// <summary>LSP items returned by prepareCallHierarchy.</summary>
    public IReadOnlyList<LspCallHierarchyItem> Items { get; init; } = Array.Empty<LspCallHierarchyItem>();

    /// <summary>Display name of the symbol (shown in the panel header).</summary>
    public string SymbolName { get; init; } = string.Empty;

    /// <summary>
    /// Live LSP client to be used for incoming/outgoing expansion.
    /// Null when no LSP server is active.
    /// </summary>
    public ILspClient? LspClient { get; init; }
}

/// <summary>
/// Published when the IDE has received LSP type hierarchy items for the caret symbol.
/// The LspTools plugin subscribes to this event to populate and show TypeHierarchyPanel.
/// </summary>
public sealed class TypeHierarchyReadyEvent
{
    /// <summary>LSP items returned by prepareTypeHierarchy.</summary>
    public IReadOnlyList<LspTypeHierarchyItem> Items { get; init; } = Array.Empty<LspTypeHierarchyItem>();

    /// <summary>Display name of the symbol (shown in the panel header).</summary>
    public string SymbolName { get; init; } = string.Empty;

    /// <summary>
    /// Live LSP client to be used for supertype/subtype expansion.
    /// Null when no LSP server is active.
    /// </summary>
    public ILspClient? LspClient { get; init; }
}
