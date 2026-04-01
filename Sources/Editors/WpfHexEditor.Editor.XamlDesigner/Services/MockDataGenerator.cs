// ==========================================================
// Project: WpfHexEditor.Editor.XamlDesigner
// File: MockDataGenerator.cs
// Author: Derek Tremblay
// Created: 2026-03-19
// Description:
//     Generates realistic mock values for common property types by
//     reflection, used by the Design-Time Data panel to produce
//     meaningful placeholder data without requiring real data sources.
//
// Architecture Notes:
//     Pure service — stateless. Reflection-based population strategy.
//     Gracefully skips properties that are read-only, indexed, or
//     throw during assignment. Per-property exceptions are caught and
//     silently swallowed to guarantee forward progress.
// ==========================================================

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace WpfHexEditor.Editor.XamlDesigner.Services;

/// <summary>
/// Populates public settable properties on an object instance with
/// realistic mock values derived from each property's name and type.
/// </summary>
public sealed class MockDataGenerator
{
    // ── Seeded name→value mappings for common string property names ───────────

    private static readonly IReadOnlyDictionary<string, string> WellKnownStringValues =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["FirstName"]   = "John",
            ["LastName"]    = "Smith",
            ["FullName"]    = "John Smith",
            ["Name"]        = "Sample Item",
            ["Title"]       = "Design-Time Title",
            ["Description"] = "This is a sample description for design-time data.",
            ["Email"]       = "john.smith@example.com",
            ["Phone"]       = "+1 (555) 123-4567",
            ["Address"]     = "123 Main Street",
            ["City"]        = "Springfield",
            ["Country"]     = "United States",
            ["ZipCode"]     = "12345",
            ["Url"]         = "https://example.com",
            ["Website"]     = "https://example.com",
            ["Username"]    = "jsmith",
            ["Company"]     = "Acme Corporation",
            ["Department"]  = "Engineering",
            ["Status"]      = "Active",
            ["Category"]    = "General",
            ["Tag"]         = "sample",
            ["Label"]       = "Label",
            ["Text"]        = "Sample text",
            ["Content"]     = "Sample content",
            ["Message"]     = "Hello, World!",
            ["Notes"]       = "Design-time notes placeholder.",
            ["Currency"]    = "USD",
            ["Symbol"]      = "$",
        };

    // ── Seeded name→value mappings for int property names ────────────────────

    private static readonly IReadOnlyDictionary<string, int> WellKnownIntValues =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["Age"]          = 32,
            ["Count"]        = 5,
            ["Total"]        = 42,
            ["Quantity"]     = 3,
            ["Index"]        = 0,
            ["Rank"]         = 1,
            ["Priority"]     = 2,
            ["Year"]         = 2026,
            ["Month"]        = 3,
            ["Day"]          = 19,
            ["Hour"]         = 9,
            ["Minute"]       = 30,
            ["Second"]       = 0,
            ["Page"]         = 1,
            ["PageSize"]     = 20,
            ["MaxItems"]     = 100,
        };

    private static readonly Random SharedRandom = new();

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Sets all public writable instance properties of <paramref name="instance"/>
    /// with realistic mock values based on their declared type and name.
    /// </summary>
    /// <param name="instance">The object to populate; must not be null.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="instance"/> is null.
    /// </exception>
    public void PopulateMockData(object instance)
    {
        if (instance is null)
            throw new ArgumentNullException(nameof(instance));

        PropertyInfo[] properties = instance
            .GetType()
            .GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (PropertyInfo property in properties)
            TrySetProperty(instance, property);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private static void TrySetProperty(object instance, PropertyInfo property)
    {
        if (!property.CanWrite)
            return;

        // Skip indexed properties (e.g. this[int]).
        if (property.GetIndexParameters().Length > 0)
            return;

        try
        {
            object? mockValue = GenerateMockValue(property.Name, property.PropertyType);
            if (mockValue is not null)
                property.SetValue(instance, mockValue);
        }
        catch
        {
            // Silently skip properties that throw during assignment.
        }
    }

    private static object? GenerateMockValue(string propertyName, Type propertyType)
    {
        // Exact type dispatch — most common types first.
        if (propertyType == typeof(string))
            return GenerateStringValue(propertyName);

        if (propertyType == typeof(int) || propertyType == typeof(int?))
            return GenerateIntValue(propertyName);

        if (propertyType == typeof(bool) || propertyType == typeof(bool?))
            return true;

        if (propertyType == typeof(DateTime) || propertyType == typeof(DateTime?))
            return DateTime.Today;

        if (propertyType == typeof(DateTimeOffset) || propertyType == typeof(DateTimeOffset?))
            return DateTimeOffset.UtcNow.Date;

        if (propertyType == typeof(double) || propertyType == typeof(double?))
            return GenerateDoubleValue(propertyName);

        if (propertyType == typeof(float) || propertyType == typeof(float?))
            return (float)GenerateDoubleValue(propertyName);

        if (propertyType == typeof(decimal) || propertyType == typeof(decimal?))
            return (decimal)GenerateDoubleValue(propertyName);

        if (propertyType == typeof(long) || propertyType == typeof(long?))
            return (long)GenerateIntValue(propertyName);

        if (propertyType == typeof(Guid) || propertyType == typeof(Guid?))
            return Guid.NewGuid();

        if (propertyType == typeof(TimeSpan) || propertyType == typeof(TimeSpan?))
            return TimeSpan.FromMinutes(30);

        if (propertyType == typeof(Uri))
            return new Uri("https://example.com");

        // IList and collection types: too complex, skip.
        if (typeof(IList).IsAssignableFrom(propertyType))
            return null;

        if (typeof(IEnumerable).IsAssignableFrom(propertyType) && propertyType != typeof(string))
            return null;

        // Nullable enum: return first defined value.
        Type? underlyingEnum = Nullable.GetUnderlyingType(propertyType);
        if (underlyingEnum?.IsEnum == true)
            return Enum.GetValues(underlyingEnum).GetValue(0);

        if (propertyType.IsEnum)
            return Enum.GetValues(propertyType).GetValue(0);

        return null;
    }

    private static string GenerateStringValue(string propertyName)
    {
        if (WellKnownStringValues.TryGetValue(propertyName, out string? known))
            return known;

        // Fallback: use the property name itself as a readable placeholder.
        return propertyName;
    }

    private static int GenerateIntValue(string propertyName)
    {
        if (WellKnownIntValues.TryGetValue(propertyName, out int known))
            return known;

        return SharedRandom.Next(1, 101);
    }

    private static double GenerateDoubleValue(string propertyName)
    {
        // Percentage-like properties → 0.0–1.0 range.
        if (propertyName.EndsWith("Ratio", StringComparison.OrdinalIgnoreCase)
            || propertyName.EndsWith("Percent", StringComparison.OrdinalIgnoreCase)
            || propertyName.Equals("Opacity", StringComparison.OrdinalIgnoreCase))
        {
            return Math.Round(SharedRandom.NextDouble(), 2);
        }

        return Math.Round(SharedRandom.NextDouble() * 100.0, 2);
    }
}
