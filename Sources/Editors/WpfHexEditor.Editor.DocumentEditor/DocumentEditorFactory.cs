// ==========================================================
// Project: WpfHexEditor.Editor.DocumentEditor
// File: DocumentEditorFactory.cs
// Description:
//     IEditorFactory that creates DocumentEditorHost instances for
//     RTF, DOCX, DOTX, ODT, and OTT files.
//     Registered in MainWindow before TextEditorFactory so that
//     document formats are intercepted before the generic text fallback.
//
// Architecture Notes:
//     The factory accepts a Func<IIDEHostContext?> to lazily resolve the
//     host context. This is required because _ideHostContext is populated
//     during plugin initialization, which runs AFTER factory registration.
// ==========================================================

using System.IO;
using WpfHexEditor.Editor.Core;
using WpfHexEditor.Editor.DocumentEditor.Controls;
using WpfHexEditor.SDK.Contracts;
using WpfHexEditor.Editor.DocumentEditor.Core;

namespace WpfHexEditor.Editor.DocumentEditor;

/// <summary>
/// <see cref="IEditorFactory"/> that creates <see cref="DocumentEditorHost"/> instances
/// for multi-format document files (RTF, DOCX, ODT).
/// </summary>
public sealed class DocumentEditorFactory : IEditorFactory
{
    private static readonly IEditorDescriptor _descriptor = new DocumentEditorDescriptor();
    private readonly Func<IIDEHostContext?> _contextResolver;

    /// <param name="contextResolver">
    ///     Lazy provider for the IDE host context.
    ///     Resolved at document-open time (not at construction) so plugins
    ///     are already loaded and <see cref="IExtensionRegistry"/> is populated.
    /// </param>
    public DocumentEditorFactory(Func<IIDEHostContext?> contextResolver)
    {
        _contextResolver = contextResolver;
    }

    /// <inheritdoc/>
    public IEditorDescriptor Descriptor => _descriptor;

    /// <inheritdoc/>
    public bool CanOpen(string filePath)
    {
        var ext = Path.GetExtension(filePath)?.ToLowerInvariant();
        return _descriptor.SupportedExtensions.Contains(ext ?? string.Empty);
    }

    // ── Pending-host registry ────────────────────────────────────────────────
    // Hosts created before _ideHostContext is assigned are kept here (weak refs).
    // NotifyContextReady propagates the context to all live pending hosts so they
    // can retry their deferred OpenAsync call.

    private readonly List<WeakReference<DocumentEditorHost>> _pendingHosts = [];

    /// <inheritdoc/>
    public IDocumentEditor Create()
    {
        var host = new DocumentEditorHost(_contextResolver());
        _pendingHosts.Add(new WeakReference<DocumentEditorHost>(host));
        return host;
    }

    /// <summary>
    /// Called by MainWindow after <c>_ideHostContext</c> is assigned.
    /// Propagates the context to all DocumentEditorHost instances that were
    /// created before the IDE finished initializing its plugin system.
    /// </summary>
    public void NotifyContextReady(IIDEHostContext context)
    {
        foreach (var wr in _pendingHosts)
            if (wr.TryGetTarget(out var host))
                host.SetContext(context);
        _pendingHosts.Clear();
    }
}

// ── Private descriptor ───────────────────────────────────────────────────────

file sealed class DocumentEditorDescriptor : IEditorDescriptor
{
    public string Id          => "document-editor";
    public string DisplayName => "Document Editor";
    public string Description =>
        "Multi-format document editor (RTF, DOCX, ODT) with binary-map sync, " +
        "forensic analysis, and cross-view undo/redo.";

    public IReadOnlyList<string> SupportedExtensions { get; } =
        [".rtf", ".docx", ".dotx", ".odt", ".ott"];
}
