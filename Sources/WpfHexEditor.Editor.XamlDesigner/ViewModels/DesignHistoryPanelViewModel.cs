// ==========================================================
// Project: WpfHexEditor.Editor.XamlDesigner
// File: DesignHistoryPanelViewModel.cs
// Author: Derek Tremblay
// Created: 2026-03-18
// Updated: 2026-03-19 — FilteredEntries, FilterText, HistorySizeLabel,
//                        HistoryCount, MaxHistory, ToggleCheckpointCommand
// Description:
//     ViewModel for the Design History Panel.
//     Mirrors the DesignUndoManager's history into an ObservableCollection
//     of DesignHistoryEntryViewModel rows, marks applied/current state,
//     and exposes commands for Clear, Jump-to-state, and Toggle Checkpoint.
//     FilteredEntries is a filtered view of Entries based on FilterText
//     and the CheckpointsOnly toggle.
//
// Architecture Notes:
//     Observer pattern — subscribes to DesignUndoManager.HistoryChanged.
//     JumpRequested event raised to the panel code-behind, which forwards it
//     to XamlDesignerSplitHost.JumpToHistoryEntry().
//     FilteredEntries is rebuilt eagerly on every filter or history change.
// ==========================================================

using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using WpfHexEditor.Editor.XamlDesigner.Services;
using WpfHexEditor.SDK.Commands;

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
    // ── Constants ─────────────────────────────────────────────────────────────

    private const int DefaultMaxHistory = 200;

    // ── Internal state ────────────────────────────────────────────────────────

    private DesignUndoManager? _manager;
    private string             _filterText        = string.Empty;
    private bool               _checkpointsOnly;

    // ── Public collections ────────────────────────────────────────────────────

    /// <summary>Full ordered history entries (oldest first) — always reflects the manager.</summary>
    public ObservableCollection<DesignHistoryEntryViewModel> Entries { get; } = new();

    /// <summary>
    /// Filtered view of <see cref="Entries"/> — updated whenever the filter text
    /// or the CheckpointsOnly toggle changes. The ListView binds to this collection.
    /// </summary>
    public ObservableCollection<DesignHistoryEntryViewModel> FilteredEntries { get; } = new();

    // ── Commands ──────────────────────────────────────────────────────────────

    /// <summary>Clears the entire undo/redo history.</summary>
    public ICommand ClearHistoryCommand { get; }

    /// <summary>Jumps to a specific entry when clicked in the ListView.</summary>
    public ICommand JumpToEntryCommand  { get; }

    /// <summary>
    /// Toggles the <c>IsCheckpoint</c> flag on a <see cref="DesignHistoryEntryViewModel"/>.
    /// Parameter must be a <see cref="DesignHistoryEntryViewModel"/>.
    /// </summary>
    public ICommand ToggleCheckpointCommand { get; }

    /// <summary>Marks (or unmarks) the most recent history entry as a checkpoint.</summary>
    public ICommand MarkCurrentCheckpointCommand { get; }

    /// <summary>Jumps to the oldest history entry (index 0).</summary>
    public ICommand JumpToFirstCommand { get; }

    /// <summary>Jumps to the most recent history entry.</summary>
    public ICommand JumpToLatestCommand { get; }

    /// <summary>Copies the full history list as plain text to the clipboard.</summary>
    public ICommand ExportHistoryCommand { get; }

    // ── Filter properties ─────────────────────────────────────────────────────

    /// <summary>
    /// Text filter applied to <see cref="FilteredEntries"/>.
    /// Setting this rebuilds the filtered collection instantly.
    /// </summary>
    public string FilterText
    {
        get => _filterText;
        set
        {
            if (_filterText == value) return;
            _filterText = value;
            OnPropertyChanged();
            RebuildFilteredEntries();
        }
    }

    /// <summary>
    /// When true, only entries with <c>IsCheckpoint = true</c> are shown in
    /// <see cref="FilteredEntries"/>.
    /// </summary>
    public bool CheckpointsOnly
    {
        get => _checkpointsOnly;
        set
        {
            if (_checkpointsOnly == value) return;
            _checkpointsOnly = value;
            OnPropertyChanged();
            RebuildFilteredEntries();
        }
    }

    // ── History size properties ───────────────────────────────────────────────

    /// <summary>Number of entries currently in the history.</summary>
    public int HistoryCount => Entries.Count;

    /// <summary>Maximum history size from the connected manager (defaults to 200).</summary>
    public int MaxHistory => _manager is not null ? DesignUndoManager.MaxDepth : DefaultMaxHistory;

    /// <summary>Human-readable size label, e.g. "42/200".</summary>
    public string HistorySizeLabel => $"{HistoryCount}/{MaxHistory}";

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
            OnPropertyChanged(nameof(MaxHistory));
            OnPropertyChanged(nameof(HistorySizeLabel));
        }
    }

    // ── Constructor ───────────────────────────────────────────────────────────

    public DesignHistoryPanelViewModel()
    {
        ClearHistoryCommand     = new RelayCommand(_ => _manager?.Clear());
        JumpToEntryCommand      = new RelayCommand(OnJumpToEntry);
        ToggleCheckpointCommand = new RelayCommand(OnToggleCheckpoint);
    }

    // ── Private methods ───────────────────────────────────────────────────────

    private void OnHistoryChanged(object? sender, EventArgs e)
        => RebuildEntries();

    /// <summary>
    /// Rebuilds the <see cref="Entries"/> collection from the manager's current history,
    /// marks each entry as applied/current, then rebuilds <see cref="FilteredEntries"/>.
    /// </summary>
    private void RebuildEntries()
    {
        Entries.Clear();

        if (_manager is not null)
        {
            var history      = _manager.History;
            int appliedCount = _manager.UndoDepth;

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

        OnPropertyChanged(nameof(HistoryCount));
        OnPropertyChanged(nameof(HistorySizeLabel));

        RebuildFilteredEntries();
    }

    /// <summary>
    /// Rebuilds <see cref="FilteredEntries"/> from the current <see cref="Entries"/>
    /// applying both the text filter and the <see cref="CheckpointsOnly"/> toggle.
    /// </summary>
    private void RebuildFilteredEntries()
    {
        FilteredEntries.Clear();

        foreach (var entry in Entries)
        {
            if (!PassesFilter(entry)) continue;
            FilteredEntries.Add(entry);
        }
    }

    /// <summary>
    /// Returns true when the entry satisfies both the text filter and the checkpoint toggle.
    /// </summary>
    private bool PassesFilter(DesignHistoryEntryViewModel entry)
    {
        if (_checkpointsOnly && !entry.IsCheckpoint)
            return false;

        if (!string.IsNullOrEmpty(_filterText) &&
            !entry.Description.Contains(_filterText, StringComparison.OrdinalIgnoreCase))
            return false;

        return true;
    }

    private void OnJumpToEntry(object? param)
    {
        if (param is not DesignHistoryEntryViewModel target) return;
        if (_manager is null) return;

        // Resolve the index in the full Entries list (not the filtered list).
        int targetIndex  = Entries.IndexOf(target);
        if (targetIndex < 0) return;

        int currentIndex = _manager.UndoDepth - 1;
        int undoCount    = 0;
        int redoCount    = 0;

        if (targetIndex < currentIndex)
            undoCount = currentIndex - targetIndex;
        else if (targetIndex > currentIndex)
            redoCount = targetIndex - currentIndex;

        if (undoCount == 0 && redoCount == 0) return;

        JumpRequested?.Invoke(this, new JumpToEntryEventArgs(undoCount, redoCount));
    }

    private void OnToggleCheckpoint(object? param)
    {
        if (param is not DesignHistoryEntryViewModel entry) return;
        entry.IsCheckpoint = !entry.IsCheckpoint;

        // If CheckpointsOnly is active, rebuild the filtered list so the toggle
        // is immediately reflected without the entry disappearing mid-click.
        if (_checkpointsOnly)
            RebuildFilteredEntries();
    }

    // ── INotifyPropertyChanged ────────────────────────────────────────────────

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
