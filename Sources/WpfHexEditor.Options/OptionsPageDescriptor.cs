// GNU Affero General Public License v3.0 - 2026
// Contributors: Claude Sonnet 4.6

using System;
using System.Windows.Controls;

namespace WpfHexEditor.Options;

/// <summary>
/// Metadata that registers one options page in <see cref="OptionsPageRegistry"/>.
/// Adding a new page requires only a single descriptor entry in the registry.
/// </summary>
/// <param name="Category">Top-level tree node label (e.g. "Environment", "Plugins").</param>
/// <param name="PageName">Child-level node label (e.g. "General", "Display").</param>
/// <param name="Factory">Creates the page UserControl lazily on first navigation.</param>
/// <param name="CategoryIcon">Optional emoji/icon for the category (e.g. "🌍", "🔌"). If null, uses default icon.</param>
public sealed record OptionsPageDescriptor(
    string Category,
    string PageName,
    Func<UserControl> Factory,
    string? CategoryIcon = null);
