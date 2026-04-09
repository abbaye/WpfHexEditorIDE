// ==========================================================
// Project: WpfHexEditor.Editor.ClassDiagram
// File: Options/WhcdSerializer.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-04-08
// Description:
//     Saves and loads WhcdDocument to/from disk as indented JSON.
//     All IO failures are silently swallowed — a missing or corrupt
//     .whcd file simply means no layout is restored (graceful fallback).
//
// Architecture Notes:
//     Static utility — no state. Uses System.Text.Json (already referenced).
//     Path helpers are co-located here to keep all format knowledge in one place.
// ==========================================================

using System.IO;
using System.Text.Json;

namespace WpfHexEditor.Editor.ClassDiagram.Options;

/// <summary>
/// Reads and writes <see cref="WhcdDocument"/> twin files.
/// </summary>
public static class WhcdSerializer
{
    private static readonly JsonSerializerOptions _opts = new() { WriteIndented = true };

    // ── Path helpers ──────────────────────────────────────────────────────────

    /// <summary>Returns the .whcd path for a single source file: "Foo.cs" → "Foo.cs.whcd".</summary>
    public static string GetWhcdPath(string sourcePath) => sourcePath + ".whcd";

    /// <summary>
    /// Returns the .whcd path for a folder diagram:
    /// "C:\Src\MyLib\" → "C:\Src\MyLib\MyLib.whcd".
    /// </summary>
    public static string GetFolderWhcdPath(string folderPath)
    {
        string folder = folderPath.TrimEnd(
            Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return Path.Combine(folder, Path.GetFileName(folder) + ".whcd");
    }

    // ── IO ────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Serializes <paramref name="doc"/> to <paramref name="whcdPath"/>.
    /// Silently swallows any IO or serialization error.
    /// </summary>
    public static void Save(string whcdPath, WhcdDocument doc)
    {
        try
        {
            string? dir = Path.GetDirectoryName(whcdPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllText(whcdPath, JsonSerializer.Serialize(doc, _opts));
        }
        catch { /* non-critical — best effort */ }
    }

    /// <summary>
    /// Loads a <see cref="WhcdDocument"/> from <paramref name="whcdPath"/>.
    /// Returns <see langword="null"/> when the file does not exist or cannot be parsed.
    /// </summary>
    public static WhcdDocument? Load(string whcdPath)
    {
        try
        {
            if (!File.Exists(whcdPath)) return null;
            return JsonSerializer.Deserialize<WhcdDocument>(File.ReadAllText(whcdPath));
        }
        catch { return null; }
    }
}
