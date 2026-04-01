//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.Collections.Generic;

namespace WpfHexEditor.Editor.Core;

/// <summary>
/// Metadata for a document editor (displayed in "Open with…" menus, etc.).
/// </summary>
public interface IEditorDescriptor
{
    /// <summary>
    /// Unique identifier: "tbl-editor", "json-editor", etc.
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Display name shown in the host UI: "TBL Character Table Editor".
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// Short description shown in tooltip or selection panel.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Supported extensions (e.g.: ".tbl", ".tblx"). Case-sensitive: lowercase.
    /// </summary>
    IReadOnlyList<string> SupportedExtensions { get; }
}
