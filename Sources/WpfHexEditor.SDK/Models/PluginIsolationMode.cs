// ==========================================================
// Project: WpfHexEditor.SDK
// File: PluginIsolationMode.cs
// Author: Auto
// Created: 2026-03-06
// Description:
//     Enumeration of plugin isolation modes declared in the manifest.
//
// Architecture Notes:
//     InProcess: AssemblyLoadContext isolation (default, best performance).
//     Sandbox:   Out-of-process via WpfHexEditor.PluginSandbox.exe (max isolation).
//
// ==========================================================

namespace WpfHexEditor.SDK.Models;

/// <summary>
/// Defines how a plugin is isolated from the IDE host process.
/// </summary>
public enum PluginIsolationMode
{
    /// <summary>
    /// Plugin runs in the IDE process using an isolated <see cref="System.Runtime.Loader.AssemblyLoadContext"/>.
    /// Best performance, standard isolation — recommended default.
    /// </summary>
    InProcess,

    /// <summary>
    /// Plugin runs in a separate <c>WpfHexEditor.PluginSandbox.exe</c> process.
    /// Maximum crash isolation — communicates via Named Pipes / IPC.
    /// Use for untrusted or experimental plugins.
    /// </summary>
    Sandbox
}
