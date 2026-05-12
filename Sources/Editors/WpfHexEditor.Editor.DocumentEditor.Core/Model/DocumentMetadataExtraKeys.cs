// ==========================================================
// Project: WpfHexEditor.Editor.DocumentEditor.Core
// File: Model/DocumentMetadataExtraKeys.cs
// Description:
//     Well-known keys for DocumentMetadata.Extra. Lives here so
//     format savers (which can only reference DocumentEditor.Core)
//     stay in sync with services in the DocumentEditor assembly.
// ==========================================================

namespace WpfHexEditor.Editor.DocumentEditor.Core.Model;

/// <summary>Well-known string keys used in <see cref="DocumentMetadata.Extra"/>.</summary>
public static class DocumentMetadataExtraKeys
{
    /// <summary>Set to <c>"true"</c> when metadata has been anonymized.</summary>
    public const string Anonymized = "anonymized";

    /// <summary>Set to <c>"true"</c> when VBA macros have been stripped.</summary>
    public const string MacrosRemoved = "macrosRemoved";

    /// <summary>Set to <c>"true"</c> for template files (.dotx/.dotm/.ott).</summary>
    public const string IsTemplate = "isTemplate";
}
