// ==========================================================
// Project: WpfHexEditor.Editor.DocumentEditor.Core
// File: Forensic/ForensicMode.cs
// Description: Forensic display mode enum.
// ==========================================================

namespace WpfHexEditor.Editor.DocumentEditor.Core.Forensic;

/// <summary>
/// Controls the level of forensic information displayed in the editor.
/// </summary>
public enum ForensicMode
{
    /// <summary>Normal editing mode — no forensic overlay.</summary>
    Normal,

    /// <summary>
    /// Debug mode — shows block offsets, boundaries, and encoding info
    /// in the gutter, but does not flag structural anomalies as errors.
    /// </summary>
    Debug,

    /// <summary>
    /// Full forensic mode — highlights offset gaps, overlaps, encoding
    /// anomalies, failed WHFMT assertions, and suspicious metadata.
    /// </summary>
    Forensic
}
