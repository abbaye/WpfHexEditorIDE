//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.Collections.Generic;

namespace WpfHexEditor.Definitions;

/// <summary>
/// Lightweight metadata record for a <c>.whlang</c> syntax definition
/// embedded in <c>WpfHexEditor.Definitions.dll</c>.
/// </summary>
/// <param name="ResourceKey">Assembly manifest resource key (used to obtain the raw JSON stream).</param>
/// <param name="Name">Human-readable language name, e.g. <c>C/C++</c>.</param>
/// <param name="Category">Logical grouping, e.g. <c>CLike</c>, <c>Script</c>.</param>
/// <param name="Extensions">File extensions associated with this language, e.g. <c>[".c", ".cpp"]</c>.</param>
public sealed record EmbeddedSyntaxEntry(
    string                  ResourceKey,
    string                  Name,
    string                  Category,
    IReadOnlyList<string>   Extensions);
