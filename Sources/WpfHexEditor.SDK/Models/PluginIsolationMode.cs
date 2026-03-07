//////////////////////////////////////////////
// Apache 2.0  - 2026
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
    /// Plugin runs in the IDE process using an isolated <see cref="System.Runtime.Loader.AssemblyLoadContext"/>.
    /// Best performance, standard isolation â€” recommended default.
    /// </summary>
    InProcess,

    /// <summary>
    /// Plugin runs in a separate <c>WpfHexEditor.PluginSandbox.exe</c> process.
    /// Maximum crash isolation â€” communicates via Named Pipes / IPC.
    /// Use for untrusted or experimental plugins.
    /// </summary>
    Sandbox
}
