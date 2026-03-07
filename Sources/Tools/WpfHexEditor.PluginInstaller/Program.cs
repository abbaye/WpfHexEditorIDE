//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

// WpfHexEditor.PluginInstaller
// Usage:
//   WpfHexEditor.PluginInstaller.exe <path-to.whxplugin>   — show installer dialog
//   WpfHexEditor.PluginInstaller.exe --silent <path>        — silent install (exit code 0=ok, 1=error)

using System.IO;
using System.Windows;

namespace WpfHexEditor.PluginInstaller;

internal static class Program
{
    [STAThread]
    private static async Task<int> Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("Usage: WpfHexEditor.PluginInstaller.exe [--silent] <plugin.whxplugin>");
            return 1;
        }

        bool silent      = args[0] == "--silent";
        string? filePath = silent && args.Length > 1 ? args[1]
                         : !silent               ? args[0]
                         : null;

        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
        {
            Console.Error.WriteLine($"Error: file not found: {filePath}");
            return 2;
        }

        if (silent)
            return await RunSilentAsync(filePath).ConfigureAwait(false);

        // GUI mode
        var app = new Application();
        var win = new InstallerWindow(filePath);
        app.Run(win);
        return 0;
    }

    private static async Task<int> RunSilentAsync(string filePath)
    {
        try
        {
            var extractor = new PluginPackageExtractor();
            var dir = await extractor.ExtractAsync(filePath).ConfigureAwait(false);
            Console.WriteLine($"Installed: {dir}");
            return 0;
        }
        catch (PluginInstallException ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 3;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Unexpected error: {ex.Message}");
            return 4;
        }
    }
}
