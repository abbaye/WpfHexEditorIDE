// ==========================================================
// Project: WpfHexEditor.Editor.XamlDesigner
// File: BindingValueResolver.cs
// Author: Derek Tremblay
// Created: 2026-03-19
// Description:
//     Reads the live resolved value of a BindingExpression on a
//     DependencyObject and resolves RelativeSource ancestors for
//     display in the Property Inspector / Binding Inspector panels.
//
// Architecture Notes:
//     Pure service — stateless, thread-agnostic (caller must be on
//     UI thread when accessing DependencyObjects).
//     Guard pattern: every public method wraps WPF calls in try/catch
//     and returns a safe display string on failure.
// ==========================================================

using System;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace WpfHexEditor.Editor.XamlDesigner.Services;

/// <summary>
/// Resolves live binding values and RelativeSource ancestors for display
/// in design-time inspector panels.
/// </summary>
public sealed class BindingValueResolver
{
    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the current resolved value of the binding on
    /// <paramref name="dp"/> as a display string.
    /// </summary>
    /// <param name="obj">The dependency object that owns the binding.</param>
    /// <param name="dp">The dependency property to inspect.</param>
    /// <returns>
    /// The resolved value's <c>ToString()</c>, <c>"(null)"</c> if the resolved
    /// source or value is null, or an error string if the inspection fails.
    /// </returns>
    public string ResolveCurrentValue(DependencyObject obj, DependencyProperty dp)
    {
        if (obj is null)
            return "(no object)";
        if (dp is null)
            return "(no property)";

        try
        {
            BindingExpressionBase? expression = BindingOperations.GetBindingExpressionBase(obj, dp);

            if (expression is null)
            {
                object? rawValue = obj.GetValue(dp);
                return FormatValue(rawValue);
            }

            if (expression is BindingExpression bindingExpression)
                return ResolveBindingExpression(obj, dp, bindingExpression);

            if (expression is MultiBindingExpression multiExpression)
                return ResolveMultiBindingExpression(multiExpression);

            return FormatValue(obj.GetValue(dp));
        }
        catch (Exception ex)
        {
            return $"(error: {ex.Message})";
        }
    }

    /// <summary>
    /// Walks the visual/logical tree upward from <paramref name="obj"/> to
    /// find the nearest ancestor of <paramref name="ancestorType"/>.
    /// </summary>
    /// <returns>
    /// The ancestor's type name and optional <c>Name</c> attribute if found;
    /// otherwise a descriptive string.
    /// </returns>
    public string ResolveRelativeSourceAncestor(DependencyObject obj, Type ancestorType)
    {
        if (obj is null)
            return "(no object)";
        if (ancestorType is null)
            return "(no ancestor type)";

        try
        {
            DependencyObject? current = GetParent(obj);

            while (current is not null)
            {
                if (ancestorType.IsInstanceOfType(current))
                    return BuildAncestorDisplayString(current, ancestorType);

                current = GetParent(current);
            }

            return $"(no ancestor of type {ancestorType.Name} found)";
        }
        catch (Exception ex)
        {
            return $"(error: {ex.Message})";
        }
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private static string ResolveBindingExpression(
        DependencyObject obj,
        DependencyProperty dp,
        BindingExpression expression)
    {
        object? resolvedSource = expression.ResolvedSource;
        if (resolvedSource is null)
            return "(binding source unresolved)";

        string resolvedPath   = expression.ResolvedSourcePropertyName ?? "(unknown path)";
        object? currentValue  = obj.GetValue(dp);

        return $"{FormatValue(currentValue)} ← {resolvedSource.GetType().Name}.{resolvedPath}";
    }

    private static string ResolveMultiBindingExpression(MultiBindingExpression expression)
    {
        int childCount = expression.BindingExpressions.Count;
        return $"(MultiBinding with {childCount} binding{(childCount == 1 ? "" : "s")})";
    }

    private static string BuildAncestorDisplayString(DependencyObject ancestor, Type ancestorType)
    {
        string typeName = ancestorType.Name;

        if (ancestor is FrameworkElement fe && !string.IsNullOrEmpty(fe.Name))
            return $"{typeName} (Name=\"{fe.Name}\")";

        return typeName;
    }

    private static DependencyObject? GetParent(DependencyObject obj)
    {
        // Prefer visual tree; fall back to logical tree for non-Visual nodes.
        if (obj is Visual or Visual3D)
        {
            DependencyObject? visualParent = VisualTreeHelper.GetParent(obj);
            if (visualParent is not null)
                return visualParent;
        }

        return LogicalTreeHelper.GetParent(obj);
    }

    private static string FormatValue(object? value)
        => value?.ToString() ?? "(null)";
}
