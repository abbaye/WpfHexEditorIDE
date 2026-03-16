// ==========================================================
// Project: WpfHexEditor.SDK
// File: Models/PluginFeature.cs
// Created: 2026-03-15
// Description:
//     Well-known semantic feature identifiers for the plugin capability system.
//     Plugins declare these strings in manifest "features" array.
//     IDE and other plugins query them via IPluginCapabilityRegistry.
//
// Architecture Notes:
//     String constants (not enum) — forward-compatible and extensible.
//     Plugins may also declare custom strings beyond the well-known set.
//     The corresponding execution contracts live in SDK/ExtensionPoints/.
// ==========================================================

namespace WpfHexEditor.SDK.Models;

/// <summary>
/// Well-known semantic feature identifiers.
/// Declare in manifest: <c>"features": ["HexViewOverlay", "BinaryAnalyzer"]</c>
/// Query via: <c>context.CapabilityRegistry.FindPluginsWithFeature(PluginFeature.BinaryAnalyzer)</c>
/// </summary>
public static class PluginFeature
{
    /// <summary>Plugin can render overlays on the hex view (highlights, annotations).</summary>
    public const string HexViewOverlay = "HexViewOverlay";

    /// <summary>Plugin performs binary analysis on open files.</summary>
    public const string BinaryAnalyzer = "BinaryAnalyzer";

    /// <summary>Plugin parses PE (Portable Executable) file format.</summary>
    public const string PEParser = "PEParser";

    /// <summary>Plugin provides disassembly of binary code.</summary>
    public const string DisassemblyProvider = "DisassemblyProvider";

    /// <summary>Plugin provides source-code decompilation of binaries.</summary>
    public const string DecompilerProvider = "DecompilerProvider";

    /// <summary>Plugin detects and identifies binary file formats.</summary>
    public const string FormatDetector = "FormatDetector";

    /// <summary>Plugin provides binary structure templates (fields, offsets).</summary>
    public const string StructureTemplate = "StructureTemplate";

    /// <summary>Plugin provides or extends a scripting engine.</summary>
    public const string ScriptEngine = "ScriptEngine";

    /// <summary>Plugin extends the integrated terminal with commands or shell types.</summary>
    public const string TerminalExtension = "TerminalExtension";
}
