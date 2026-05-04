// ==========================================================
// Project: WpfHexEditor.Plugins.Debugger
// File: Properties/DebuggerResources.Designer.cs
// Description: Strongly-typed resource class for Debugger plugin.
// Architecture: Standard ResourceManager pattern; satellite assemblies
//               provide fr-CA translations at runtime.
// ==========================================================

using System.Globalization;
using System.Resources;

namespace WpfHexEditor.Plugins.Debugger.Properties;

internal static class DebuggerResources
{
    private static ResourceManager? _resourceManager;
    private static CultureInfo?     _resourceCulture;

    internal static ResourceManager ResourceManager
    {
        get
        {
            _resourceManager ??= new ResourceManager(
                "WpfHexEditor.Plugins.Debugger.Properties.DebuggerResources",
                typeof(DebuggerResources).Assembly);
            return _resourceManager;
        }
    }

    internal static CultureInfo? Culture
    {
        get => _resourceCulture;
        set => _resourceCulture = value;
    }

    /// <summary>Gets the localized panel title for the Breakpoints panel.</summary>
    internal static string Debugger_BreakpointsPanelTitle
        => ResourceManager.GetString("Debugger_BreakpointsPanelTitle", _resourceCulture)!;

    /// <summary>Gets the localized panel title for the Call Stack panel.</summary>
    internal static string Debugger_CallStackPanelTitle
        => ResourceManager.GetString("Debugger_CallStackPanelTitle", _resourceCulture)!;

    /// <summary>Gets the localized panel title for the Locals panel.</summary>
    internal static string Debugger_LocalsPanelTitle
        => ResourceManager.GetString("Debugger_LocalsPanelTitle", _resourceCulture)!;

    /// <summary>Gets the localized panel title for the Autos panel.</summary>
    internal static string Debugger_AutosPanelTitle
        => ResourceManager.GetString("Debugger_AutosPanelTitle", _resourceCulture)!;

    /// <summary>Gets the localized panel title for the Exception Settings panel.</summary>
    internal static string Debugger_ExceptionsPanelTitle
        => ResourceManager.GetString("Debugger_ExceptionsPanelTitle", _resourceCulture)!;

    /// <summary>Gets the localized panel title for the Immediate window panel.</summary>
    internal static string Debugger_ImmediatePanelTitle
        => ResourceManager.GetString("Debugger_ImmediatePanelTitle", _resourceCulture)!;

    /// <summary>Gets the localized panel title for the Modules panel.</summary>
    internal static string Debugger_ModulesPanelTitle
        => ResourceManager.GetString("Debugger_ModulesPanelTitle", _resourceCulture)!;

    /// <summary>Gets the localized panel title for the Tasks panel.</summary>
    internal static string Debugger_TasksPanelTitle
        => ResourceManager.GetString("Debugger_TasksPanelTitle", _resourceCulture)!;

    /// <summary>Gets the localized panel title for the Disassembly panel.</summary>
    internal static string Debugger_DisassemblyPanelTitle
        => ResourceManager.GetString("Debugger_DisassemblyPanelTitle", _resourceCulture)!;

    /// <summary>Gets the localized panel title for the Memory window panel.</summary>
    internal static string Debugger_MemoryPanelTitle
        => ResourceManager.GetString("Debugger_MemoryPanelTitle", _resourceCulture)!;

    /// <summary>Gets the localized panel title for the Registers panel.</summary>
    internal static string Debugger_RegistersPanelTitle
        => ResourceManager.GetString("Debugger_RegistersPanelTitle", _resourceCulture)!;

    /// <summary>Gets the localized panel title for the Parallel Watch panel.</summary>
    internal static string Debugger_ParallelWatchPanelTitle
        => ResourceManager.GetString("Debugger_ParallelWatchPanelTitle", _resourceCulture)!;

    /// <summary>Gets the localized panel title for the Watch panel.</summary>
    internal static string Debugger_WatchPanelTitle
        => ResourceManager.GetString("Debugger_WatchPanelTitle", _resourceCulture)!;

    /// <summary>Gets the localized panel title for the Debug Console panel.</summary>
    internal static string Debugger_ConsolePanelTitle
        => ResourceManager.GetString("Debugger_ConsolePanelTitle", _resourceCulture)!;

    /// <summary>Gets the localized panel title for the Threads panel.</summary>
    internal static string Debugger_ThreadsPanelTitle
        => ResourceManager.GetString("Debugger_ThreadsPanelTitle", _resourceCulture)!;

    /// <summary>Gets the localized panel title for the Parallel Stacks panel.</summary>
    internal static string Debugger_ParallelStacksPanelTitle
        => ResourceManager.GetString("Debugger_ParallelStacksPanelTitle", _resourceCulture)!;

    /// <summary>Gets the localized message shown when no valid breakpoints are found during import.</summary>
    internal static string Debugger_NoValidBreakpoints
        => ResourceManager.GetString("Debugger_NoValidBreakpoints", _resourceCulture)!;

    internal static string Debugger_LaunchConfigTitle
        => ResourceManager.GetString("Debugger_LaunchConfigTitle", _resourceCulture)!;

    internal static string Debugger_NoFileLoaded
        => ResourceManager.GetString("Debugger_NoFileLoaded", _resourceCulture)!;

    internal static string Debugger_ServiceNotAvailable
        => ResourceManager.GetString("Debugger_ServiceNotAvailable", _resourceCulture)!;

    internal static string Debugger_NoConfigLoaded
        => ResourceManager.GetString("Debugger_NoConfigLoaded", _resourceCulture)!;

    internal static string Debugger_NoConfigsArray
        => ResourceManager.GetString("Debugger_NoConfigsArray", _resourceCulture)!;

    internal static string Debugger_NoMatchingConfig
        => ResourceManager.GetString("Debugger_NoMatchingConfig", _resourceCulture)!;

    internal static string Debugger_ImportBreakpoints
        => ResourceManager.GetString("Debugger_ImportBreakpoints", _resourceCulture)!;

    internal static string Debugger_ExportBreakpoints
        => ResourceManager.GetString("Debugger_ExportBreakpoints", _resourceCulture)!;

    /// <summary>Gets the localized header for the Debug &gt; Continue menu item.</summary>
    internal static string Debugger_Menu_Continue
        => ResourceManager.GetString("Debugger_Menu_Continue", _resourceCulture)!;

    /// <summary>Gets the localized header for the Debug &gt; Step Over menu item.</summary>
    internal static string Debugger_Menu_StepOver
        => ResourceManager.GetString("Debugger_Menu_StepOver", _resourceCulture)!;

    /// <summary>Gets the localized header for the Debug &gt; Step Into menu item.</summary>
    internal static string Debugger_Menu_StepInto
        => ResourceManager.GetString("Debugger_Menu_StepInto", _resourceCulture)!;
}
