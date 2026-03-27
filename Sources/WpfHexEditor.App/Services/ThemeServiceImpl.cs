//////////////////////////////////////////////
// Project: WpfHexEditor.App
// File: Services/ThemeServiceImpl.cs
// Description:
//     Centralized theme management service. Owns the full theme lifecycle:
//     loading ResourceDictionaries, fallback on failure, persisting selection,
//     notifying plugins (IThemeService contract), and syncing HexEditors.
// Architecture:
//     Extracted from MainWindow.ApplyTheme to reduce MainWindow code-behind.
//     MainWindow theme menu handlers delegate to ApplyTheme(file, name).
//     Callback SyncHexEditors is set by MainWindow at construction time.
//////////////////////////////////////////////

using System.Windows;
using WpfHexEditor.Core.Options;
using WpfHexEditor.SDK.Contracts;

namespace WpfHexEditor.App.Services;

/// <summary>
/// Centralized theme management service. Handles resource dictionary loading,
/// fallback, persistence, and SDK contract notification.
/// </summary>
public sealed class ThemeServiceImpl : IThemeService
{
    private const string FallbackThemeFile = "DarkTheme.xaml";
    private const string FallbackThemeName = "Dark";
    private const string ThemePackPrefix = "pack://application:,,,/WpfHexEditor.Shell;component/Themes/";

    private string _currentTheme = "Dark";
    private string _lastAppliedTheme = string.Empty;

    /// <summary>
    /// Callback invoked after theme resources are loaded. MainWindow sets this to
    /// <c>SyncAllHexEditorThemes()</c> so editor controls pick up the new palette.
    /// </summary>
    public Action? SyncHexEditors { get; set; }

    /// <summary>
    /// Callback invoked after theme change to forward theme XAML to sandboxed plugins.
    /// </summary>
    public Func<string, Task>? NotifySandboxPlugins { get; set; }

    // ── IThemeService contract ───────────────────────────────────────────────

    public string CurrentTheme => _currentTheme;

    public event EventHandler? ThemeChanged;

    public ResourceDictionary GetThemeResources()
        => Application.Current?.Resources ?? new ResourceDictionary();

    public void RegisterThemeAwareControl(FrameworkElement element) { }
    public void UnregisterThemeAwareControl(FrameworkElement element) { }

    // ── Public API ───────────────────────────────────────────────────────────

    /// <summary>
    /// Applies the theme saved in AppSettings. No-op if already applied.
    /// </summary>
    public void ApplyFromSettings()
    {
        var stem = AppSettingsService.Instance.Current.ActiveThemeName;
        if (string.IsNullOrWhiteSpace(stem) || stem == _lastAppliedTheme) return;
        ApplyTheme($"{stem}.xaml", stem);
    }

    /// <summary>
    /// Loads a theme ResourceDictionary, syncs editors, notifies plugins, and persists.
    /// </summary>
    public void ApplyTheme(string themeFile, string themeName)
    {
        try
        {
            Application.Current.Resources.MergedDictionaries.Clear();
            Application.Current.Resources.MergedDictionaries.Add(
                new ResourceDictionary
                {
                    Source = new Uri($"{ThemePackPrefix}{themeFile}")
                });
        }
        catch (Exception ex) when (ex is System.IO.IOException or System.IO.FileNotFoundException or UriFormatException)
        {
            OutputLogger.Warn($"Theme '{themeName}' could not be loaded: {ex.Message}. Reverting to {FallbackThemeName}.");
            AppSettingsService.Instance.Current.ActiveThemeName = string.Empty;
            AppSettingsService.Instance.Save();
            _lastAppliedTheme = string.Empty;
            if (!themeFile.Equals(FallbackThemeFile, StringComparison.OrdinalIgnoreCase))
                ApplyTheme(FallbackThemeFile, FallbackThemeName);
            return;
        }

        SyncHexEditors?.Invoke();
        OutputLogger.Info($"Theme changed to {themeName}.");

        // Notify SDK consumers
        _currentTheme = themeName;
        ThemeChanged?.Invoke(this, EventArgs.Empty);

        // Forward to sandbox plugins
        if (NotifySandboxPlugins is not null)
        {
            var themeXaml = WpfHexEditor.PluginHost.Sandbox.ThemeResourceSerializer.Serialize(
                Application.Current.Resources);
            _ = NotifySandboxPlugins(themeXaml);
        }

        // Persist
        var stem = System.IO.Path.GetFileNameWithoutExtension(themeFile);
        AppSettingsService.Instance.Current.ActiveThemeName = stem;
        AppSettingsService.Instance.Save();
        _lastAppliedTheme = stem;
    }
}
