// ==========================================================
// Project: WpfHexEditor.Plugins.AIAssistant
// File: MentionParser.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.6
// Created: 2026-03-31
// License: GNU Affero General Public License v3.0 (AGPL-3.0)
// Description:
//     Parses @mentions in user input text (e.g. @selection, @file:path, @errors).
// ==========================================================
using System.Text.RegularExpressions;

namespace WpfHexEditor.Plugins.AIAssistant.Context;

public static partial class MentionParser
{
    public static List<MentionToken> Parse(string input)
    {
        var tokens = new List<MentionToken>();

        foreach (Match m in MentionRegex().Matches(input))
        {
            var kind = m.Groups["kind"].Value.ToLowerInvariant() switch
            {
                "file" => ContextKind.File,
                "selection" => ContextKind.Selection,
                "errors" => ContextKind.Errors,
                "solution" => ContextKind.Solution,
                "hex" => ContextKind.Hex,
                _ => (ContextKind?)null
            };

            if (kind is not null)
            {
                var arg = m.Groups["arg"].Success ? m.Groups["arg"].Value : null;
                tokens.Add(new MentionToken(kind.Value, arg, m.Index, m.Length));
            }
        }

        return tokens;
    }

    public static string RemoveMentions(string input) => MentionRegex().Replace(input, "").Trim();

    [GeneratedRegex(@"@(?<kind>file|selection|errors|solution|hex)(?::(?<arg>[^\s]+))?", RegexOptions.IgnoreCase)]
    private static partial Regex MentionRegex();
}

public sealed record MentionToken(ContextKind Kind, string? Argument, int Start, int Length);
