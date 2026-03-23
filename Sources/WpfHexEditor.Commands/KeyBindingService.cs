//////////////////////////////////////////////
// Project      : WpfHexEditor.Commands
// File         : KeyBindingService.cs
// Description  : Resolves effective keyboard gestures using user overrides.
//                Holds overrides in memory; persistence is the caller's responsibility.
//                The host (MainWindow) loads initial overrides via LoadOverrides()
//                and subscribes to OverridesChanged to persist on every mutation.
// Architecture : No AppSettings dependency — decoupled via event callback pattern.
//////////////////////////////////////////////

namespace WpfHexEditor.Commands;

/// <summary>
/// Manages user-configurable gesture overrides per command.
/// Effective gesture = user override ?? CommandDefinition.DefaultGesture.
/// </summary>
public sealed class KeyBindingService : IKeyBindingService
{
    private readonly ICommandRegistry _registry;
    private readonly Dictionary<string, string> _overrides =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Raised after any mutation (SetOverride, ResetOverride, ResetAll).
    /// The host should persist <see cref="GetOverrides"/> in response.
    /// </summary>
    public event Action? OverridesChanged;

    public KeyBindingService(ICommandRegistry registry)
    {
        _registry = registry;
    }

    /// <summary>
    /// Populates the in-memory overrides from persisted storage.
    /// Call once at startup after loading settings.
    /// </summary>
    public void LoadOverrides(IReadOnlyDictionary<string, string> persisted)
    {
        _overrides.Clear();
        foreach (var (k, v) in persisted)
            _overrides[k] = v;
    }

    /// <inheritdoc />
    public string? ResolveGesture(string commandId)
    {
        if (_overrides.TryGetValue(commandId, out var userGesture))
            return string.IsNullOrEmpty(userGesture) ? null : userGesture;

        return _registry.Find(commandId)?.DefaultGesture;
    }

    /// <inheritdoc />
    public void SetOverride(string commandId, string? gesture)
    {
        if (gesture is null)
            _overrides.Remove(commandId);
        else
            _overrides[commandId] = gesture;

        OverridesChanged?.Invoke();
    }

    /// <inheritdoc />
    public void ResetOverride(string commandId)
    {
        _overrides.Remove(commandId);
        OverridesChanged?.Invoke();
    }

    /// <inheritdoc />
    public void ResetAll()
    {
        _overrides.Clear();
        OverridesChanged?.Invoke();
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, string> GetOverrides() => _overrides;
}
