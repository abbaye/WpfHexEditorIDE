// ==========================================================
// Project: whfmt.Backfill
// File: Emit/WhfmtAppender.cs
// Description: Surgically inserts new top-level JSON properties before the file's closing brace.
// Architecture: Pure text manipulation — preserves comments, key order, formatting of original.
// ==========================================================

using System.Text;

namespace WhfmtBackfill.Emit;

/// <summary>Appends new top-level JSON properties to a .whfmt file by inserting before the final closing brace.</summary>
public static class WhfmtAppender
{
    /// <summary>
    /// Insert <paramref name="propertyFragments"/> as new top-level JSON properties.
    /// Each fragment must be a complete <c>"key": value</c> pair (no trailing comma).
    /// </summary>
    public static string Append(string original, IReadOnlyList<string> propertyFragments)
    {
        if (propertyFragments.Count == 0) return original;

        int closingBrace = FindFinalClosingBrace(original);
        if (closingBrace < 0)
            throw new InvalidOperationException("Could not locate the final closing brace of the JSON document.");

        // Find the last non-whitespace character before the closing brace.
        // If it's a "," we drop it (trailing comma); otherwise we ensure the previous property has a trailing comma.
        int lastContent = closingBrace - 1;
        while (lastContent >= 0 && char.IsWhiteSpace(original[lastContent])) lastContent--;

        var sb = new StringBuilder(original.Length + propertyFragments.Sum(f => f.Length) + 32);

        if (lastContent < 0)
        {
            // Empty object — should not happen for whfmt, but be safe.
            sb.Append("{\n");
            for (int i = 0; i < propertyFragments.Count; i++)
            {
                sb.Append(propertyFragments[i]);
                if (i < propertyFragments.Count - 1) sb.Append(',');
                sb.Append('\n');
            }
            sb.Append("}\n");
            return sb.ToString();
        }

        // Append head (everything up to and including last content char)
        sb.Append(original, 0, lastContent + 1);

        // Ensure a trailing comma after the previous property
        if (original[lastContent] != ',') sb.Append(',');

        sb.Append('\n');

        // Append fragments separated by commas
        for (int i = 0; i < propertyFragments.Count; i++)
        {
            sb.Append(propertyFragments[i]);
            if (i < propertyFragments.Count - 1) sb.Append(',');
            sb.Append('\n');
        }

        // Append closing brace and any trailing content (e.g. trailing newline)
        sb.Append(original, closingBrace, original.Length - closingBrace);

        return sb.ToString();
    }

    /// <summary>
    /// Locate the final top-level <c>}</c>. Walks the file backwards skipping line/block comments and trailing whitespace.
    /// Returns -1 if not found.
    /// </summary>
    public static int FindFinalClosingBrace(string text)
    {
        for (int i = text.Length - 1; i >= 0; i--)
        {
            char c = text[i];
            if (char.IsWhiteSpace(c)) continue;
            if (c == '}') return i;
            // Anything else after the final } is unexpected; bail.
            return -1;
        }
        return -1;
    }
}
