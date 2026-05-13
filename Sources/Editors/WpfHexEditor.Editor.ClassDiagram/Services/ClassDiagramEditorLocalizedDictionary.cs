///////////////////////////////////////////////////////////////
// GNU Affero General Public License v3.0  2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Project     : WpfHexEditor.Editor.ClassDiagram
// File        : Services/ClassDiagramEditorLocalizedDictionary.cs
///////////////////////////////////////////////////////////////

using WpfHexEditor.Core.Localization.Services;
using WpfHexEditor.Editor.ClassDiagram.Properties;

namespace WpfHexEditor.Editor.ClassDiagram.Services;

/// <summary>
/// WPF ResourceDictionary that exposes all WpfHexEditor.Editor.ClassDiagram localized strings
/// as static resources, updated automatically on culture change.
/// Usage in XAML:
///   xmlns:cdEdSvc="clr-namespace:WpfHexEditor.Editor.ClassDiagram.Services;assembly=WpfHexEditor.Editor.ClassDiagram"
///   &lt;cdEdSvc:ClassDiagramEditorLocalizedDictionary/&gt;
/// </summary>
public sealed class ClassDiagramEditorLocalizedDictionary : LocalizedResourceDictionary
{
    public ClassDiagramEditorLocalizedDictionary()
    {
        RegisterResourceManager(ClassDiagramResources.ResourceManager);
        LoadResources();
    }
}
