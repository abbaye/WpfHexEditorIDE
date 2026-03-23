// ==========================================================
// Project: WpfHexEditor.Plugins.AssemblyExplorer
// File: Services/AssemblyHexSyncService.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-23
// Description:
//     Reverse-navigation service: when the user moves the hex editor selection,
//     this service resolves the active byte offset to an ECMA-335 metadata token
//     and selects the matching tree node in the Assembly Explorer panel.
//
// Architecture Notes:
//     Pattern: Service — subscribes to IHexEditorService.SelectionChanged
//     and delegates token resolution to PeOffsetResolver.
//     IDisposable: unsubscribes the handler when the plugin is unloaded.
//     Thread safety: SelectionChanged is raised on the UI thread (per SDK contract)
//     so no explicit marshalling is required.
// ==========================================================

using System.IO;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using WpfHexEditor.Core.AssemblyAnalysis.Services;
using WpfHexEditor.Plugins.AssemblyExplorer.ViewModels;
using WpfHexEditor.SDK.Contracts.Services;

namespace WpfHexEditor.Plugins.AssemblyExplorer.Services;

/// <summary>
/// Subscribes to <see cref="IHexEditorService.SelectionChanged"/> and drives reverse
/// Hex → Tree navigation: when the user's hex editor caret lands inside a known
/// method body or metadata row, the corresponding tree node is selected.
/// </summary>
public sealed class AssemblyHexSyncService : IDisposable
{
    private readonly IHexEditorService        _hexService;
    private readonly AssemblyExplorerViewModel _vm;
    private readonly PeOffsetResolver          _resolver = new();
    private bool                               _disposed;

    // Cached PEReader/MetadataReader per file so we don't re-open the file on every keystroke.
    private string?         _cachedFilePath;
    private PEReader?       _cachedPeReader;
    private MetadataReader? _cachedMdReader;

    public AssemblyHexSyncService(IHexEditorService hexService, AssemblyExplorerViewModel vm)
    {
        _hexService = hexService ?? throw new ArgumentNullException(nameof(hexService));
        _vm         = vm         ?? throw new ArgumentNullException(nameof(vm));

        _hexService.SelectionChanged   += OnSelectionChanged;
        _hexService.ActiveEditorChanged += OnActiveEditorChanged;
    }

    // ── IDisposable ───────────────────────────────────────────────────────────

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _hexService.SelectionChanged    -= OnSelectionChanged;
        _hexService.ActiveEditorChanged -= OnActiveEditorChanged;

        DisposeReaders();
    }

    // ── Event handlers ────────────────────────────────────────────────────────

    private void OnSelectionChanged(object? sender, EventArgs e)
    {
        if (!_vm.SyncWithHexEditor) return;

        var offset   = _hexService.SelectionStart;
        if (offset < 0) return;

        var filePath = _hexService.CurrentFilePath;
        if (string.IsNullOrEmpty(filePath) || !_vm.IsAssemblyLoaded(filePath)) return;

        var token = ResolveTokenAt(filePath, offset);
        _vm.SelectNode(token);
    }

    private void OnActiveEditorChanged(object? sender, EventArgs e)
    {
        // Invalidate cached readers when the user switches to a different file.
        var newPath = _hexService.CurrentFilePath;
        if (!string.Equals(newPath, _cachedFilePath, StringComparison.OrdinalIgnoreCase))
            DisposeReaders();
    }

    // ── Token resolution ──────────────────────────────────────────────────────

    /// <summary>
    /// Resolves a raw PE file byte offset to an ECMA-335 metadata token.
    /// Returns null when the offset does not match any known token.
    /// </summary>
    private int? ResolveTokenAt(string filePath, long fileOffset)
    {
        try
        {
            EnsureReadersOpen(filePath);
            if (_cachedPeReader is null || _cachedMdReader is null) return null;
            if (!_cachedMdReader.IsAssembly) return null;

            return _resolver.ResolveToken(fileOffset, _cachedPeReader, _cachedMdReader);
        }
        catch
        {
            DisposeReaders(); // Invalidate on any I/O error.
            return null;
        }
    }

    private void EnsureReadersOpen(string filePath)
    {
        if (string.Equals(filePath, _cachedFilePath, StringComparison.OrdinalIgnoreCase)
            && _cachedPeReader is not null)
            return;

        DisposeReaders();

        // FileShare.ReadWrite: allows the hex editor to hold the file open concurrently.
        var stream = new FileStream(
            filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

        _cachedPeReader = new PEReader(stream);
        if (_cachedPeReader.HasMetadata)
            _cachedMdReader = _cachedPeReader.GetMetadataReader();

        _cachedFilePath = filePath;
    }

    private void DisposeReaders()
    {
        _cachedMdReader = null;
        _cachedPeReader?.Dispose();
        _cachedPeReader = null;
        _cachedFilePath = null;
    }
}
