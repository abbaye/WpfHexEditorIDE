// ==========================================================
// Project: WpfHexEditor.Plugins.AIAssistant
// File: ApplyCodeCommand.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.6
// Created: 2026-03-31
// License: GNU Affero General Public License v3.0 (AGPL-3.0)
// Description:
//     Parses fenced code blocks from assistant responses and opens a diff preview
//     via IDiffService so the user can review before applying changes.
// ==========================================================
using System.IO;
using System.Text.RegularExpressions;
using WpfHexEditor.SDK.Contracts;

namespace WpfHexEditor.Plugins.AIAssistant.Commands;

public static partial class ApplyCodeCommand
{
    public static async Task ApplyAsync(string responseText, IIDEHostContext context, CancellationToken ct = default)
    {
        var codeBlocks = ExtractCodeBlocks(responseText);
        if (codeBlocks.Count == 0) return;

        var currentFile = context.CodeEditor?.CurrentFilePath;
        if (string.IsNullOrEmpty(currentFile)) return;

        var diffService = context.DiffService;
        if (diffService is null) return;

        // Use the first code block as the proposed new content
        var proposed = codeBlocks[0].Code;

        // Write to a temp file for diff comparison
        var tempDir = Path.Combine(Path.GetTempPath(), "WpfHexEditor", "AIAssistant");
        Directory.CreateDirectory(tempDir);
        var tempFile = Path.Combine(tempDir, $"proposed_{Path.GetFileName(currentFile)}");
        await File.WriteAllTextAsync(tempFile, proposed, ct);

        await diffService.OpenInViewerAsync(currentFile, tempFile, ct);
    }

    public static List<CodeBlock> ExtractCodeBlocks(string text)
    {
        var blocks = new List<CodeBlock>();
        foreach (Match m in FencedCodeRegex().Matches(text))
        {
            var lang = m.Groups["lang"].Value;
            var code = m.Groups["code"].Value.Trim();
            blocks.Add(new CodeBlock(lang, code));
        }
        return blocks;
    }

    [GeneratedRegex(@"```(?<lang>\w*)\r?\n(?<code>[\s\S]*?)```", RegexOptions.Multiline)]
    private static partial Regex FencedCodeRegex();
}

public sealed record CodeBlock(string Language, string Code);
