// ==========================================================
// Project: WpfHexEditor.Plugins.EntropyVisualizer
// File: Services/EntropyVisualizerLocalizedDictionary.cs
// Description: WPF ResourceDictionary exposing all localized strings as
//              DynamicResource keys, updated automatically on culture change.
// ==========================================================

using WpfHexEditor.Core.Localization.Services;
using WpfHexEditor.Plugins.EntropyVisualizer.Properties;

namespace WpfHexEditor.Plugins.EntropyVisualizer.Services;

public sealed class EntropyVisualizerLocalizedDictionary : LocalizedResourceDictionary
{
    public EntropyVisualizerLocalizedDictionary()
    {
        RegisterResourceManager(EntropyVisualizerResources.ResourceManager);
        LoadResources();
    }
}
