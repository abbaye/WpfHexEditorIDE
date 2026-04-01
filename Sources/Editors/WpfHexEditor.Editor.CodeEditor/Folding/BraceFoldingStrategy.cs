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
            foreach (char ch in text)
            {
                if (ch == '{')
                {
                    // When '{' is the only non-whitespace char on this line (e.g. Allman style),
                    // attach the region to the keyword line above so that collapsing hides only
                    // the body, not the declaration itself.
                    bool braceAlone = text.Trim() == "{";
                    int effectiveStart = (braceAlone && i > 0) ? i - 1 : i;
                    stack.Push(effectiveStart);
                }
                else if (ch == '}' && stack.Count > 0)
                {
                    int startLine = stack.Pop();
                    if (i > startLine + 1) // must span at least 2 lines to be useful
                        regions.Add(new FoldingRegion(startLine, i, "{ \u2026 }"));
                }
            }
        }

        return regions;
    }
}
