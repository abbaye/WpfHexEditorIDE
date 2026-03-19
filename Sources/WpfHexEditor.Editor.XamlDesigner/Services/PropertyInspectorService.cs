// ==========================================================
// Project: WpfHexEditor.Editor.XamlDesigner
// File: PropertyInspectorService.cs
// Author: Derek Tremblay
// Created: 2026-03-16
// Description:
//     Reflects all DependencyProperties from a DependencyObject's type
//     and returns them as PropertyInspectorEntry instances grouped by category.
//
// Architecture Notes:
//     Pure C# service — no WPF rendering dependency beyond DependencyObject.
//     Uses DependencyPropertyDescriptor + reflection for DP discovery.
//     SetPropertyValue handles type conversion for TextBox input.
//     Phase D — attached property discovery: scans Grid/Canvas/DockPanel/
//     ScrollViewer for parent-attached DPs when the element has a typed parent.
//     Phase D — AllowedValues: enum DPs are enriched with Enum.GetValues() so
//     the PropertyInspectorPanel can render a ComboBox editor automatically.
// ==========================================================

using System.ComponentModel;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using WpfHexEditor.Editor.XamlDesigner.Models;

namespace WpfHexEditor.Editor.XamlDesigner.Services;

/// <summary>
/// Reflects the DependencyProperties of a <see cref="DependencyObject"/> for display
/// in the Property Inspector panel.
/// </summary>
public sealed class PropertyInspectorService
{
    // ── Layout category DP names ──────────────────────────────────────────────

    private static readonly HashSet<string> _layoutProps = new(StringComparer.OrdinalIgnoreCase)
    {
        "Width", "Height", "MinWidth", "MinHeight", "MaxWidth", "MaxHeight",
        "Margin", "Padding", "HorizontalAlignment", "VerticalAlignment",
        "HorizontalContentAlignment", "VerticalContentAlignment",
        "Canvas.Left", "Canvas.Top", "Canvas.Right", "Canvas.Bottom",
        "Grid.Row", "Grid.Column", "Grid.RowSpan", "Grid.ColumnSpan",
        "DockPanel.Dock", "StackPanel.ZIndex",
        "ActualWidth", "ActualHeight"
    };

    // ── Appearance category DP names ──────────────────────────────────────────

    private static readonly HashSet<string> _appearanceProps = new(StringComparer.OrdinalIgnoreCase)
    {
        "Background", "Foreground", "BorderBrush", "BorderThickness",
        "Opacity", "Visibility", "FontFamily", "FontSize", "FontWeight", "FontStyle",
        "TextAlignment", "TextWrapping", "TextTrimming",
        "IsEnabled", "IsTabStop", "CornerRadius"
    };

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns all DependencyProperties for the type of <paramref name="obj"/>,
    /// ordered by category then name. Returns an empty list when <paramref name="obj"/> is null.
    /// </summary>
    public IReadOnlyList<PropertyInspectorEntry> GetProperties(DependencyObject? obj)
    {
        if (obj is null) return Array.Empty<PropertyInspectorEntry>();

        var result  = new List<PropertyInspectorEntry>();
        var type    = obj.GetType();
        var visited = new HashSet<string>(StringComparer.Ordinal);

        // Walk the inheritance chain to collect all public static DP fields.
        foreach (var t in GetTypeHierarchy(type))
        {
            foreach (var field in t.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                if (field.FieldType != typeof(DependencyProperty)) continue;
                if (field.GetValue(null) is not DependencyProperty dp) continue;

                var name = dp.Name;
                if (!visited.Add(name)) continue; // skip if already seen from a subclass

                var descriptor = DependencyPropertyDescriptor.FromProperty(dp, type);
                if (descriptor is null) continue;

                object? currentValue;
                try   { currentValue = obj.GetValue(dp); }
                catch { continue; }

                bool isDefault = IsDefaultValue(obj, dp, currentValue);

                result.Add(new PropertyInspectorEntry
                {
                    PropertyName  = name,
                    CategoryName  = GetCategory(name),
                    PropertyType  = dp.PropertyType,
                    DP            = dp,
                    Value         = FormatValue(currentValue),
                    IsDefault     = isDefault,
                    IsReadOnly    = descriptor.IsReadOnly,
                    // Phase D (D5): populate allowed values for enum DPs.
                    AllowedValues = BuildAllowedValues(dp.PropertyType)
                });
            }
        }

        // Phase D: discover attached properties from common container-parent types.
        AppendAttachedProperties(obj, result, visited);

        // Sort: category order (Layout → Appearance → Misc), then name alphabetically.
        result.Sort((a, b) =>
        {
            int catA = CategoryOrder(a.CategoryName);
            int catB = CategoryOrder(b.CategoryName);
            return catA != catB
                ? catA.CompareTo(catB)
                : string.Compare(a.PropertyName, b.PropertyName, StringComparison.OrdinalIgnoreCase);
        });

        return result;
    }

    // ── Phase D: Attached property discovery ──────────────────────────────────

    /// <summary>
    /// Discovers parent-attached DPs from Grid, Canvas, DockPanel, and ScrollViewer
    /// when the element has a typed parent that matches one of those sources.
    /// </summary>
    private static void AppendAttachedProperties(
        DependencyObject obj,
        List<PropertyInspectorEntry> result,
        HashSet<string> visited)
    {
        if (obj is not FrameworkElement fe || fe.Parent is not DependencyObject parent)
            return;

        var attachedSources = new[] { typeof(Grid), typeof(Canvas), typeof(DockPanel), typeof(ScrollViewer) };

        foreach (var sourceType in attachedSources)
        {
            // Only inspect when the parent is an instance of this source type.
            if (!sourceType.IsInstanceOfType(parent))
                continue;

            AppendAttachedFromSource(obj, parent, sourceType, result, visited);
        }
    }

    private static void AppendAttachedFromSource(
        DependencyObject obj,
        DependencyObject parent,
        Type sourceType,
        List<PropertyInspectorEntry> result,
        HashSet<string> visited)
    {
        var fields = sourceType.GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.FieldType == typeof(DependencyProperty));

        foreach (var field in fields)
        {
            if (field.GetValue(null) is not DependencyProperty dp) continue;
            if (dp.ReadOnly) continue;

            var displayName = $"{sourceType.Name}.{dp.Name}";
            if (!visited.Add(displayName)) continue;

            object? currentValue;
            try   { currentValue = obj.GetValue(dp); }
            catch { continue; }

            var allowedValues = BuildAllowedValues(dp.PropertyType);

            result.Add(new PropertyInspectorEntry
            {
                PropertyName  = displayName,
                CategoryName  = "Layout (Attached)",
                PropertyType  = dp.PropertyType,
                DP            = dp,
                Value         = FormatValue(currentValue),
                IsDefault     = IsDefaultValue(obj, dp, currentValue),
                IsReadOnly    = false,
                AllowedValues = allowedValues
            });
        }
    }

    /// <summary>
    /// Attempts to set <paramref name="entry"/>'s value on <paramref name="obj"/>.
    /// Handles string → target type conversion for simple scalar types.
    /// </summary>
    public void SetPropertyValue(DependencyObject obj, PropertyInspectorEntry entry, object? value)
    {
        if (obj is null || entry.DP is null || entry.IsReadOnly) return;

        try
        {
            var targetType    = entry.DP.PropertyType;
            var convertedValue = Convert(value, targetType);
            obj.SetValue(entry.DP, convertedValue);
        }
        catch
        {
            // Swallow invalid conversion — leave current value intact.
        }
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private static IEnumerable<Type> GetTypeHierarchy(Type type)
    {
        var current = type;
        while (current is not null && current != typeof(object))
        {
            yield return current;
            current = current.BaseType;
        }
    }

    private static string GetCategory(string propertyName) =>
        _layoutProps.Contains(propertyName)     ? "Layout" :
        _appearanceProps.Contains(propertyName) ? "Appearance" :
        "Misc";

    private static int CategoryOrder(string category) => category switch
    {
        "Layout"     => 0,
        "Appearance" => 1,
        _            => 2
    };

    private static bool IsDefaultValue(DependencyObject obj, DependencyProperty dp, object? current)
    {
        try
        {
            var meta = dp.GetMetadata(obj.GetType());
            return Equals(current, meta?.DefaultValue);
        }
        catch
        {
            return false;
        }
    }

    private static object? FormatValue(object? value) => value switch
    {
        null   => "(null)",
        double d when double.IsNaN(d) => "Auto",
        _      => value
    };

    /// <summary>
    /// Returns an ordered list of all enum values when <paramref name="type"/> is an enum;
    /// otherwise returns null. Used to populate ComboBox editors in the Property Inspector.
    /// </summary>
    private static IReadOnlyList<object>? BuildAllowedValues(Type type)
    {
        if (!type.IsEnum) return null;
        return Enum.GetValues(type).Cast<object>().ToList();
    }

    private static object? Convert(object? value, Type targetType)
    {
        if (value is null) return null;
        if (targetType.IsInstanceOfType(value)) return value;

        if (value is string s)
        {
            var converter = TypeDescriptor.GetConverter(targetType);
            if (converter.CanConvertFrom(typeof(string)))
                return converter.ConvertFromInvariantString(s);
        }

        return System.Convert.ChangeType(value, targetType);
    }
}
