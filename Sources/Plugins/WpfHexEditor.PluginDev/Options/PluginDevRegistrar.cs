// ==========================================================
// Project: WpfHexEditor.PluginDev
// File: Options/PluginDevRegistrar.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     One-shot registrar called from the host application at startup
//     to wire PluginDev services into the IDE.
// ==========================================================

using WpfHexEditor.Core.Options;

namespace WpfHexEditor.PluginDev.Options;

/// <summary>
/// Registers PluginDev options page and other IDE extensions at host startup.
/// Call <see cref="Register"/> once after the IDE host is initialised.
/// </summary>
public static class PluginDevRegistrar
{
    private static bool _registered;

    /// <summary>
    /// Registers the "Plugin Development" options page via <see cref="OptionsPageRegistry"/>.
    /// Idempotent — safe to call multiple times.
    /// </summary>
    public static void Register()
    {
        if (_registered) return;
        _registered = true;

        OptionsPageRegistry.RegisterDynamic(
            "Tools",
            "Plugin Development",
            static () => new PluginDevOptionsPage(),
            "🛠");
    }
}
