// ==========================================================
// Project: WpfHexEditor.App
// File: Services/RoamingDataHealthChecker.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-05-20
// Description:
//     Silent health check for the four managed roaming files.
//     Each file is validated by attempting to deserialize it.
//     Missing files are considered healthy (first-run scenario).
// ==========================================================

using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using WpfHexEditor.Core.Options;
using WpfHexEditor.Docking.Core.Serialization;

namespace WpfHexEditor.App.Services;

/// <summary>
/// Validates roaming data files without modifying them.
/// Used at startup to surface silent corruption via the InfoBar.
/// </summary>
internal static class RoamingDataHealthChecker
{
    public sealed record HealthReport(IReadOnlyList<string> CorruptFiles)
    {
        public bool IsHealthy => CorruptFiles.Count == 0;
    }

    private static readonly JsonSerializerOptions _settingsOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>
    /// Attempts to deserialize each managed roaming file.
    /// Returns a report listing any files that fail parsing.
    /// Missing files are considered healthy.
    /// </summary>
    public static HealthReport Check()
    {
        var root    = RoamingDataBackupService.RoamingRoot;
        var corrupt = new List<string>();

        CheckFile(root, "settings.json",
            text => JsonSerializer.Deserialize<AppSettings>(text, _settingsOptions),
            corrupt);

        CheckFile(root, Path.Combine("App", "layout.json"),
            text => DockLayoutSerializer.Deserialize(text),
            corrupt);

        // session.json and plugin-isolation-overrides.json — validate as JSON documents
        CheckFile(root, Path.Combine("App", "session.json"),
            text => { using var _ = JsonDocument.Parse(text); },
            corrupt);

        CheckFile(root, "plugin-isolation-overrides.json",
            text => { using var _ = JsonDocument.Parse(text); },
            corrupt);

        return new HealthReport(corrupt);
    }

    private static void CheckFile(string root, string relative, Action<string> parse, List<string> corrupt)
    {
        var path = Path.Combine(root, relative);
        if (!File.Exists(path)) return;

        try
        {
            parse(File.ReadAllText(path));
        }
        catch (Exception ex)
        {
            OutputLogger.Warn($"[RoamingHealth] '{relative}' failed validation: {ex.Message}");
            corrupt.Add(relative);
        }
    }
}
