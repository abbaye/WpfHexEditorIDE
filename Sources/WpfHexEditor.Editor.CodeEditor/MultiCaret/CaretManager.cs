// ==========================================================
// Project: WpfHexEditor.Editor.CodeEditor
// File: MultiCaret/CaretManager.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     Manages a list of independent carets for multi-caret editing.
//     The first caret (index 0) is always the "primary" caret — it drives
//     scrolling and single-caret fallback behaviour.
//
//     Typical interaction pattern:
//       • Alt+Click      → AddCaret(line, col)
//       • Escape         → CollapseToFirst()
//       • Any navigation → MoveAll() or MoveAt(primary index)
//
// Architecture Notes:
//     Pattern: Collection Manager
//     - Carets are stored as an ImmutableList to ensure thread-safe snapshots
//       while edits are applied.
//     - Duplicate carets (same line+col) are silently deduped on Add.
//     - CaretsChanged fires after every mutation; CodeEditor subscribes and
//       triggers an invalidation pass to redraw caret highlights.
// ==========================================================

using System.Collections.Immutable;

namespace WpfHexEditor.Editor.CodeEditor.MultiCaret;

/// <summary>
/// Tracks zero or more editor carets for multi-caret editing.
/// </summary>
public sealed class CaretManager
{
    // -----------------------------------------------------------------------
    // State
    // -----------------------------------------------------------------------

    private ImmutableList<CaretState> _carets;

    // -----------------------------------------------------------------------
    // Construction
    // -----------------------------------------------------------------------

    /// <summary>Initialises with a single primary caret at (0, 0).</summary>
    public CaretManager()
        => _carets = ImmutableList.Create(CaretState.At(0, 0));

    // -----------------------------------------------------------------------
    // Properties
    // -----------------------------------------------------------------------

    /// <summary>Immutable snapshot of all current carets (index 0 = primary).</summary>
    public ImmutableList<CaretState> Carets => _carets;

    /// <summary>The primary (first) caret. Never null.</summary>
    public CaretState Primary => _carets[0];

    /// <summary>Number of active carets.</summary>
    public int Count => _carets.Count;

    /// <summary><c>true</c> when more than one caret is active.</summary>
    public bool IsMultiCaret => _carets.Count > 1;

    // -----------------------------------------------------------------------
    // Mutation helpers
    // -----------------------------------------------------------------------

    /// <summary>
    /// Adds a caret at (<paramref name="line"/>, <paramref name="col"/>).
    /// Duplicate positions are silently ignored.
    /// </summary>
    public void AddCaret(int line, int col)
    {
        var newCaret = CaretState.At(line, col);
        if (_carets.Any(c => c.Line == line && c.Column == col)) return;
        _carets = _carets.Add(newCaret);
        CaretsChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Moves the caret at <paramref name="index"/> to (<paramref name="line"/>, <paramref name="col"/>),
    /// collapsing any selection on that caret.
    /// </summary>
    public void MoveAt(int index, int line, int col)
    {
        if (index < 0 || index >= _carets.Count) return;
        _carets = _carets.SetItem(index, _carets[index].MoveTo(line, col));
        DeduplicateAndNotify();
    }

    /// <summary>
    /// Extends the selection of the caret at <paramref name="index"/> to
    /// (<paramref name="line"/>, <paramref name="col"/>).
    /// </summary>
    public void ExtendAt(int index, int line, int col)
    {
        if (index < 0 || index >= _carets.Count) return;
        _carets = _carets.SetItem(index, _carets[index].ExtendTo(line, col));
        CaretsChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Moves ALL carets by the given delta. Carets that would become identical
    /// after the move are collapsed (deduplication keeps the first occurrence).
    /// </summary>
    public void MoveAll(int lineDelta, int colDelta)
    {
        _carets = _carets.Select(c =>
            c.MoveTo(Math.Max(0, c.Line + lineDelta),
                     Math.Max(0, c.Column + colDelta)))
            .ToImmutableList();
        DeduplicateAndNotify();
    }

    /// <summary>
    /// Replaces the entire caret list with a single primary caret positioned
    /// at the current <see cref="Primary"/> location.
    /// </summary>
    public void CollapseToFirst()
    {
        if (_carets.Count == 1) return;
        _carets = ImmutableList.Create(Primary.MoveTo(Primary.Line, Primary.Column));
        CaretsChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Replaces all carets with a single collapsed caret at (<paramref name="line"/>, <paramref name="col"/>).
    /// </summary>
    public void SetSingleCaret(int line, int col)
    {
        _carets = ImmutableList.Create(CaretState.At(line, col));
        CaretsChanged?.Invoke(this, EventArgs.Empty);
    }

    // -----------------------------------------------------------------------
    // Events
    // -----------------------------------------------------------------------

    /// <summary>Raised whenever the caret list changes.</summary>
    public event EventHandler? CaretsChanged;

    // -----------------------------------------------------------------------
    // Private helpers
    // -----------------------------------------------------------------------

    private void DeduplicateAndNotify()
    {
        // Remove duplicate positions, keeping first occurrence.
        var seen    = new HashSet<(int, int)>();
        var unique  = _carets.Where(c => seen.Add((c.Line, c.Column))).ToImmutableList();

        if (unique.Count != _carets.Count)
            _carets = unique;

        CaretsChanged?.Invoke(this, EventArgs.Empty);
    }
}
