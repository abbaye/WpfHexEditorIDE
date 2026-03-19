// ==========================================================
// Project: WpfHexEditor.Editor.XamlDesigner
// File: ResourceEntryViewModel.cs
// Author: Derek Tremblay
// Created: 2026-03-17
// Description:
//     ViewModel representing a single resource entry in the Resource Browser panel.
//     Holds the resource key, value type, scope, and a computed preview string.
//
// Architecture Notes:
//     INPC record-like object.
//     Preview computed once at construction (potentially expensive for large values).
// ==========================================================

using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace WpfHexEditor.Editor.XamlDesigner.ViewModels;

/// <summary>
/// A single resource entry for display in the Resource Browser panel.
/// </summary>
public sealed class ResourceEntryViewModel : INotifyPropertyChanged
{
    private bool _isSelected;

    // ── Constructor ───────────────────────────────────────────────────────────

    public ResourceEntryViewModel(object key, object? value, string scope)
    {
        Key          = key?.ToString() ?? "(null)";
        Scope        = scope;
        ValueType    = value?.GetType().Name ?? "(null)";
        Value        = value;
        PreviewText  = BuildPreview(value);
        PreviewBrush = value is Brush b ? b : null;
    }

    // ── Properties ────────────────────────────────────────────────────────────

    public string   Key          { get; }
    public string   Scope        { get; }
    public string   ValueType    { get; }
    public object?  Value        { get; }
    public string   PreviewText  { get; }
    public Brush?   PreviewBrush { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set { if (_isSelected == value) return; _isSelected = value; OnPropertyChanged(); }
    }

    // ── INPC ──────────────────────────────────────────────────────────────────

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    // ── Private ───────────────────────────────────────────────────────────────

    private static string BuildPreview(object? value)
    {
        return value switch
        {
            null                 => "(null)",
            SolidColorBrush scb  => $"#{scb.Color.A:X2}{scb.Color.R:X2}{scb.Color.G:X2}{scb.Color.B:X2}",
            System.Windows.Media.Color c
                                 => $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}",
            string s             => $"\"{s}\"",
            double d             => d.ToString("G4"),
            System.Windows.Thickness t => $"{t.Left},{t.Top},{t.Right},{t.Bottom}",
            _                    => value.ToString() ?? string.Empty
        };
    }
}
