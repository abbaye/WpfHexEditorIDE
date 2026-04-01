// ==========================================================
// Project: WpfHexEditor.HexEditor
// File: EnrichedFormatViewModel.cs (FORWARDING STUB)
// Description:
//     Type alias for backward compatibility. The canonical implementation
//     has moved to WpfHexEditor.Core.ViewModels.EnrichedFormatViewModel
//     so that any editor/plugin can use it without depending on HexEditor.
// ==========================================================

// Re-export the type so existing code using the old namespace keeps compiling.
// New code should use WpfHexEditor.Core.ViewModels.EnrichedFormatViewModel directly.

namespace WpfHexEditor.HexEditor.ViewModels
{
    /// <summary>
    /// Backward-compatible alias. Use <see cref="WpfHexEditor.Core.ViewModels.EnrichedFormatViewModel"/> directly.
    /// </summary>
    [System.Obsolete("Use WpfHexEditor.Core.ViewModels.EnrichedFormatViewModel instead.")]
    public class EnrichedFormatViewModel : WpfHexEditor.Core.ViewModels.EnrichedFormatViewModel
    {
    }
}
