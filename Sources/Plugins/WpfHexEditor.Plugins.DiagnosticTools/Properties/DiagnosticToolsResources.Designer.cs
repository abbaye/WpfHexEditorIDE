// ==========================================================
// Project: WpfHexEditor.Plugins.DiagnosticTools
// File: Properties/DiagnosticToolsResources.Designer.cs
// Description: Auto-generated strongly-typed resource class for Diagnostic Tools plugin.
// Architecture: Standard ResX pattern — do not edit manually.
// ==========================================================

using System.Globalization;
using System.Resources;

namespace WpfHexEditor.Plugins.DiagnosticTools.Properties;

internal static class DiagnosticToolsResources
{
    private static ResourceManager? _resourceManager;
    private static CultureInfo? _resourceCulture;

    internal static ResourceManager ResourceManager
    {
        get
        {
            _resourceManager ??= new ResourceManager(
                "WpfHexEditor.Plugins.DiagnosticTools.Properties.DiagnosticToolsResources",
                typeof(DiagnosticToolsResources).Assembly);
            return _resourceManager;
        }
    }

    internal static CultureInfo? Culture
    {
        get => _resourceCulture;
        set => _resourceCulture = value;
    }

    /// <summary>Localized: "Diagnostic Tools"</summary>
    internal static string DiagTools_PluginName
        => ResourceManager.GetString("DiagTools_PluginName", _resourceCulture)!;

    /// <summary>Localized: "Export Diagnostic Metrics"</summary>
    internal static string DiagTools_ExportMetrics
        => ResourceManager.GetString("DiagTools_ExportMetrics", _resourceCulture)!;
}
