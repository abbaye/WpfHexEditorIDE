// Apache 2.0 - 2026
// Contributors: Claude Sonnet 4.6

using System;

namespace WpfHexEditor.Options;

/// <summary>
/// Contract every options page UserControl must implement.
/// <list type="bullet">
///   <item><description><see cref="Load"/> — populate controls from settings (called once, on first display)</description></item>
///   <item><description><see cref="Flush"/> — write control state back to settings (called after <see cref="Changed"/>)</description></item>
///   <item><description><see cref="Changed"/> — raised by the page on any user-driven control change, triggering auto-save</description></item>
/// </list>
/// </summary>
public interface IOptionsPage
{
    /// <summary>Populate controls from <paramref name="settings"/> current values.</summary>
    void Load(AppSettings settings);

    /// <summary>Write control state back into <paramref name="settings"/>.</summary>
    void Flush(AppSettings settings);

    /// <summary>
    /// Raised when the user changes any control.
    /// The <see cref="OptionsEditorControl"/> subscribes and calls
    /// <see cref="Flush"/> then <see cref="AppSettingsService.Save"/>.
    /// </summary>
    event EventHandler? Changed;
}
