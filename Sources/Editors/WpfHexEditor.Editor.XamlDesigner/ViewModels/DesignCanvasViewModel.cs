// ==========================================================
// Project: WpfHexEditor.Editor.XamlDesigner
// File: DesignCanvasViewModel.cs
// Author: Derek Tremblay
// Created: 2026-03-17
// Description:
//     ViewModel exposing design canvas settings: grid visibility, grid size,
//     snap options, and ruler visibility. Persisted through XamlDesignerOptions.
//
// Architecture Notes:
//     INPC. Consumed by XamlDesignerSplitHost toolbar and SnapEngineService.
// ==========================================================

using System.ComponentModel;
using System.Runtime.CompilerServices;
using WpfHexEditor.Core.ViewModels;

namespace WpfHexEditor.Editor.XamlDesigner.ViewModels;

/// <summary>
/// Settings for the XAML design canvas (grid, snap, rulers).
/// </summary>
public sealed class DesignCanvasViewModel : ViewModelBase
{
    private bool   _showGrid       = true;
    private int    _gridSize       = 8;
    private bool   _snapToGrid     = true;
    private bool   _snapToElements = true;
    private bool   _showRulers     = true;

    public bool ShowGrid
    {
        get => _showGrid;
        set { if (_showGrid == value) return; _showGrid = value; OnPropertyChanged(); }
    }

    public int GridSize
    {
        get => _gridSize;
        set
        {
            int clamped = Math.Clamp(value, 1, 128);
            if (_gridSize == clamped) return;
            _gridSize = clamped;
            OnPropertyChanged();
        }
    }

    public bool SnapToGrid
    {
        get => _snapToGrid;
        set { if (_snapToGrid == value) return; _snapToGrid = value; OnPropertyChanged(); }
    }

    public bool SnapToElements
    {
        get => _snapToElements;
        set { if (_snapToElements == value) return; _snapToElements = value; OnPropertyChanged(); }
    }

    public bool ShowRulers
    {
        get => _showRulers;
        set { if (_showRulers == value) return; _showRulers = value; OnPropertyChanged(); }
    }


}
