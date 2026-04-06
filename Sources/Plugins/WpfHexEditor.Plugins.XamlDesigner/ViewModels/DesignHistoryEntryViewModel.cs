// ==========================================================
// Project: WpfHexEditor.Plugins.XamlDesigner
// File: DesignHistoryEntryViewModel.cs
// Author: Derek Tremblay
// Created: 2026-03-18
//          2026-03-22 â€” Moved to plugin project (WpfHexEditor.Plugins.XamlDesigner.ViewModels).
// Description:
//     ViewModel for a single row in the Design History Panel.
//     Wraps an IDesignUndoEntry and exposes display-ready properties:
//     icon glyph, applied/current state, opacity for undone entries.
//
// Architecture: Plugin-owned. Consumes IDesignUndoEntry from editor core Services.
// ==========================================================

using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using WpfHexEditor.Editor.XamlDesigner.Models;
using WpfHexEditor.Editor.XamlDesigner.Services;
using WpfHexEditor.Core.ViewModels;

namespace WpfHexEditor.Plugins.XamlDesigner.ViewModels;

/// <summary>
/// Represents a single entry in the Design History Panel list.
/// </summary>
public sealed class DesignHistoryEntryViewModel : ViewModelBase
{
    public IDesignUndoEntry Source { get; }

    private bool _isApplied    = true;
    private bool _isCurrent;
    private bool _isCheckpoint;

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

    public string   Description    => Source.Description;
    public DateTime Timestamp      => Source.Timestamp;
    public int      OperationCount => Source.OperationCount;
    public double   OpacityFactor  => _isApplied ? 1.0 : 0.45;

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

    public DesignHistoryEntryViewModel(IDesignUndoEntry source)
    {
        Source = source;
    }


}
