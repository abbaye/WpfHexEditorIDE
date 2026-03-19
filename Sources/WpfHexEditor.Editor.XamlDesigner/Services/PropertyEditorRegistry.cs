// ==========================================================
// Project: WpfHexEditor.Editor.XamlDesigner
// File: PropertyEditorRegistry.cs
// Author: Derek Tremblay
// Created: 2026-03-17
// Description:
//     Maps CLR property types to WPF DataTemplate keys used by the
//     Property Inspector panel to select the correct inline editor.
//     Replaces the basic PropertyEditorTemplateSelector from Phase 1.
//
// Architecture Notes:
//     Registry / Strategy pattern.
//     Each entry maps a System.Type (or type name string) to a resource key.
//     The PropertyInspectorPanel uses GetTemplateKey to pick the DataTemplate
//     from the merged ResourceDictionaries.
// ==========================================================

using System.Windows;
using System.Windows.Media;

namespace WpfHexEditor.Editor.XamlDesigner.Services;

/// <summary>
/// Maps property types to editor DataTemplate resource keys.
/// </summary>
public sealed class PropertyEditorRegistry
{
    private readonly Dictionary<Type, string> _typeToKey = new();

    // ── Singleton ─────────────────────────────────────────────────────────────

    public static PropertyEditorRegistry Instance { get; } = new();

    private PropertyEditorRegistry()
    {
        RegisterDefaults();
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the DataTemplate resource key for <paramref name="propertyType"/>,
    /// or the generic "TextPropertyTemplate" if no specific editor is registered.
    /// </summary>
    public string GetTemplateKey(Type propertyType)
    {
        if (_typeToKey.TryGetValue(propertyType, out var key))
            return key;

        // Walk base types.
        var t = propertyType.BaseType;
        while (t is not null && t != typeof(object))
        {
            if (_typeToKey.TryGetValue(t, out key))
                return key;
            t = t.BaseType;
        }

        // Check interfaces.
        foreach (var iface in propertyType.GetInterfaces())
        {
            if (_typeToKey.TryGetValue(iface, out key))
                return key;
        }

        return "TextPropertyTemplate";
    }

    /// <summary>Registers a custom editor key for a type.</summary>
    public void Register(Type propertyType, string templateKey)
        => _typeToKey[propertyType] = templateKey;

    // ── Private ───────────────────────────────────────────────────────────────

    private void RegisterDefaults()
    {
        _typeToKey[typeof(bool)]              = "BoolPropertyTemplate";
        _typeToKey[typeof(bool?)]             = "BoolPropertyTemplate";
        _typeToKey[typeof(Color)]             = "ColorPickerTemplate";
        _typeToKey[typeof(Brush)]             = "ColorPickerTemplate";
        _typeToKey[typeof(SolidColorBrush)]   = "ColorPickerTemplate";
        _typeToKey[typeof(LinearGradientBrush)]  = "GradientEditorTemplate";
        _typeToKey[typeof(RadialGradientBrush)]  = "GradientEditorTemplate";
        _typeToKey[typeof(FontFamily)]        = "FontPickerTemplate";
        _typeToKey[typeof(Thickness)]         = "ThicknessEditorTemplate";
        _typeToKey[typeof(CornerRadius)]      = "CornerRadiusEditorTemplate";
        _typeToKey[typeof(double)]            = "NumericSliderTemplate";
        _typeToKey[typeof(float)]             = "NumericSliderTemplate";
        _typeToKey[typeof(int)]               = "NumericSliderTemplate";
    }
}
