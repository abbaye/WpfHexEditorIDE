// ==========================================================
// Project: WpfHexEditor.Editor.Core
// File: Documents/IDocumentBuffer.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-23
// Description:
//     Shared content buffer for one unique file path.
//     Allows multiple editor tabs on the same file to stay in sync
//     and provides the authoritative text source for LSP routing.
//
// Architecture Notes:
//     Pattern: Shared State / Observable Buffer
//     - One DocumentBuffer instance per unique file path (case-insensitive).
//     - DocumentManager owns the buffer lifecycle (create on first open,
//       remove when last tab on that path closes).
//     - IBufferAwareEditor opt-in: editors that implement it get attached
//       to the buffer so edits propagate bidirectionally.
//     - Thread safety: SetText is lock-guarded; Changed event is
//       marshalled to the WPF Dispatcher before raising.
// ==========================================================

namespace WpfHexEditor.Editor.Core.Documents;

/// <summary>
/// Shared content buffer for a single open file.
/// Multiple editor tabs on the same path share one buffer instance.
/// </summary>
public interface IDocumentBuffer
{
    /// <summary>Absolute file path this buffer represents.</summary>
    string FilePath   { get; }

    /// <summary>Language identifier (e.g. "csharp", "python") for LSP routing.</summary>
    string LanguageId { get; }

    /// <summary>Monotonically increasing version number, incremented on every <see cref="SetText"/> call.</summary>
    int    Version    { get; }

    /// <summary>Current full text content of the document.</summary>
    string Text       { get; }

    /// <summary>
    /// Replaces the entire document text and increments <see cref="Version"/>.
    /// </summary>
    /// <param name="text">New full text content.</param>
    /// <param name="source">The editor making the change, or <c>null</c> for external updates.
    /// Recipients receiving <see cref="Changed"/> must ignore updates where
    /// <see cref="DocumentBufferChangedEventArgs.Source"/> is themselves.</param>
    void SetText(string text, IDocumentEditor? source = null);

    /// <summary>Fired on the WPF Dispatcher thread after every successful <see cref="SetText"/> call.</summary>
    event EventHandler<DocumentBufferChangedEventArgs> Changed;
}

/// <summary>
/// Event arguments for <see cref="IDocumentBuffer.Changed"/>.
/// </summary>
public sealed class DocumentBufferChangedEventArgs : EventArgs
{
    /// <summary>The full new text after the change.</summary>
    public string           NewText    { get; init; } = string.Empty;

    /// <summary>The new version number (same as <see cref="IDocumentBuffer.Version"/> at raise time).</summary>
    public int              NewVersion { get; init; }

    /// <summary>The editor that triggered the change, or <c>null</c> for external/LSP updates.</summary>
    public IDocumentEditor? Source     { get; init; }
}
