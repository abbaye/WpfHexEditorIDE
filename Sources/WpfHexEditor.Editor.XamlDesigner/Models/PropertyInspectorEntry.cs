// ==========================================================
// Project: WpfHexEditor.Editor.XamlDesigner
// File: PropertyInspectorEntry.cs
// Author: Derek Tremblay
// Created: 2026-03-16
// Description:
//     View model entry for a single property in the Property Inspector panel.
//     Wraps a DependencyProperty (or CLR property) on the selected element.
//
// Architecture Notes:
//     INPC — value changes trigger write-back to the live DependencyObject.
// ==========================================================

using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;

namespace WpfHexEditor.Editor.XamlDesigner.Models;

/// <summary>
/// Represents a single property row in the Property Inspector.
/// </summary>
public sealed class PropertyInspectorEntry : INotifyPropertyChanged
{
    private object? _value;

    // ── Identity ──────────────────────────────────────────────────────────────

    /// <summary>Display name of the property (e.g. "Width", "Background").</summary>
    public string PropertyName { get; init; } = string.Empty;

    /// <summary>Category for grouping (e.g. "Layout", "Appearance", "Misc").</summary>
    public string CategoryName { get; init; } = string.Empty;

    /// <summary>CLR type of the property value.</summary>
    public Type PropertyType { get; init; } = typeof(object);

    /// <summary>The backing DependencyProperty if available; null for CLR-only properties.</summary>
    public DependencyProperty? DP { get; init; }

    /// <summary>True when the property value equals the default for this element type.</summary>
    public bool IsDefault { get; init; }

    /// <summary>True when this property cannot be edited (read-only DP or no setter).</summary>
    public bool IsReadOnly { get; init; }

    // ── Value (INPC) ──────────────────────────────────────────────────────────

    /// <summary>
    /// Current property value. Setting this fires <see cref="PropertyChanged"/>
    /// and the owning ViewModel performs write-back to the live DependencyObject.
    /// </summary>
    public object? Value
    {
        get => _value;
        set
        {
            if (Equals(_value, value)) return;
            _value = value;
            OnPropertyChanged();
        }
    }

    // ── INPC ──────────────────────────────────────────────────────────────────

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
