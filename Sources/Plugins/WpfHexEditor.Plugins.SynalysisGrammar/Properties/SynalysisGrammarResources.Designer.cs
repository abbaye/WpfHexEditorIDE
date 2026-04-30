// ==========================================================
// Project: WpfHexEditor.Plugins.SynalysisGrammar
// File: Properties/SynalysisGrammarResources.Designer.cs
// Description: Auto-generated strongly-typed resource class for Synalysis Grammar plugin.
// Architecture: Standard ResX pattern — do not edit manually.
// ==========================================================

using System.Globalization;
using System.Resources;

namespace WpfHexEditor.Plugins.SynalysisGrammar.Properties;

internal static class SynalysisGrammarResources
{
    private static ResourceManager? _resourceManager;
    private static CultureInfo? _resourceCulture;

    internal static ResourceManager ResourceManager
    {
        get
        {
            _resourceManager ??= new ResourceManager(
                "WpfHexEditor.Plugins.SynalysisGrammar.Properties.SynalysisGrammarResources",
                typeof(SynalysisGrammarResources).Assembly);
            return _resourceManager;
        }
    }

    internal static CultureInfo? Culture
    {
        get => _resourceCulture;
        set => _resourceCulture = value;
    }

    /// <summary>Localized: "Overlay cleared"</summary>
    internal static string Synalysis_OverlayCleared
        => ResourceManager.GetString("Synalysis_OverlayCleared", _resourceCulture)!;

    /// <summary>Localized: "Open Grammar File"</summary>
    internal static string Synalysis_OpenGrammarFile
        => ResourceManager.GetString("Synalysis_OpenGrammarFile", _resourceCulture)!;
}
