// ==========================================================
// Project: WpfHexEditor.Editor.XamlDesigner
// File: BindingInspectorService.cs
// Author: Derek Tremblay
// Created: 2026-03-17
// Description:
//     Inspects live BindingExpression instances on a DependencyObject,
//     returning structured BindingInfo records for display in the
//     binding editor inline in the Property Inspector.
//
// Architecture Notes:
//     Pure reflection service — no WPF rendering dependency.
//     Uses BindingOperations.GetBindingExpression and GetBinding.
// ==========================================================

using System.Windows;
using System.Windows.Data;

namespace WpfHexEditor.Editor.XamlDesigner.Services;

/// <summary>
/// Retrieves binding information from a live DependencyObject.
/// </summary>
public sealed class BindingInspectorService
{
    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns binding details for <paramref name="dp"/> on <paramref name="obj"/>,
    /// or null if no binding is present.
    /// </summary>
    public BindingInfo? GetBinding(DependencyObject obj, DependencyProperty dp)
    {
        var expression = BindingOperations.GetBindingExpression(obj, dp);
        if (expression is null) return null;

        var binding = BindingOperations.GetBinding(obj, dp);
        if (binding is null) return null;

        return new BindingInfo(
            Path:                        binding.Path?.Path ?? string.Empty,
            Mode:                        binding.Mode,
            Source:                      binding.Source?.GetType().Name ?? string.Empty,
            ElementName:                 binding.ElementName ?? string.Empty,
            RelativeSource:              binding.RelativeSource?.Mode.ToString() ?? string.Empty,
            Converter:                   binding.Converter?.GetType().Name ?? string.Empty,
            FallbackValue:               binding.FallbackValue?.ToString() ?? string.Empty,
            TargetNullValue:             binding.TargetNullValue?.ToString() ?? string.Empty,
            UpdateSourceTrigger:         binding.UpdateSourceTrigger,
            ValidatesOnDataErrors:       binding.ValidatesOnDataErrors,
            ValidatesOnNotifyDataErrors: binding.ValidatesOnNotifyDataErrors,
            IsValid:                     expression.Status == BindingStatus.Active);
    }

    /// <summary>
    /// Returns all bindings set on the given object, including MultiBindings.
    /// </summary>
    public IReadOnlyList<(DependencyProperty Dp, BindingInfo Info)> GetAllBindings(DependencyObject obj)
    {
        var result = new List<(DependencyProperty, BindingInfo)>();
        var localValueEnumerator = obj.GetLocalValueEnumerator();

        while (localValueEnumerator.MoveNext())
        {
            var entry = localValueEnumerator.Current;
            if (entry.Value is not BindingExpressionBase) continue;

            // Check for MultiBinding first.
            var multiBinding = BindingOperations.GetMultiBinding(obj, entry.Property);
            if (multiBinding is not null)
            {
                var children = new List<BindingInfo>();
                foreach (var child in multiBinding.Bindings)
                {
                    if (child is Binding cb)
                        children.Add(BuildBindingInfo(cb, isValid: true));
                }

                result.Add((entry.Property, new BindingInfo(
                    Path:                        string.Empty,
                    Mode:                        multiBinding.Mode,
                    Source:                      "(MultiBinding)",
                    ElementName:                 string.Empty,
                    RelativeSource:              string.Empty,
                    Converter:                   multiBinding.Converter?.GetType().Name ?? string.Empty,
                    FallbackValue:               multiBinding.FallbackValue?.ToString() ?? string.Empty,
                    TargetNullValue:             string.Empty,
                    UpdateSourceTrigger:         multiBinding.UpdateSourceTrigger,
                    ValidatesOnDataErrors:       multiBinding.ValidatesOnDataErrors,
                    ValidatesOnNotifyDataErrors: multiBinding.ValidatesOnNotifyDataErrors,
                    IsValid:                     true,
                    IsMultiBinding:              true,
                    ChildBindings:               children)));
                continue;
            }

            var info = GetBinding(obj, entry.Property);
            if (info is not null)
                result.Add((entry.Property, info));
        }

        return result;
    }

    // ── Private ───────────────────────────────────────────────────────────────

    private static BindingInfo BuildBindingInfo(Binding binding, bool isValid) =>
        new(Path:                        binding.Path?.Path ?? string.Empty,
            Mode:                        binding.Mode,
            Source:                      binding.Source?.GetType().Name ?? string.Empty,
            ElementName:                 binding.ElementName ?? string.Empty,
            RelativeSource:              binding.RelativeSource?.Mode.ToString() ?? string.Empty,
            Converter:                   binding.Converter?.GetType().Name ?? string.Empty,
            FallbackValue:               binding.FallbackValue?.ToString() ?? string.Empty,
            TargetNullValue:             binding.TargetNullValue?.ToString() ?? string.Empty,
            UpdateSourceTrigger:         binding.UpdateSourceTrigger,
            ValidatesOnDataErrors:       binding.ValidatesOnDataErrors,
            ValidatesOnNotifyDataErrors: binding.ValidatesOnNotifyDataErrors,
            IsValid:                     isValid);
}

/// <summary>Structured information about a WPF Binding (or MultiBinding) expression.</summary>
public sealed record BindingInfo(
    string              Path,
    BindingMode         Mode,
    string              Source,
    string              ElementName,
    string              RelativeSource,
    string              Converter,
    string              FallbackValue,
    string              TargetNullValue,
    UpdateSourceTrigger UpdateSourceTrigger,
    bool                ValidatesOnDataErrors,
    bool                ValidatesOnNotifyDataErrors,
    bool                IsValid,
    bool                IsMultiBinding    = false,
    IReadOnlyList<BindingInfo>? ChildBindings = null);
