// ==========================================================
// Project: WpfHexEditor.Plugins.StringExtractor
// File: Models/StringExtractionOptions.cs
// Description: User-configurable options for string extraction.
// ==========================================================

namespace WpfHexEditor.Plugins.StringExtractor.Models;

internal sealed class StringExtractionOptions
{
    public int  MinLength      { get; set; } = 4;
    public bool ScanAscii      { get; set; } = true;
    public bool ScanUtf8       { get; set; } = true;
    public bool ScanUtf16Le    { get; set; } = true;
    public bool ScanUtf16Be    { get; set; } = false;
}
