///////////////////////////////////////////////////////////////
// GNU Affero General Public License v3.0  2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Project     : WpfHexEditor.Plugins.PatternAnalysis
// File        : PatternAnalysisLocalizedDictionary.cs
///////////////////////////////////////////////////////////////

using WpfHexEditor.Core.Localization.Services;
using WpfHexEditor.Plugins.PatternAnalysis.Properties;

namespace WpfHexEditor.Plugins.PatternAnalysis.Services;

/// <summary>
/// WPF ResourceDictionary that exposes all WpfHexEditor.Plugins.PatternAnalysis localized strings
/// as dynamic resources, updated automatically on culture change.
/// Usage in XAML:
///   xmlns:patternaSvc="clr-namespace:WpfHexEditor.Plugins.PatternAnalysis.Services"
///   &lt;patternaSvc:PatternAnalysisLocalizedDictionary/&gt;
/// </summary>
public sealed class PatternAnalysisLocalizedDictionary : LocalizedResourceDictionary
{
    public PatternAnalysisLocalizedDictionary()
    {
        RegisterResourceManager(PatternAnalysisResources.ResourceManager);
        LoadResources();
    }
}
