// ==========================================================
// Project: WpfHexEditor.Plugins.AssemblyExplorer
// File: Events/AssemblyLoadedEvent.cs
// Author: Derek Tremblay
// Created: 2026-03-08
// Description:
//     EventBus event published when the AssemblyExplorer successfully
//     loads and analyzes a PE file. Other plugins can subscribe via
//     IPluginEventBus.Subscribe<AssemblyLoadedEvent>() to react.
//
// Architecture Notes:
//     Plugin-private event (namespace AssemblyExplorer.Events).
//     Not part of the SDK — depends on no SDK types.
// ==========================================================

namespace WpfHexEditor.Plugins.AssemblyExplorer.Events;

/// <summary>
/// Published on <c>IPluginEventBus</c> after the Assembly Explorer
/// successfully analyzes a PE file.
/// </summary>
public sealed class AssemblyLoadedEvent
{
    /// <summary>Absolute path to the analyzed PE file.</summary>
    public string FilePath { get; init; } = string.Empty;

    /// <summary>Simple assembly name (without extension).</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Assembly version from the AssemblyDef row, or null for native PE.</summary>
    public Version? Version { get; init; }

    /// <summary>True when the PE contains managed .NET metadata.</summary>
    public bool IsManaged { get; init; }

    /// <summary>Total number of type definitions found.</summary>
    public int TypeCount { get; init; }

    /// <summary>Total number of method definitions across all types.</summary>
    public int MethodCount { get; init; }
}
