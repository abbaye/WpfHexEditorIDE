// ==========================================================
// Project: WpfHexEditor.Plugins.XamlDesigner
// File: DesignHistoryPanelViewModel.cs
// Author: Derek Tremblay
// Created: 2026-03-18
// Updated: 2026-03-19 â€” FilteredEntries, FilterText, HistorySizeLabel,
//                        HistoryCount, MaxHistory, ToggleCheckpointCommand
//          2026-03-22 â€” Moved to plugin project (WpfHexEditor.Plugins.XamlDesigner.ViewModels).
// Description:
//     ViewModel for the Design History Panel.
//     Mirrors the DesignUndoManager's history into an ObservableCollection
//     of DesignHistoryEntryViewModel rows, marks applied/current state,
//     and exposes commands for Clear, Jump-to-state, and Toggle Checkpoint.
//
// Architecture: Plugin-owned panel ViewModel; consumes DesignUndoManager from editor core.
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
using WpfHexEditor.Core.ViewModels;

namespace WpfHexEditor.Plugins.XamlDesigner.ViewModels;

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
public sealed class DesignHistoryPanelViewModel : ViewModelBase
{
    private const int DefaultMaxHistory = 200;

    private DesignUndoManager? _manager;
    private string             _filterText        = string.Empty;
    private bool               _checkpointsOnly;

    public ObservableCollection<DesignHistoryEntryViewModel> Entries { get; } = new();
    public ObservableCollection<DesignHistoryEntryViewModel> FilteredEntries { get; } = new();

    // ── Commands ──────────────────────────────────────────────────────────────

    public ICommand ClearHistoryCommand          { get; }
    public ICommand JumpToEntryCommand           { get; }
    public ICommand ToggleCheckpointCommand      { get; }
    public ICommand MarkCurrentCheckpointCommand { get; }
    public ICommand JumpToFirstCommand           { get; }
    public ICommand JumpToLatestCommand          { get; }
    public ICommand ExportHistoryCommand         { get; }

    // ── Filter properties ─────────────────────────────────────────────────────

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

    public int HistoryCount => Entries.Count;
    public int MaxHistory   => _manager is not null ? DesignUndoManager.MaxDepth : DefaultMaxHistory;
    public string HistorySizeLabel => $"{HistoryCount}/{MaxHistory}";

    // ── Events ────────────────────────────────────────────────────────────────

    public event EventHandler<JumpToEntryEventArgs>? JumpRequested;

    // ── Manager wiring ────────────────────────────────────────────────────────

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

        MarkCurrentCheckpointCommand = new RelayCommand(
            _ =>
            {
                var last = Entries.Count > 0 ? Entries[^1] : null;
                if (last is not null)
                    last.IsCheckpoint = !last.IsCheckpoint;
            },
            _ => Entries.Count > 0);

        JumpToFirstCommand = new RelayCommand(
            _ => JumpRequested?.Invoke(this, new JumpToEntryEventArgs(Entries.Count - 1, 0)),
            _ => Entries.Count > 1);

        JumpToLatestCommand = new RelayCommand(
            _ => JumpRequested?.Invoke(this, new JumpToEntryEventArgs(0, 0)),
            _ => Entries.Count > 0);

        ExportHistoryCommand = new RelayCommand(
            _ =>
            {
                var text = string.Join(Environment.NewLine,
                    Entries.Select((e, i) => $"{i + 1}. [{(e.IsCheckpoint ? "â˜…" : " ")}] {e.Description}"));
                if (!string.IsNullOrEmpty(text))
                    Clipboard.SetText(text);
            },
            _ => Entries.Count > 0);
    }

    // ── Private methods ───────────────────────────────────────────────────────

    private void OnHistoryChanged(object? sender, EventArgs e)
        => RebuildEntries();

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

    private void RebuildFilteredEntries()
    {
        FilteredEntries.Clear();

        foreach (var entry in Entries)
        {
            if (!PassesFilter(entry)) continue;
            FilteredEntries.Add(entry);
        }
    }

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

        if (_checkpointsOnly)
            RebuildFilteredEntries();
    }

    // ── INotifyPropertyChanged ────────────────────────────────────────────────


}
