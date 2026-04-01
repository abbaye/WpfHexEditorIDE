//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.IO;

namespace WpfHexEditor.PluginHost;

/// <summary>
/// Persists and restores plugin state to/from %AppData%/WpfHexEditor/Plugins/.
/// </summary>
internal sealed class PluginStateSerializer
{
    private readonly string _baseDirectory;

    public PluginStateSerializer()
    {
        _baseDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "WpfHexEditor", "Plugins");
    }

    /// <summary>
    /// Loads the persisted state for the specified plugin.
    /// Returns null if no state file exists or the file is empty.
    /// </summary>
    public async Task<string?> LoadStateAsync(string pluginId)
    {
        string path = GetStatePath(pluginId);
        if (!File.Exists(path)) return null;

        try
        {
            string content = await File.ReadAllTextAsync(path).ConfigureAwait(false);
            return string.IsNullOrWhiteSpace(content) ? null : content;
        }
        catch (IOException)
        {
            return null;
        }
    }

    /// <summary>
    /// Saves the plugin state atomically (write to .tmp, then rename).
    /// </summary>
    public async Task SaveStateAsync(string pluginId, string state)
    {
        Directory.CreateDirectory(_baseDirectory);
        string path = GetStatePath(pluginId);
        string tempPath = path + ".tmp";

        await File.WriteAllTextAsync(tempPath, state).ConfigureAwait(false);
        File.Move(tempPath, path, overwrite: true);
    }

    /// <summary>Deletes the persisted state for the specified plugin.</summary>
    public void DeleteState(string pluginId)
    {
        string path = GetStatePath(pluginId);
        if (File.Exists(path))
            File.Delete(path);
    }

    private string GetStatePath(string pluginId) =>
        Path.Combine(_baseDirectory, $"{pluginId}.json");
}
