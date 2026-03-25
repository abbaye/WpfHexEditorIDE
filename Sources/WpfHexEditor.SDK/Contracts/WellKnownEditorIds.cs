// ==========================================================
// Project: WpfHexEditor.SDK
// File: Contracts/WellKnownEditorIds.cs
// Description:
//     Compile-time constants for the built-in editor factory IDs.
//     Use these instead of raw string literals when calling
//     IDocumentHostService.OpenDocument(path, preferredEditorId).
//
// Architecture Notes:
//     String IDs (not an enum) are intentional — the editor registry is
//     open/extensible: plugins register IEditorFactory with arbitrary IDs
//     at runtime. An enum would require SDK modification for every new
//     editor type and would break the open/closed principle.
//     These constants cover only the built-in first-party editors.
// ==========================================================

namespace WpfHexEditor.SDK.Contracts;

/// <summary>
/// Well-known editor factory IDs for use with
/// <see cref="Services.IDocumentHostService.OpenDocument"/>.
/// </summary>
public static class WellKnownEditorIds
{
    /// <summary>Binary hex editor (default fallback for all file types).</summary>
    public const string HexEditor = "hex-editor";

    /// <summary>Syntax-highlighted code / text editor (AvalonEdit-based).</summary>
    public const string CodeEditor = "code-editor";

    /// <summary>RESX resource grid editor.</summary>
    public const string ResxEditor = "resx-editor";

    /// <summary>Markdown preview + source split editor.</summary>
    public const string MarkdownEditor = "markdown-editor";

    /// <summary>XAML live-preview designer split editor.</summary>
    public const string XamlDesigner = "xaml-designer";

    /// <summary>Audio waveform viewer.</summary>
    public const string AudioViewer = "audio-viewer";

    /// <summary>Plain-text editor (lightweight, no syntax highlighting).</summary>
    public const string TextEditor = "text-editor";

    /// <summary>TBL character-table editor.</summary>
    public const string TblEditor = "tbl-editor";

    /// <summary>UML class diagram editor (DSL + canvas).</summary>
    public const string ClassDiagramEditor = "class-diagram-editor";

    /// <summary>Raster image viewer (PNG, BMP, JPEG, GIF, ICO, TIFF, WebP, TGA…).</summary>
    public const string ImageViewer = "image-viewer";

    /// <summary>Disassembly viewer (x86/x64/ARM instruction stream).</summary>
    public const string DisassemblyViewer = "disassembly-viewer";

    /// <summary>Side-by-side diff viewer (text and binary).</summary>
    public const string DiffViewer = "diff-viewer";

    /// <summary>Tile / sprite editor for retro game graphics (.chr, .til, .gfx).</summary>
    public const string TileEditor = "tile-editor";

    /// <summary>Binary structure / template editor (.whfmt).</summary>
    public const string StructureEditor = "structure-editor";

    /// <summary>Entropy / byte-frequency visualiser (opened from Tools menu).</summary>
    public const string EntropyViewer = "entropy-viewer";

    /// <summary>Changeset (diff patch) editor (.whchg).</summary>
    public const string ChangesetEditor = "changeset-editor";
}
