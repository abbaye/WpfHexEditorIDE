// ==========================================================
// Project: WpfHexEditor.App
// File: Controls/Welcome/WelcomeTemplateItem.cs
// Description: View model record for a quick-start template card in WelcomePanel.
// ==========================================================

namespace WpfHexEditor.App.Controls.Welcome;

internal sealed record WelcomeTemplateItem(
    string Title,
    string Description,
    string IconGlyph,
    string Category,
    Action OnCreate);
