// ==========================================================
// Project: WpfHexEditor.App
// File: Controls/Welcome/WelcomeNewsItem.cs
// Description: View model record for a news feed entry in WelcomePanel.
// ==========================================================

namespace WpfHexEditor.App.Controls.Welcome;

internal sealed record WelcomeNewsItem(
    string Title,
    string Summary,
    string Category,
    DateTime Date,
    string? Url);
