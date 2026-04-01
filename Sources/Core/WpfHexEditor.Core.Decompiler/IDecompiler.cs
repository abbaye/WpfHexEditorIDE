// ==========================================================
// Project: WpfHexEditor.Core.Decompiler
// File: IDecompiler.cs
// Description:
//     Contract for pluggable decompiler backends.
//     Implementations (e.g. ILSpy, Ghidra-bridge) register themselves
//     in DecompilerRegistry and are discovered by DisassemblyViewer.
// ==========================================================

namespace WpfHexEditor.Core.Decompiler;

/// <summary>
/// A decompiler backend that can transform binary files into human-readable text.
/// </summary>
public interface IDecompiler
{
    /// <summary>Human-readable name shown in the UI (e.g. "ILSpy 9.0").</summary>
    string DisplayName { get; }

    /// <summary>Architecture tag shown in the viewer status bar (e.g. "x86-64", "CIL").</summary>
    string Architecture { get; }

    /// <summary>Returns true if this decompiler can process the given file.</summary>
    bool CanDecompile(string filePath);

    /// <summary>
    /// Decompiles the file at <paramref name="filePath"/> and returns the text output.
    /// </summary>
    Task<string> DecompileAsync(string filePath, CancellationToken ct = default);
}
