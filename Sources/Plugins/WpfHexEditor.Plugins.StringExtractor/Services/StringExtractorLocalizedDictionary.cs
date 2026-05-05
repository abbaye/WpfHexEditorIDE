// ==========================================================
// Project: WpfHexEditor.Plugins.StringExtractor
// File: Services/StringExtractorLocalizedDictionary.cs
// Description: WPF ResourceDictionary that exposes all String Extractor
//              localized strings as dynamic resources, updated on culture change.
// ==========================================================

using WpfHexEditor.Core.Localization.Services;
using WpfHexEditor.Plugins.StringExtractor.Properties;

namespace WpfHexEditor.Plugins.StringExtractor.Services;

/// <summary>
/// WPF ResourceDictionary that exposes all WpfHexEditor.Plugins.StringExtractor localized strings
/// as dynamic resources, updated automatically on culture change.
/// Usage in XAML:
///   xmlns:strSvc="clr-namespace:WpfHexEditor.Plugins.StringExtractor.Services"
///   &lt;strSvc:StringExtractorLocalizedDictionary/&gt;
/// </summary>
public sealed class StringExtractorLocalizedDictionary : LocalizedResourceDictionary
{
    public StringExtractorLocalizedDictionary()
    {
        RegisterResourceManager(StringExtractorResources.ResourceManager);
        LoadResources();
    }
}
