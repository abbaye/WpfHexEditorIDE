///////////////////////////////////////////////////////////////
// GNU Affero General Public License v3.0  2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Project     : WpfHexEditor
// File        : HexEditorLocalizedDictionary.cs
// Description : Self-contained localized ResourceDictionary for WpfHexEditor.
//               Registers two ResourceManagers:
//               1. CommonResources  — shared strings (Close, OK, Undo, Copy…)
//               2. Core.Resources   — HexEditor-specific strings (Bookmarks,
//                  BytesPerLine, FillWithByte, RelativeSearch, TBL, Settings…)
//               Injected into ContextMenu.Resources so that DynamicResource
//               lookups resolve correctly inside the isolated ContextMenu popup
//               visual tree (Application.Resources lookup can fail there).
//
// Usage (HexEditor.xaml ContextMenu.Resources):
//   xmlns:hexSvc="clr-namespace:WpfHexEditor.HexEditor.Services"
//   <hexSvc:HexEditorLocalizedDictionary/>
///////////////////////////////////////////////////////////////

using WpfHexEditor.Core.Localization.Properties;
using WpfHexEditor.Core.Localization.Services;
using WpfHexEditor.Core.Properties;

namespace WpfHexEditor.HexEditor.Services;

/// <summary>
/// A <see cref="LocalizedResourceDictionary"/> that covers all string keys used
/// by the HexEditor control — common strings via CommonResources and
/// HexEditor-specific strings via WpfHexEditor.Core.Properties.Resources.
/// </summary>
public sealed class HexEditorLocalizedDictionary : LocalizedResourceDictionary
{
    /// <summary>
    /// Initialises the dictionary with both common strings and HexEditor-specific strings.
    /// </summary>
    public HexEditorLocalizedDictionary()
    {
        RegisterResourceManager(CommonResources.ResourceManager);
        RegisterResourceManager(Resources.ResourceManager);
        LoadResources();
    }
}
