//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

namespace WpfHexEditor.Editor.Core;

/// <summary>
/// A single row in the Properties panel (name + value + optional edit).
/// Instances are created fresh each time <see cref="IPropertyProvider.GetProperties"/>
/// is called so they are always in sync with the current editor state.
/// </summary>
public sealed class PropertyEntry
{
    /// <summary>
    /// Label shown in the left column.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Longer description shown in the description area at the bottom of the
    /// Properties panel when this row is focused.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Current value. The panel formats it according to <see cref="Type"/>.
    /// </summary>
    public object? Value { get; set; }

    /// <summary>
    /// When <see langword="false"/> the panel renders an inline editor widget
    /// and calls <see cref="OnValueChanged"/> on commit.
    /// </summary>
    public bool IsReadOnly { get; set; } = true;

    public PropertyEntryType Type { get; set; } = PropertyEntryType.Text;

    /// <summary>
    /// Ordered list of choices for <see cref="PropertyEntryType.Enum"/> entries.
    /// Each element is converted to string via <c>ToString()</c> for display.
    /// </summary>
    public IReadOnlyList<object>? AllowedValues { get; set; }

    /// <summary>
    /// Called by the panel after the user commits a new value.
    /// The argument is the new value (same type as the items in <see cref="AllowedValues"/>
    /// for Enum entries, or <c>string</c> / <c>bool</c> / <c>int</c> for the others).
    /// </summary>
    public Action<object?>? OnValueChanged { get; set; }
}
