//////////////////////////////////////////////
// Apache 2.0  - 2026
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

namespace WpfHexEditor.Editor.Core;

/// <summary>
/// Metadata d'un éditeur de document (affiché dans les menus "Ouvrir avec…", etc.).
/// </summary>
public interface IEditorDescriptor
{
    /// <summary>Identifiant unique : "tbl-editor", "json-editor", etc.</summary>
    string Id { get; }

    /// <summary>Nom affiché dans l'UI du host : "TBL Character Table Editor".</summary>
    string DisplayName { get; }

    /// <summary>Description courte affichée en tooltip ou panneau de sélection.</summary>
    string Description { get; }

    /// <summary>Extensions supportées (ex : ".tbl", ".tblx"). Sensible à la casse : minuscules.</summary>
    IReadOnlyList<string> SupportedExtensions { get; }
}
