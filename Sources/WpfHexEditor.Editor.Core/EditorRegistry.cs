//////////////////////////////////////////////
// Apache 2.0  - 2026
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

namespace WpfHexEditor.Editor.Core;

/// <summary>
/// Implémentation de référence de <see cref="IEditorRegistry"/>.
/// Thread-safe en lecture ; l'enregistrement est prévu au démarrage (single-thread).
/// </summary>
public sealed class EditorRegistry : IEditorRegistry
{
    private readonly List<IEditorFactory> _factories = [];

    /// <inheritdoc />
    public void Register(IEditorFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        _factories.Add(factory);
    }

    /// <inheritdoc />
    public IEditorFactory? FindFactory(string filePath)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        return _factories.FirstOrDefault(f => f.CanOpen(filePath));
    }

    /// <inheritdoc />
    public IReadOnlyList<IEditorFactory> GetAll() => _factories.AsReadOnly();
}
