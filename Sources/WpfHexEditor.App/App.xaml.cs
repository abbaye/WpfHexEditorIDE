//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5, Claude Sonnet 4.6
//////////////////////////////////////////////

using System.Globalization;
using System.IO;
using System.Windows;
using WpfHexEditor.Core.Localization.Services;
using WpfHexEditor.Core.Options;
using LocalizationService = WpfHexEditor.Core.Localization.Services.LocalizationService;

namespace WpfHexEditor.App;

public partial class App : Application
{
    /// <summary>
    /// File or solution path passed via command-line argument (--open "path" or bare path).
    /// Consumed by MainWindow.OnLoaded to open on startup.
    /// </summary>
    public static string? StartupFilePath { get; private set; }

    /// <summary>
    /// When set, MainWindow opens a DiffViewer comparing these two files on startup.
    /// Usage: WpfHexEditor.exe --diff "left.bin" "right.bin"
    /// </summary>
    public static (string Left, string Right)? StartupDiffPaths { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        LocalizationService.Instance = new LocalizationService();
        ParseCommandLine(e.Args);
        RestorePreferredLanguage();
    }

    /// <summary>
    /// Restores the UI language persisted in AppSettings.
    /// Empty/missing = keep the system default (no-op).
    /// </summary>
    private static void RestorePreferredLanguage()
    {
        var cultureName = AppSettingsService.Instance.Current.PreferredLanguage;
        if (string.IsNullOrWhiteSpace(cultureName)) return;

        try
        {
            LocalizedResourceDictionary.ChangeCulture(new CultureInfo(cultureName));
        }
        catch (CultureNotFoundException)
        {
            // Unknown culture name stored in settings — ignore, keep system default.
        }
    }

    private static void ParseCommandLine(string[] args)
    {
        if (args.Length == 0) return;

        // Pattern 1: --diff "left" "right"
        for (int i = 0; i < args.Length - 2; i++)
        {
            if (args[i].Equals("--diff", StringComparison.OrdinalIgnoreCase))
            {
                StartupDiffPaths = (args[i + 1], args[i + 2]);
                return;
            }
        }

        // Pattern 2: --open "path"
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i].Equals("--open", StringComparison.OrdinalIgnoreCase))
            {
                StartupFilePath = args[i + 1];
                return;
            }
        }

        // Pattern 3: bare path as first argument (file association, drag-and-drop)
        var first = args[0];
        if (!first.StartsWith('-') && File.Exists(first))
            StartupFilePath = first;
    }
}
