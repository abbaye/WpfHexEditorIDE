// ==========================================================
// Project: WpfHexEditor.Plugins.EntropyVisualizer
// File: Properties/EntropyVisualizerResources.Designer.cs
// Description: Strongly-typed accessor for EntropyVisualizerResources.resx.
//              Auto-generated pattern — do not hand-edit.
// ==========================================================

using System.Resources;

namespace WpfHexEditor.Plugins.EntropyVisualizer.Properties;

internal static class EntropyVisualizerResources
{
    private static ResourceManager? _resourceManager;
    private static System.Globalization.CultureInfo? _resourceCulture;

    internal static ResourceManager ResourceManager =>
        _resourceManager ??= new ResourceManager(
            "WpfHexEditor.Plugins.EntropyVisualizer.Properties.EntropyVisualizerResources",
            typeof(EntropyVisualizerResources).Assembly);

    internal static System.Globalization.CultureInfo? Culture
    {
        get => _resourceCulture;
        set => _resourceCulture = value;
    }

    private static string Get(string key) =>
        ResourceManager.GetString(key, _resourceCulture)!;

    internal static string EntropyVisualizer_PluginName    => Get(nameof(EntropyVisualizer_PluginName));
    internal static string EntropyVisualizer_PanelTitle    => Get(nameof(EntropyVisualizer_PanelTitle));
    internal static string EntropyVisualizer_MenuItem      => Get(nameof(EntropyVisualizer_MenuItem));

    internal static string EntropyVisualizer_AnalyzeButton => Get(nameof(EntropyVisualizer_AnalyzeButton));
    internal static string EntropyVisualizer_CancelButton  => Get(nameof(EntropyVisualizer_CancelButton));

    internal static string EntropyVisualizer_Tooltip_Analyze   => Get(nameof(EntropyVisualizer_Tooltip_Analyze));
    internal static string EntropyVisualizer_Tooltip_ChunkSize => Get(nameof(EntropyVisualizer_Tooltip_ChunkSize));
    internal static string EntropyVisualizer_Tooltip_Navigate  => Get(nameof(EntropyVisualizer_Tooltip_Navigate));

    internal static string EntropyVisualizer_ChunkSizeLabel    => Get(nameof(EntropyVisualizer_ChunkSizeLabel));

    internal static string EntropyVisualizer_Analyzing     => Get(nameof(EntropyVisualizer_Analyzing));
    internal static string EntropyVisualizer_NoFile        => Get(nameof(EntropyVisualizer_NoFile));
    internal static string EntropyVisualizer_NoResults     => Get(nameof(EntropyVisualizer_NoResults));

    internal static string EntropyVisualizer_StatusReady   => Get(nameof(EntropyVisualizer_StatusReady));
    internal static string EntropyVisualizer_StatusChunks  => Get(nameof(EntropyVisualizer_StatusChunks));
    internal static string EntropyVisualizer_StatusOffset  => Get(nameof(EntropyVisualizer_StatusOffset));

    internal static string EntropyVisualizer_LegendLow     => Get(nameof(EntropyVisualizer_LegendLow));
    internal static string EntropyVisualizer_LegendHigh    => Get(nameof(EntropyVisualizer_LegendHigh));

    internal static string EntropyVisualizer_AnnotationHigh       => Get(nameof(EntropyVisualizer_AnnotationHigh));
    internal static string EntropyVisualizer_AnnotationCompressed => Get(nameof(EntropyVisualizer_AnnotationCompressed));

    internal static string EntropyVisualizer_CtxNavigateTo  => Get(nameof(EntropyVisualizer_CtxNavigateTo));
    internal static string EntropyVisualizer_CtxCopyOffset  => Get(nameof(EntropyVisualizer_CtxCopyOffset));
    internal static string EntropyVisualizer_CtxCopyEntropy => Get(nameof(EntropyVisualizer_CtxCopyEntropy));
}
