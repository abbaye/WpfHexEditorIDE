// Apache 2.0 - 2026
// Contributors: Claude Sonnet 4.6

using System;
using System.Windows.Controls;

namespace WpfHexEditor.Options;

/// <summary>
/// Metadata that registers one options page in <see cref="OptionsPageRegistry"/>.
/// Adding a new page requires only a single descriptor entry in the registry.
/// </summary>
/// <param name="Category">Top-level tree node label (e.g. "Environnement").</param>
/// <param name="PageName">Child-level node label (e.g. "Général").</param>
/// <param name="Factory">Creates the page UserControl lazily on first navigation.</param>
public sealed record OptionsPageDescriptor(
    string Category,
    string PageName,
    Func<UserControl> Factory);
