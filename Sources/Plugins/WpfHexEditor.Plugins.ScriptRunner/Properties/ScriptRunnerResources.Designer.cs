// ==========================================================
// Project: WpfHexEditor.Plugins.ScriptRunner
// File: Properties/ScriptRunnerResources.Designer.cs
// Description: Auto-generated strongly-typed resource class for ScriptRunner plugin.
// Architecture: Standard ResX pattern — do not edit manually.
// ==========================================================

using System.Globalization;
using System.Resources;

namespace WpfHexEditor.Plugins.ScriptRunner.Properties;

internal static class ScriptRunnerResources
{
    private static ResourceManager? _resourceManager;
    private static CultureInfo? _resourceCulture;

    internal static ResourceManager ResourceManager
    {
        get
        {
            _resourceManager ??= new ResourceManager(
                "WpfHexEditor.Plugins.ScriptRunner.Properties.ScriptRunnerResources",
                typeof(ScriptRunnerResources).Assembly);
            return _resourceManager;
        }
    }

    internal static CultureInfo? Culture
    {
        get => _resourceCulture;
        set => _resourceCulture = value;
    }

    /// <summary>Localized: "Scripting engine not available"</summary>
    internal static string ScriptRunner_EngineNotAvailable
        => ResourceManager.GetString("ScriptRunner_EngineNotAvailable", _resourceCulture)!;

    /// <summary>Localized: "Running…"</summary>
    internal static string ScriptRunner_Running
        => ResourceManager.GetString("ScriptRunner_Running", _resourceCulture)!;

    /// <summary>Localized: "Cancelled"</summary>
    internal static string ScriptRunner_Cancelled
        => ResourceManager.GetString("ScriptRunner_Cancelled", _resourceCulture)!;

    /// <summary>Localized: "Error — see output"</summary>
    internal static string ScriptRunner_Error
        => ResourceManager.GetString("ScriptRunner_Error", _resourceCulture)!;

    /// <summary>Localized: "Cancelling…"</summary>
    internal static string ScriptRunner_Cancelling
        => ResourceManager.GetString("ScriptRunner_Cancelling", _resourceCulture)!;

    /// <summary>Localized: "Ready"</summary>
    internal static string ScriptRunner_Ready
        => ResourceManager.GetString("ScriptRunner_Ready", _resourceCulture)!;

    /// <summary>Localized: "CSharp"</summary>
    internal static string ScriptRunner_LangCSharp
        => ResourceManager.GetString("ScriptRunner_LangCSharp", _resourceCulture)!;

    /// <summary>Localized: "FSharp"</summary>
    internal static string ScriptRunner_LangFSharp
        => ResourceManager.GetString("ScriptRunner_LangFSharp", _resourceCulture)!;

    /// <summary>Localized: "VBNet"</summary>
    internal static string ScriptRunner_LangVBNet
        => ResourceManager.GetString("ScriptRunner_LangVBNet", _resourceCulture)!;
}
