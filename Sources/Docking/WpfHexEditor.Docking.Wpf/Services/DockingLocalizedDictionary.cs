///////////////////////////////////////////////////////////////
// GNU Affero General Public License v3.0  2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Project     : WpfDocking
// File        : DockingLocalizedDictionary.cs
// Description : Self-contained localized ResourceDictionary for WpfDocking.
//               Extends LocalizedResourceDictionary with Docking-specific
//               strings (Docking_TabSettings_*).
//               Common strings (Close, etc.) are pre-loaded by the base class.
//
// Usage (App.xaml or Window.Resources):
//   xmlns:dockSvc="clr-namespace:WpfHexEditor.Shell.Services;assembly=WpfHexEditor.Docking.Wpf"
//   <dockSvc:DockingLocalizedDictionary/>
///////////////////////////////////////////////////////////////

using WpfHexEditor.Core.Localization.Services;
using WpfHexEditor.Shell.Properties;

namespace WpfHexEditor.Shell.Services;

/// <summary>
/// A <see cref="LocalizedResourceDictionary"/> that includes Docking-specific
/// strings in addition to the common strings from WpfHexEditor.Core.Localization.
/// Self-contained: NuGet consumers need no other localization dependency.
/// </summary>
public sealed class DockingLocalizedDictionary : LocalizedResourceDictionary
{
    /// <summary>
    /// Initialises the dictionary with both common strings and
    /// Docking-specific strings (Docking_TabSettings_*).
    /// </summary>
    public DockingLocalizedDictionary()
    {
        RegisterResourceManager(DockingResources.ResourceManager);
    }
}
