// ==========================================================
// Project: WpfHexEditor.Editor.XamlDesigner
// File: Services/CodeGen/ICodeBehindDocumentBuffer.cs
// Author: Derek Tremblay
// Created: 2026-05-05
// Description:
//     Abstraction over the .xaml.cs companion file.
//     Decouples CodeBehindSyncService from whether the file is currently
//     open as an IDE buffer or exists only on disk, enabling both paths
//     without branching in the orchestration layer.
//
// Architecture Notes:
//     Dependency Inversion — SyncService depends on this interface, not on
//     the concrete IDE buffer or file I/O implementation.
//     LinkedCodeBehindBuffer resolves the companion path automatically from
//     the XAML file path (Foo.xaml → Foo.xaml.cs).
// ==========================================================

using System.IO;

namespace WpfHexEditor.Editor.XamlDesigner.Services.CodeGen;

/// <summary>
/// Abstraction over the code-behind companion file for a XAML document.
/// Provides read/write access regardless of whether the file is open in the IDE.
/// </summary>
public interface ICodeBehindDocumentBuffer
{
    /// <summary>Absolute path to the .xaml.cs file (may not exist yet).</summary>
    string FilePath { get; }

    /// <summary>True when the file currently exists on disk or as an open buffer.</summary>
    bool Exists { get; }

    /// <summary>Reads the current text of the code-behind file. Returns empty string when absent.</summary>
    Task<string> ReadAsync(CancellationToken ct = default);

    /// <summary>
    /// Writes <paramref name="newText"/> to the code-behind file, creating it if absent.
    /// When the file is open as an IDE document, the live buffer is updated in-place.
    /// </summary>
    Task WriteAsync(string newText, CancellationToken ct = default);
}

/// <summary>
/// <see cref="ICodeBehindDocumentBuffer"/> implementation backed by the file system.
/// Resolves the companion path as <c>{xamlPath}.cs</c> (e.g. Foo.xaml → Foo.xaml.cs).
/// </summary>
public sealed class LinkedCodeBehindBuffer : ICodeBehindDocumentBuffer
{
    public string FilePath { get; }

    public bool Exists => File.Exists(FilePath);

    public LinkedCodeBehindBuffer(string xamlFilePath)
    {
        // Standard WPF convention: Foo.xaml → Foo.xaml.cs
        FilePath = xamlFilePath + ".cs";
    }

    public async Task<string> ReadAsync(CancellationToken ct = default)
    {
        if (!File.Exists(FilePath))
            return string.Empty;

        return await File.ReadAllTextAsync(FilePath, System.Text.Encoding.UTF8, ct)
                         .ConfigureAwait(false);
    }

    public async Task WriteAsync(string newText, CancellationToken ct = default)
    {
        string? dir = System.IO.Path.GetDirectoryName(FilePath);
        if (dir is not null)
            Directory.CreateDirectory(dir);

        await File.WriteAllTextAsync(FilePath, newText, System.Text.Encoding.UTF8, ct)
                  .ConfigureAwait(false);
    }
}
