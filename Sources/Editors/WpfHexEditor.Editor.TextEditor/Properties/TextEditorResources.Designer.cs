///////////////////////////////////////////////////////////////
// GNU Affero General Public License v3.0  2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Project     : WpfHexEditor.Editor.TextEditor
// File        : TextEditorResources.Designer.cs
// Description : Strongly-typed accessor for TextEditorResources.resx.
//               Provides compile-time safe access to all localized strings
//               used by the TextEditor editor module.
///////////////////////////////////////////////////////////////

namespace WpfHexEditor.Editor.TextEditor.Properties;

using System.Globalization;
using System.Resources;

/// <summary>
/// Strongly-typed resource accessor for <c>TextEditorResources.resx</c>.
/// </summary>
internal static class TextEditorResources
{
    private static ResourceManager? _resourceManager;
    private static CultureInfo? _resourceCulture;

    /// <summary>
    /// Returns the cached <see cref="ResourceManager"/> instance used by this class.
    /// </summary>
    [global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Advanced)]
    internal static ResourceManager ResourceManager
    {
        get
        {
            if (_resourceManager is null)
            {
                _resourceManager = new ResourceManager(
                    "WpfHexEditor.Editor.TextEditor.Properties.TextEditorResources",
                    typeof(TextEditorResources).Assembly);
            }
            return _resourceManager;
        }
    }

    /// <summary>
    /// Overrides the current thread's <see cref="CultureInfo"/> for all resource lookups.
    /// </summary>
    [global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Advanced)]
    internal static CultureInfo? Culture
    {
        get => _resourceCulture;
        set => _resourceCulture = value;
    }

    private static string GetString(string name) =>
        ResourceManager.GetString(name, _resourceCulture) ?? name;

    /// <summary>Gets "Plain Text"</summary>
    internal static string TextEditor_LanguageLabelPlainText => GetString(nameof(TextEditor_LanguageLabelPlainText));

    /// <summary>Gets "Ln 1, Col 1"</summary>
    internal static string TextEditor_StatusBarCaretPosition => GetString(nameof(TextEditor_StatusBarCaretPosition));

    /// <summary>Gets "UTF-8"</summary>
    internal static string TextEditor_StatusBarEncoding => GetString(nameof(TextEditor_StatusBarEncoding));
}
