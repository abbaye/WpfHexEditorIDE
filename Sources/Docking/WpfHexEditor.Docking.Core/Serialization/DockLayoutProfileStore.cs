//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.6, Claude Sonnet 4.6
//////////////////////////////////////////////

using System.Text.Json;

namespace WpfHexEditor.Docking.Core.Serialization;

/// <summary>
/// Stores named layout profiles as serialized JSON strings.
/// Profiles can be persisted to/from a JSON file on disk for quick workspace switching.
/// </summary>
public class DockLayoutProfileStore
{
    private readonly Dictionary<string, string> _profiles = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<string> ProfileNames => _profiles.Keys;

    /// <summary>
    /// Saves a layout under the given profile name.
    /// </summary>
    public void SaveProfile(string name, DockLayoutRoot layout)
    {
        _profiles[name] = DockLayoutSerializer.Serialize(layout);
    }

    /// <summary>
    /// Loads a previously saved profile. Returns null if the profile doesn't exist.
    /// </summary>
    public DockLayoutRoot? LoadProfile(string name)
    {
        if (!_profiles.TryGetValue(name, out var json)) return null;
        return DockLayoutSerializer.Deserialize(json);
    }

    /// <summary>
    /// Removes a profile by name.
    /// </summary>
    public bool RemoveProfile(string name) => _profiles.Remove(name);

    /// <summary>
    /// Checks if a profile exists.
    /// </summary>
    public bool HasProfile(string name) => _profiles.ContainsKey(name);

    /// <summary>
    /// Saves all profiles to a JSON file on disk.
    /// </summary>
    public void SaveToFile(string filePath)
    {
        var json = JsonSerializer.Serialize(_profiles, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(filePath, json);
    }

    /// <summary>
    /// Loads profiles from a JSON file on disk.
    /// </summary>
    public void LoadFromFile(string filePath)
    {
        if (!File.Exists(filePath)) return;
        var json = File.ReadAllText(filePath);
        var loaded = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
        if (loaded is null) return;

        _profiles.Clear();
        foreach (var (name, layoutJson) in loaded)
            _profiles[name] = layoutJson;
    }
}
