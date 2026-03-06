//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;

namespace WpfHexEditor.Editor.Core;

/// <summary>
/// Reference implementation of <see cref="IEditorRegistry"/>.
/// Thread-safe for reads; registration is expected at startup (single-thread).
/// </summary>
public sealed class EditorRegistry : IEditorRegistry
{
    private readonly List<IEditorFactory> _factories = new();

    /// <inheritdoc />
    public void Register(IEditorFactory factory)
    {
        if (factory == null) throw new ArgumentNullException(nameof(factory));
        _factories.Add(factory);
    }

    /// <inheritdoc />
    public IEditorFactory? FindFactory(string filePath)
    {
        if (filePath == null) throw new ArgumentNullException(nameof(filePath));
        return _factories.FirstOrDefault(f => f.CanOpen(filePath));
    }

    /// <inheritdoc />
    public IEditorFactory? FindFactory(string filePath, string? preferredId)
    {
        if (filePath == null) throw new ArgumentNullException(nameof(filePath));

        if (!string.IsNullOrEmpty(preferredId))
        {
            var preferred = _factories.FirstOrDefault(
                f => f.Descriptor.Id == preferredId && f.CanOpen(filePath));
            if (preferred is not null) return preferred;
        }

        return FindFactory(filePath);
    }

    /// <inheritdoc />
    public IReadOnlyList<IEditorFactory> GetAll() => _factories.AsReadOnly();
}
