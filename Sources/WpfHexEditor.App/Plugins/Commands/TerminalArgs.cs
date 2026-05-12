// ==========================================================
// Project: WpfHexEditor.App
// File: Plugins/Commands/TerminalArgs.cs
// Description: Shared --flag value parser for plugin terminal commands.
// ==========================================================

namespace WpfHexEditor.App.Plugins.Commands;

internal static class TerminalArgs
{
    /// <summary>
    /// Returns the value of <paramref name="flag"/>, accepting both
    /// <c>--flag value</c> and <c>--flag=value</c> forms; null if absent.
    /// </summary>
    public static string? GetFlag(string[] args, string flag)
    {
        var prefix = flag + "=";
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == flag && i + 1 < args.Length) return args[i + 1];
            if (args[i].StartsWith(prefix, StringComparison.Ordinal)) return args[i][prefix.Length..];
        }
        return null;
    }
}
