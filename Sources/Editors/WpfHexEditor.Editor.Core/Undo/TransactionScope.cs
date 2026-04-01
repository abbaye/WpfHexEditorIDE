// ==========================================================
// Project: WpfHexEditor.Editor.Core
// File: Undo/TransactionScope.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-18
// Description:
//     IDisposable scope returned by UndoEngine.BeginTransaction().
//     Disposing commits all buffered entries into a CompositeUndoEntry.
//
// Architecture Notes:
//     Pattern: RAII / Disposable Scope
//     A using-statement guarantees CommitTransaction is called even if the body throws.
//     Call Rollback() inside the using block to discard without pushing.
// ==========================================================

using System;

namespace WpfHexEditor.Editor.Core.Undo;

/// <summary>
/// Returned by <see cref="UndoEngine.BeginTransaction"/>. Disposing the scope
/// commits all entries collected during the transaction into a single
/// <see cref="CompositeUndoEntry"/>. Call <see cref="Rollback"/> to discard instead.
/// </summary>
public sealed class TransactionScope : IDisposable
{
    private readonly UndoEngine _engine;
    private bool _finished;

    internal TransactionScope(UndoEngine engine)
    {
        _engine = engine;
    }

    /// <summary>Commits the transaction. Idempotent after the first call.</summary>
    public void Dispose()
    {
        if (_finished) return;
        _finished = true;
        _engine.CommitTransaction();
    }

    /// <summary>Discards all entries collected so far without pushing a composite entry.</summary>
    public void Rollback()
    {
        if (_finished) return;
        _finished = true;
        _engine.RollbackTransaction();
    }
}
