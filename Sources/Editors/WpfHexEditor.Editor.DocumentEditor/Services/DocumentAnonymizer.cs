// ==========================================================
// Project: WpfHexEditor.Editor.DocumentEditor
// File: Services/DocumentAnonymizer.cs
// Description:
//     Forensic metadata strip — clears authoring identity, timestamps
//     and optionally VBA macros from a DocumentModel before save.
//     Marks the model via Metadata.Extra["anonymized"]="true" so format
//     savers know to rewrite docProps/core.xml and drop vbaProject.bin
//     instead of copying them verbatim from the source ZIP.
// ==========================================================

using WpfHexEditor.Editor.DocumentEditor.Core.Model;

namespace WpfHexEditor.Editor.DocumentEditor.Services;

/// <summary>
/// Strips identifying metadata from a <see cref="DocumentModel"/> in-memory.
/// Savers consult <see cref="DocumentMetadata.Extra"/>["anonymized"]="true" to
/// know they must rewrite container metadata too (docProps/core.xml, etc.)
/// rather than copy them from the original archive.
/// </summary>
public static class DocumentAnonymizer
{
    // Marker keys live on DocumentMetadataExtraKeys so format savers can
    // honor the same names without referencing the DocumentEditor assembly.

    /// <summary>
    /// Clears author/creation/modification fields and any custom Extra
    /// properties from <paramref name="model"/>. When <paramref name="stripMacros"/>
    /// is true and the source format embeds macros, sets a flag so the saver
    /// drops vbaProject.bin (DOCM/DOTM → DOCX-equivalent payload).
    /// </summary>
    public static AnonymizeResult Anonymize(DocumentModel model, bool stripMacros = true)
    {
        if (model is null) throw new ArgumentNullException(nameof(model));
        var meta = model.Metadata ?? new DocumentMetadata();

        var result = new AnonymizeResult
        {
            HadAuthor   = !string.IsNullOrEmpty(meta.Author),
            HadCreated  = meta.CreatedUtc.HasValue,
            HadModified = meta.ModifiedUtc.HasValue,
            HadMacros   = meta.HasMacros,
        };

        // Wipe identifying fields. Title is kept (often = filename, user-visible context).
        meta.Author      = string.Empty;
        meta.CreatedUtc  = null;
        meta.ModifiedUtc = null;

        // Preserve format-identity keys; drop everything else (custom properties,
        // company name, last-printed timestamps, etc. that may leak provenance).
        int removed = 0;
        var allowlist = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            DocumentMetadataExtraKeys.IsTemplate,
        };
        foreach (var key in meta.Extra.Keys.ToArray())
        {
            if (allowlist.Contains(key)) continue;
            meta.Extra.Remove(key);
            removed++;
        }
        meta.Extra[DocumentMetadataExtraKeys.Anonymized] = "true";

        if (stripMacros && meta.HasMacros)
        {
            meta.HasMacros = false;
            meta.Extra[DocumentMetadataExtraKeys.MacrosRemoved] = "true";
        }

        result = result with { ExtraKeysRemoved = removed };

        model.Metadata = meta;
        return result;
    }
}

/// <summary>Outcome of an <see cref="DocumentAnonymizer.Anonymize"/> call.</summary>
public sealed record AnonymizeResult
{
    public bool HadAuthor       { get; init; }
    public bool HadCreated      { get; init; }
    public bool HadModified     { get; init; }
    public bool HadMacros       { get; init; }
    public int  ExtraKeysRemoved { get; init; }
}
