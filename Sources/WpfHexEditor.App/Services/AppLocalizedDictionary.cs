///////////////////////////////////////////////////////////////
// GNU Affero General Public License v3.0  2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Project     : WpfHexEditor.App
// File        : Services/AppLocalizedDictionary.cs
// Description : Injects AppResources (main-window menu headers, toolbar
//               tooltips, and shell-level strings) into the WPF dynamic
//               resource tree so every DynamicResource binding in
//               MainWindow.xaml resolves at runtime.
///////////////////////////////////////////////////////////////

using WpfHexEditor.App.Properties;
using WpfHexEditor.Core.Localization.Services;

namespace WpfHexEditor.App.Services;

public sealed class AppLocalizedDictionary : LocalizedResourceDictionary
{
    public AppLocalizedDictionary()
    {
        RegisterResourceManager(AppResources.ResourceManager);
        LoadResources();
    }
}
