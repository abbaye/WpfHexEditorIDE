// ==========================================================
// Project: WpfHexEditor.PluginHost
// File: DevTools/WhxpluginPackager.cs
// Description:
//     Packages a built plugin folder into a .whxplugin ZIP archive
//     containing manifest.json, the entry-assembly DLL, satellites, and
//     any extra runtime libraries.
// ==========================================================

using System.IO;
using System.IO.Compression;

namespace WpfHexEditor.PluginHost.DevTools;

public sealed record PackResult(string OutputPath, bool Success, string? Error = null);

/// <summary>Builds <c>.whxplugin</c> packages from a plugin source folder.</summary>
public sealed class WhxpluginPackager
{
    /// <summary>
    /// Packs <paramref name="pluginDir"/> into a <c>.whxplugin</c> ZIP at
    /// <paramref name="outputPath"/>. Looks for manifest.json and includes
    /// the produced binaries under bin/Release|Debug/net8.0-windows/.
    /// </summary>
    public PackResult Pack(string pluginDir, string outputPath)
    {
        try
        {
            var manifest = Path.Combine(pluginDir, "manifest.json");
            if (!File.Exists(manifest))
                return new PackResult(outputPath, false, "manifest.json not found in plugin directory.");

            var binaries = FindBinaries(pluginDir);
            if (binaries.Count == 0)
                return new PackResult(outputPath, false, "No built binaries found — run `dotnet build` first.");

            if (File.Exists(outputPath)) File.Delete(outputPath);

            using var archive = ZipFile.Open(outputPath, ZipArchiveMode.Create);
            archive.CreateEntryFromFile(manifest, "manifest.json", CompressionLevel.Optimal);

            foreach (var (path, rel) in binaries)
                archive.CreateEntryFromFile(path, rel, CompressionLevel.Optimal);

            return new PackResult(outputPath, true);
        }
        catch (Exception ex)
        {
            return new PackResult(outputPath, false, ex.Message);
        }
    }

    private static readonly HashSet<string> BinaryExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".dll", ".pdb", ".json", ".xml" };

    private static readonly string[] BuildConfigs = ["Release", "Debug"];

    private static List<(string Path, string Rel)> FindBinaries(string pluginDir)
    {
        foreach (var config in BuildConfigs)
        {
            var dir = Path.Combine(pluginDir, "bin", config, "net8.0-windows");
            if (!Directory.Exists(dir)) continue;

            var list = new List<(string, string)>();
            foreach (var file in Directory.EnumerateFiles(dir, "*.*", SearchOption.AllDirectories))
            {
                if (!BinaryExtensions.Contains(Path.GetExtension(file))) continue;
                if (Path.GetFileName(file).EndsWith(".deps.json", StringComparison.OrdinalIgnoreCase)) continue;

                var rel = Path.GetRelativePath(dir, file).Replace(Path.DirectorySeparatorChar, '/');
                list.Add((file, rel));
            }
            if (list.Count > 0) return list;
        }
        return [];
    }
}
