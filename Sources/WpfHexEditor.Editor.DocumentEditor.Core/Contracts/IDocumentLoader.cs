// ==========================================================
// Project: WpfHexEditor.Editor.Core
// File: Contracts/IDocumentLoader.cs
// Author: WpfHexEditor Contributors
// Created: 2026-03-29
// Description:
//     Extension point contract for pluggable document format loaders.
//     Mirrors the ISolutionLoader pattern: each format (RTF, DOCX, ODT …)
//     is contributed by a separate plugin that registers an implementation
//     via IExtensionRegistry.Register<IDocumentLoader>(pluginId, instance).
//
//     DocumentEditorHost auto-selects the loader at open time:
//       var loader = registry.GetExtensions<IDocumentLoader>()
//                            .FirstOrDefault(l => l.CanLoad(filePath));
//
// Architecture Notes:
//     - No WPF dependency (used from DocumentEditor.Core and plugins alike).
//     - CanLoad() must be fast — extension-check only, no I/O.
//     - LoadAsync() must populate target.Blocks and target.BinaryMap.
//     - Partial / best-effort loading on malformed files is preferred over
//       throwing; add a ForensicAlert{Kind=ParseError} to target instead.
// ==========================================================

using WpfHexEditor.Editor.DocumentEditor.Core.BinaryMap;
using WpfHexEditor.Editor.DocumentEditor.Core.Model;

namespace WpfHexEditor.Editor.Core;

/// <summary>
/// Extension point: loads a raw stream into a <see cref="DocumentModel"/>.
/// <para>
/// Register via
/// <c>IExtensionRegistry.Register&lt;IDocumentLoader&gt;(pluginId, instance)</c>.
/// Auto-selected by <c>DocumentEditorHost</c> using <see cref="CanLoad"/>.
/// </para>
/// </summary>
public interface IDocumentLoader
{
    /// <summary>Human-readable name, e.g. <c>"RTF Loader"</c>.</summary>
    string LoaderName { get; }

    /// <summary>
    /// File extensions handled by this loader, <em>without</em> the leading dot
    /// (e.g. <c>["rtf"]</c>, <c>["docx","dotx"]</c>).
    /// Used to build file-dialog filters and for quick CanLoad evaluation.
    /// </summary>
    IReadOnlyList<string> SupportedExtensions { get; }

    /// <summary>
    /// Fast pre-check — extension only, no I/O.
    /// Called before <see cref="LoadAsync"/> to find the right loader.
    /// </summary>
    bool CanLoad(string filePath);

    /// <summary>
    /// Parses <paramref name="stream"/> and populates <paramref name="target"/>.
    /// <para>
    /// Implementations <em>must</em> fill:
    /// <list type="bullet">
    ///   <item><see cref="DocumentModel.Blocks"/> — the logical block tree</item>
    ///   <item><see cref="DocumentModel.BinaryMap"/> — offset ↔ block mapping</item>
    ///   <item><see cref="DocumentModel.Metadata"/> — title, author, version</item>
    /// </list>
    /// On malformed input: catch exceptions, add a
    /// <c>ForensicAlert{Kind=ParseError}</c> to <see cref="DocumentModel.ForensicAlerts"/>
    /// and return a partial model — do NOT rethrow.
    /// </para>
    /// </summary>
    Task LoadAsync(string filePath, Stream stream, DocumentModel target,
                   CancellationToken ct = default);
}
