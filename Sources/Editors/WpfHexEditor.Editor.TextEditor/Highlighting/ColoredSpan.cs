//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

namespace WpfHexEditor.Editor.TextEditor.Highlighting;

/// <summary>
/// A colored text segment within a single line, produced by <see cref="RegexSyntaxHighlighter"/>.
/// </summary>
/// <param name="Start">Zero-based start index within the line string.</param>
/// <param name="Length">Length of the span in characters.</param>
/// <param name="ColorKey">Theme resource key (e.g. "TE_Keyword").</param>
public readonly record struct ColoredSpan(int Start, int Length, string ColorKey);
