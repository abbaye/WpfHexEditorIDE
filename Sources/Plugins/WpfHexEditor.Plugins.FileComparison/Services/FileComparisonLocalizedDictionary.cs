///////////////////////////////////////////////////////////////
// GNU Affero General Public License v3.0  2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Project     : WpfHexEditor.Plugins.FileComparison
// File        : FileComparisonLocalizedDictionary.cs
///////////////////////////////////////////////////////////////

using WpfHexEditor.Core.Localization.Services;
using WpfHexEditor.Plugins.FileComparison.Properties;

namespace WpfHexEditor.Plugins.FileComparison.Services;

/// <summary>
/// WPF ResourceDictionary that exposes all WpfHexEditor.Plugins.FileComparison localized strings
/// as dynamic resources, updated automatically on culture change.
/// Usage in XAML:
///   xmlns:filecompSvc="clr-namespace:WpfHexEditor.Plugins.FileComparison.Services"
///   &lt;filecompSvc:FileComparisonLocalizedDictionary/&gt;
/// </summary>
public sealed class FileComparisonLocalizedDictionary : LocalizedResourceDictionary
{
    public FileComparisonLocalizedDictionary()
    {
        RegisterResourceManager(FileComparisonResources.ResourceManager);
        LoadResources();
    }
}
