//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5, Claude Sonnet 4.6
//////////////////////////////////////////////

using System.IO;
using System.Windows;

namespace WpfHexEditor.App;

public partial class App : Application
{
    /// <summary>
    /// File or solution path passed via command-line argument (--open "path" or bare path).
    /// Consumed by MainWindow.OnLoaded to open on startup.
    /// </summary>
    public static string? StartupFilePath { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        StartupFilePath = ParseStartupFilePath(e.Args);
    }

    private static string? ParseStartupFilePath(string[] args)
    {
        if (args.Length == 0) return null;

        // Pattern 1: --open "path"
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i].Equals("--open", StringComparison.OrdinalIgnoreCase))
                return args[i + 1];

        // Pattern 2: bare path as first argument (file association, drag-and-drop)
        var first = args[0];
        if (!first.StartsWith('-') && File.Exists(first))
            return first;

        return null;
    }
}
