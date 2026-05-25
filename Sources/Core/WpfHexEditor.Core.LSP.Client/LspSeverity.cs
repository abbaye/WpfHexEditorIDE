// ==========================================================
// Project: WpfHexEditor.Core.LSP.Client
// File: LspSeverity.cs
// Description: Shared LSP diagnostic severity integer → string mapper.
// ==========================================================

namespace WpfHexEditor.Core.LSP.Client;

internal static class LspSeverity
{
    // LSP spec: 1=Error, 2=Warning, 3=Information, 4=Hint
    internal static string ToString(int s) => s switch
    {
        1 => "error",
        2 => "warning",
        3 => "information",
        4 => "hint",
        _ => "information",
    };
}
