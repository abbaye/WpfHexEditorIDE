// ==========================================================
// Project: WpfHexEditor.Editor.ClassDiagram
// File: Services/ClassDiagramUndoManager.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-19
// Description:
//     Undo/redo history manager for the class diagram editor.
//     Supports single operations, batch scopes, and DSL snapshots.
//     Maximum stack depth: 200 entries.
//
// Architecture Notes:
//     Pattern: Command + Memento hybrid.
//     Split-stack approach: _pointer divides undo-able (below) and
//     redo-able (above) entries. Push always truncates redo entries first.
//     BatchScope collects children during a using() block and commits
//     them as a single BatchClassDiagramUndoEntry on Dispose.
// ==========================================================

namespace WpfHexEditor.Editor.ClassDiagram.Services;

// ---------------------------------------------------------------------------
// Entry interfaces and records
// ---------------------------------------------------------------------------

/// <summary>
/// Common contract for all class diagram undo/redo history entries.
/// </summary>
public interface IClassDiagramUndoEntry
{
    /// <summary>Human-readable description shown in the history panel.</summary>
    string Description { get; }

    /// <summary>UTC timestamp of when this entry was created.</summary>
    DateTime Timestamp { get; }

    /// <summary>Reverts the operation represented by this entry.</summary>
    void Undo();

    /// <summary>Re-applies the operation represented by this entry.</summary>
    void Redo();
}

/// <summary>
/// Represents a single reversible operation with explicit undo/redo delegates.
/// </summary>
public sealed record SingleClassDiagramUndoEntry(
    string Description,
    Action UndoAction,
    Action RedoAction) : IClassDiagramUndoEntry
{
    public DateTime Timestamp { get; } = DateTime.UtcNow;
    public void Undo() => UndoAction();
    public void Redo() => RedoAction();
}

/// <summary>
/// Groups multiple entries into a single atomic operation.
/// Undo iterates children in reverse; Redo iterates forward.
/// </summary>
public sealed record BatchClassDiagramUndoEntry(
    IReadOnlyList<IClassDiagramUndoEntry> Entries,
    string Description) : IClassDiagramUndoEntry
{
    public DateTime Timestamp { get; } = DateTime.UtcNow;

    public void Undo()
    {
        for (int i = Entries.Count - 1; i >= 0; i--)
            Entries[i].Undo();
    }

    public void Redo()
    {
        foreach (var entry in Entries)
            entry.Redo();
    }
}

/// <summary>
/// Captures a full before/after DSL snapshot for coarse-grained undo.
/// Suitable for complex operations where tracking deltas is impractical.
/// </summary>
public sealed record SnapshotClassDiagramUndoEntry(
    string BeforeDsl,
    string AfterDsl,
    string Description,
    Action<string> ApplyDsl) : IClassDiagramUndoEntry
{
    public DateTime Timestamp { get; } = DateTime.UtcNow;
    public void Undo() => ApplyDsl(BeforeDsl);
    public void Redo() => ApplyDsl(AfterDsl);
}

// ---------------------------------------------------------------------------
// Undo manager
// ---------------------------------------------------------------------------

/// <summary>
/// Manages the undo/redo history stack for the class diagram editor.
/// Thread-affinity: must be accessed from the UI thread only.
/// </summary>
public sealed class ClassDiagramUndoManager
{
    private const int MaxEntries = 200;

    private readonly List<IClassDiagramUndoEntry> _stack = new(MaxEntries + 1);
    private int _pointer;

    // Active batch scope — null when no batch is in progress
    private ClassDiagramBatchScope? _activeBatch;

    // ---------------------------------------------------------------------------
    // Public query properties
    // ---------------------------------------------------------------------------

    public bool CanUndo => _pointer > 0;
    public bool CanRedo => _pointer < _stack.Count;
    public int  UndoCount => _pointer;
    public int  RedoCount => _stack.Count - _pointer;

    public IReadOnlyList<IClassDiagramUndoEntry> Entries => _stack.AsReadOnly();

    // ---------------------------------------------------------------------------
    // Events
    // ---------------------------------------------------------------------------

    public event EventHandler? HistoryChanged;

    private void RaiseHistoryChanged() =>
        HistoryChanged?.Invoke(this, EventArgs.Empty);

    // ---------------------------------------------------------------------------
    // Stack mutation
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Pushes a new entry onto the stack, truncating any redo entries first.
    /// If a batch scope is active the entry is collected into the batch instead.
    /// </summary>
    public void Push(IClassDiagramUndoEntry entry)
    {
        if (_activeBatch is not null)
        {
            _activeBatch.Collect(entry);
            return;
        }

        // Truncate redo branch
        if (_pointer < _stack.Count)
            _stack.RemoveRange(_pointer, _stack.Count - _pointer);

        _stack.Add(entry);

        // Enforce cap
        if (_stack.Count > MaxEntries)
            _stack.RemoveAt(0);
        else
            _pointer++;

        RaiseHistoryChanged();
    }

    /// <summary>
    /// Undoes the most recent operation. No-op if CanUndo is false.
    /// </summary>
    public void Undo()
    {
        if (!CanUndo) return;
        _pointer--;
        _stack[_pointer].Undo();
        RaiseHistoryChanged();
    }

    /// <summary>
    /// Redoes the next operation in the stack. No-op if CanRedo is false.
    /// </summary>
    public void Redo()
    {
        if (!CanRedo) return;
        _stack[_pointer].Redo();
        _pointer++;
        RaiseHistoryChanged();
    }

    /// <summary>
    /// Clears all undo/redo history.
    /// </summary>
    public void Clear()
    {
        _stack.Clear();
        _pointer = 0;
        RaiseHistoryChanged();
    }

    // ---------------------------------------------------------------------------
    // Batch scope
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Begins a batch scope. All entries pushed during the scope are collected
    /// and committed as a single <see cref="BatchClassDiagramUndoEntry"/> when
    /// the returned scope is disposed.
    /// </summary>
    public ClassDiagramBatchScope BeginBatch(string description)
    {
        _activeBatch = new ClassDiagramBatchScope(description, CommitBatch);
        return _activeBatch;
    }

    private void CommitBatch(ClassDiagramBatchScope scope, IReadOnlyList<IClassDiagramUndoEntry> children)
    {
        _activeBatch = null;
        if (children.Count == 0) return;

        var batch = new BatchClassDiagramUndoEntry(children, scope.Description);
        Push(batch);
    }

    // ---------------------------------------------------------------------------
    // Nested batch scope class
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Disposable scope that accumulates entries during its lifetime and commits
    /// them as a single <see cref="BatchClassDiagramUndoEntry"/> on Dispose.
    /// </summary>
    public sealed class ClassDiagramBatchScope : IDisposable
    {
        private readonly List<IClassDiagramUndoEntry> _children = [];
        private readonly Action<ClassDiagramBatchScope, IReadOnlyList<IClassDiagramUndoEntry>> _commit;
        private bool _disposed;

        internal ClassDiagramBatchScope(
            string description,
            Action<ClassDiagramBatchScope, IReadOnlyList<IClassDiagramUndoEntry>> commit)
        {
            Description = description;
            _commit = commit;
        }

        public string Description { get; }

        internal void Collect(IClassDiagramUndoEntry entry) => _children.Add(entry);

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _commit(this, _children.AsReadOnly());
        }
    }
}
