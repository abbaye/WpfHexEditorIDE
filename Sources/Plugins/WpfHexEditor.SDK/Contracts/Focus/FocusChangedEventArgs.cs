//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

namespace WpfHexEditor.SDK.Contracts.Focus;

/// <summary>
/// Event arguments raised when the active document or panel changes in the IDE.
/// </summary>
public sealed class FocusChangedEventArgs : EventArgs
{
    /// <summary>Gets the previously active document (null if none was active).</summary>
    public IDocument? PreviousDocument { get; init; }

    /// <summary>Gets the currently active document (null if no document is active).</summary>
    public IDocument? ActiveDocument { get; init; }

    /// <summary>Gets the currently active panel (null if no panel is focused).</summary>
    public IPanel? ActivePanel { get; init; }

    /// <summary>Gets the previously active panel (null if none was focused).</summary>
    public IPanel? PreviousPanel { get; init; }
}
