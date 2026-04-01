//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

namespace WpfHexEditor.SDK.Models;

/// <summary>
/// Declares the capabilities (permissions) a plugin requires.
/// Serialised as the <c>permissions</c> section in <c>WpfHexEditor.plugin.json</c>.
/// </summary>
public sealed class PluginCapabilities
{
    /// <summary>Plugin requires read/write access to the file system.</summary>
    public bool AccessFileSystem { get; set; }

    /// <summary>Plugin requires outbound network access.</summary>
    public bool AccessNetwork { get; set; }

    /// <summary>Plugin requires read access to HexEditor content, selection, and offset.</summary>
    public bool AccessHexEditor { get; set; }

    /// <summary>Plugin requires read/interaction access to the CodeEditor.</summary>
    public bool AccessCodeEditor { get; set; }

    /// <summary>Plugin registers menus, toolbar items, or dockable panels.</summary>
    public bool RegisterMenus { get; set; }

    /// <summary>Plugin writes to the OutputPanel.</summary>
    public bool WriteOutput { get; set; }

    /// <summary>Plugin writes to the ErrorPanel.</summary>
    public bool WriteErrorPanel { get; set; }

    /// <summary>Plugin reads or writes its own settings section.</summary>
    public bool AccessSettings { get; set; }

    /// <summary>Plugin writes output lines to the IDE Terminal panel via <c>ITerminalService</c>.</summary>
    public bool WriteTerminal { get; set; }

    /// <summary>
    /// Plugin is exclusively a Terminal command extension.
    /// When true, WPF theme compliance is not required for this plugin.
    /// Must be declared in the manifest as <c>"isTerminalOnly": true</c>.
    /// </summary>
    public bool IsTerminalOnly { get; set; }

    /// <summary>
    /// Plugin registers and unregisters custom commands in the HxTerminal command registry
    /// via <c>ITerminalService.RegisterCommand</c> / <c>UnregisterCommand</c>.
    /// </summary>
    public bool RegisterTerminalCommands { get; set; }

    /// <summary>
    /// Converts this capability declaration to the corresponding <see cref="PluginPermission"/> flags.
    /// </summary>
    public PluginPermission ToPermissionFlags()
    {
        var flags = PluginPermission.None;
        if (AccessFileSystem)          flags |= PluginPermission.AccessFileSystem;
        if (AccessNetwork)             flags |= PluginPermission.AccessNetwork;
        if (AccessHexEditor)           flags |= PluginPermission.AccessHexEditor;
        if (AccessCodeEditor)          flags |= PluginPermission.AccessCodeEditor;
        if (RegisterMenus)             flags |= PluginPermission.RegisterMenus;
        if (WriteOutput)               flags |= PluginPermission.WriteOutput;
        if (WriteErrorPanel)           flags |= PluginPermission.WriteErrorPanel;
        if (AccessSettings)            flags |= PluginPermission.AccessSettings;
        if (WriteTerminal)             flags |= PluginPermission.WriteTerminal;
        if (IsTerminalOnly)            flags |= PluginPermission.TerminalOnly;
        if (RegisterTerminalCommands)  flags |= PluginPermission.RegisterTerminalCommands;
        return flags;
    }
}
