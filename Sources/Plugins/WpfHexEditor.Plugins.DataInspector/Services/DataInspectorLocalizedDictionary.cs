///////////////////////////////////////////////////////////////
// GNU Affero General Public License v3.0  2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Project     : WpfHexEditor.Plugins.DataInspector
// File        : DataInspectorLocalizedDictionary.cs
///////////////////////////////////////////////////////////////

using WpfHexEditor.Core.Localization.Services;
using WpfHexEditor.Plugins.DataInspector.Properties;

namespace WpfHexEditor.Plugins.DataInspector.Services;

/// <summary>
/// WPF ResourceDictionary that exposes all WpfHexEditor.Plugins.DataInspector localized strings
/// as dynamic resources, updated automatically on culture change.
/// Usage in XAML:
///   xmlns:datainspSvc="clr-namespace:WpfHexEditor.Plugins.DataInspector.Services"
///   &lt;datainspSvc:DataInspectorLocalizedDictionary/&gt;
/// </summary>
public sealed class DataInspectorLocalizedDictionary : LocalizedResourceDictionary
{
    public DataInspectorLocalizedDictionary()
    {
        RegisterResourceManager(DataInspectorResources.ResourceManager);
        LoadResources();
    }
}
