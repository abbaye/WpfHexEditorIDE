// ==========================================================
// Project: WpfHexEditor.Editor.ClassDiagram
// File: ViewModels/DiagramCanvasViewModel.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-19
// Description:
//     ViewModel for the diagram canvas: zoom, pan offset,
//     snap and grid toggle state, multi-select mode flag.
//
// Architecture Notes:
//     Pattern: ViewModel (MVVM).
//     ZoomIn/ZoomOut clamp between 0.1x and 10x.
//     All properties are INotifyPropertyChanged — bound from
//     ZoomPanCanvas DependencyProperties via TwoWay bindings.
// ==========================================================

using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WpfHexEditor.Editor.ClassDiagram.ViewModels;

/// <summary>
/// Holds canvas view state: zoom, pan offset, snap, grid, selection mode.
/// </summary>
public sealed class DiagramCanvasViewModel : INotifyPropertyChanged
{
    private const double MinZoom = 0.1;
    private const double MaxZoom = 10.0;
    private const double ZoomStep = 0.1;

    private double _zoomFactor = 1.0;
    private double _offsetX;
    private double _offsetY;
    private bool _snapEnabled = true;
    private bool _gridVisible = true;
    private bool _isMultiSelectMode;

    // ---------------------------------------------------------------------------
    // Zoom & Pan
    // ---------------------------------------------------------------------------

    public double ZoomFactor
    {
        get => _zoomFactor;
        set
        {
            double clamped = Math.Max(MinZoom, Math.Min(MaxZoom, value));
            if (Math.Abs(_zoomFactor - clamped) < 0.0001) return;
            _zoomFactor = clamped;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ZoomPercent));
        }
    }

    public string ZoomPercent => $"{(int)Math.Round(_zoomFactor * 100)}%";

    public double OffsetX
    {
        get => _offsetX;
        set { if (_offsetX == value) return; _offsetX = value; OnPropertyChanged(); }
    }

    public double OffsetY
    {
        get => _offsetY;
        set { if (_offsetY == value) return; _offsetY = value; OnPropertyChanged(); }
    }

    // ---------------------------------------------------------------------------
    // Display toggles
    // ---------------------------------------------------------------------------

    public bool SnapEnabled
    {
        get => _snapEnabled;
        set { if (_snapEnabled == value) return; _snapEnabled = value; OnPropertyChanged(); }
    }

    public bool GridVisible
    {
        get => _gridVisible;
        set { if (_gridVisible == value) return; _gridVisible = value; OnPropertyChanged(); }
    }

    public bool IsMultiSelectMode
    {
        get => _isMultiSelectMode;
        set { if (_isMultiSelectMode == value) return; _isMultiSelectMode = value; OnPropertyChanged(); }
    }

    // ---------------------------------------------------------------------------
    // Zoom commands
    // ---------------------------------------------------------------------------

    public void ZoomIn()  => ZoomFactor += ZoomStep;
    public void ZoomOut() => ZoomFactor -= ZoomStep;
    public void ResetZoom()
    {
        ZoomFactor = 1.0;
        OffsetX = 0;
        OffsetY = 0;
    }

    // ---------------------------------------------------------------------------
    // INotifyPropertyChanged
    // ---------------------------------------------------------------------------

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
