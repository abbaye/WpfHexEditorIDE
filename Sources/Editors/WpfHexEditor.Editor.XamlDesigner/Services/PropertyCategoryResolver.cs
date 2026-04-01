// ==========================================================
// Project: WpfHexEditor.Editor.XamlDesigner
// File: PropertyCategoryResolver.cs
// Author: Derek Tremblay
// Created: 2026-03-19
// Description:
//     Maps DependencyProperty names to display category strings for
//     the Property Inspector grouping in the XAML Designer panels.
//
// Architecture Notes:
//     Pure service — stateless, immutable lookup tables via
//     ImmutableHashSet for O(1) membership tests.
//     Strategy pattern: each category is a frozen set; resolution
//     walks categories in priority order and falls back to
//     "Miscellaneous".
// ==========================================================

using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;

namespace WpfHexEditor.Editor.XamlDesigner.Services;

/// <summary>
/// Maps a dependency-property name to a display category string suitable
/// for grouping in a Property Inspector panel.
/// </summary>
public sealed class PropertyCategoryResolver
{
    // ── Category labels ────────────────────────────────────────────────────────

    public const string CategoryLayout        = "Layout";
    public const string CategoryAppearance    = "Appearance";
    public const string CategoryText          = "Text";
    public const string CategoryBehavior      = "Behavior";
    public const string CategoryTransform     = "Transform";
    public const string CategoryEvents        = "Events";
    public const string CategoryMiscellaneous = "Miscellaneous";

    // ── Lookup tables (built once, shared across all instances) ───────────────
    // Note: FrozenSet.ToFrozenSet(comparer) is the .NET 8-compatible API.
    // FrozenSet.Create(comparer, params) requires .NET 9+.

    private static readonly FrozenSet<string> LayoutProperties = new[]
    {
        "Width", "Height", "MinWidth", "MaxWidth", "MinHeight", "MaxHeight",
        "Margin", "Padding",
        "HorizontalAlignment", "VerticalAlignment",
        "HorizontalContentAlignment", "VerticalContentAlignment",
        "Dock",
        "Row", "Column", "RowSpan", "ColumnSpan",
        "Canvas.Left", "Canvas.Top", "Canvas.Right", "Canvas.Bottom",
        "Panel.ZIndex",
        "Grid.Row", "Grid.Column", "Grid.RowSpan", "Grid.ColumnSpan",
    }.ToFrozenSet(StringComparer.Ordinal);

    private static readonly FrozenSet<string> AppearanceProperties = new[]
    {
        "Background", "Foreground", "BorderBrush", "BorderThickness",
        "Opacity", "Visibility", "CornerRadius",
        "Fill", "Stroke", "StrokeThickness",
        "Effect", "Style", "Template",
        "RenderTransform", "LayoutTransform",
    }.ToFrozenSet(StringComparer.Ordinal);

    private static readonly FrozenSet<string> TextProperties = new[]
    {
        "FontFamily", "FontSize", "FontWeight", "FontStyle", "FontStretch",
        "TextAlignment", "TextWrapping", "TextTrimming", "TextDecorations",
        "LineHeight", "Text", "Content",
    }.ToFrozenSet(StringComparer.Ordinal);

    private static readonly FrozenSet<string> BehaviorProperties = new[]
    {
        "IsEnabled", "IsTabStop", "TabIndex", "Focusable",
        "IsHitTestVisible", "AllowDrop", "Cursor",
        "ContextMenu", "InputBindings", "CommandBindings", "ToolTip",
    }.ToFrozenSet(StringComparer.Ordinal);

    // Transform intentionally overlaps with Appearance for RenderTransform /
    // LayoutTransform; Transform wins when the property is *exclusively*
    // transform-related (e.g. RenderTransformOrigin, ClipToBounds).
    private static readonly FrozenSet<string> TransformProperties = new[]
    {
        "RenderTransformOrigin", "ClipToBounds", "Clip",
        "SnapsToDevicePixels", "UseLayoutRounding",
    }.ToFrozenSet(StringComparer.Ordinal);

    // ── Ordered resolution pipeline ───────────────────────────────────────────

    // (category label, predicate) — evaluated top-to-bottom; first match wins.
    private static readonly IReadOnlyList<(string Category, Predicate<(string Name, Type Type)> Matches)> Pipeline =
    [
        (CategoryLayout,     ctx => LayoutProperties.Contains(ctx.Name)),
        (CategoryTransform,  ctx => TransformProperties.Contains(ctx.Name)),
        (CategoryAppearance, ctx => AppearanceProperties.Contains(ctx.Name)),
        (CategoryText,       ctx => TextProperties.Contains(ctx.Name)),
        (CategoryBehavior,   ctx => BehaviorProperties.Contains(ctx.Name)),
        (CategoryEvents,     ctx => IsEventCategory(ctx.Name, ctx.Type)),
    ];

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the display category for a property with the given
    /// <paramref name="propertyName"/> and <paramref name="propertyType"/>.
    /// </summary>
    /// <param name="propertyName">
    /// The CLR property name, optionally qualified (e.g. <c>"Canvas.Left"</c>).
    /// </param>
    /// <param name="propertyType">
    /// The CLR type of the property value; used to detect event-typed entries.
    /// </param>
    /// <returns>One of the <c>Category*</c> constants defined on this class.</returns>
    public string GetCategory(string propertyName, Type propertyType)
    {
        if (string.IsNullOrEmpty(propertyName))
            return CategoryMiscellaneous;

        var context = (Name: propertyName, Type: propertyType ?? typeof(object));

        foreach ((string category, Predicate<(string, Type)> matches) in Pipeline)
        {
            if (matches(context))
                return category;
        }

        return CategoryMiscellaneous;
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private static bool IsEventCategory(string propertyName, Type propertyType)
    {
        // Delegate types represent event handlers.
        if (propertyType.IsSubclassOf(typeof(Delegate)))
            return true;

        // Convention: properties ending in "Command" are command bindings
        // displayed under the Events grouping (mirrors VS behaviour).
        return propertyName.EndsWith("Command", StringComparison.Ordinal);
    }
}
