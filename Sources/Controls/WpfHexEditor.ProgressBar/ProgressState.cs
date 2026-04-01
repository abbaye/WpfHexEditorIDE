// ==========================================================
// Project: WpfHexEditor.ProgressBar
// File: ProgressState.cs
// Description:
//     Visual state enum for all progress bar controls.
//     Determines which brush is used for the active fill.
// ==========================================================

namespace WpfHexEditor.ProgressBar;

/// <summary>
/// Visual state of a progress bar control.
/// Each state maps to a dedicated brush DP on <see cref="Controls.ProgressBarBase"/>.
/// </summary>
public enum ProgressState
{
    /// <summary>Default blue accent fill.</summary>
    Normal,

    /// <summary>Yellow/amber fill — operation paused.</summary>
    Paused,

    /// <summary>Red fill — operation failed.</summary>
    Error,

    /// <summary>Green fill — operation completed successfully.</summary>
    Success
}
