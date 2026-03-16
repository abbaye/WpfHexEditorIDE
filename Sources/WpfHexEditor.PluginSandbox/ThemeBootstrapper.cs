//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

// ==========================================================
// Project: WpfHexEditor.PluginSandbox
// File: ThemeBootstrapper.cs
// Created: 2026-03-15
// Description:
//     Applies the host IDE's serialized theme XAML into the sandbox
//     Application.Resources so plugin WPF controls see the same
//     brush/color tokens as the rest of the IDE.
//
// Architecture Notes:
//     - Called from SandboxedPluginRunner after plugin initialization,
//       passing the ThemeResourcesXaml string from InitializeRequestPayload.
//     - XamlReader.Parse() deserializes the XAML into a ResourceDictionary
//       which is then added to Application.Current.Resources.MergedDictionaries.
//     - On theme change (ThemeChangedNotification), Apply() is called again
//       with the new XAML — the old sandbox theme dict is replaced.
//     - Must be called on the WPF STA Dispatcher thread.
// ==========================================================

using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Threading;

namespace WpfHexEditor.PluginSandbox;

/// <summary>
/// Merges the host theme resources into the sandbox <see cref="Application"/>.
/// Keeps a reference to the last injected dictionary for clean replacement on theme change.
/// </summary>
internal sealed class ThemeBootstrapper
{
    private ResourceDictionary? _current;

    /// <summary>
    /// ResourceDictionaries loaded from Source URIs (Styles, ControlTemplates, etc.).
    /// Tracked so they can be removed on <see cref="Remove"/>.
    /// Loaded once at init time; not replaced on theme change because all style values
    /// use <c>{DynamicResource}</c> so they automatically track brush updates.
    /// </summary>
    private readonly List<ResourceDictionary> _currentUriDicts = [];

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Parses <paramref name="themeXaml"/> and merges it into
    /// <see cref="Application.Current"/>.Resources on the STA thread.
    /// If a previous theme dictionary was installed it is replaced atomically.
    /// </summary>
    public void Apply(string themeXaml)
    {
        if (string.IsNullOrWhiteSpace(themeXaml)) return;

        EnsureDispatcher(() =>
        {
            try
            {
                var newDict = (ResourceDictionary)XamlReader.Parse(themeXaml);

                var appResources = Application.Current?.Resources;
                if (appResources is null) return;

                // Replace old injected dict atomically
                if (_current is not null)
                    appResources.MergedDictionaries.Remove(_current);

                appResources.MergedDictionaries.Add(newDict);
                _current = newDict;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[ThemeBootstrapper] Failed to apply theme: {ex.Message}");
            }
        });
    }

    /// <summary>
    /// Loads each pack:// URI as a <see cref="ResourceDictionary"/> and merges it into
    /// <see cref="Application.Current"/>.Resources BEFORE the primitive resources
    /// (see <see cref="Apply"/>).  This ensures Style keys such as
    /// <c>PanelToolbarStyle</c>, <c>PanelTreeViewItemStyle</c>, etc. are present
    /// in <c>Application.Resources</c> when plugin XAML calls
    /// <c>InitializeComponent()</c> and resolves <c>{StaticResource PanelToolbarStyle}</c>.
    /// <para>
    /// For a pack URI to resolve, the referenced assembly must be loaded into the
    /// default <see cref="AssemblyLoadContext"/>.  This method pre-loads any referenced
    /// assembly found under <see cref="AppDomain.CurrentDomain"/>.<see cref="AppDomain.BaseDirectory"/>.
    /// </para>
    /// Call this once at plugin init; NOT on theme change (styles use DynamicResource
    /// for all color values, so brush updates propagate without reloading the dicts).
    /// </summary>
    public void ApplyUris(IReadOnlyList<string> uris)
    {
        if (uris.Count == 0) return;

        EnsureDispatcher(() =>
        {
            var appResources = Application.Current?.Resources;
            if (appResources is null) return;

            foreach (var uri in uris)
            {
                try
                {
                    EnsureAssemblyForPackUri(uri);
                    var rd = new ResourceDictionary { Source = new Uri(uri, UriKind.RelativeOrAbsolute) };
                    appResources.MergedDictionaries.Add(rd);
                    _currentUriDicts.Add(rd);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine(
                        $"[ThemeBootstrapper] Failed to load URI '{uri}': {ex.Message}");
                }
            }
        });
    }

    /// <summary>Removes all injected theme dictionaries (called on shutdown).</summary>
    public void Remove()
    {
        EnsureDispatcher(() =>
        {
            var appResources = Application.Current?.Resources;
            if (appResources is null) return;

            if (_current is not null)
            {
                appResources.MergedDictionaries.Remove(_current);
                _current = null;
            }

            foreach (var rd in _currentUriDicts)
                appResources.MergedDictionaries.Remove(rd);
            _currentUriDicts.Clear();
        });
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// For a pack:// URI like <c>pack://application:,,,/Foo.Bar;component/Themes/X.xaml</c>,
    /// extracts the assembly name <c>Foo.Bar</c> and loads
    /// <c>Foo.Bar.dll</c> from <see cref="AppDomain.BaseDirectory"/> into
    /// <see cref="AssemblyLoadContext.Default"/> so WPF can resolve the pack URI.
    /// No-op for non-pack URIs or if the assembly is already loaded.
    /// </summary>
    private static void EnsureAssemblyForPackUri(string uri)
    {
        const string PackPrefix = "pack://application:,,,/";
        if (!uri.StartsWith(PackPrefix, StringComparison.OrdinalIgnoreCase)) return;

        var tail = uri[PackPrefix.Length..];
        var semicolonIdx = tail.IndexOf(';');
        if (semicolonIdx < 0) return;

        var assemblyName = tail[..semicolonIdx];
        if (string.IsNullOrEmpty(assemblyName)) return;

        // Already loaded in any context?
        var already = AppDomain.CurrentDomain.GetAssemblies()
            .Any(a => string.Equals(a.GetName().Name, assemblyName,
                                    StringComparison.OrdinalIgnoreCase));
        if (already) return;

        var dllPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, assemblyName + ".dll");
        if (!File.Exists(dllPath)) return;

        try { AssemblyLoadContext.Default.LoadFromAssemblyPath(dllPath); }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                $"[ThemeBootstrapper] Could not pre-load '{assemblyName}': {ex.Message}");
        }
    }

    private static void EnsureDispatcher(Action action)
    {
        var app = Application.Current;
        if (app is null)
        {
            action(); // no application yet — execute inline (should not happen)
            return;
        }

        if (app.Dispatcher.CheckAccess())
            action();
        else
            app.Dispatcher.Invoke(action, DispatcherPriority.Normal);
    }
}
