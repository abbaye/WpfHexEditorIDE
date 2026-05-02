///////////////////////////////////////////////////////////////
// GNU Affero General Public License v3.0  2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Project     : WpfHexEditor.Core.Options
// File        : OptionsLocalizedDictionary.cs
// Description : Self-contained localized ResourceDictionary for the Options editor.
//               Registers two ResourceManagers:
//               1. CommonResources — shared strings
//               2. OptionsResources — options-page strings
//                  (EnvironmentGeneralPage appearance + language sections).
//
// Architecture Notes:
//               Used by the host app (App.xaml) to enable DynamicResource
//               lookups in the options editor pages. Not required if the host
//               app already merges a broader LocalizedResourceDictionary that
//               covers these keys.
///////////////////////////////////////////////////////////////

using WpfHexEditor.Core.Localization.Properties;
using WpfHexEditor.Core.Localization.Services;
using WpfHexEditor.Core.Options.Properties;

namespace WpfHexEditor.Core.Options.Services;

/// <summary>
/// A <see cref="LocalizedResourceDictionary"/> that covers all string keys used
/// by the Options editor pages.
/// </summary>
public sealed class OptionsLocalizedDictionary : LocalizedResourceDictionary
{
    /// <summary>
    /// Initialises the dictionary with common strings and options-page strings.
    /// </summary>
    public OptionsLocalizedDictionary()
    {
        RegisterResourceManager(CommonResources.ResourceManager);
        RegisterResourceManager(OptionsResources.ResourceManager);
        LoadResources();
    }
}
