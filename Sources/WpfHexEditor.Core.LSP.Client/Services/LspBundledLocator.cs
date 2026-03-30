// ==========================================================
// Project: WpfHexEditor.Core.LSP.Client
// File: Services/LspBundledLocator.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-29
// Description:
//     Resolves the path to bundled LSP server executables shipped alongside
//     the application in output/tools/lsp/<ServerName>/<ServerName>.exe.
//     Checked before PATH discovery so the bundled version always wins.
//
// Architecture Notes:
//     Static helper — no instance state, no DI required.
//     Executables copied to output by MSBuild ItemGroup in App.csproj.
// ==========================================================

namespace WpfHexEditor.Core.LSP.Client.Services;

/// <summary>
/// Resolves the absolute path of an LSP server executable bundled alongside
/// the application under <c>tools/lsp/&lt;ServerName&gt;/</c>.
/// </summary>
internal static class LspBundledLocator
{
    /// <summary>
    /// Returns the absolute path to the bundled executable for
    /// <paramref name="serverName"/>, or <c>null</c> when not present.
    /// </summary>
    /// <param name="serverName">
    /// Case-sensitive folder name under <c>tools/lsp/</c>
    /// (e.g. <c>"OmniSharp"</c>, <c>"clangd"</c>).
    /// </param>
    public static string? TryGetBundledExecutable(string serverName)
    {
        var lspDir = Path.Combine(AppContext.BaseDirectory, "tools", "lsp", serverName);

        // Try exact name, then .exe suffix (Windows).
        foreach (var candidate in new[] { serverName, serverName + ".exe" })
        {
            var full = Path.Combine(lspDir, candidate);
            if (File.Exists(full)) return full;
        }

        return null;
    }

    /// <summary>
    /// Returns <c>true</c> when <paramref name="executablePath"/> points inside
    /// the bundled <c>tools/lsp/</c> directory tree.
    /// </summary>
    public static bool IsBundledPath(string executablePath) =>
        executablePath.Contains(
            Path.Combine("tools", "lsp"),
            StringComparison.OrdinalIgnoreCase);
}
