// ==========================================================
// Project: WpfHexEditor.Plugins.SolutionLoader.VS
// File: SlnFileEditor.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.6
// Created: 2026-04-05
// Description:
//     Static helpers for modifying .sln files on disk (remove project,
//     rename project). Used when the IDE persists structural changes
//     back to a Visual Studio solution file.
//
// Architecture Notes:
//     String-based manipulation (same approach as DotNetProjectTemplate.PatchSlnAsync).
//     Preserves encoding (BOM detection + round-trip).
// ==========================================================

using System.Text;
using System.Text.RegularExpressions;

namespace WpfHexEditor.Plugins.SolutionLoader.VS;

/// <summary>
/// Modifies Visual Studio <c>.sln</c> files on disk — remove or rename projects.
/// </summary>
internal static class SlnFileEditor
{
    /// <summary>
    /// Removes a project entry and its configuration lines from the <c>.sln</c> file.
    /// </summary>
    public static async Task RemoveProjectAsync(
        string slnPath, string projectName, string projectGuid, CancellationToken ct = default)
    {
        var (content, encoding) = await ReadWithEncodingAsync(slnPath, ct);

        // Remove Project(...)...EndProject block.
        var escapedName = Regex.Escape(projectName);
        var projectBlockPattern = $@"Project\([^)]+\)\s*=\s*""{escapedName}""[^\r\n]*\r?\n(.*?\r?\n)?EndProject\r?\n";
        content = Regex.Replace(content, projectBlockPattern, "", RegexOptions.Singleline);

        // Remove all configuration lines referencing this project GUID.
        if (!string.IsNullOrEmpty(projectGuid))
        {
            var guidPattern = Regex.Escape(projectGuid);
            // Lines like: {GUID}.Debug|Any CPU.ActiveCfg = ...
            content = Regex.Replace(content, $@"^\s*{guidPattern}\.[^\r\n]*\r?\n", "",
                RegexOptions.Multiline);

            // NestedProjects lines: {child} = {parent} where child or parent matches.
            content = Regex.Replace(content, $@"^\s*\{{{guidPattern.TrimStart(@"\{").TrimEnd(@"\}")}\}}[^\r\n]*\r?\n", "",
                RegexOptions.Multiline);
        }

        await File.WriteAllTextAsync(slnPath, content, encoding, ct);
    }

    /// <summary>
    /// Renames a project entry in the <c>.sln</c> file (display name only — does not
    /// rename the .csproj file or directory on disk).
    /// </summary>
    public static async Task RenameProjectAsync(
        string slnPath, string oldName, string newName, CancellationToken ct = default)
    {
        var (content, encoding) = await ReadWithEncodingAsync(slnPath, ct);

        // Replace the project name in the Project(...) = "Name", "Path", "{GUID}" line.
        var escapedOld = Regex.Escape(oldName);
        content = Regex.Replace(content,
            $@"(Project\([^)]+\)\s*=\s*)""{escapedOld}""",
            $@"$1""{newName}""");

        await File.WriteAllTextAsync(slnPath, content, encoding, ct);
    }

    // -- Encoding helpers -----------------------------------------------------

    /// <summary>
    /// Reads a file and detects whether it uses UTF-8 with BOM.
    /// Returns the content and the appropriate encoding for round-trip writes.
    /// </summary>
    internal static async Task<(string Content, Encoding Encoding)> ReadWithEncodingAsync(
        string path, CancellationToken ct = default)
    {
        var bytes = await File.ReadAllBytesAsync(path, ct);
        var hasBom = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
        var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: hasBom);
        var content = encoding.GetString(hasBom ? bytes.AsSpan(3) : bytes);
        return (content, encoding);
    }
}
