//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.Windows.Controls;
using WpfHexEditor.SDK.Contracts;

namespace WpfHexEditor.PluginHost.Services;

/// <summary>
/// Represents a registered plugin options entry.
/// </summary>
public sealed record PluginOptionsEntry(string PluginId, string PluginName, IPluginWithOptions Plugin);

/// <summary>
/// Runtime registry of plugin options pages.
/// PluginHost auto-registers entries for every loaded plugin that implements IPluginWithOptions.
/// </summary>
public interface IPluginOptionsRegistry
{
    void RegisterPluginPage(string pluginId, string pluginName, IPluginWithOptions plugin);
    void UnregisterPluginPage(string pluginId);
    IReadOnlyList<PluginOptionsEntry> GetAll();
}

/// <summary>
/// Thread-safe implementation of IPluginOptionsRegistry.
/// Automatically registers plugin options pages with the IDE OptionsPageRegistry when plugins are loaded.
/// </summary>
public sealed class PluginOptionsRegistry : IPluginOptionsRegistry
{
    private readonly object _lock = new();
    private readonly Dictionary<string, PluginOptionsEntry> _entries =
        new(StringComparer.OrdinalIgnoreCase);

    public void RegisterPluginPage(string pluginId, string pluginName, IPluginWithOptions plugin)
    {
        ArgumentNullException.ThrowIfNull(plugin);

        lock (_lock)
        {
            _entries[pluginId] = new PluginOptionsEntry(pluginId, pluginName, plugin);
        }

        // Get category and icon from plugin (with defaults)
        var category = plugin.GetOptionsCategory();
        var icon = plugin.GetOptionsCategoryIcon();

        // Register with IDE Options system for automatic UI integration
        // This triggers OptionsEditorControl to refresh its TreeView automatically
        try
        {
            WpfHexEditor.Options.OptionsPageRegistry.RegisterDynamic(
                category: category,
                pageName: pluginName,
                factory: () =>
                {
                    // Wrap the plugin's options page in a UserControl container
                    var optionsPage = plugin.CreateOptionsPage();

                    // If it's already a UserControl, return it directly
                    if (optionsPage is UserControl uc)
                        return uc;

                    // Otherwise, wrap it in a simple container
                    return new UserControl { Content = optionsPage };
                },
                categoryIcon: icon);
        }
        catch (Exception ex)
        {
            // Log error but don't fail plugin load
            System.Diagnostics.Debug.WriteLine($"[PluginOptionsRegistry] Failed to register options page for '{pluginName}': {ex.Message}");
        }
    }

    public void UnregisterPluginPage(string pluginId)
    {
        string? pluginName = null;
        string? category = null;

        lock (_lock)
        {
            if (_entries.TryGetValue(pluginId, out var entry))
            {
                pluginName = entry.PluginName;
                category = entry.Plugin.GetOptionsCategory();
                _entries.Remove(pluginId);
            }
        }

        // Unregister from IDE Options system
        if (pluginName != null && category != null)
        {
            try
            {
                WpfHexEditor.Options.OptionsPageRegistry.UnregisterDynamic(category, pluginName);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PluginOptionsRegistry] Failed to unregister options page for '{pluginName}': {ex.Message}");
            }
        }
    }

    public IReadOnlyList<PluginOptionsEntry> GetAll()
    {
        lock (_lock) return [.. _entries.Values];
    }
}
