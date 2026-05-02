///////////////////////////////////////////////////////////////
// GNU Affero General Public License v3.0  2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Project     : WpfHexEditor.Plugins.DiagnosticTools
// File        : DiagnosticToolsLocalizedDictionary.cs
///////////////////////////////////////////////////////////////

using WpfHexEditor.Core.Localization.Services;
using WpfHexEditor.Plugins.DiagnosticTools.Properties;

namespace WpfHexEditor.Plugins.DiagnosticTools.Services;

/// <summary>
/// WPF ResourceDictionary that exposes all WpfHexEditor.Plugins.DiagnosticTools localized strings
/// as dynamic resources, updated automatically on culture change.
/// Usage in XAML:
///   xmlns:diagnostSvc="clr-namespace:WpfHexEditor.Plugins.DiagnosticTools.Services"
///   &lt;diagnostSvc:DiagnosticToolsLocalizedDictionary/&gt;
/// </summary>
public sealed class DiagnosticToolsLocalizedDictionary : LocalizedResourceDictionary
{
    public DiagnosticToolsLocalizedDictionary()
    {
        RegisterResourceManager(DiagnosticToolsResources.ResourceManager);
        LoadResources();
    }
}
