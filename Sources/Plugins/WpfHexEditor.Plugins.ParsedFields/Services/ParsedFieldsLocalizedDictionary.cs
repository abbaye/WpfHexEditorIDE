///////////////////////////////////////////////////////////////
// GNU Affero General Public License v3.0  2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Project     : WpfHexEditor.Plugins.ParsedFields
// File        : ParsedFieldsLocalizedDictionary.cs
///////////////////////////////////////////////////////////////

using WpfHexEditor.Core.Localization.Services;
using WpfHexEditor.Plugins.ParsedFields.Properties;

namespace WpfHexEditor.Plugins.ParsedFields.Services;

/// <summary>
/// WPF ResourceDictionary that exposes all WpfHexEditor.Plugins.ParsedFields localized strings
/// as dynamic resources, updated automatically on culture change.
/// Usage in XAML:
///   xmlns:parsedfiSvc="clr-namespace:WpfHexEditor.Plugins.ParsedFields.Services"
///   &lt;parsedfiSvc:ParsedFieldsLocalizedDictionary/&gt;
/// </summary>
public sealed class ParsedFieldsLocalizedDictionary : LocalizedResourceDictionary
{
    public ParsedFieldsLocalizedDictionary()
    {
        RegisterResourceManager(ParsedFieldsResources.ResourceManager);
        LoadResources();
    }
}
