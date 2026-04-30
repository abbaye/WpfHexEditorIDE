///////////////////////////////////////////////////////////////
// GNU Affero General Public License v3.0  2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Project     : WpfHexEditor.Terminal
// File        : TerminalLocalizedDictionary.cs
///////////////////////////////////////////////////////////////

using WpfHexEditor.Core.Localization.Services;
using WpfHexEditor.Terminal.Properties;

namespace WpfHexEditor.Terminal.Services;

/// <summary>
/// WPF ResourceDictionary that exposes all Terminal localized strings
/// as dynamic resources, updated automatically on culture change.
/// Usage in XAML:
///   xmlns:termSvc="clr-namespace:WpfHexEditor.Terminal.Services"
///   &lt;termSvc:TerminalLocalizedDictionary/&gt;
/// </summary>
public sealed class TerminalLocalizedDictionary : LocalizedResourceDictionary
{
    public TerminalLocalizedDictionary()
    {
        RegisterResourceManager(TerminalResources.ResourceManager);
        LoadResources();
    }
}
