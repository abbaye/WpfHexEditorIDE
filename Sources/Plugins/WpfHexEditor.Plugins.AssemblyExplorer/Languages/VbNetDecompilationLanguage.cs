// ==========================================================
// Project: WpfHexEditor.Plugins.AssemblyExplorer
// File: Languages/VbNetDecompilationLanguage.cs
// Author: Derek Tremblay
// Created: 2026-03-16
// License: GNU Affero General Public License v3.0 (AGPL-3.0)
// Description:
//     IDecompilationLanguage implementation for VB.NET output.
//     Uses ICSharpCode.CodeConverter (Roslyn-based) to convert
//     the ILSpy C# output to VB.NET.
//     Falls back gracefully when conversion fails — returns the
//     original C# text with a diagnostic comment header.
//
// Architecture Notes:
//     Pattern: Strategy (IDecompilationLanguage).
//     CodeConverter.ConvertAsync is static; no instance allocation per call.
//     Contract: never throws except OperationCanceledException.
//     Registration: AssemblyExplorerPlugin.InitializeAsync →
//         DecompilationLanguageRegistry.Register(new VbNetDecompilationLanguage())
// ==========================================================

using System.Threading;
using System.Threading.Tasks;
using ICSharpCode.CodeConverter;
using WpfHexEditor.Core.AssemblyAnalysis.Languages;

namespace WpfHexEditor.Plugins.AssemblyExplorer.Languages;

/// <summary>
/// VB.NET decompilation language — converts ILSpy C# output to VB.NET
/// via <c>ICSharpCode.CodeConverter</c>.
/// Falls back to the original C# with an error comment on any failure.
/// </summary>
public sealed class VbNetDecompilationLanguage : IDecompilationLanguage
{
    public string  Id                 => "VBNet";
    public string  DisplayName        => "VB.NET";
    public string  FileExtension      => ".vb";
    public string? EditorLanguageName => "VB.NET";
    public string  GlyphCode          => "\uE8C9"; // Segoe MDL2 "Tag"

    /// <inheritdoc/>
    /// <remarks>
    /// Calls <c>ICSharpCode.CodeConverter.CodeConverter.ConvertAsync</c> with
    /// language strings "C#" → "Visual Basic".  On any non-cancellation failure
    /// the method returns the original C# code prefixed by a diagnostic comment
    /// block, so the Code tab always shows something meaningful.
    /// </remarks>
    public async Task<(string code, bool success)> TransformFromCSharpAsync(
        string csharpCode, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(csharpCode))
            return (csharpCode, true);

        try
        {
            var options = new CodeWithOptions(csharpCode)
                .SetFromLanguage("C#")
                .SetToLanguage("Visual Basic");

            var result = await CodeConverter.ConvertAsync(options, ct).ConfigureAwait(false);

            if (result.Success && !string.IsNullOrEmpty(result.ConvertedCode))
                return (result.ConvertedCode, true);

            // Conversion produced errors — annotate and return original C#
            var errorHeader = BuildErrorHeader(result);
            return ($"{errorHeader}{csharpCode}", false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Unwrap the full exception chain so the diagnostic comment reveals the root cause
            // (e.g. TargetInvocationException wraps the real FileNotFoundException / TypeLoadException).
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("' VB.NET conversion failed:");
            var current = ex;
            var depth = 0;
            while (current != null && depth < 6)
            {
                var indent = depth == 0 ? "'   " : "'   " + new string(' ', depth * 2);
                sb.AppendLine($"{indent}{current.GetType().Name}: {current.Message}");
                current = current.InnerException;
                depth++;
            }
            sb.AppendLine("' Showing original C# source instead.");
            sb.AppendLine();
            return (sb.ToString() + csharpCode, false);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string BuildErrorHeader(ConversionResult result)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("' VB.NET conversion encountered errors:");
        if (result.Exceptions is { Count: > 0 } exceptions)
        {
            foreach (var msg in exceptions)
                sb.AppendLine($"'   {msg}");
        }
        sb.AppendLine("' Showing original C# source instead.");
        sb.AppendLine();
        return sb.ToString();
    }
}
