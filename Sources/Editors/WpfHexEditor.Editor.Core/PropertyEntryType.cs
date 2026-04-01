//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

namespace WpfHexEditor.Editor.Core;

/// <summary>
/// Controls which editor widget the Properties panel uses to display and
/// optionally edit a <see cref="PropertyEntry"/>.
/// </summary>
public enum PropertyEntryType
{
    /// <summary>
    /// Read-only or editable single-line text.
    /// </summary>
    Text,

    /// <summary>
    /// Integer value; the panel uses a validated TextBox.
    /// </summary>
    Integer,

    /// <summary>
    /// Hexadecimal value (e.g. 0x000000FF); displayed with 0x prefix.
    /// </summary>
    Hex,

    /// <summary>
    /// Boolean value; the panel uses a CheckBox.
    /// </summary>
    Boolean,

    /// <summary>
    /// One of a fixed set of values; the panel uses a ComboBox fed by <see cref="PropertyEntry.AllowedValues"/>.
    /// </summary>
    Enum,

    /// <summary>
    /// ARGB colour; the panel uses the WpfHexEditor.ColorPicker control.
    /// </summary>
    Color,

    /// <summary>
    /// Filesystem path; the panel shows a TextBox + browse button.
    /// </summary>
    FilePath,

    /// <summary>
    /// WPF <see cref="System.Windows.Thickness"/> (Left, Top, Right, Bottom);
    /// the panel renders four numeric TextBox fields labelled L / T / R / B.
    /// </summary>
    Thickness,

    /// <summary>
    /// WPF <see cref="System.Windows.Media.Brush"/> value.
    /// Routes to the color picker for <see cref="System.Windows.Media.SolidColorBrush"/>
    /// and falls back to a text editor for gradient / image brushes.
    /// </summary>
    Brush,
}
