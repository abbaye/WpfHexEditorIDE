// ==========================================================
// Project: WpfHexEditor.Editor.DocumentEditor.Core
// File: Contracts/IDocumentSaver.cs
// Description:
//     Extension point contract for pluggable document format savers.
//     Symmetric to IDocumentLoader. Implementations live in the plugins
//     project and are driven by the documentSchema section of each
//     format's .whfmt file — no hardcoded format rules in C#.
//
//     DocumentEditorHost selects the saver at save time:
//       var saver = registry.GetExtensions<IDocumentSaver>()
//                           .FirstOrDefault(s => s.CanSave(filePath));
// ==========================================================

using WpfHexEditor.Editor.DocumentEditor.Core.Model;

namespace WpfHexEditor.Editor.DocumentEditor.Core;

/// <summary>
/// Extension point: serialises a <see cref="DocumentModel"/> back to a raw stream.
/// <para>
/// Register via
/// <c>IExtensionRegistry.Register&lt;IDocumentSaver&gt;(pluginId, instance)</c>.
/// Auto-selected by <c>DocumentEditorHost.SaveAsync</c> using <see cref="CanSave"/>.
/// </para>
/// </summary>
public interface IDocumentSaver
{
    /// <summary>Human-readable name, e.g. <c>"DOCX Saver"</c>.</summary>
    string SaverName { get; }

    /// <summary>
    /// File extensions handled by this saver, without the leading dot
    /// (e.g. <c>["docx","dotx"]</c>).
    /// </summary>
    IReadOnlyList<string> SupportedExtensions { get; }

    /// <summary>
    /// Fast pre-check — extension only, no I/O.
    /// Called before <see cref="SaveAsync"/> to find the right saver.
    /// </summary>
    bool CanSave(string filePath);

    /// <summary>
    /// Serialises <paramref name="model"/> into <paramref name="output"/>.
    /// <para>
    /// Implementations must write a complete, valid file — not a partial update.
    /// The caller handles atomic rename (write to .tmp, then replace).
    /// </para>
    /// </summary>
    Task SaveAsync(DocumentModel model, Stream output, CancellationToken ct = default);
}
