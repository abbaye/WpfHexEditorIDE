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

using System.Windows;
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
        var callback = _patchCallback; // capture for lambda
        return new PropertyEntry
        {
            Name          = e.PropertyName,
            Value         = e.Value?.ToString() ?? string.Empty,
            Type          = ResolveEntryType(e.PropertyType),
            IsReadOnly    = e.IsReadOnly,
            AllowedValues = e.AllowedValues,
            OnValueChanged = val => callback?.Invoke(e.PropertyName, val?.ToString())
        };
    }

    private static PropertyEntryType ResolveEntryType(Type? t)
    {
        if (t == typeof(bool))   return PropertyEntryType.Boolean;
        if (t == typeof(int))    return PropertyEntryType.Integer;
        if (t?.IsEnum == true)   return PropertyEntryType.Enum;
        return PropertyEntryType.Text;
    }

    private static int CategoryOrder(string category) => category switch
    {
        "Layout"           => 0,
        "Layout (Attached)"=> 1,
        "Appearance"       => 2,
        _                  => 3
    };
}
