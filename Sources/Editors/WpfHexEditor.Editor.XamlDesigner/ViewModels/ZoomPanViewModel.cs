// ==========================================================
// Project: WpfHexEditor.Editor.XamlDesigner
// File: ZoomPanViewModel.cs
// Author: Derek Tremblay
// Created: 2026-03-17
// Description:
//     ViewModel bridging ZoomPanCanvas state to toolbar controls.
//     Provides zoom-level display text and ICommand bindings for the
//     zoom presets and fit-to-content action.
//
// Architecture Notes:
//     INPC + RelayCommand. Wraps the ZoomPanCanvas instance injected
//     by XamlDesignerSplitHost during construction.
// ==========================================================

using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using WpfHexEditor.Editor.XamlDesigner.Controls;
using WpfHexEditor.SDK.Commands;

namespace WpfHexEditor.Editor.XamlDesigner.ViewModels;

/// <summary>
/// ViewModel for zoom and pan controls in the designer toolbar.
/// </summary>
public sealed class ZoomPanViewModel : INotifyPropertyChanged
{
    private readonly ZoomPanCanvas _canvas;

    // ── Constructor ───────────────────────────────────────────────────────────

    public ZoomPanViewModel(ZoomPanCanvas canvas)
    {
        _canvas = canvas;
        _canvas.ZoomChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(ZoomPercent));
            OnPropertyChanged(nameof(ZoomLabel));
        };

        ZoomInCommand       = new RelayCommand(_ => _canvas.ZoomIn());
        ZoomOutCommand      = new RelayCommand(_ => _canvas.ZoomOut());
        ZoomResetCommand    = new RelayCommand(_ => _canvas.ZoomReset());
        FitToContentCommand = new RelayCommand(_ => _canvas.FitToContent());
        SetZoomCommand      = new RelayCommand(p =>
        {
            if (p is double d)    _canvas.ZoomLevel = d;
            else if (p is string s && double.TryParse(s, out double pct))
                _canvas.ZoomLevel = pct / 100.0;
        });
    }

    // ── Properties ────────────────────────────────────────────────────────────

    /// <summary>Current zoom level as a percentage integer (e.g. 150).</summary>
    public int ZoomPercent => (int)Math.Round(_canvas.ZoomLevel * 100.0);

    /// <summary>Human-readable label for status bar / combobox (e.g. "150%").</summary>
    public string ZoomLabel => $"{ZoomPercent}%";

    /// <summary>Available zoom presets for the combobox.</summary>
    public static IReadOnlyList<ZoomPreset> Presets { get; } =
    [
        new ZoomPreset("10%",  0.10),
        new ZoomPreset("25%",  0.25),
        new ZoomPreset("50%",  0.50),
        new ZoomPreset("75%",  0.75),
        new ZoomPreset("100%", 1.00),
        new ZoomPreset("125%", 1.25),
        new ZoomPreset("150%", 1.50),
        new ZoomPreset("200%", 2.00),
        new ZoomPreset("300%", 3.00),
        new ZoomPreset("400%", 4.00),
        new ZoomPreset("Fit",  -1.0),
    ];

    // ── Commands ──────────────────────────────────────────────────────────────

    public ICommand ZoomInCommand       { get; }
    public ICommand ZoomOutCommand      { get; }
    public ICommand ZoomResetCommand    { get; }
    public ICommand FitToContentCommand { get; }
    public ICommand SetZoomCommand      { get; }

    // ── INPC ──────────────────────────────────────────────────────────────────

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>A named zoom preset for the zoom combobox.</summary>
public sealed record ZoomPreset(string Label, double Value);

