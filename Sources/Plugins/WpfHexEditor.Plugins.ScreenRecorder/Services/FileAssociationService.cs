// ==========================================================
// Project: WpfHexEditor.Plugins.ScreenRecorder
// File: Services/FileAssociationService.cs
// Description: Registers the .whscr extension in the Windows registry so the OS
//              associates it with the IDE. Non-fatal: any exception is swallowed;
//              the plugin operates normally without the association.
// ==========================================================

using Microsoft.Win32;

namespace WpfHexEditor.Plugins.ScreenRecorder.Services;

public static class FileAssociationService
{
    private const string Extension    = ".whscr";
    private const string ProgId       = "WpfHexEditor.ScreenCapture.1";
    private const string FriendlyName = "WpfHexEditor Screen Capture Session";

    public static void RegisterIfNeeded()
    {
        try
        {
            Register();
        }
        catch (UnauthorizedAccessException) { /* elevation required — advisory only */ }
        catch { /* non-fatal */ }
    }

    private static void Register()
    {
        using var ext = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{Extension}");
        ext.SetValue("", ProgId);

        using var prog = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{ProgId}");
        prog.SetValue("", FriendlyName);

        var exePath = Environment.ProcessPath ?? System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
        if (string.IsNullOrEmpty(exePath)) return;

        using var open = prog.CreateSubKey(@"shell\open\command");
        open.SetValue("", $"\"{exePath}\" \"%1\"");
    }
}
