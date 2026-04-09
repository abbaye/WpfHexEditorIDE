// ==========================================================
// Project: WpfHexEditor.Editor.ClassDiagram
// File: Options/WhscdSerializer.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-04-08
// Description:
//     Saves and loads WhscdDocument to/from disk as indented JSON.
//     All IO failures are silently swallowed — a missing or corrupt
//     .whscd file simply means no state is restored (graceful fallback).
//
// Architecture Notes:
//     Static utility — no state. Mirrors WhcdSerializer patterns.
//     Path convention: MySolution.whsln → MySolution.whsln.whscd
//     (appends ".whscd" to the solution file path).
// ==========================================================

using System.IO;
using System.Text.Json;

namespace WpfHexEditor.Editor.ClassDiagram.Options;

/// <summary>
/// Reads and writes <see cref="WhscdDocument"/> solution twin files.
/// </summary>
public static class WhscdSerializer
{
    private static readonly JsonSerializerOptions _opts = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    // ── Path helpers ──────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the .whscd path for a solution file.
    /// <example><c>MySolution.whsln</c> → <c>MySolution.whsln.whscd</c></example>
    /// </summary>
    public static string GetWhscdPath(string solutionFilePath)
        => solutionFilePath + ".whscd";

    // ── IO ────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Serializes <paramref name="doc"/> to <paramref name="whscdPath"/>.
    /// Silently swallows any IO or serialization error.
    /// </summary>
    public static void Save(string whscdPath, WhscdDocument doc)
    {
        try
        {
            string? dir = Path.GetDirectoryName(whscdPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllText(whscdPath, JsonSerializer.Serialize(doc, _opts));
        }
        catch { /* non-critical — best effort */ }
    }

    /// <summary>
    /// Loads a <see cref="WhscdDocument"/> from <paramref name="whscdPath"/>.
    /// Returns <see langword="null"/> when the file does not exist or cannot be parsed.
    /// </summary>
    public static WhscdDocument? Load(string whscdPath)
    {
        try
        {
            if (!File.Exists(whscdPath)) return null;
            return JsonSerializer.Deserialize<WhscdDocument>(
                File.ReadAllText(whscdPath), _opts);
        }
        catch { return null; }
    }
}
