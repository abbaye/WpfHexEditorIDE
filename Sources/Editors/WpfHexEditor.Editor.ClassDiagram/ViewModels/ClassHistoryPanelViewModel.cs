// ==========================================================
// Project: WpfHexEditor.Editor.ClassDiagram
// File: ViewModels/ClassHistoryPanelViewModel.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-19
// Description:
//     ViewModel for the Design History panel. Mirrors the undo manager's
//     entry list and exposes the current pointer index for visual
//     highlighting. Jump requests route back to the host.
//
// Architecture Notes:
//     Pattern: ViewModel (MVVM) + Observer.
//     Subscribes to ClassDiagramUndoManager.HistoryChanged to stay
//     in sync. JumpRequested carries the target index so the host
//     can execute the appropriate number of Undo/Redo calls.
// ==========================================================

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using WpfHexEditor.Editor.ClassDiagram.Services;
using WpfHexEditor.Core.ViewModels;

namespace WpfHexEditor.Editor.ClassDiagram.ViewModels;

/// <summary>
/// ViewModel for the class diagram history panel.
/// </summary>
public sealed class ClassHistoryPanelViewModel : ViewModelBase
{
    private readonly ObservableCollection<IClassDiagramUndoEntry> _entries = [];
    private int _currentIndex;
    private ClassDiagramUndoManager? _manager;

    // ---------------------------------------------------------------------------
    // Collections and state
    // ---------------------------------------------------------------------------

    public ObservableCollection<IClassDiagramUndoEntry> Entries => _entries;

    public int CurrentIndex
    {
        get => _currentIndex;
        private set { if (_currentIndex == value) return; _currentIndex = value; OnPropertyChanged(); }
    }

    // ---------------------------------------------------------------------------
    // Events
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Fired when the user clicks an entry to jump to that history state.
    /// Carries the target stack index (0 = initial state).
    /// </summary>
    public event EventHandler<int>? JumpRequested;

    // ---------------------------------------------------------------------------
    // Manager binding
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Binds this ViewModel to the given undo manager.
    /// Subscribes to HistoryChanged to keep the entry list in sync.
    /// </summary>
    public void SetManager(ClassDiagramUndoManager manager)
    {
        if (_manager is not null)
            _manager.HistoryChanged -= OnHistoryChanged;

        _manager = manager;
        _manager.HistoryChanged += OnHistoryChanged;
        Refresh();
    }

    /// <summary>
    /// Requests a jump to the history entry at the given index.
    /// The host handles the actual undo/redo execution.
    /// </summary>
    public void RequestJumpTo(int index)
    {
        if (_manager is null) return;
        if (index < 0 || index > _manager.Entries.Count) return;
        JumpRequested?.Invoke(this, index);
    }

    // ---------------------------------------------------------------------------
    // Private
    // ---------------------------------------------------------------------------

    private void OnHistoryChanged(object? sender, EventArgs e) => Refresh();

    private void Refresh()
    {
        if (_manager is null) return;

        _entries.Clear();
        foreach (var entry in _manager.Entries)
            _entries.Add(entry);

        CurrentIndex = _manager.UndoCount;
    }

    // ---------------------------------------------------------------------------
    // INotifyPropertyChanged
    // ---------------------------------------------------------------------------


}
