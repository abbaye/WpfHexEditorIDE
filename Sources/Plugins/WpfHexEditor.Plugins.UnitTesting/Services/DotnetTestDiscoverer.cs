// ==========================================================
// Project: WpfHexEditor.Plugins.UnitTesting
// File: Services/DotnetTestDiscoverer.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-24
// Updated: 2026-03-25 (BUG — MSTest 4.x short names + Spanish locale header)
// Description:
//     Discovers tests via `dotnet test --list-tests` without running them.
//     Fast path: --no-build (already-built project, instant).
//     Fallback:  no --no-build flag (lets dotnet build first when output is stale).
//
//     MSTest 4.x quirk: outputs short method names only (no namespace/class prefix).
//     A source-scan fallback resolves [TestClass] → class name for such projects.
//
//     Locale-agnostic: detects any list header ending with ':' that mentions
//     "test" or "prueba" — covers English, Spanish, French, German, etc.
// ==========================================================

using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using WpfHexEditor.Plugins.UnitTesting.Models;

namespace WpfHexEditor.Plugins.UnitTesting.Services;

/// <summary>
/// Discovers test cases via <c>dotnet test --list-tests</c> without executing them.
/// Uses a two-pass strategy: fast <c>--no-build</c> first, then falls back to a full
/// build pass when the project output is missing or stale.
/// </summary>
public sealed class DotnetTestDiscoverer
{
    public async Task<IReadOnlyList<DiscoveredTest>> DiscoverAsync(
        string            projectFilePath,
        CancellationToken ct = default)
    {
        // Fast path — already-built projects respond in < 1 s.
        var result = await RunListTestsAsync(projectFilePath, noBuild: true, ct)
                          .ConfigureAwait(false);

        // Fallback — allow dotnet to build first if nothing was found.
        if (result.Count == 0)
            result = await RunListTestsAsync(projectFilePath, noBuild: false, ct)
                          .ConfigureAwait(false);

        return result;
    }

    private static async Task<IReadOnlyList<DiscoveredTest>> RunListTestsAsync(
        string            projectFilePath,
        bool              noBuild,
        CancellationToken ct)
    {
        var noBuildArg   = noBuild ? " --no-build" : string.Empty;
        var verbosityArg = noBuild ? string.Empty  : " --verbosity quiet";
        var args         = $"test \"{projectFilePath}\" --list-tests{noBuildArg}{verbosityArg}";
        var psi          = new ProcessStartInfo("dotnet", args)
        {
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            UseShellExecute        = false,
            CreateNoWindow         = true,
        };

        using var proc = new Process { StartInfo = psi };
        proc.Start();

        // Drain stderr concurrently to avoid deadlock.
        var stderrTask = proc.StandardError.ReadToEndAsync(ct);

        // Accepts ≥2-space indentation: VSTest classic uses 4 spaces, MSTest 4.x may use 2.
        static bool IsIndented(string s) => s.Length >= 2 && s[0] == ' ' && s[1] == ' ';

        // Locale-agnostic header: any non-indented line ending with ':' that contains
        // "test" (English/French/German) or "prueba" (Spanish).
        static bool IsListHeader(string raw, string trimmed) =>
            !IsIndented(raw)
            && trimmed.Length > 1
            && trimmed[^1] == ':'
            && (trimmed.IndexOf("test",   StringComparison.OrdinalIgnoreCase) >= 0
             || trimmed.IndexOf("prueba", StringComparison.OrdinalIgnoreCase) >= 0);

        var names  = new List<string>();
        var inList = false;
        string? line;
        while ((line = await proc.StandardOutput.ReadLineAsync(ct).ConfigureAwait(false)) is not null)
        {
            if (!inList)
            {
                var trimmedHeader = line.TrimStart();

                // Primary: locale-agnostic header detection.
                if (IsListHeader(line, trimmedHeader))
                {
                    inList = true;
                    continue;
                }

                // Locale fallback: any indented line with no path separators, colons, or spaces
                // IS a test name.  Spaces excluded to filter build-status lines ("Build succeeded").
                // Dot requirement removed — MSTest 4.x outputs short method names without dots.
                var trimmedFb = line.TrimStart();
                if (IsIndented(line)
                    && trimmedFb.Length > 0
                    && !trimmedFb.Contains(':')
                    && !trimmedFb.Contains('\\')
                    && !trimmedFb.Contains('/')
                    && !trimmedFb.Contains(' '))
                {
                    inList = true;
                    // fall through — first test name is on this line
                }
            }

            if (inList && IsIndented(line))
            {
                var name = line.Trim();
                // Strip optional MTP timing suffix: " [42ms]" or " [1.2s]"
                var bracketIdx = name.LastIndexOf(" [", StringComparison.Ordinal);
                if (bracketIdx > 0 && name.EndsWith(']'))
                    name = name[..bracketIdx];
                if (!string.IsNullOrWhiteSpace(name))
                    names.Add(name);
            }
        }

        await stderrTask.ConfigureAwait(false);

        try
        {
            await proc.WaitForExitAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            try { proc.Kill(entireProcessTree: true); } catch { /* ignore */ }
            throw;
        }

        var projectName = Path.GetFileNameWithoutExtension(projectFilePath);

        // MSTest 4.x emits short method names (no namespace/class prefix).
        // Build a source-code class map to resolve method → class for those names.
        var needsClassMap = names.Any(n => !n.Contains('.') && !n.Contains('('));
        var sourceClassMap = needsClassMap
            ? BuildSourceClassMap(projectFilePath)
            : null;

        return names
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Select(n => SplitFullName(n, projectName, sourceClassMap))
            .ToList();
    }

    /// <summary>
    /// Splits "Namespace.ClassName.MethodName(params)" into (ClassName, TestName).
    /// For MSTest short names (no dots), looks up the class in <paramref name="sourceClassMap"/>.
    /// </summary>
    private static DiscoveredTest SplitFullName(
        string fullName,
        string projectName,
        IReadOnlyDictionary<string, string>? sourceClassMap)
    {
        var parenIdx = fullName.IndexOf('(');
        var searchIn = parenIdx >= 0 ? fullName[..parenIdx] : fullName;
        var dotIdx   = searchIn.LastIndexOf('.');

        if (dotIdx < 0)
        {
            // MSTest short name — look up class from source scan.
            var cls = sourceClassMap?.GetValueOrDefault(searchIn) ?? string.Empty;
            return new(projectName, cls, fullName);
        }

        return new(projectName, fullName[..dotIdx], fullName[(dotIdx + 1)..]);
    }

    /// <summary>
    /// Scans all <c>.cs</c> files in the project directory and builds a map of
    /// <c>methodName → className</c> by reading <c>[TestClass]</c> + <c>[TestMethod]</c> attributes.
    /// Used to recover class names for MSTest 4.x short-name output.
    /// </summary>
    private static IReadOnlyDictionary<string, string> BuildSourceClassMap(string projectFilePath)
    {
        var map        = new Dictionary<string, string>(StringComparer.Ordinal);
        var projectDir = Path.GetDirectoryName(projectFilePath) ?? string.Empty;

        try
        {
            foreach (var csFile in Directory.EnumerateFiles(
                         projectDir, "*.cs", SearchOption.AllDirectories))
            {
                try   { ParseTestClassMap(File.ReadAllLines(csFile), map); }
                catch { /* skip unreadable files */ }
            }
        }
        catch { /* ignore access errors */ }

        return map;
    }

    private static void ParseTestClassMap(string[] lines, Dictionary<string, string> map)
    {
        string? currentClass     = null;
        bool    nextIsTestMethod = false;

        foreach (var raw in lines)
        {
            var trimmed = raw.Trim();

            // Track current class declaration.
            var cm = Regex.Match(trimmed, @"\bclass\s+(\w+)");
            if (cm.Success)
            {
                currentClass     = cm.Groups[1].Value;
                nextIsTestMethod = false;
                continue;
            }

            // Detect [TestMethod] / [DataTestMethod] attribute on the preceding line.
            if (trimmed is "[TestMethod]" or "[DataTestMethod]"
             || trimmed.StartsWith("[TestMethod(",    StringComparison.Ordinal)
             || trimmed.StartsWith("[DataTestMethod(", StringComparison.Ordinal))
            {
                nextIsTestMethod = true;
                continue;
            }

            if (nextIsTestMethod && currentClass is not null)
            {
                var mm = Regex.Match(trimmed, @"\bpublic\b.*?\s(\w+)\s*[\(<]");
                if (mm.Success)
                    map.TryAdd(mm.Groups[1].Value, currentClass);

                // Stay in nextIsTestMethod state if the next line is still an attribute;
                // otherwise reset.
                nextIsTestMethod = trimmed.StartsWith("[", StringComparison.Ordinal);
            }
            else if (!trimmed.StartsWith("[", StringComparison.Ordinal))
            {
                nextIsTestMethod = false;
            }
        }
    }
}
