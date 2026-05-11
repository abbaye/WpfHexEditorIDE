// ==========================================================
// Project: WpfHexEditor.App
// File: Plugins/Commands/TerminalArgs.cs
// Description: Shared --flag value parser for plugin terminal commands.
// ==========================================================

namespace WpfHexEditor.App.Plugins.Commands;

internal static class TerminalArgs
{
    /// <summary>Returns the value following <paramref name="flag"/> in <paramref name="args"/>, or null.</summary>
    public static string? GetFlag(string[] args, string flag)
    {
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == flag) return args[i + 1];
        return null;
    }
}
