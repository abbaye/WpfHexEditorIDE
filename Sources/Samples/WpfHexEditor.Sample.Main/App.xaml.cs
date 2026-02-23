using System.Windows;
using WpfHexEditor.Sample.Main.Services;
using Props = WpfHexEditor.Sample.Main.Properties;

namespace WpfHexEditor.Sample.Main
{
    public partial class App : Application
    {
        public App()
        {
            // CRITICAL: Migrate settings BEFORE any initialization
            MigrateSettings();

            // CRITICAL: Initialize culture BEFORE any WPF initialization
            // DynamicResourceManager handles culture loading from settings and application-wide culture management
            DynamicResourceManager.Initialize();

            // Initialize theme system AFTER culture
            // ThemeManager handles theme loading from settings and application-wide theme management
            ThemeManager.Initialize();
        }

        private void MigrateSettings()
        {
            var settings = Props.Settings.Default;

            // Check if this is first run or needs migration
            if (settings.SettingsVersion < 2)
            {
                // V1 -> V2 Migration
                // BytesPerLine and ShowStatusBar were removed from SettingsPanelViewModel
                // but were never persisted, so no migration needed

                // Mark as migrated
                settings.SettingsVersion = 2;
                settings.Save();
            }
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
        }
    }
}
