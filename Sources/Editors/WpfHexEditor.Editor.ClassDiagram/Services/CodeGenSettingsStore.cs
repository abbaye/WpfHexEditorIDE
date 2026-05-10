// ==========================================================
// Project: WpfHexEditor.Editor.ClassDiagram
// File: Services/CodeGenSettingsStore.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.7
// Created: 2026-05-08
// Description:
//     Loads and saves the user's last selected code-generation
//     options to a JSON file under %AppData%/WpfHexEditor/codegen-options.json.
//
// Architecture Notes:
//     CodeGenOptions is a record so we serialise it directly via
//     System.Text.Json. Failures are swallowed silently — settings
//     are convenience, not correctness.
// ==========================================================

using System.IO;
using System.Text.Json;
using WpfHexEditor.Editor.ClassDiagram.Core.CodeGen.Abstractions;
using WpfHexEditor.Editor.ClassDiagram.Core.CodeGen.Options;

namespace WpfHexEditor.Editor.ClassDiagram.Services;

/// <summary>Loads and saves the last selected code-generation options.</summary>
public static class CodeGenSettingsStore
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static string SettingsFolder => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "WpfHexEditor");

    private static string SettingsFile => Path.Combine(SettingsFolder, "codegen-options.json");

    /// <summary>Loads the stored settings, or returns the supplied defaults when no file exists.</summary>
    public static CodeGenSettings Load(CodeGenSettings fallback)
    {
        try
        {
            if (!File.Exists(SettingsFile))
                return fallback;
            var json = File.ReadAllText(SettingsFile);
            return JsonSerializer.Deserialize<CodeGenSettings>(json, Json) ?? fallback;
        }
        catch
        {
            return fallback;
        }
    }

    /// <summary>Persists the supplied settings to disk; swallows IO errors silently.</summary>
    public static void Save(CodeGenSettings settings)
    {
        try
        {
            Directory.CreateDirectory(SettingsFolder);
            var json = JsonSerializer.Serialize(settings, Json);
            File.WriteAllText(SettingsFile, json);
        }
        catch
        {
            // Settings persistence is best-effort.
        }
    }
}

/// <summary>Wraps a language id with the matching <see cref="CodeGenOptions"/>.</summary>
public sealed record CodeGenSettings
{
    /// <summary>The selected language id (see <see cref="LanguageIds"/>).</summary>
    public string LanguageId { get; init; } = LanguageIds.CSharp;

    /// <summary>The selected generator options.</summary>
    public CodeGenOptions Options { get; init; } = CodeGenOptions.Default;
}
