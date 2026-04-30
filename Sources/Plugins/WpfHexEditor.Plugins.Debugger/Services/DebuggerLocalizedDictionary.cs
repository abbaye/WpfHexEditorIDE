///////////////////////////////////////////////////////////////
// GNU Affero General Public License v3.0  2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Project     : WpfHexEditor.Plugins.Debugger
// File        : DebuggerLocalizedDictionary.cs
///////////////////////////////////////////////////////////////

using WpfHexEditor.Core.Localization.Services;
using WpfHexEditor.Plugins.Debugger.Properties;

namespace WpfHexEditor.Plugins.Debugger.Services;

/// <summary>
/// WPF ResourceDictionary that exposes all WpfHexEditor.Plugins.Debugger localized strings
/// as dynamic resources, updated automatically on culture change.
/// Usage in XAML:
///   xmlns:debuggerSvc="clr-namespace:WpfHexEditor.Plugins.Debugger.Services"
///   &lt;debuggerSvc:DebuggerLocalizedDictionary/&gt;
/// </summary>
public sealed class DebuggerLocalizedDictionary : LocalizedResourceDictionary
{
    public DebuggerLocalizedDictionary()
    {
        RegisterResourceManager(DebuggerResources.ResourceManager);
        LoadResources();
    }
}
