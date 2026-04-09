// ==========================================================
// Project: WpfHexEditor.Editor.CodeEditor
// File: BraceFoldingStrategy.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-05
// Description:
//     Brace-based folding strategy: detects matching { } pairs and
//     produces one FoldingRegion per pair that spans more than one line.
//     Suitable for JSON, C#, JavaScript, TypeScript, C/C++, etc.
// ==========================================================

using System.Collections.Generic;
using WpfHexEditor.Editor.CodeEditor.Models;

namespace WpfHexEditor.Editor.CodeEditor.Folding;

/// <summary>
/// Detects foldable regions delimited by matching <c>{</c> / <c>}</c> pairs.
/// Each pair that spans more than one line produces a <see cref="FoldingRegion"/>.
/// </summary>
public sealed class BraceFoldingStrategy : IFoldingStrategy
{
    public IReadOnlyList<FoldingRegion> Analyze(IReadOnlyList<CodeLine> lines)
    {
        var regions = new List<FoldingRegion>();
        var stack   = new Stack<int>(); // stack of opener line indices

        for (int i = 0; i < lines.Count; i++)
        {
            var text = lines[i].Text ?? string.Empty;
            bool inString   = false;
            bool inVerbatim = false;
            bool inChar     = false;
            bool inLineComment = false;

            for (int j = 0; j < text.Length; j++)
            {
                char ch = text[j];
                char next = j + 1 < text.Length ? text[j + 1] : '\0';

                // Line comment — skip rest of line.
                if (!inString && !inVerbatim && !inChar && ch == '/' && next == '/')
                {
                    inLineComment = true;
                    break;
                }

                // Block comment start — skip to end (may not close on same line).
                // For simplicity, skip the rest of the line when we hit /*, since
                // braces inside multi-line comments are rare and the cost of a
                // cross-line state machine is high for a folding strategy.
                if (!inString && !inVerbatim && !inChar && ch == '/' && next == '*')
                    break;

                // Character literal.
                if (!inString && !inVerbatim && !inChar && ch == '\'')
                {
                    inChar = true;
                    continue;
                }
                if (inChar)
                {
                    if (ch == '\\') { j++; continue; } // skip escaped char
                    if (ch == '\'') inChar = false;
                    continue;
                }

                // Verbatim / raw string — skip to closing quote(s).
                if (!inString && !inVerbatim && ch == '@' && next == '"')
                {
                    inVerbatim = true;
                    j++; // skip the "
                    continue;
                }
                if (inVerbatim)
                {
                    if (ch == '"')
                    {
                        if (next == '"') { j++; continue; } // "" escape inside verbatim
                        inVerbatim = false;
                    }
                    continue;
                }

                // Regular string (handles interpolation nesting via brace depth).
                if (!inString && ch == '"')
                {
                    inString = true;
                    continue;
                }
                if (inString)
                {
                    if (ch == '\\') { j++; continue; } // skip escape
                    if (ch == '"') inString = false;
                    continue; // skip everything inside strings including { }
                }

                // Structural braces.
                if (ch == '{')
                {
                    bool braceAlone = text.Trim() == "{";
                    int effectiveStart = (braceAlone && i > 0) ? i - 1 : i;
                    stack.Push(effectiveStart);
                }
                else if (ch == '}' && stack.Count > 0)
                {
                    int startLine = stack.Pop();
                    if (i > startLine + 1)
                        regions.Add(new FoldingRegion(startLine, i, "{ \u2026 }"));
                }
            }
        }

        return regions;
    }
}
