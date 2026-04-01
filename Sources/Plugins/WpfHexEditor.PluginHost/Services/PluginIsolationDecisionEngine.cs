// ==========================================================
// Project: WpfHexEditor.PluginHost
// File: PluginIsolationDecisionEngine.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-15
// Description:
//     Static engine that resolves PluginIsolationMode.Auto to a concrete
//     InProcess or Sandbox decision based on the plugin manifest's capability
//     declarations and trust level.
//
// Architecture Notes:
//     Pattern: Strategy — pure function with no state, directly testable.
//     Decision priority (first match wins):
//       1. Untrusted publisher        → Sandbox   (security always wins)
//       2. RegisterMenus == true      → InProcess (WPF panels require STA + HWND)
//       3. AccessNetwork == true      → Sandbox   (extra isolation for network-capable plugins)
//       4. IsTerminalOnly == true     → InProcess (no WPF, lightweight, safe in-process)
//       Default                       → InProcess (trusted, no dangerous caps)
// ==========================================================

using WpfHexEditor.SDK.Models;

namespace WpfHexEditor.PluginHost.Services;

/// <summary>
/// Resolves <see cref="PluginIsolationMode.Auto"/> to a concrete InProcess or Sandbox
/// decision based on the plugin manifest's capability declarations and trust level.
/// For manifests that explicitly declare InProcess or Sandbox, the declared value is
/// returned unchanged.
/// </summary>
internal static class PluginIsolationDecisionEngine
{
    /// <summary>
    /// Returns the concrete isolation mode for the given manifest.
    /// If <see cref="PluginManifest.IsolationMode"/> is not Auto, returns it unchanged.
    /// </summary>
    public static PluginIsolationMode Resolve(PluginManifest manifest)
    {
        if (manifest.IsolationMode != PluginIsolationMode.Auto)
            return manifest.IsolationMode;

        return ResolveFromCapabilities(manifest);
    }

    private static PluginIsolationMode ResolveFromCapabilities(PluginManifest manifest)
    {
        var p = manifest.Permissions;

        // Rule 1: Untrusted publisher — always sandbox regardless of capabilities.
        if (!manifest.TrustedPublisher)
            return PluginIsolationMode.Sandbox;

        // Rule 2: Plugin registers WPF panels / menus — must run in-process for STA + HWND integration.
        if (p?.RegisterMenus == true)
            return PluginIsolationMode.InProcess;

        // Rule 3: Network access — sandbox even for trusted plugins (extra exfiltration isolation).
        if (p?.AccessNetwork == true)
            return PluginIsolationMode.Sandbox;

        // Rule 4: Terminal-only plugin — no WPF UI, trivially safe in-process.
        if (p?.IsTerminalOnly == true)
            return PluginIsolationMode.InProcess;

        // Default: trusted publisher, no risky capabilities — run in-process.
        return PluginIsolationMode.InProcess;
    }
}
