// ==========================================================
// Project: WpfHexEditor.Editor.ClassDiagram
// File: Options/ClassDiagramSessionStateSerializer.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-04-07
// Description:
//     Reads and writes ClassDiagramSessionState to/from a JSON file
//     stored in %AppData%\WpfHexEditor\cd-session.json.
//     All failures are silently swallowed so that a corrupt file
//     never prevents the application from opening.
//
// Architecture Notes:
//     Static utility — no state. Uses System.Text.Json for zero-dep serialization.
// ==========================================================

using System.IO;
using System.Text.Json;

namespace WpfHexEditor.Editor.ClassDiagram.Options;

/// <summary>
/// Saves and loads <see cref="ClassDiagramSessionState"/> from disk.
/// </summary>
public static class ClassDiagramSessionStateSerializer
{
    private static readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    private static readonly string _fallbackDir =
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "WpfHexEditor");

    private static string GetSessionFilePath(string? solutionDir) =>
        Path.Combine(solutionDir ?? _fallbackDir, ".whidews", "cd-session.json");

    /// <summary>
    /// Persists <paramref name="state"/> to disk next to the solution folder.
    /// Falls back to %AppData%\WpfHexEditor when <paramref name="solutionDir"/> is null.
    /// Silently swallows any IO errors.
    /// </summary>
    public static void Save(ClassDiagramSessionState state, string? solutionDir = null)
    {
        try
        {
            string path = GetSessionFilePath(solutionDir);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            string json = JsonSerializer.Serialize(state, _jsonOptions);
            File.WriteAllText(path, json);
        }
        catch { /* non-critical — best effort */ }
    }

    /// <summary>
    /// Loads the persisted session state.
    /// Returns <see langword="null"/> when the file does not exist or cannot be parsed.
    /// </summary>
    public static ClassDiagramSessionState? Load(string? solutionDir = null)
    {
        try
        {
            string path = GetSessionFilePath(solutionDir);
            if (!File.Exists(path)) return null;
            string json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<ClassDiagramSessionState>(json);
        }
        catch { return null; }
    }
}
