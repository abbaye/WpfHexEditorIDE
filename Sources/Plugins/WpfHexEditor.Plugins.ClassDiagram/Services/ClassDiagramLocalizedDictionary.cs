///////////////////////////////////////////////////////////////
// GNU Affero General Public License v3.0  2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Project     : WpfHexEditor.Plugins.ClassDiagram
// File        : ClassDiagramLocalizedDictionary.cs
///////////////////////////////////////////////////////////////

using WpfHexEditor.Core.Localization.Services;
using WpfHexEditor.Plugins.ClassDiagram.Properties;

namespace WpfHexEditor.Plugins.ClassDiagram.Services;

/// <summary>
/// WPF ResourceDictionary that exposes all WpfHexEditor.Plugins.ClassDiagram localized strings
/// as dynamic resources, updated automatically on culture change.
/// Usage in XAML:
///   xmlns:classdiaSvc="clr-namespace:WpfHexEditor.Plugins.ClassDiagram.Services"
///   &lt;classdiaSvc:ClassDiagramLocalizedDictionary/&gt;
/// </summary>
public sealed class ClassDiagramLocalizedDictionary : LocalizedResourceDictionary
{
    public ClassDiagramLocalizedDictionary()
    {
        RegisterResourceManager(ClassDiagramResources.ResourceManager);
        LoadResources();
    }
}
