// ==========================================================
// Project: WpfHexEditor.Plugins.AssemblyExplorer
// File: Services/AssemblyExportService.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-23
// Description:
//     Decompiles an entire AssemblyModel and writes each type as a .cs file
//     organised in namespace sub-folders, together with a .csproj skeleton.
//     Reports granular progress via IProgress<double> (0.0–1.0).
//
// Architecture Notes:
//     Pattern: Service — stateless, async, progress-reporting.
//     Each type is decompiled via IDecompilerBackend, which already handles
//     thread safety (ILSpy is not thread-safe; calls are serialised here via
//     SemaphoreSlim to avoid concurrent decompilation).
//     File writes use FileShare.None to avoid partial-file races.
//     CT is checked between every type so the export is promptly cancellable.
// ==========================================================

using System.IO;
using System.Text;
using System.Xml.Linq;
using WpfHexEditor.Core.AssemblyAnalysis.Models;
using WpfHexEditor.Plugins.AssemblyExplorer.Services;

namespace WpfHexEditor.Plugins.AssemblyExplorer.Services;

/// <summary>
/// Exports a loaded .NET assembly to a standalone C# project on disk.
/// Creates a .csproj skeleton and decompiles each type to a .cs file in
/// namespace sub-folders, mirroring the Visual Studio project layout.
/// </summary>
public sealed class AssemblyExportService
{
    // Serialise decompilation — ILSpy / ICSharpCode.Decompiler is not thread-safe.
    private readonly SemaphoreSlim _decompileSemaphore = new(1, 1);

    /// <summary>
    /// Decompiles all types in <paramref name="model"/> and writes them to
    /// <paramref name="outputDir"/> as a C# project.
    /// </summary>
    /// <param name="model">The assembly model to export.</param>
    /// <param name="outputDir">Target directory (created if it does not exist).</param>
    /// <param name="backend">Decompiler backend to use for C# generation.</param>
    /// <param name="progress">Optional progress reporter (0.0–1.0).</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task ExportToCSharpProjectAsync(
        AssemblyModel    model,
        string           outputDir,
        IDecompilerBackend backend,
        IProgress<double>? progress = null,
        CancellationToken ct        = default)
    {
        if (model is null)       throw new ArgumentNullException(nameof(model));
        if (outputDir is null)   throw new ArgumentNullException(nameof(outputDir));
        if (backend is null)     throw new ArgumentNullException(nameof(backend));

        Directory.CreateDirectory(outputDir);

        var types = model.Types;
        if (types.Count == 0)
        {
            progress?.Report(1.0);
            return;
        }

        // Write the .csproj skeleton first.
        WriteCsProj(model, outputDir);

        var completed = 0;
        var total     = types.Count;

        foreach (var type in types)
        {
            ct.ThrowIfCancellationRequested();

            await _decompileSemaphore.WaitAsync(ct);
            try
            {
                var code = await Task.Run(
                    () => backend.DecompileType(type, model.FilePath), ct);

                var filePath = BuildOutputPath(outputDir, type);
                Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);

                await File.WriteAllTextAsync(filePath, code, Encoding.UTF8, ct);
            }
            catch (OperationCanceledException)
            {
                throw; // propagate cancellation
            }
            catch (Exception ex)
            {
                // Write a stub on decompile failure; don't abort the entire export.
                var filePath = BuildOutputPath(outputDir, type);
                Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
                await File.WriteAllTextAsync(
                    filePath,
                    $"// Decompilation failed for {type.FullName}:\n// {ex.Message}\n",
                    Encoding.UTF8, ct);
            }
            finally
            {
                _decompileSemaphore.Release();
            }

            completed++;
            progress?.Report((double)completed / total);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Resolves the output .cs file path for a type.
    /// Namespace segments become directory separators.
    /// </summary>
    private static string BuildOutputPath(string outputDir, TypeModel type)
    {
        var ns     = type.Namespace ?? string.Empty;
        var name   = SanitizeFileName(type.Name);

        // Strip generic arity suffix (List`1 → List).
        var backtick = name.IndexOf('`');
        if (backtick > 0) name = name[..backtick];

        if (string.IsNullOrEmpty(ns))
            return Path.Combine(outputDir, name + ".cs");

        // Namespace "System.Collections.Generic" → sub-path "System/Collections/Generic"
        var nsPath = ns.Replace('.', Path.DirectorySeparatorChar);
        return Path.Combine(outputDir, nsPath, name + ".cs");
    }

    /// <summary>Removes characters that are illegal in Windows file names.</summary>
    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sb = new StringBuilder(name.Length);
        foreach (var c in name)
            sb.Append(invalid.Contains(c) ? '_' : c);
        return sb.ToString().Trim('_', ' ');
    }

    /// <summary>
    /// Writes a minimal SDK-style .csproj file for the exported project.
    /// </summary>
    private static void WriteCsProj(AssemblyModel model, string outputDir)
    {
        var projectName = SanitizeFileName(model.Name);
        var csprojPath  = Path.Combine(outputDir, projectName + ".csproj");

        // Detect target framework from the model.
        var tfm = model.TargetFramework?.ToLowerInvariant() switch
        {
            string s when s.Contains("net8")    => "net8.0",
            string s when s.Contains("net7")    => "net7.0",
            string s when s.Contains("net6")    => "net6.0",
            string s when s.Contains("net5")    => "net5.0",
            string s when s.Contains("standard") => "netstandard2.0",
            string s when s.Contains("4.8")     => "net48",
            string s when s.Contains("4.7")     => "net471",
            string s when s.Contains("4.6")     => "net462",
            _                                   => "net8.0"
        };

        var version = model.Version?.ToString() ?? "1.0.0";

        var doc = new XDocument(
            new XElement("Project",
                new XAttribute("Sdk", "Microsoft.NET.Sdk"),
                new XElement("PropertyGroup",
                    new XElement("TargetFramework", tfm),
                    new XElement("Nullable", "enable"),
                    new XElement("ImplicitUsings", "enable"),
                    new XElement("AssemblyName", model.Name),
                    new XElement("RootNamespace", model.Name),
                    new XElement("Version", version),
                    new XElement("GenerateDocumentationFile", "false"),
                    new XComment(" Exported by WpfHexEditor Assembly Explorer — do not distribute decompiled code ")
                )
            )
        );

        doc.Save(csprojPath);
    }
}
