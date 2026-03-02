//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

namespace WpfHexEditor.Editor.Core;

/// <summary>
/// Contract for the Properties panel (F4).
/// The host drives it by calling <see cref="SetProvider"/> whenever the
/// active document changes or the Solution Explorer selection changes.
/// </summary>
public interface IPropertiesPanel
{
    /// <summary>
    /// Replaces the active provider. Pass <see langword="null"/> to show
    /// the "No selection" placeholder.
    /// </summary>
    void SetProvider(IPropertyProvider? provider);
}
