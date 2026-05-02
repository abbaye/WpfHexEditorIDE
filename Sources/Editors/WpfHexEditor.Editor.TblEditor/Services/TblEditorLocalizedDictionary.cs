///////////////////////////////////////////////////////////////
// GNU Affero General Public License v3.0  2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Project     : WpfHexEditor.Editor.TblEditor
// File        : TblEditorLocalizedDictionary.cs
///////////////////////////////////////////////////////////////

using WpfHexEditor.Core.Localization.Services;
using WpfHexEditor.Editor.TblEditor.Properties;

namespace WpfHexEditor.Editor.TblEditor.Services;

/// <summary>
/// WPF ResourceDictionary that exposes all WpfHexEditor.Editor.TblEditor localized strings
/// as dynamic resources, updated automatically on culture change.
/// Usage in XAML:
///   xmlns:tblSvc="clr-namespace:WpfHexEditor.Editor.TblEditor.Services;assembly=WpfHexEditor.Editor.TblEditor"
///   &lt;tblSvc:TblEditorLocalizedDictionary/&gt;
/// </summary>
public sealed class TblEditorLocalizedDictionary : LocalizedResourceDictionary
{
    public TblEditorLocalizedDictionary()
    {
        RegisterResourceManager(TblEditorResources.ResourceManager);
        LoadResources();
    }
}
