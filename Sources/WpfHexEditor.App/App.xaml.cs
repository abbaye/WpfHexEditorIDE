//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
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

    /// <summary>
    /// Two file paths for a diff comparison passed via --diff left right.
    /// Consumed by MainWindow.HandleStartupFile to open DiffViewer directly.
    /// </summary>
    public static (string Left, string Right)? StartupDiffPaths { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ParseStartupArgs(e.Args);
    }

    private static void ParseStartupArgs(string[] args)
    {
        if (args.Length == 0) return;

        // Pattern 1: --diff left right
        var diffIdx = Array.IndexOf(args, "--diff");
        if (diffIdx < 0)
            diffIdx = Array.FindIndex(args, a => a.Equals("--diff", StringComparison.OrdinalIgnoreCase));
        if (diffIdx >= 0 && diffIdx + 2 < args.Length)
        {
            StartupDiffPaths = (args[diffIdx + 1], args[diffIdx + 2]);
            return;
        }

        // Pattern 2: --open "path"
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i].Equals("--open", StringComparison.OrdinalIgnoreCase))
            { StartupFilePath = args[i + 1]; return; }

        // Pattern 3: bare path as first argument (file association, drag-and-drop)
        var first = args[0];
        if (!first.StartsWith('-') && File.Exists(first))
            StartupFilePath = first;
    }
}
