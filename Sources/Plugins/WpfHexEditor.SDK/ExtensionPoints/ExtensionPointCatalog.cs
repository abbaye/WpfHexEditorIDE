// ==========================================================
// Project: WpfHexEditor.SDK
// File: ExtensionPoints/ExtensionPointCatalog.cs
// Created: 2026-03-15
// Description:
//     Maps well-known extension point name strings (from manifest "extensions" dict)
//     to their corresponding contract types.
//     Used by WpfPluginHost to resolve the correct interface when loading plugin contributions.
//
// Architecture Notes:
//     Case-insensitive string comparison for forward-compatibility.
//     Unknown names are logged as warnings — future IDE versions may add new points.
// ==========================================================

using WpfHexEditor.SDK.Contracts.Terminal;

namespace WpfHexEditor.SDK.ExtensionPoints;

/// <summary>
/// Static catalog mapping well-known extension point names to their contract types.
/// <para>
/// WpfPluginHost uses this to resolve the interface type when a plugin declares:
/// <code>"extensions": { "FileAnalyzer": "MyPlugin.MyClass" }</code>
/// </para>
/// </summary>
public static class ExtensionPointCatalog
{
    /// <summary>Map of well-known extension point names (case-insensitive) to contract types.</summary>
    public static readonly IReadOnlyDictionary<string, Type> KnownPoints =
        new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase)
        {
            ["FileAnalyzer"]    = typeof(IFileAnalyzerExtension),
            ["HexViewOverlay"]  = typeof(IHexViewOverlayExtension),
            ["BinaryParser"]    = typeof(IBinaryParserExtension),
            ["Decompiler"]      = typeof(IDecompilerExtension),
            ["QuickInfo"]       = typeof(IQuickInfoProvider),
            ["Minimap"]           = typeof(IMinimapExtension),
            ["TerminalCommand"]   = typeof(ITerminalCommandProvider),
        };

    /// <summary>
    /// Tries to resolve the contract type for a given extension point name.
    /// Returns null and logs a warning for unknown names (forward-compatible).
    /// </summary>
    public static Type? TryResolve(string extensionPointName)
        => KnownPoints.TryGetValue(extensionPointName, out var type) ? type : null;
}
