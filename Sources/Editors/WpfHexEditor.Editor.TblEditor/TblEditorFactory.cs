//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using WpfHexEditor.Editor.Core;
using WpfHexEditor.Editor.TblEditor.Services;

namespace WpfHexEditor.Editor.TblEditor;

/// <summary>
/// Optional <see cref="IEditorFactory"/> for the TBL editor.
/// Register this at application startup when plug-in auto-discovery is desired:
/// <code>EditorRegistry.Instance.Register(new TblEditorFactory());</code>
/// When plug-in support is not needed, simply instantiate <see cref="TblEditor"/> directly.
/// </summary>
public sealed class TblEditorFactory : IEditorFactory, IFileValidator
{
    private static readonly IEditorDescriptor _descriptor = new TblEditorDescriptor();

    public IEditorDescriptor Descriptor => _descriptor;

    public bool CanOpen(string filePath)
    {
        var ext = Path.GetExtension(filePath)?.ToLowerInvariant();
        return ext is ".tbl" or ".tblx";
    }

    public IDocumentEditor Create() => new Controls.TblEditor();

    // -- IFileValidator ----------------------------------------------------

    /// <summary>
    /// Validates a .tbl/.tblx file without opening a UI editor.
    /// Returns diagnostics with line numbers — safe to call on a background thread.
    /// </summary>
    public async Task<IReadOnlyList<DiagnosticEntry>> ValidateAsync(
        string filePath, CancellationToken ct = default)
    {
        var raw = await File.ReadAllTextAsync(filePath, ct);
        var result = new TblRepairService().Repair(raw, Path.GetFileName(filePath));

        // Inject the absolute FilePath into each diagnostic (repair service uses null)
        var withPath = new List<DiagnosticEntry>(result.Diagnostics.Count);
        foreach (var d in result.Diagnostics)
            withPath.Add(d with { FilePath = filePath });

        return withPath;
    }
}

file sealed class TblEditorDescriptor : IEditorDescriptor
{
    public string Id          => "tbl-editor";
    public string DisplayName => "TBL Editor";
    public string Description => "Character table editor for game ROM translation projects";
    public IReadOnlyList<string> SupportedExtensions => [".tbl", ".tblx"];
}
