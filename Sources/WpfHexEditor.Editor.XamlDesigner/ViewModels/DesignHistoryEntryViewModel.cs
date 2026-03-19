// ==========================================================
// Project: WpfHexEditor.Editor.XamlDesigner
// File: DesignHistoryEntryViewModel.cs
// Author: Derek Tremblay
// Created: 2026-03-18
// Description:
//     ViewModel for a single row in the Design History Panel.
//     Wraps an IDesignUndoEntry and exposes display-ready properties:
//     icon glyph, applied/current state, opacity for undone entries.
//
// Architecture Notes:
//     INPC ViewModel pattern.
//     IsApplied / IsCurrent set externally by DesignHistoryPanelViewModel
//     whenever the undo stack changes.
// ==========================================================

using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using WpfHexEditor.Editor.XamlDesigner.Models;
using WpfHexEditor.Editor.XamlDesigner.Services;

namespace WpfHexEditor.Editor.XamlDesigner.ViewModels;

/// <summary>
/// Represents a single entry in the Design History Panel list.
/// </summary>
public sealed class DesignHistoryEntryViewModel : INotifyPropertyChanged
{
    // ── Source entry ──────────────────────────────────────────────────────────

    /// <summary>The underlying undo entry this VM wraps.</summary>
    public IDesignUndoEntry Source { get; }

    // ── INPC state ────────────────────────────────────────────────────────────

    private bool _isApplied    = true;
    private bool _isCurrent;
    private bool _isCheckpoint;

    /// <summary>
    /// True when this entry is in the undo stack (has been applied).
    /// False when this entry is in the redo stack (was undone).
    /// </summary>
    public bool IsApplied
    {
        get => _isApplied;
        set
        {
            if (_isApplied == value) return;
            _isApplied = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(OpacityFactor));
        }
    }

    /// <summary>
    /// True for the most recently applied entry (shows the ▶ marker).
    /// </summary>
    public bool IsCurrent
    {
        get => _isCurrent;
        set
        {
            if (_isCurrent == value) return;
            _isCurrent = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// True when the user has starred this entry as a checkpoint.
    /// Checkpoints survive Clear() if the manager preserves them.
    /// </summary>
    public bool IsCheckpoint
    {
        get => _isCheckpoint;
        set
        {
            if (_isCheckpoint == value) return;
            _isCheckpoint = value;
            OnPropertyChanged();
        }
    }

    // ── Derived display properties ────────────────────────────────────────────

    /// <summary>Human-readable description.</summary>
    public string Description  => Source.Description;

    /// <summary>Timestamp of the operation.</summary>
    public DateTime Timestamp  => Source.Timestamp;

    /// <summary>Number of atomic operations bundled in this entry.</summary>
    public int OperationCount  => Source.OperationCount;

    /// <summary>
    /// Opacity: 1.0 when applied, 0.45 when undone (greyed-out in history).
    /// </summary>
    public double OpacityFactor => _isApplied ? 1.0 : 0.45;

    /// <summary>
    /// Segoe MDL2 Assets glyph representing the operation type.
    /// </summary>
    public string GlyphIcon => Source switch
    {
        SingleDesignUndoEntry s => s.Operation.Type switch
        {
            DesignOperationType.Move           => "\uE7C4",
            DesignOperationType.Resize         => "\uE740",
            DesignOperationType.PropertyChange => "\uE70F",
            DesignOperationType.Insert         => "\uE710",
            DesignOperationType.Delete         => "\uE74D",
            DesignOperationType.Alignment      => "\uE8A2",
            _                                  => "\uE71D"
        },
        BatchDesignUndoEntry    => "\uE8A2",
        SnapshotDesignUndoEntry => "\uE7B8",
        _                       => "\uE71D"
    };

    // ── Constructor ───────────────────────────────────────────────────────────

    /// <summary>
    /// Initialises the ViewModel from the given history entry.
    /// </summary>
    public DesignHistoryEntryViewModel(IDesignUndoEntry source)
    {
        Source = source;
    }

    // ── INotifyPropertyChanged ────────────────────────────────────────────────

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
