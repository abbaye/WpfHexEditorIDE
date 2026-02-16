using System.Windows;
using WpfHexEditor.Sample.Main.Services;

namespace WpfHexEditor.Sample.Main
{
    public partial class App : Application
    {
        public App()
        {
            // CRITICAL: Initialize culture BEFORE any WPF initialization
            // DynamicResourceManager handles culture loading from settings and application-wide culture management
            DynamicResourceManager.Initialize();

            System.Diagnostics.Debug.WriteLine($"[App.Constructor] DynamicResourceManager initialized with culture: {DynamicResourceManager.CurrentCulture.Name}");

            // Initialize theme system AFTER culture
            // ThemeManager handles theme loading from settings and application-wide theme management
            ThemeManager.Initialize();

            System.Diagnostics.Debug.WriteLine($"[App.Constructor] ThemeManager initialized with theme: {ThemeManager.CurrentTheme}");
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"[App.OnStartup] Current UI Culture: {DynamicResourceManager.CurrentCulture.Name}");
            base.OnStartup(e);
        }
    }
}
