///////////////////////////////////////////////////////////////
// GNU Affero General Public License v3.0  2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Project     : WpfHexEditor.Editor.XamlDesigner
// File        : XamlDesignerEditorLocalizedDictionary.cs
///////////////////////////////////////////////////////////////

using WpfHexEditor.Core.Localization.Services;
using WpfHexEditor.Editor.XamlDesigner.Properties;

namespace WpfHexEditor.Editor.XamlDesigner.Services;

/// <summary>
/// WPF ResourceDictionary that exposes all WpfHexEditor.Editor.XamlDesigner localized strings
/// as dynamic resources, updated automatically on culture change.
/// Usage in XAML:
///   xmlns:xdeSvc="clr-namespace:WpfHexEditor.Editor.XamlDesigner.Services;assembly=WpfHexEditor.Editor.XamlDesigner"
///   &lt;xdeSvc:XamlDesignerEditorLocalizedDictionary/&gt;
/// </summary>
public sealed class XamlDesignerEditorLocalizedDictionary : LocalizedResourceDictionary
{
    public XamlDesignerEditorLocalizedDictionary()
    {
        RegisterResourceManager(XamlDesignerResources.ResourceManager);
        LoadResources();
    }
}
