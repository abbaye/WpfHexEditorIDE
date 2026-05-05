// ==========================================================
// Project: WpfHexEditor.Plugins.StringExtractor
// File: Models/ExtractedString.cs
// Description: Immutable model representing a single extracted string result.
// ==========================================================

namespace WpfHexEditor.Plugins.StringExtractor.Models;

/// <summary>
/// A single printable string found in a binary file.
/// </summary>
internal sealed record ExtractedString(
    long   Offset,
    int    Length,
    string Encoding,
    string Value)
{
    /// <summary>Offset formatted as hex for display (e.g. "0x00001A40").</summary>
    public string OffsetHex => $"0x{Offset:X8}";
}
