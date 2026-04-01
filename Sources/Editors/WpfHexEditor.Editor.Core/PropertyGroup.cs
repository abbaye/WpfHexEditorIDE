//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

namespace WpfHexEditor.Editor.Core;

/// <summary>
/// A named, collapsible group of <see cref="PropertyEntry"/> rows in the
/// Properties panel (e.g. "Position", "Value", "Document").
/// </summary>
public sealed class PropertyGroup
{
    public string Name { get; set; } = string.Empty;

    public IReadOnlyList<PropertyEntry> Entries { get; set; } = [];
}
