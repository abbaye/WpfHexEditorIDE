// GNU Affero General Public License v3.0 - 2026
// Contributors: Claude Sonnet 4.6

namespace WpfHexEditor.Options;

/// <summary>
/// Represents a category header in the options TreeView.
/// Categories can now be selected to show an overview page.
/// </summary>
/// <param name="Name">Category name (e.g., "Environment", "Plugins").</param>
/// <param name="Icon">Optional emoji/icon for the category.</param>
public sealed record CategoryDescriptor(
    string Name,
    string Icon = "📁");
