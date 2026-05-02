///////////////////////////////////////////////////////////////
// GNU Affero General Public License v3.0  2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Project     : WpfHexEditor.Plugins.UnitTesting
// File        : UnitTestingLocalizedDictionary.cs
///////////////////////////////////////////////////////////////

using WpfHexEditor.Core.Localization.Services;
using WpfHexEditor.Plugins.UnitTesting.Properties;

namespace WpfHexEditor.Plugins.UnitTesting.Services;

/// <summary>
/// WPF ResourceDictionary that exposes all WpfHexEditor.Plugins.UnitTesting localized strings
/// as dynamic resources, updated automatically on culture change.
/// Usage in XAML:
///   xmlns:unittestSvc="clr-namespace:WpfHexEditor.Plugins.UnitTesting.Services"
///   &lt;unittestSvc:UnitTestingLocalizedDictionary/&gt;
/// </summary>
public sealed class UnitTestingLocalizedDictionary : LocalizedResourceDictionary
{
    public UnitTestingLocalizedDictionary()
    {
        RegisterResourceManager(UnitTestingResources.ResourceManager);
        LoadResources();
    }
}
