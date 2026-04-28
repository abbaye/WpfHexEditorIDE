///////////////////////////////////////////////////////////////
// GNU Affero General Public License v3.0  2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Project     : WpfHexEditor.Shell.Panels
// File        : ShellPanelsLocalizedDictionary.cs
// Description : Self-contained localized ResourceDictionary for Shell IDE panels.
//               Registers two ResourceManagers:
//               1. CommonResources — shared strings (Close, OK, Copy…)
//               2. ShellPanelsResources — panel-specific strings
//                  (Bookmarks, Error, Properties, SolutionExplorer,
//                   WhfmtBrowser, WhfmtCatalog, WhfmtDetail).
//               Injected into App.Resources so that DynamicResource
//               lookups resolve correctly across all panel popup trees.
//
// Usage (App.xaml Application.Resources MergedDictionaries):
//   xmlns:svc="clr-namespace:WpfHexEditor.Shell.Panels.Services;assembly=WpfHexEditor.Shell.Panels"
//   <svc:ShellPanelsLocalizedDictionary/>
///////////////////////////////////////////////////////////////

using WpfHexEditor.Core.Localization.Properties;
using WpfHexEditor.Core.Localization.Services;
using WpfHexEditor.Shell.Panels.Properties;

namespace WpfHexEditor.Shell.Panels.Services;

/// <summary>
/// A <see cref="LocalizedResourceDictionary"/> that covers all string keys used
/// by the Shell IDE panels — common strings via CommonResources and
/// panel-specific strings via ShellPanelsResources.
/// </summary>
public sealed class ShellPanelsLocalizedDictionary : LocalizedResourceDictionary
{
    /// <summary>
    /// Initialises the dictionary with common strings and Shell panel strings.
    /// </summary>
    public ShellPanelsLocalizedDictionary()
    {
        RegisterResourceManager(CommonResources.ResourceManager);
        RegisterResourceManager(ShellPanelsResources.ResourceManager);
    }
}
