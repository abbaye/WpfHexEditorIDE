///////////////////////////////////////////////////////////////
// GNU Affero General Public License v3.0  2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Project     : WpfHexEditor.Plugins.SynalysisGrammar
// File        : SynalysisGrammarLocalizedDictionary.cs
///////////////////////////////////////////////////////////////

using WpfHexEditor.Core.Localization.Services;
using WpfHexEditor.Plugins.SynalysisGrammar.Properties;

namespace WpfHexEditor.Plugins.SynalysisGrammar.Services;

/// <summary>
/// WPF ResourceDictionary that exposes all WpfHexEditor.Plugins.SynalysisGrammar localized strings
/// as dynamic resources, updated automatically on culture change.
/// Usage in XAML:
///   xmlns:synalysiSvc="clr-namespace:WpfHexEditor.Plugins.SynalysisGrammar.Services"
///   &lt;synalysiSvc:SynalysisGrammarLocalizedDictionary/&gt;
/// </summary>
public sealed class SynalysisGrammarLocalizedDictionary : LocalizedResourceDictionary
{
    public SynalysisGrammarLocalizedDictionary()
    {
        RegisterResourceManager(SynalysisGrammarResources.ResourceManager);
        LoadResources();
    }
}
