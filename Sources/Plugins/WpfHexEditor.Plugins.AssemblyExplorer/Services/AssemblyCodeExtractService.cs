// ==========================================================
// Project: WpfHexEditor.Plugins.AssemblyExplorer
// File: Services/AssemblyCodeExtractService.cs
// Author: Derek Tremblay
// Created: 2026-03-16
// Description:
//     Writes decompiled C# text to a WH native project via ISolutionManager,
//     or to disk via SaveFileDialog for VS/external projects.
//     Returns the absolute path of the created file, or null if cancelled.
//
// Architecture Notes:
//     Pattern: Service — stateless, receives dependencies per call.
//     WH project path: ISolutionManager.CreateItemAsync with ProjectItemType.Script.
//     VS/disk path: System.IO.File.WriteAllBytes after SaveFileDialog.
// ==========================================================

using System.IO;
using System.Text;
using Microsoft.Win32;
using WpfHexEditor.Core.AssemblyAnalysis.Languages;
using WpfHexEditor.Editor.Core;

namespace WpfHexEditor.Plugins.AssemblyExplorer.Services;

/// <summary>
/// Writes decompiled C# source to either a WH native project or a file on disk.
/// </summary>
public sealed class AssemblyCodeExtractService
{
    /// <summary>
    /// Extracts <paramref name="csharpText"/> as a <c>.cs</c> file into a WH native project.
    /// </summary>
    /// <returns>
    /// The absolute path of the created item on success, or <see langword="null"/> if the
    /// operation was skipped or failed.
    /// </returns>
    public async Task<string?> ExtractToWhProjectAsync(
        IProject        project,
        string          fileName,
        string          csharpText,
        ISolutionManager solutionManager,
        CancellationToken ct = default)
    {
        var content = Encoding.UTF8.GetBytes(csharpText);
        try
        {
            var item = await solutionManager.CreateItemAsync(
                project,
                fileName,
                ProjectItemType.Script,
                virtualFolderId:  null,
                initialContent:   content,
                ct:               ct);

            return item?.AbsolutePath;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to add '{fileName}' to project '{project.Name}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Saves <paramref name="sourceText"/> to a user-chosen path via SaveFileDialog.
    /// Must be called on the UI thread.
    /// </summary>
    /// <param name="suggestedFileName">Proposed file name (without extension — extension comes from <paramref name="language"/>).</param>
    /// <param name="sourceText">Decompiled source text to write.</param>
    /// <param name="language">Target language — controls file filter and default extension.</param>
    /// <returns>The saved file path, or <see langword="null"/> if the user cancelled.</returns>
    public string? ExtractToDiskViaSaveDialog(
        string                 suggestedFileName,
        string                 sourceText,
        IDecompilationLanguage? language = null)
    {
        language ??= CSharpDecompilationLanguage.Instance;

        var ext         = language.FileExtension;           // e.g. ".cs" or ".vb"
        var extNoDot    = ext.TrimStart('.');               // "cs" or "vb"
        var displayName = language.DisplayName;             // "C#" or "VB.NET"
        var filter      = $"{displayName} Source Files (*{ext})|*{ext}|All Files (*.*)|*.*";

        // Ensure the suggested file name has the correct extension.
        var baseName = Path.GetFileNameWithoutExtension(suggestedFileName);
        var fileName = baseName + ext;

        var dlg = new SaveFileDialog
        {
            Title           = "Extract Decompiled Code",
            Filter          = filter,
            DefaultExt      = extNoDot,
            FileName        = fileName,
            OverwritePrompt = true
        };

        if (dlg.ShowDialog() != true) return null;

        File.WriteAllBytes(dlg.FileName, Encoding.UTF8.GetBytes(sourceText));
        return dlg.FileName;
    }

    /// <summary>
    /// Derives a safe source file name from the node's display name,
    /// using the correct file extension for <paramref name="language"/>.
    /// Strips generic arity suffix (e.g. "`1") and removes illegal path characters.
    /// </summary>
    public static string SuggestFileName(string displayName, IDecompilationLanguage? language = null)
    {
        language ??= CSharpDecompilationLanguage.Instance;

        // Strip generic arity like List`1 → List
        var backtick = displayName.IndexOf('`');
        if (backtick > 0) displayName = displayName[..backtick];

        // Strip angle-bracket generic params: List<T> → List
        var angle = displayName.IndexOf('<');
        if (angle > 0) displayName = displayName[..angle];

        // Replace illegal file name characters
        foreach (var c in Path.GetInvalidFileNameChars())
            displayName = displayName.Replace(c, '_');

        return displayName.Trim('_', ' ') + language.FileExtension;
    }
}
