//////////////////////////////////////////////
// Project      : WpfHexEditor.Commands
// File         : IKeyBindingService.cs
// Description  : Manages user-configurable keyboard gesture overrides per command.
// Architecture : Backed by AppSettings.KeyBindingOverrides (persisted JSON).
//                Used by Command Palette (gesture display) and Keyboard Shortcuts options page.
//////////////////////////////////////////////

namespace WpfHexEditor.Core.Commands;

/// <summary>
/// Resolves the effective keyboard gesture for a command:
/// user override (if set) takes precedence over the command's default gesture.
/// </summary>
public interface IKeyBindingService
{
    /// <summary>
    /// Returns the effective gesture for <paramref name="commandId"/>.
    /// Priority: user override → command's <see cref="CommandDefinition.DefaultGesture"/> → null.
    /// </summary>
    string? ResolveGesture(string commandId);

    /// <summary>
    /// Sets or clears the user override gesture for <paramref name="commandId"/>.
    /// Pass null to remove the override (revert to default).
    /// Persists immediately via <see cref="WpfHexEditor.Core.Options.AppSettingsService"/>.
    /// </summary>
    void SetOverride(string commandId, string? gesture);

    /// <summary>Removes the user override for a single command.</summary>
    void ResetOverride(string commandId);

    /// <summary>Clears all user overrides and persists.</summary>
    void ResetAll();

    /// <summary>Read-only snapshot of current user overrides (commandId → gesture string).</summary>
    IReadOnlyDictionary<string, string> GetOverrides();
}
