//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.Windows;

namespace WpfHexEditor.SDK.Contracts;

/// <summary>
/// Optional interface for plugins that expose a settings page in the IDE Options panel.
/// Plugins implementing this interface will have their options page automatically
/// registered under the "Plugins" category when the plugin is loaded.
/// </summary>
public interface IPluginWithOptions
{
    /// <summary>
    /// Creates a new instance of the plugin's options UI control.
    /// Called once per Options panel session; must not return null.
    /// </summary>
    FrameworkElement CreateOptionsPage();

    /// <summary>
    /// Persists current option values. Called when the user clicks Apply or OK.
    /// </summary>
    void SaveOptions();

    /// <summary>
    /// Loads saved option values into the options page UI.
    /// </summary>
    void LoadOptions();

    /// <summary>
    /// Optional: Returns the category name for this plugin's options page.
    /// Default: "Plugins"
    /// Example: "Data Analysis", "Editors", "Debugging Tools"
    /// </summary>
    string GetOptionsCategory() => "Extensions";

    /// <summary>
    /// Optional: Returns an icon prefix for the options category header.
    /// Use BMP Unicode symbols (≤ U+FFFF) to avoid 4-byte emoji encoding issues.
    /// Default: "⚙" (U+2699 GEAR — safe, renders in all WPF themes).
    /// </summary>
    string GetOptionsCategoryIcon() => "⚙";
}
