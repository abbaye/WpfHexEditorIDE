//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

namespace WpfHexEditor.SDK.Models;

/// <summary>
/// Granular permissions a plugin may request and be granted at runtime.
/// </summary>
[Flags]
public enum PluginPermission : long
{
    /// <summary>No permissions granted.</summary>
    None = 0,

    /// <summary>Plugin may read and write to the file system.</summary>
    AccessFileSystem = 1L << 0,

    /// <summary>Plugin may make outbound network requests.</summary>
    AccessNetwork = 1L << 1,

    /// <summary>Plugin may read the active HexEditor content, selection, and offset.</summary>
    AccessHexEditor = 1L << 2,

    /// <summary>Plugin may read and interact with the active CodeEditor.</summary>
    AccessCodeEditor = 1L << 3,

    /// <summary>Plugin may register menus, toolbar items, and panels in the IDE.</summary>
    RegisterMenus = 1L << 4,

    /// <summary>Plugin may write to the OutputPanel.</summary>
    WriteOutput = 1L << 5,

    /// <summary>Plugin may write to the ErrorPanel.</summary>
    WriteErrorPanel = 1L << 6,

    /// <summary>Plugin may read and write to its own settings section.</summary>
    AccessSettings = 1L << 7,

    /// <summary>Plugin may write output lines to the IDE Terminal panel via <c>ITerminalService</c>.</summary>
    WriteTerminal = 1L << 9,

    /// <summary>Plugin is exclusively a Terminal extension (no UI theme required).</summary>
    TerminalOnly = 1L << 8,

    /// <summary>Plugin may register and unregister custom commands in the HxTerminal command registry.</summary>
    RegisterTerminalCommands = 1L << 10,
}
