///////////////////////////////////////////////////////////////
// GNU Affero General Public License v3.0  2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Project     : WpfColorPicker
// File        : ColorPickerLocalizedDictionary.cs
// Description : Self-contained localized ResourceDictionary for WpfColorPicker.
//               Extends LocalizedResourceDictionary with ColorPicker-specific
//               strings (ColorPicker_Tab_*, ColorPicker_Section_*, etc.).
//               Common strings (OK, Cancel…) are pre-loaded by the base class.
//
// Usage (App.xaml or Window.Resources):
//   xmlns:cp="clr-namespace:WpfHexEditor.ColorPicker.Services;assembly=WpfHexEditor.ColorPicker"
//   <cp:ColorPickerLocalizedDictionary/>
///////////////////////////////////////////////////////////////

using WpfHexEditor.ColorPicker.Properties;
using WpfHexEditor.Core.Localization.Services;

namespace WpfHexEditor.ColorPicker.Services;

/// <summary>
/// A <see cref="LocalizedResourceDictionary"/> that includes ColorPicker-specific
/// strings in addition to the common strings from WpfHexEditor.Core.Localization.
/// Self-contained: NuGet consumers need no other localization dependency.
/// </summary>
public sealed class ColorPickerLocalizedDictionary : LocalizedResourceDictionary
{
    /// <summary>
    /// Initialises the dictionary with both common strings and
    /// ColorPicker-specific strings (ColorPicker_Tab_*, ColorPicker_Section_*, etc.).
    /// </summary>
    public ColorPickerLocalizedDictionary()
    {
        RegisterResourceManager(ColorPickerResources.ResourceManager);
    }
}
