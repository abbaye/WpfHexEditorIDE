// ==========================================================
// Project: WpfHexEditor.Editor.Core
// File: LSP/ILspServerRegistry.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     Maps file extensions / language IDs to configured LSP server executables
//     and provides factory methods for obtaining ILspClient instances.
//
// Architecture Notes:
//     Registry Pattern — central lookup avoids scattering server-path knowledge
//     across callers. Implementations persist configuration in AppSettings.
// ==========================================================

namespace WpfHexEditor.Editor.Core.LSP;

/// <summary>
/// Descriptor for a configured Language Server entry.
/// </summary>
public sealed class LspServerEntry
{
    /// <summary>Language identifier (e.g. "json", "csharp", "xml").</summary>
    public required string LanguageId { get; init; }

    /// <summary>File extensions handled by this server (e.g. ".json", ".cs").</summary>
    public required IReadOnlyList<string> FileExtensions { get; init; }

    /// <summary>Absolute path to the server executable.</summary>
    public required string ExecutablePath { get; init; }

    /// <summary>Optional command-line arguments passed to the server on start-up.</summary>
    public string? Arguments { get; init; }

    /// <summary>Whether this entry is currently enabled.</summary>
    public bool IsEnabled { get; init; } = true;

    /// <summary>
    /// <c>true</c> when this entry was resolved from the application's bundled
    /// <c>tools/lsp/</c> directory rather than from the system PATH or a user-supplied path.
    /// </summary>
    public bool IsBundled { get; init; }
}

/// <summary>
/// Registry of known LSP servers keyed by language / file extension.
/// Accessible via <c>IIDEHostContext.LspServers</c>.
/// </summary>
public interface ILspServerRegistry
{
    /// <summary>All registered server entries (enabled and disabled).</summary>
    IReadOnlyList<LspServerEntry> Entries { get; }

    /// <summary>
    /// Returns the best-matching <see cref="LspServerEntry"/> for the given
    /// <paramref name="fileExtension"/> (e.g. ".json"), or null when none is configured.
    /// </summary>
    LspServerEntry? FindByExtension(string fileExtension);

    /// <summary>
    /// Returns the best-matching entry for the given language identifier
    /// (e.g. "json", "csharp"), or null.
    /// </summary>
    LspServerEntry? FindByLanguage(string languageId);

    /// <summary>
    /// Creates (but does not initialize) a new <see cref="ILspClient"/> for the
    /// specified <paramref name="entry"/>.
    /// <paramref name="workspacePath"/> is sent as <c>rootUri</c> in the LSP initialize
    /// request so the server can index project files (required by OmniSharp).
    /// Callers must call <see cref="ILspClient.InitializeAsync"/> before use.
    /// </summary>
    ILspClient CreateClient(LspServerEntry entry, string? workspacePath = null);

    /// <summary>Registers or replaces a server entry (persists to settings).</summary>
    void Register(LspServerEntry entry);

    /// <summary>Removes a server entry by language ID.</summary>
    void Unregister(string languageId);
}
