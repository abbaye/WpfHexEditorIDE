// ==========================================================
// Project: WpfHexEditor.SDK
// File: Models/PluginAssemblyConflictInfo.cs
// Created: 2026-03-15
// Description:
//     Describes a dependency version conflict detected when a plugin's AssemblyLoadContext
//     tried to load an assembly version different from the one already in the host ALC.
//
// Architecture Notes:
//     Placed in SDK so PluginManagerControl.xaml can bind to it without a PluginHost reference.
//     Host ALC always wins — this record is for diagnostics display only.
// ==========================================================

namespace WpfHexEditor.SDK.Models;

/// <summary>
/// Records a version conflict detected in a plugin's <c>PluginLoadContext</c> when the plugin's
/// assembly directory contains a different version of an assembly already loaded in the host ALC.
/// The host version always wins; this record surfaces the discrepancy for diagnostics.
/// </summary>
public sealed record PluginAssemblyConflictInfo(
    string AssemblyName,
    Version HostVersion,
    Version RequestedVersion,
    DateTime DetectedAt);
