// ==========================================================
// Project: WpfHexEditor.Editor.XamlDesigner
// File: DesignDataJsonSerializer.cs
// Author: Derek Tremblay
// Created: 2026-03-19
// Description:
//     Serializes a design-time instance to a formatted JSON string for
//     display in the Design Data panel's JSON tab. Provides a safe
//     fallback message when serialization is not possible.
//
// Architecture Notes:
//     Pure service — stateless.
//     Uses System.Text.Json (inbox in .NET 8) with depth-limited options
//     to avoid StackOverflowException on self-referential WPF object graphs.
//     All exceptions are caught and surfaced as a human-readable error string
//     so the panel always has something to display.
// ==========================================================

using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WpfHexEditor.Editor.XamlDesigner.Services;

/// <summary>
/// Serializes design-time data instances to indented JSON for the
/// Design Data panel's JSON tab.
/// </summary>
public sealed class DesignDataJsonSerializer
{
    // ── Shared serializer options ─────────────────────────────────────────────

    // MaxDepth = 4 prevents stack overflow on deep or circular WPF object graphs.
    // ReferenceHandler.IgnoreCycles is the belt-and-suspenders guard.
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented       = true,
        MaxDepth            = 4,
        ReferenceHandler    = ReferenceHandler.IgnoreCycles,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Serializes <paramref name="instance"/> to an indented JSON string.
    /// </summary>
    /// <param name="instance">
    /// The object to serialize. When <see langword="null"/> the return value
    /// is the literal JSON string <c>"null"</c>.
    /// </param>
    /// <returns>
    /// An indented JSON string on success, or a
    /// <c>(serialization error: …)</c> message on failure.
    /// </returns>
    public string Serialize(object? instance)
    {
        if (instance is null)
            return "null";

        try
        {
            return JsonSerializer.Serialize(instance, instance.GetType(), SerializerOptions);
        }
        catch (Exception ex)
        {
            return BuildErrorMessage(ex);
        }
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private static string BuildErrorMessage(Exception ex)
    {
        // Surface only the innermost useful message to keep the panel readable.
        Exception root = ex;
        while (root.InnerException is not null)
            root = root.InnerException;

        return $"(serialization error: {root.Message})";
    }
}
