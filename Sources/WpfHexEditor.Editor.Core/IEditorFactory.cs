//////////////////////////////////////////////
// Apache 2.0  - 2026
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

namespace WpfHexEditor.Editor.Core;

/// <summary>
/// Factory permettant d'enregistrer un éditeur dans le <see cref="IEditorRegistry"/>
/// pour une intégration plug-in avec le système de docking.
///
/// <para>Usage optionnel : un éditeur peut parfaitement être instancié directement
/// sans passer par cette interface (<c>new TblEditorControl()</c>). La factory n'est
/// nécessaire que si l'éditeur doit être découvrable via le registre (ouverture
/// automatique selon l'extension, menu "Ouvrir avec…", etc.).</para>
///
/// <para>CONTRAT : les instances retournées par <see cref="Create"/> doivent également
/// être des <c>System.Windows.FrameworkElement</c> pour être embarquables dans le
/// système de docking WPF. Le host caste après création.</para>
/// </summary>
public interface IEditorFactory
{
    /// <summary>Métadonnées de l'éditeur (id, nom, extensions).</summary>
    IEditorDescriptor Descriptor { get; }

    /// <summary>
    /// Retourne <c>true</c> si cet éditeur peut ouvrir <paramref name="filePath"/>.
    /// Basé sur l'extension et/ou une inspection rapide du fichier.
    /// </summary>
    bool CanOpen(string filePath);

    /// <summary>
    /// Crée une nouvelle instance vierge de l'éditeur.
    /// Appeler <see cref="IDocumentEditor"/> members sur le résultat pour charger un fichier.
    /// </summary>
    IDocumentEditor Create();
}
