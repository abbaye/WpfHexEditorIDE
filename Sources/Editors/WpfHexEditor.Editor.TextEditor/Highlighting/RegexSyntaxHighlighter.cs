//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.Text.RegularExpressions;

namespace WpfHexEditor.Editor.TextEditor.Highlighting;

/// <summary>
/// Tokenises a line of text using the rules from a <see cref="SyntaxDefinition"/> and returns
/// a list of non-overlapping <see cref="ColoredSpan"/>s sorted by start position.
/// Rules are applied in declaration order; already-covered character ranges are skipped.
/// </summary>
public sealed class RegexSyntaxHighlighter
{
    private readonly SyntaxDefinition _definition;

    /// <param name="definition">The syntax definition that drives highlighting.</param>
    public RegexSyntaxHighlighter(SyntaxDefinition definition)
        => _definition = definition ?? throw new ArgumentNullException(nameof(definition));

    /// <summary>
    /// Highlights a single line of text.
    /// </summary>
    /// <param name="line">The raw text of the line (without trailing newline).</param>
    /// <returns>Sorted, non-overlapping colored spans. Empty list for empty/null input.</returns>
    public IReadOnlyList<ColoredSpan> Highlight(string? line)
    {
        if (string.IsNullOrEmpty(line) || _definition.Rules.Count == 0)
            return [];

        // Track which character positions are already covered.
        // For typical lines (< 1000 chars) a bit array is fast enough.
        var covered = new bool[line.Length];
        var spans = new List<ColoredSpan>();

        foreach (var rule in _definition.Rules)
        {
            if (string.IsNullOrEmpty(rule.Pattern))
                continue;

            try
            {
                foreach (Match m in rule.CompiledRegex.Matches(line))
                {
                    if (!m.Success || m.Length == 0)
                        continue;

                    // If a named group "1" exists (capture group), use it; otherwise use the full match.
                    var group = m.Groups.Count > 1 && m.Groups[1].Success ? m.Groups[1] : m.Groups[0];
                    var start = group.Index;
                    var end = start + group.Length;

                    if (start >= line.Length || end > line.Length)
                        continue;

                    // Check for overlap with already-covered positions.
                    bool overlaps = false;
                    for (int i = start; i < end; i++)
                    {
                        if (covered[i]) { overlaps = true; break; }
                    }
                    if (overlaps) continue;

                    // Mark as covered.
                    for (int i = start; i < end; i++)
                        covered[i] = true;

                    spans.Add(new ColoredSpan(start, group.Length, rule.ColorKey));
                }
            }
            catch (RegexMatchTimeoutException)
            {
                // Safety valve: skip rule if regex times out on a pathological line.
                // MatchCollection is lazy — the timeout fires during iteration, not during Matches().
                continue;
            }
        }

        spans.Sort(static (a, b) => a.Start.CompareTo(b.Start));
        return spans;
    }

    /// <summary>
    /// Highlights multiple lines at once. Each element in the returned list corresponds to the
    /// same-indexed line in <paramref name="lines"/>.
    /// </summary>
    public IReadOnlyList<IReadOnlyList<ColoredSpan>> HighlightLines(IReadOnlyList<string> lines)
    {
        var result = new IReadOnlyList<ColoredSpan>[lines.Count];
        for (int i = 0; i < lines.Count; i++)
            result[i] = Highlight(lines[i]);
        return result;
    }
}
