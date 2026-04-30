///////////////////////////////////////////////////////////////
// GNU Affero General Public License v3.0  2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Project     : WpfHexEditor.Editor.ScriptEditor
// File        : ScriptEditorResources.Designer.cs
// Description : Strongly-typed accessor for ScriptEditor-specific UI strings.
///////////////////////////////////////////////////////////////

using System.Globalization;
using System.Resources;

namespace WpfHexEditor.Editor.ScriptEditor.Properties;

internal static class ScriptEditorResources
{
    private static ResourceManager? _resourceManager;
    private static CultureInfo? _resourceCulture;

    internal static ResourceManager ResourceManager =>
        _resourceManager ??= new ResourceManager(
            "WpfHexEditor.Editor.ScriptEditor.Properties.ScriptEditorResources",
            typeof(ScriptEditorResources).Assembly);

    internal static CultureInfo? Culture
    {
        get => _resourceCulture;
        set => _resourceCulture = value;
    }

    private static string GetString(string name) =>
        ResourceManager.GetString(name, _resourceCulture) ?? name;

    internal static string Script_EngineNotAvailable => GetString(nameof(Script_EngineNotAvailable));
    internal static string Script_StatusCancelled    => GetString(nameof(Script_StatusCancelled));
    internal static string Script_StatusError        => GetString(nameof(Script_StatusError));
    internal static string Script_StatusSaved        => GetString(nameof(Script_StatusSaved));
}
