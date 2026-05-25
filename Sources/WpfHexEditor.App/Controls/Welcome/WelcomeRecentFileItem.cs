// ==========================================================
// Project: WpfHexEditor.App
// File: Controls/Welcome/WelcomeRecentFileItem.cs
// Description: View model record for a recent file entry in WelcomePanel.
// ==========================================================

namespace WpfHexEditor.App.Controls.Welcome;

internal sealed record WelcomeRecentFileItem(
    string Path,
    string FileName,
    string Directory,
    DateTime LastAccessed,
    long FileSizeBytes,
    bool IsPinned,
    bool IsSolution,
    string IconGlyph);
