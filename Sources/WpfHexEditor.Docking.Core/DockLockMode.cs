//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5, Claude Sonnet 4.6
//////////////////////////////////////////////

namespace WpfHexEditor.Docking.Core;

/// <summary>
/// Flags controlling which layout operations are locked.
/// </summary>
[Flags]
public enum DockLockMode
{
    None = 0,
    PreventSplitting = 1,
    PreventUndocking = 2,
    PreventClosing = 4,
    Full = PreventSplitting | PreventUndocking | PreventClosing
}
