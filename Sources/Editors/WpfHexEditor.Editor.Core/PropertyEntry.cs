//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
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

    /// <summary>
    /// Optional validation delegate invoked by the panel before calling
    /// <see cref="OnValueChanged"/>.  Return <see langword="null"/> when the
    /// value is valid; return a human-readable error message string otherwise.
    /// When this is <see langword="null"/> no validation is performed.
    /// </summary>
    public Func<object?, string?>? Validator { get; set; }

    /// <summary>
    /// When <see langword="true"/> the property holds its default / unset value.
    /// The panel renders the property name in normal weight and hides the reset button.
    /// </summary>
    public bool IsDefault { get; set; } = true;

    /// <summary>
    /// Called when the user clicks the "Reset to default" icon next to this property.
    /// Passing <see langword="null"/> to the underlying patch callback removes the
    /// XML attribute, restoring the WPF default.
    /// When this is <see langword="null"/> the reset button is hidden.
    /// </summary>
    public Action? OnResetToDefault { get; set; }
}
