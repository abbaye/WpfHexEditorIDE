// ==========================================================
// Project: WpfHexEditor.Editor.DocumentEditor
// File: Controls/DocumentHexHighlightManager.cs
// Description:
//     Manages CustomBackgroundBlock mutation overlays in the HexPane.
//     TextEdited → semi-transparent orange overlay on block's raw range.
//     Deleted    → semi-transparent red overlay on block's raw range.
//     Inserted blocks (RawOffset=-1) produce no overlay.
//     After UndoEngine.MarkSaved() → Clear() is called to wipe overlays.
// ==========================================================

using System.Windows.Media;
using WpfHexEditor.Core;
using WpfHexEditor.Editor.DocumentEditor.Core.Editing;
using WpfHexEditor.Editor.DocumentEditor.Core.Model;

namespace WpfHexEditor.Editor.DocumentEditor.Controls;

/// <summary>
/// Translates <see cref="BlockMutatedArgs"/> events into
/// <see cref="CustomBackgroundBlock"/> overlays on the hex pane.
/// </summary>
internal sealed class DocumentHexHighlightManager
{
    private const string EditedTag  = "DE_MutEdit";
    private const string DeletedTag = "DE_MutDel";

    private readonly DocumentHexPane _hexPane;

    public DocumentHexHighlightManager(DocumentHexPane hexPane) => _hexPane = hexPane;

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Applies an overlay for the mutated block.</summary>
    public void Apply(DocumentBlock block, BlockMutationKind kind)
    {
        // Blocks with no binary origin produce no hex overlay
        if (block.RawOffset < 0 || block.RawLength <= 0) return;

        switch (kind)
        {
            case BlockMutationKind.TextEdited:
            case BlockMutationKind.AttributeChanged:
                _hexPane.AddMutationOverlay(block.RawOffset, block.RawLength, isDelete: false);
                break;

            case BlockMutationKind.Deleted:
                _hexPane.AddMutationOverlay(block.RawOffset, block.RawLength, isDelete: true);
                break;

            case BlockMutationKind.Inserted:
                // No binary range — skip
                break;
        }
    }

    /// <summary>
    /// Removes all mutation overlays (called after a successful save or undo to save point).
    /// </summary>
    public void Clear() => _hexPane.ClearMutationOverlays();
}
