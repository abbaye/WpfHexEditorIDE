using System.Globalization;
using System.Threading;
using System.Windows;
using System.Windows.Markup;
using WpfHexEditor.Sample.Main.Properties;

namespace WpfHexEditor.Sample.Main
{
    public partial class App : Application
    {
        public App()
        {
            // CRITICAL: Set culture in constructor BEFORE any WPF initialization
            var cultureName = Settings.Default.PreferredCulture;

            System.Diagnostics.Debug.WriteLine($"[App.Constructor] Loaded PreferredCulture from settings: '{cultureName}'");

            if (!string.IsNullOrEmpty(cultureName))
            {
                try
                {
                    var culture = new CultureInfo(cultureName);
                    Thread.CurrentThread.CurrentCulture = culture;
                    Thread.CurrentThread.CurrentUICulture = culture;
                    CultureInfo.DefaultThreadCurrentCulture = culture;
                    CultureInfo.DefaultThreadCurrentUICulture = culture;

                    // CRITICAL FIX: Set WPF's Language property for all FrameworkElements
                    // This ensures WPF respects the culture setting and doesn't reset it
                    FrameworkElement.LanguageProperty.OverrideMetadata(
                        typeof(FrameworkElement),
                        new FrameworkPropertyMetadata(
                            XmlLanguage.GetLanguage(culture.IetfLanguageTag)));

                    System.Diagnostics.Debug.WriteLine($"[App.Constructor] Successfully set culture to: {culture.Name} ({culture.NativeName})");
                    System.Diagnostics.Debug.WriteLine($"[App.Constructor] Set FrameworkElement.Language to: {culture.IetfLanguageTag}");
                }
                catch (CultureNotFoundException ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[App.Constructor] CultureNotFoundException: {ex.Message}");
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("[App.Constructor] PreferredCulture is null or empty, using system default");
            }
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"[App.OnStartup] Current UI Culture: {Thread.CurrentThread.CurrentUICulture.Name}");
            base.OnStartup(e);
        }
    }
}
