//////////////////////////////////////////////
// Apache 2.0  - 2026
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

namespace WpfHexEditor.Editor.Core;

/// <summary>
/// Registre centralisé des éditeurs de documents disponibles dans l'application.
///
/// Usage typique (host au démarrage) :
/// <code>
/// registry.Register(new TblEditorFactory());
/// registry.Register(new JsonEditorFactory());
/// // …
/// // Lors de l'ouverture d'un fichier :
/// var factory = registry.FindFactory(filePath) ?? registry.GetFallback();
/// var editor  = factory.Create();
/// dockingHost.OpenDocument((FrameworkElement)editor, editor.TitleChanged);
/// </code>
/// </summary>
public interface IEditorRegistry
{
    /// <summary>Enregistre un éditeur disponible.</summary>
    void Register(IEditorFactory factory);

    /// <summary>
    /// Retourne la factory la plus appropriée pour <paramref name="filePath"/>,
    /// ou <c>null</c> si aucun éditeur ne supporte ce fichier.
    /// </summary>
    IEditorFactory? FindFactory(string filePath);

    /// <summary>Retourne toutes les factories enregistrées.</summary>
    IReadOnlyList<IEditorFactory> GetAll();
}
