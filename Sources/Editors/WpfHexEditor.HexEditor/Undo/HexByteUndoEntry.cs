// ==========================================================
// Project: WpfHexEditor.HexEditor
// File: Undo/HexByteUndoEntry.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-04-13
// Description:
//     IExecutableUndoEntry wrapping one HexEditor byte operation for the
//     shared per-buffer UndoEngine (Feature #107 — undo/redo unification).
//
// Architecture Notes:
//     Pattern: Command with captured closures
//     - Revert and Apply are Action delegates captured at creation time in
//       HexEditor.UndoAware.cs. They hold a ref to the ByteProvider via the
//       HexEditorViewModel, so no cross-editor reference is needed at dispatch time.
//     - TryMerge always returns false: coalescence already occurred inside
//       UndoRedoManager before this entry was promoted. Double-coalescing
//       at the shared-engine level would corrupt the byte snapshot.
//     - Standalone safety: this class is only instantiated from
//       HexEditor.UndoAware.cs, which is only wired when DocumentManager
//       calls AttachSharedUndo(). Standalone HexEditor never reaches this path.
// ==========================================================

using System;
using System.Diagnostics.CodeAnalysis;
using WpfHexEditor.Editor.Core.Undo;

namespace WpfHexEditor.HexEditor.Undo;

/// <summary>
/// <see cref="IExecutableUndoEntry"/> wrapping a HexEditor byte operation
/// for the shared per-buffer <see cref="UndoEngine"/>.
/// Carries apply/revert closures so dispatch needs no reference to the
/// originating editor.
/// </summary>
public sealed class HexByteUndoEntry : IExecutableUndoEntry
{
    private readonly Action _revert;
    private readonly Action _apply;

    // ── IUndoEntry ────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public string   Description { get; }

    /// <inheritdoc/>
    public long     Revision    { get; set; }

    /// <inheritdoc/>
    public DateTime Timestamp   { get; }

    // ── Construction ─────────────────────────────────────────────────────

    /// <param name="description">Human-readable label (e.g. "Modify 3 bytes").</param>
    /// <param name="revert">Action that calls ByteProvider.Undo() with replay guard set.</param>
    /// <param name="apply">Action that calls ByteProvider.Redo() with replay guard set.</param>
    public HexByteUndoEntry(string description, Action revert, Action apply)
    {
        Description = description ?? "Edit bytes";
        Timestamp   = DateTime.UtcNow;
        _revert     = revert ?? throw new ArgumentNullException(nameof(revert));
        _apply      = apply  ?? throw new ArgumentNullException(nameof(apply));
    }

    // ── IExecutableUndoEntry ─────────────────────────────────────────────

    /// <inheritdoc/>
    public void Revert() => _revert();

    /// <inheritdoc/>
    public void Apply() => _apply();

    // ── IUndoEntry : Merging ─────────────────────────────────────────────

    /// <inheritdoc/>
    /// <remarks>
    /// Always returns <see langword="false"/>. Coalescence already happened inside
    /// <c>UndoRedoManager</c> before promotion. Double-merging at the shared-engine
    /// level would corrupt the byte snapshot carried by the closures.
    /// </remarks>
    public bool TryMerge(IUndoEntry next, [NotNullWhen(true)] out IUndoEntry? merged)
    {
        merged = null;
        return false;
    }
}
