//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

namespace WpfHexEditor.Editor.TextEditor.Highlighting;

/// <summary>
/// Technical references for a syntax definition.
/// </summary>
public sealed class SyntaxReferences
{
    /// <summary>
    /// Official specification document names (e.g. "ISO/IEC 14882:2020 (C++20)").
    /// </summary>
    public IReadOnlyList<string> Specifications { get; init; } = [];

    /// <summary>
    /// Web links to specifications and documentation.
    /// </summary>
    public IReadOnlyList<string> WebLinks { get; init; } = [];
}
