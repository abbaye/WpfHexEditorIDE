// ==========================================================
// Project: WpfHexEditor.Editor.XamlDesigner
// File: DesignHistoryPanelViewModel.cs
// Author: Derek Tremblay
// Created: 2026-03-18
// Description:
//     ViewModel for the Design History Panel.
//     Mirrors the DesignUndoManager's history into an ObservableCollection
//     of DesignHistoryEntryViewModel rows, marks applied/current state,
//     and exposes commands for Clear and Jump-to-state.
//
// Architecture Notes:
//     Observer pattern — subscribes to DesignUndoManager.HistoryChanged.
//     JumpRequested event raised to the panel code-behind, which forwards it
//     to XamlDesignerSplitHost.JumpToHistoryEntry().
// ==========================================================

using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using WpfHexEditor.SDK.Commands;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using WpfHexEditor.Editor.XamlDesigner.Services;

namespace WpfHexEditor.Editor.XamlDesigner.ViewModels;

// ── Event args ────────────────────────────────────────────────────────────────

/// <summary>
/// Carries the number of undo and redo steps required to jump to a target history entry.
/// </summary>
public sealed class JumpToEntryEventArgs : EventArgs
{
    public int UndoCount { get; }
    public int RedoCount { get; }

    public JumpToEntryEventArgs(int undoCount, int redoCount)
    {
        UndoCount = undoCount;
        RedoCount = redoCount;
    }
}

// ── ViewModel ─────────────────────────────────────────────────────────────────

/// <summary>
/// ViewModel for the VS-Like Design History dockable panel.
/// </summary>
public sealed class DesignHistoryPanelViewModel : INotifyPropertyChanged
{
    // ── Internal state ────────────────────────────────────────────────────────

    private DesignUndoManager? _manager;

    // ── Public collections & commands ────────────────────────────────────────

    /// <summary>Ordered history entries displayed in the panel (oldest first).</summary>
    public ObservableCollection<DesignHistoryEntryViewModel> Entries { get; } = new();

    /// <summary>Clears the entire undo/redo history.</summary>
    public ICommand ClearHistoryCommand { get; }

    /// <summary>Jumps to a specific entry when clicked in the ListView.</summary>
    public ICommand JumpToEntryCommand  { get; }

    // ── Events ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Raised when the user requests a jump-to-state.
    /// The panel code-behind forwards this to <c>XamlDesignerSplitHost.JumpToHistoryEntry</c>.
    /// </summary>
    public event EventHandler<JumpToEntryEventArgs>? JumpRequested;

    // ── Manager wiring ────────────────────────────────────────────────────────

    /// <summary>
    /// Sets the active <see cref="DesignUndoManager"/> and subscribes to its
    /// <c>HistoryChanged</c> event. Unsubscribes from any previously set manager.
    /// </summary>
    public DesignUndoManager? Manager
    {
        set
        {
            if (_manager is not null)
                _manager.HistoryChanged -= OnHistoryChanged;

            _manager = value;

            if (_manager is not null)
                _manager.HistoryChanged += OnHistoryChanged;

            RebuildEntries();
        }
    }

    // ── Constructor ───────────────────────────────────────────────────────────

    public DesignHistoryPanelViewModel()
    {
        ClearHistoryCommand = new RelayCommand(_ => _manager?.Clear());
        JumpToEntryCommand  = new RelayCommand(OnJumpToEntry);
    }

    // ── Private methods ───────────────────────────────────────────────────────

    private void OnHistoryChanged(object? sender, EventArgs e)
        => RebuildEntries();

    /// <summary>
    /// Rebuilds the <see cref="Entries"/> collection from the manager's current history.
    /// Marks each entry as applied/current based on the undo stack depth.
    /// </summary>
    private void RebuildEntries()
    {
        Entries.Clear();

        if (_manager is null) return;

        var history     = _manager.History;   // oldest → newest (applied + undone)
        int appliedCount = _manager.UndoDepth; // number of entries that are applied

        for (int i = 0; i < history.Count; i++)
        {
            var vm = new DesignHistoryEntryViewModel(history[i])
            {
                IsApplied = i < appliedCount,
                IsCurrent = i == appliedCount - 1
            };
            Entries.Add(vm);
        }
    }

    private void OnJumpToEntry(object? param)
    {
        if (param is not DesignHistoryEntryViewModel target) return;
        if (_manager is null) return;

        // Determine the target index in the Entries collection.
        int targetIndex  = Entries.IndexOf(target);
        if (targetIndex < 0) return;

        int currentIndex = _manager.UndoDepth - 1;

        int undoCount = 0;
        int redoCount = 0;

        if (targetIndex < currentIndex)
            undoCount = currentIndex - targetIndex;
        else if (targetIndex > currentIndex)
            redoCount = targetIndex - currentIndex;

        if (undoCount == 0 && redoCount == 0) return;

        JumpRequested?.Invoke(this, new JumpToEntryEventArgs(undoCount, redoCount));
    }

    // ── INotifyPropertyChanged ────────────────────────────────────────────────

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
