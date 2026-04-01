// ==========================================================
// Project: WpfHexEditor.PluginDev
// File: Packaging/PluginPackager.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     Packages a built plugin into a distributable .whxplugin file.
//     A .whxplugin is a ZIP archive containing:
//       - All plugin DLLs from the build output directory
//       - plugin.manifest.json
//       - README.md (if present)
//
// Architecture Notes:
//     Pattern: Builder — assembles the archive from collected artifacts.
//     Validation checks manifest completeness before packaging.
// ==========================================================

using System.IO;
using System.IO.Compression;
using System.Text.Json;

namespace WpfHexEditor.PluginDev.Packaging;

/// <summary>
/// Packages a plugin build output into a <c>.whxplugin</c> ZIP archive.
/// </summary>
public sealed class PluginPackager
{
    // -----------------------------------------------------------------------
    // Constants
    // -----------------------------------------------------------------------

    private const string ManifestFileName = "plugin.manifest.json";
    private const string PackageExtension = ".whxplugin";

    // -----------------------------------------------------------------------
    // Public API
    // -----------------------------------------------------------------------

    /// <summary>
    /// Creates a <c>.whxplugin</c> archive from <paramref name="buildOutputDir"/>.
    /// </summary>
    /// <param name="buildOutputDir">Directory containing the compiled plugin DLLs.</param>
    /// <param name="projectDir">Project root directory (for manifest + README).</param>
    /// <param name="outputDir">Destination directory for the package (default: %AppData%\WpfHexEditor\Plugins\).</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task<PackageResult> PackageAsync(
        string            buildOutputDir,
        string            projectDir,
        string?           outputDir = null,
        CancellationToken ct        = default)
    {
        var errors = new List<string>();

        // ---- Validate manifest -----------------------------------------------
        var manifestPath = FindManifest(buildOutputDir, projectDir);
        if (manifestPath is null)
        {
            errors.Add($"'{ManifestFileName}' not found in output or project directory.");
            return new PackageResult(false, string.Empty, errors);
        }

        var validation = await ValidateManifestAsync(manifestPath, ct);
        if (!validation.IsValid)
            return new PackageResult(false, string.Empty, validation.Errors);

        // ---- Determine output path ------------------------------------------
        var dest = outputDir ?? DefaultPluginDir();
        Directory.CreateDirectory(dest);

        var packageName = $"{validation.PluginId}_{validation.PluginVersion}{PackageExtension}";
        var packagePath = Path.Combine(dest, packageName);

        if (File.Exists(packagePath)) File.Delete(packagePath);

        // ---- Collect artifacts ----------------------------------------------
        using var zip = ZipFile.Open(packagePath, ZipArchiveMode.Create);

        // DLLs from build output
        foreach (var dll in Directory.EnumerateFiles(buildOutputDir, "*.dll"))
        {
            ct.ThrowIfCancellationRequested();
            zip.CreateEntryFromFile(dll, Path.GetFileName(dll), CompressionLevel.Optimal);
        }

        // Manifest (from build output or project dir)
        zip.CreateEntryFromFile(manifestPath, ManifestFileName, CompressionLevel.Optimal);

        // README (optional)
        var readmePath = Path.Combine(projectDir, "README.md");
        if (File.Exists(readmePath))
            zip.CreateEntryFromFile(readmePath, "README.md", CompressionLevel.Optimal);

        return new PackageResult(true, packagePath, []);
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static string? FindManifest(string buildOutputDir, string projectDir)
    {
        // Prefer the manifest from the build output (in case it was modified during build).
        var inOutput  = Path.Combine(buildOutputDir, ManifestFileName);
        var inProject = Path.Combine(projectDir,     ManifestFileName);

        if (File.Exists(inOutput))  return inOutput;
        if (File.Exists(inProject)) return inProject;
        return null;
    }

    private static async Task<ManifestValidation> ValidateManifestAsync(
        string manifestPath, CancellationToken ct)
    {
        var errors = new List<string>();

        try
        {
            var json = await File.ReadAllTextAsync(manifestPath, ct);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var id      = GetString(root, "id");
            var name    = GetString(root, "name");
            var version = GetString(root, "version");
            var entry   = GetString(root, "entryPoint");

            if (string.IsNullOrWhiteSpace(id))      errors.Add("Manifest: 'id' is required.");
            if (string.IsNullOrWhiteSpace(name))    errors.Add("Manifest: 'name' is required.");
            if (string.IsNullOrWhiteSpace(version)) errors.Add("Manifest: 'version' is required.");
            if (string.IsNullOrWhiteSpace(entry))   errors.Add("Manifest: 'entryPoint' is required.");

            return new ManifestValidation(errors.Count == 0, id ?? string.Empty, version ?? string.Empty, errors);
        }
        catch (Exception ex)
        {
            errors.Add($"Manifest parse error: {ex.Message}");
            return new ManifestValidation(false, string.Empty, string.Empty, errors);
        }
    }

    private static string? GetString(JsonElement el, string prop)
        => el.TryGetProperty(prop, out var v) ? v.GetString() : null;

    private static string DefaultPluginDir()
        => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "WpfHexEditor", "Plugins");

    // -----------------------------------------------------------------------
    // Records
    // -----------------------------------------------------------------------

    private sealed record ManifestValidation(
        bool                  IsValid,
        string                PluginId,
        string                PluginVersion,
        IReadOnlyList<string> Errors);
}

/// <summary>Result of a package operation.</summary>
public sealed record PackageResult(
    bool                  IsSuccess,
    string                PackagePath,
    IReadOnlyList<string> Errors);
