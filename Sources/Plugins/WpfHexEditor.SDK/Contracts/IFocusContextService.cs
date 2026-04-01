//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using WpfHexEditor.SDK.Contracts.Focus;

namespace WpfHexEditor.SDK.Contracts;

/// <summary>
/// Tracks the active document and panel in the IDE.
/// Provides change notifications to plugins without requiring polling.
/// </summary>
public interface IFocusContextService
{
    /// <summary>Gets the currently active document, or null if none is active.</summary>
    IDocument? ActiveDocument { get; }

    /// <summary>Gets the currently active panel, or null if no panel is focused.</summary>
    IPanel? ActivePanel { get; }

    /// <summary>
    /// Raised when the active document or panel changes.
    /// Always raised on the UI (Dispatcher) thread.
    /// </summary>
    event EventHandler<FocusChangedEventArgs> FocusChanged;
}
