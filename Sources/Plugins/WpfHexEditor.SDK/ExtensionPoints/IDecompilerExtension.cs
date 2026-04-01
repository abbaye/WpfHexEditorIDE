// ==========================================================
// Project: WpfHexEditor.SDK
// File: ExtensionPoints/IDecompilerExtension.cs
// Created: 2026-03-15
// Description:
//     Extension point contract for plugins that provide source-code decompilation.
//     Used by "Open in Decompiler" context menu command.
// ==========================================================

namespace WpfHexEditor.SDK.ExtensionPoints;

/// <summary>
/// Extension point contract: binary decompilation.
/// Plugins implementing this are offered when the user invokes "Open in Decompiler"
/// on a binary selection or file node.
/// Register in manifest: <c>"extensions": { "Decompiler": "MyPlugin.MyDecompilerClass" }</c>
/// </summary>
public interface IDecompilerExtension
{
    /// <summary>Display name for this decompiler (e.g. "ILSpy Decompiler").</summary>
    string DecompilerName { get; }

    /// <summary>File extensions supported by this decompiler (with dot, e.g. ".exe", ".dll").</summary>
    IReadOnlyList<string> SupportedExtensions { get; }

    /// <summary>
    /// Decompiles the given <paramref name="data"/> bytes into human-readable source code.
    /// Returns null if decompilation is not supported for this data.
    /// </summary>
    Task<string?> DecompileAsync(byte[] data, string fileExtension, CancellationToken ct = default);
}
