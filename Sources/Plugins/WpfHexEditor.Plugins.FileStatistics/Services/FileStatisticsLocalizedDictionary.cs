///////////////////////////////////////////////////////////////
// GNU Affero General Public License v3.0  2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Project     : WpfHexEditor.Plugins.FileStatistics
// File        : FileStatisticsLocalizedDictionary.cs
///////////////////////////////////////////////////////////////

using WpfHexEditor.Core.Localization.Services;
using WpfHexEditor.Plugins.FileStatistics.Properties;

namespace WpfHexEditor.Plugins.FileStatistics.Services;

/// <summary>
/// WPF ResourceDictionary that exposes all WpfHexEditor.Plugins.FileStatistics localized strings
/// as dynamic resources, updated automatically on culture change.
/// Usage in XAML:
///   xmlns:filestatSvc="clr-namespace:WpfHexEditor.Plugins.FileStatistics.Services"
///   &lt;filestatSvc:FileStatisticsLocalizedDictionary/&gt;
/// </summary>
public sealed class FileStatisticsLocalizedDictionary : LocalizedResourceDictionary
{
    public FileStatisticsLocalizedDictionary()
    {
        RegisterResourceManager(FileStatisticsResources.ResourceManager);
        LoadResources();
    }
}
