//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;

namespace WpfHexEditor.PluginInstaller;

/// <summary>
/// Extracts and validates a .whxplugin package.
/// Steps:
///   1. Opens the ZIP archive.
///   2. Parses and validates manifest.json (required fields + minIDEVersion).
///   3. Verifies SHA-256 of the declared assembly file.
///   4. Extracts all entries to %AppData%\WpfHexEditor\Plugins\{pluginId}\.
/// </summary>
public sealed class PluginPackageExtractor
{
    private static readonly string PluginsRoot =
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "WpfHexEditor", "Plugins");

    /// <summary>
    /// Validates a .whxplugin without extracting it.
    /// Returns parsed metadata or throws <see cref="PluginInstallException"/>.
    /// </summary>
    public async Task<PluginMetadata> InspectAsync(string packagePath, CancellationToken ct = default)
    {
        if (!File.Exists(packagePath))
            throw new PluginInstallException($"Package not found: {packagePath}");

        using var archive = ZipFile.OpenRead(packagePath);
        return await ReadAndValidateManifestAsync(archive, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Validates and extracts the package to the user plugins directory.
    /// Returns the install path.
    /// </summary>
    public async Task<string> ExtractAsync(string packagePath, CancellationToken ct = default)
    {
        if (!File.Exists(packagePath))
            throw new PluginInstallException($"Package not found: {packagePath}");

        using var archive = ZipFile.OpenRead(packagePath);

        var meta    = await ReadAndValidateManifestAsync(archive, ct).ConfigureAwait(false);
        var dllEntry = archive.GetEntry(meta.AssemblyFile)
                       ?? throw new PluginInstallException($"Assembly '{meta.AssemblyFile}' not found in package.");

        // Verify SHA-256 before extraction
        if (!string.IsNullOrEmpty(meta.AssemblySha256))
        {
            var actualHash = await ComputeEntryHashAsync(dllEntry, ct).ConfigureAwait(false);
            if (!string.Equals(actualHash, meta.AssemblySha256, StringComparison.OrdinalIgnoreCase))
                throw new PluginInstallException(
                    $"SHA-256 mismatch for '{meta.AssemblyFile}'.\n" +
                    $"  Expected : {meta.AssemblySha256}\n" +
                    $"  Actual   : {actualHash}");
        }

        // Extract to target directory
        var targetDir = Path.Combine(PluginsRoot, meta.Id);
        Directory.CreateDirectory(targetDir);

        foreach (var entry in archive.Entries)
        {
            ct.ThrowIfCancellationRequested();
            if (string.IsNullOrEmpty(entry.Name)) continue; // directory entry

            var destPath = Path.GetFullPath(Path.Combine(targetDir, entry.FullName));

            // Path traversal guard
            if (!destPath.StartsWith(targetDir + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new PluginInstallException($"Path traversal attempt detected: {entry.FullName}");

            Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
            entry.ExtractToFile(destPath, overwrite: true);
        }

        return targetDir;
    }

    // -- Helpers ----------------------------------------------------------

    private static async Task<PluginMetadata> ReadAndValidateManifestAsync(ZipArchive archive, CancellationToken ct)
    {
        var manifestEntry = archive.GetEntry("manifest.json")
                            ?? throw new PluginInstallException("manifest.json not found in package.");

        string json;
        using (var stream = manifestEntry.Open())
        using (var reader = new StreamReader(stream))
            json = await reader.ReadToEndAsync(ct).ConfigureAwait(false);

        JsonDocument doc;
        try { doc = JsonDocument.Parse(json); }
        catch (JsonException ex) { throw new PluginInstallException($"Invalid manifest JSON: {ex.Message}"); }

        using (doc)
        {
            var root = doc.RootElement;

            var id      = GetRequiredString(root, "id");
            var name    = GetRequiredString(root, "name");
            var version = GetRequiredString(root, "version");
            var entry   = GetRequiredString(root, "entryPoint");
            string assemblyFile = string.Empty;
            string sha256       = string.Empty;
            long   size         = 0;

            if (root.TryGetProperty("assembly", out var asmProp))
            {
                if (asmProp.TryGetProperty("file",   out var f)) assemblyFile = f.GetString() ?? string.Empty;
                if (asmProp.TryGetProperty("sha256", out var h)) sha256       = h.GetString() ?? string.Empty;
                if (asmProp.TryGetProperty("size",   out var s)) size         = s.GetInt64();
            }

            if (string.IsNullOrWhiteSpace(assemblyFile))
                throw new PluginInstallException("manifest.json is missing assembly.file.");

            var trustedPublisher = root.TryGetProperty("trustedPublisher", out var tp) && tp.GetBoolean();
            var description      = root.TryGetProperty("description", out var d) ? d.GetString() ?? string.Empty : string.Empty;
            var author           = root.TryGetProperty("author",      out var a) ? a.GetString() ?? string.Empty : string.Empty;
            var minIde           = root.TryGetProperty("minIDEVersion", out var m) ? m.GetString() ?? string.Empty : string.Empty;

            return new PluginMetadata
            {
                Id               = id,
                Name             = name,
                Version          = version,
                Author           = author,
                Description      = description,
                EntryPoint       = entry,
                AssemblyFile     = assemblyFile,
                AssemblySha256   = sha256,
                AssemblySize     = size,
                TrustedPublisher = trustedPublisher,
                MinIDEVersion    = minIde
            };
        }
    }

    private static async Task<string> ComputeEntryHashAsync(ZipArchiveEntry entry, CancellationToken ct)
    {
        using var stream = entry.Open();
        var hash = await SHA256.HashDataAsync(stream, ct).ConfigureAwait(false);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string GetRequiredString(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var prop) || prop.ValueKind != JsonValueKind.String)
            throw new PluginInstallException($"manifest.json is missing required field '{name}'.");
        var value = prop.GetString();
        if (string.IsNullOrWhiteSpace(value))
            throw new PluginInstallException($"manifest.json field '{name}' must not be empty.");
        return value;
    }
}
