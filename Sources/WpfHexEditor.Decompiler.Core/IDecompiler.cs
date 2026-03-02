//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

namespace WpfHexEditor.Decompiler.Core;

/// <summary>
/// Contract for an architecture-specific decompiler plug-in.
/// Implementations are registered via <see cref="DecompilerRegistry.Register"/> at application startup.
/// </summary>
public interface IDecompiler
{
    /// <summary>
    /// Architecture identifier (e.g. "x86", "x86-64", "ARM", "WASM").
    /// </summary>
    string Architecture { get; }

    /// <summary>
    /// Human-readable display name shown in the UI.
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// Returns <c>true</c> if this decompiler can handle the given file
    /// (typically based on file extension and/or magic bytes inspection).
    /// </summary>
    bool CanDecompile(string filePath);

    /// <summary> 
    /// Decompiles the file at <paramref name="filePath"/> and returns the
    /// decompiled text (assembly listing, pseudo-C, etc.).
    /// </summary>
    Task<string> DecompileAsync(string filePath, CancellationToken ct = default);
}

/// <summary>
/// Global registry for <see cref="IDecompiler"/> plug-ins.
/// Call <see cref="Register"/> at application startup for each decompiler implementation.
/// </summary>
public static class DecompilerRegistry
{
    private static readonly List<IDecompiler> _items = [];

    /// <summary>
    /// All registered decompilers (read-only view).
    /// </summary>
    public static IReadOnlyList<IDecompiler> All => _items;

    /// <summary>
    /// Registers a decompiler plug-in.
    /// </summary>
    public static void Register(IDecompiler d) => _items.Add(d);
}
