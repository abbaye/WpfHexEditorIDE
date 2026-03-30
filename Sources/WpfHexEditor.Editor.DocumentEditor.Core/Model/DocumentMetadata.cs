// ==========================================================
// Project: WpfHexEditor.Editor.DocumentEditor.Core
// File: Model/DocumentMetadata.cs
// Description: Document-level metadata extracted by loaders.
// ==========================================================

namespace WpfHexEditor.Editor.DocumentEditor.Core.Model;

/// <summary>
/// Document-level metadata extracted by the format loader.
/// </summary>
public sealed record DocumentMetadata
{
    /// <summary>Document title (from properties or file name).</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Primary author name.</summary>
    public string Author { get; set; } = string.Empty;

    /// <summary>Creation date/time (UTC), or null if unavailable.</summary>
    public DateTime? CreatedUtc { get; set; }

    /// <summary>Last-modified date/time (UTC), or null if unavailable.</summary>
    public DateTime? ModifiedUtc { get; set; }

    /// <summary>Format version string (e.g. "RTF 1.9", "OOXML 2016").</summary>
    public string FormatVersion { get; set; } = string.Empty;

    /// <summary>Detected MIME type.</summary>
    public string MimeType { get; set; } = string.Empty;

    /// <summary>True if the document contains macros (VBA or similar).</summary>
    public bool HasMacros { get; set; }

    /// <summary>Additional loader-specific properties.</summary>
    public Dictionary<string, string> Extra { get; } = [];
}
