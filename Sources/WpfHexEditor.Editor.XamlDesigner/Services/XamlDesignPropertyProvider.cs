// ==========================================================
// Project: WpfHexEditor.Editor.XamlDesigner
// File: XamlDesignPropertyProvider.cs
// Created: 2026-03-18
// Description:
//     Bridges the XAML Designer's selected element to the IDE's
//     F4 Properties panel via IPropertyProvider.
//     Translates PropertyInspectorService output into IDE PropertyGroup format.
//
// Architecture Notes:
//     Adapter Pattern — adapts XD PropertyInspectorEntry → IDE PropertyEntry.
//     Long-lived provider: SetTarget is called on each selection change,
//     and PropertiesChanged is raised to refresh the panel.
// ==========================================================

using System.Globalization;
using System.Windows;
using System.Windows.Media;
using WpfHexEditor.Editor.Core;
using WpfHexEditor.Editor.XamlDesigner.Models;

namespace WpfHexEditor.Editor.XamlDesigner.Services;

/// <summary>
/// Adapts the XAML Designer's selected <see cref="DependencyObject"/> to
/// the IDE's <see cref="IPropertyProvider"/> contract for the F4 Properties panel.
/// </summary>
public sealed class XamlDesignPropertyProvider : IPropertyProvider
{
    private readonly PropertyInspectorService _inspector = new();

    private DependencyObject?           _target;
    private string                      _label         = "No selection";
    private Action<string, string?>?    _patchCallback;

    // ── IPropertyProvider ─────────────────────────────────────────────────────

    /// <inheritdoc/>
    public string ContextLabel => _label;

    /// <inheritdoc/>
    public event EventHandler? PropertiesChanged;

    /// <inheritdoc/>
    public IReadOnlyList<PropertyGroup> GetProperties()
    {
        if (_target is null) return Array.Empty<PropertyGroup>();

        var entries = _inspector.GetProperties(_target);
        return MapEntriesToGroups(entries);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Called by <see cref="Controls.XamlDesignerSplitHost"/> whenever the canvas
    /// selection changes. Raises <see cref="PropertiesChanged"/> so the panel refreshes.
    /// </summary>
    /// <param name="obj">Newly selected element, or null for "no selection".</param>
    /// <param name="label">Short type description shown in the panel's header.</param>
    /// <param name="patchCallback">
    /// Invoked when the user edits a property value in the panel.
    /// Signature: (propertyName, newStringValue).
    /// </param>
    public void SetTarget(DependencyObject? obj, string label, Action<string, string?> patchCallback)
    {
        _target        = obj;
        _label         = label;
        _patchCallback = patchCallback;
        PropertiesChanged?.Invoke(this, EventArgs.Empty);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private IReadOnlyList<PropertyGroup> MapEntriesToGroups(IReadOnlyList<PropertyInspectorEntry> entries)
    {
        return entries
            .GroupBy(e => e.CategoryName)
            .OrderBy(g => CategoryOrder(g.Key))
            .Select(g => new PropertyGroup
            {
                Name    = g.Key,
                Entries = g.Select(MapToIdeEntry).ToList()
            })
            .ToList();
    }

    private PropertyEntry MapToIdeEntry(PropertyInspectorEntry e)
    {
        var callback   = _patchCallback; // capture for lambda
        var entryType  = ResolveEntryType(e.PropertyType);
        var display    = SerializeValueForType(e.Value, entryType);

        return new PropertyEntry
        {
            Name             = e.PropertyName,
            Value            = display,
            Type             = entryType,
            IsReadOnly       = e.IsReadOnly,
            AllowedValues    = e.AllowedValues,
            IsDefault        = e.IsDefault,                                     // P7
            Validator        = BuildValidator(e.PropertyType),                   // P6
            OnValueChanged   = val => callback?.Invoke(e.PropertyName, val?.ToString()),
            OnResetToDefault = e.IsDefault ? null :                              // P7
                               () => callback?.Invoke(e.PropertyName, null)
        };
    }

    /// <summary>
    /// Returns a type-appropriate validator delegate for inline validation (P6).
    /// Returns <see langword="null"/> when no validation is needed.
    /// </summary>
    private static Func<object?, string?>? BuildValidator(Type? t)
    {
        if (t == typeof(double) || t == typeof(float))
            return val =>
            {
                var s = val?.ToString();
                if (string.Equals(s, "Auto", StringComparison.OrdinalIgnoreCase)) return null;
                return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out _)
                    ? null
                    : "Must be a number or 'Auto'";
            };

        if (t == typeof(int))
            return val => int.TryParse(val?.ToString(), out _)
                ? null
                : "Must be an integer";

        if (t == typeof(Thickness))
            return val =>
            {
                var parts = val?.ToString()?.Split(',');
                return parts?.Length >= 1 && parts.All(p => double.TryParse(p.Trim(),
                    NumberStyles.Float, CultureInfo.InvariantCulture, out _))
                    ? null
                    : "Must be 'uniform', 'h,v', or 'L,T,R,B'";
            };

        if (typeof(Brush).IsAssignableFrom(t))
            return val =>
            {
                try { ColorConverter.ConvertFromString(val?.ToString()); return null; }
                catch { return "Invalid color format (e.g. #RRGGBB or named color)"; }
            };

        return null;
    }

    private static PropertyEntryType ResolveEntryType(Type? t)
    {
        if (t is null)                                              return PropertyEntryType.Text;
        if (t == typeof(bool))                                      return PropertyEntryType.Boolean;
        if (t == typeof(int))                                       return PropertyEntryType.Integer;
        if (t == typeof(double) || t == typeof(float))              return PropertyEntryType.Integer;
        if (t.IsEnum)                                               return PropertyEntryType.Enum;
        if (t == typeof(Thickness))                                 return PropertyEntryType.Thickness;
        if (t == typeof(Color))                                     return PropertyEntryType.Color;
        if (typeof(Brush).IsAssignableFrom(t))                      return PropertyEntryType.Brush;
        return PropertyEntryType.Text;
    }

    /// <summary>
    /// Serializes a property value to its display string, applying special formatting
    /// for Thickness (comma-separated L,T,R,B) and SolidColorBrush (#AARRGGBB).
    /// </summary>
    private static string SerializeValueForType(object? value, PropertyEntryType type) => type switch
    {
        PropertyEntryType.Thickness when value is Thickness t
            => $"{t.Left},{t.Top},{t.Right},{t.Bottom}",
        PropertyEntryType.Brush when value is SolidColorBrush scb
            => scb.Color.ToString(),
        _   => value?.ToString() ?? string.Empty
    };

    private static int CategoryOrder(string category) => category switch
    {
        "Layout"           => 0,
        "Layout (Attached)"=> 1,
        "Appearance"       => 2,
        _                  => 3
    };
}
