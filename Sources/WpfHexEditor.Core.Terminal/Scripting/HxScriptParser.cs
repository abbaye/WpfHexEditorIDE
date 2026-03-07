//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using WpfHexEditor.Core.Terminal.Scripting.ScriptInstructions;

namespace WpfHexEditor.Core.Terminal.Scripting;

/// <summary>
/// Parses a .hxscript source into a flat list of <see cref="IScriptInstruction"/>.
///
/// Supported syntax:
///   command arg1 arg2       — command invocation
///   # comment               — ignored
///   sleep &lt;ms&gt;              — delay
///   if &lt;exitcode&gt;           — conditional block (else/endif close)
///   loop &lt;n&gt;               — repetition block (endloop closes)
/// </summary>
public sealed class HxScriptParser
{
    public IReadOnlyList<IScriptInstruction> Parse(string source, int lastExitCode = 0)
    {
        var lines = source.Split('\n').Select(l => l.Trim()).ToList();
        var (instructions, _) = ParseBlock(lines, 0, lines.Count, ref lastExitCode);
        return instructions;
    }

    private static (List<IScriptInstruction> instructions, int nextIndex) ParseBlock(
        List<string> lines, int start, int end, ref int lastExitCode)
    {
        var result = new List<IScriptInstruction>();
        int i = start;

        while (i < end)
        {
            var line = lines[i];

            // Skip blank lines and comments
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
            {
                i++;
                continue;
            }

            var parts = SplitArgs(line);
            var keyword = parts[0].ToLowerInvariant();

            if (keyword == "sleep")
            {
                int ms = parts.Length > 1 && int.TryParse(parts[1], out var v) ? v : 1000;
                result.Add(new SleepInstruction(ms));
                i++;
                continue;
            }

            if (keyword == "if")
            {
                int code = parts.Length > 1 && int.TryParse(parts[1], out var c) ? c : 0;

                // Scan for else/endif
                int thenEnd = FindKeyword(lines, i + 1, end, "else", "endif");
                int ifEnd   = FindKeyword(lines, thenEnd + 1, end, "endif");

                int lec = lastExitCode;
                var (thenBranch, _) = ParseBlock(lines, i + 1, thenEnd, ref lec);
                List<IScriptInstruction> elseBranch = [];

                if (thenEnd < end && lines[thenEnd].Trim().ToLowerInvariant() == "else")
                {
                    lec = lastExitCode;
                    (elseBranch, _) = ParseBlock(lines, thenEnd + 1, ifEnd, ref lec);
                }

                result.Add(new IfInstruction(code, thenBranch, elseBranch, lastExitCode));
                i = ifEnd + 1;
                continue;
            }

            if (keyword == "loop")
            {
                int count = parts.Length > 1 && int.TryParse(parts[1], out var n) ? n : 1;
                int loopEnd = FindKeyword(lines, i + 1, end, "endloop");
                int lec = lastExitCode;
                var (body, _) = ParseBlock(lines, i + 1, loopEnd, ref lec);
                result.Add(new LoopInstruction(count, body));
                i = loopEnd + 1;
                continue;
            }

            // Regular command
            result.Add(new CommandInstruction(parts[0], parts.Skip(1).ToArray()));
            i++;
        }

        return (result, i);
    }

    private static int FindKeyword(List<string> lines, int start, int end, params string[] keywords)
    {
        for (int i = start; i < end; i++)
        {
            var kw = lines[i].Trim().ToLowerInvariant();
            if (keywords.Contains(kw)) return i;
        }
        return end; // not found → return end sentinel
    }

    private static string[] SplitArgs(string line)
    {
        // Simple tokenizer — splits on spaces, respects double-quoted tokens
        var args = new List<string>();
        bool inQuote = false;
        var current = new System.Text.StringBuilder();

        foreach (var ch in line)
        {
            if (ch == '"') { inQuote = !inQuote; continue; }
            if (ch == ' ' && !inQuote)
            {
                if (current.Length > 0) { args.Add(current.ToString()); current.Clear(); }
                continue;
            }
            current.Append(ch);
        }
        if (current.Length > 0) args.Add(current.ToString());
        return [.. args];
    }
}
