//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.Text.Json.Serialization;

namespace WpfHexEditor.SDK.Models;

/// <summary>
/// Defines how a plugin is isolated from the IDE host process.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PluginIsolationMode
{
    /// <summary>
    /// The host automatically decides InProcess or Sandbox based on the plugin's
    /// capability declarations and trust level. This is the recommended default.
    /// Decision rules (first match wins):
    ///   1. Untrusted publisher        → Sandbox
    ///   2. RegisterMenus == true      → InProcess (WPF UI / STA required)
    ///   3. AccessNetwork == true      → Sandbox   (extra isolation)
    ///   4. IsTerminalOnly == true     → InProcess (no WPF UI)
    ///   Default                       → InProcess
    /// </summary>
    Auto,

    /// <summary>
    /// Plugin runs in the IDE process using an isolated <see cref="System.Runtime.Loader.AssemblyLoadContext"/>.
    /// Best performance, standard isolation.
    /// </summary>
    InProcess,

    /// <summary>
    /// Plugin runs in a separate <c>WpfHexEditor.PluginSandbox.exe</c> process.
    /// Maximum crash isolation — communicates via Named Pipes / IPC.
    /// Use for untrusted or experimental plugins.
    /// </summary>
    Sandbox
}
