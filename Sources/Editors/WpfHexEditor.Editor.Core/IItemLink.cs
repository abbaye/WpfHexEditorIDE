//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

namespace WpfHexEditor.Editor.Core;

/// <summary>
/// A typed link from one project item to another (e.g. a binary referencing its TBL table).
/// </summary>
public interface IItemLink
{
    /// <summary>
    /// The <see cref="IProjectItem.Id"/> of the target item.
    /// </summary>
    string ItemId { get; }

    /// <summary>
    /// Free-form role descriptor.
    /// Well-known values: <c>"Tbl"</c>, <c>"TblAlternate"</c>, <c>"Patch"</c>,
    /// <c>"FormatDefinition"</c>, <c>"Reference"</c>.
    /// </summary>
    string Role { get; }
}
