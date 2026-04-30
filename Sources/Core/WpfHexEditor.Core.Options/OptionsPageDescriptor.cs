// GNU Affero General Public License v3.0 - 2026
// Contributors: Claude Sonnet 4.6

using System;
using System.Windows.Controls;

namespace WpfHexEditor.Core.Options;

/// <summary>
/// Metadata that registers one options page in <see cref="OptionsPageRegistry"/>.
/// <para>
/// <see cref="Category"/> and <see cref="PageName"/> are resolved lazily via
/// <see cref="Func{String}"/> delegates so that they always reflect the active
/// UI culture — the tree labels update correctly when the user changes language.
/// </para>
/// Adding a new page requires only a single descriptor entry in the registry.
/// </summary>
public sealed class OptionsPageDescriptor
{
    private readonly Func<string> _categoryFn;
    private readonly Func<string> _pageNameFn;

    /// <summary>
    /// Top-level tree node label — resolved live from the resource manager each access.
    /// </summary>
    public string Category => _categoryFn();

    /// <summary>
    /// Child-level tree node label — resolved live from the resource manager each access.
    /// </summary>
    public string PageName => _pageNameFn();

    /// <summary>Creates the page <see cref="UserControl"/> lazily on first navigation.</summary>
    public Func<UserControl> Factory { get; }

    /// <summary>Optional emoji/icon for the category (e.g. "🌍", "🔌"). If null, uses default icon.</summary>
    public string? CategoryIcon { get; }

    /// <summary>Extra keywords the search bar matches against (e.g. ["font", "color"]).</summary>
    public string[]? SearchKeywords { get; }

    /// <summary>
    /// Initializes a descriptor whose labels are resolved dynamically.
    /// </summary>
    /// <param name="categoryFn">
    ///   Lambda that returns the current (localized) category name, e.g.
    ///   <c>() =&gt; OptionsPageStrings.CategoryEnvironment</c>.
    /// </param>
    /// <param name="pageNameFn">
    ///   Lambda that returns the current (localized) page name, e.g.
    ///   <c>() =&gt; OptionsPageStrings.PageGeneral</c>.
    /// </param>
    public OptionsPageDescriptor(
        Func<string> categoryFn,
        Func<string> pageNameFn,
        Func<UserControl> factory,
        string? categoryIcon = null,
        string[]? searchKeywords = null)
    {
        _categoryFn    = categoryFn    ?? throw new ArgumentNullException(nameof(categoryFn));
        _pageNameFn    = pageNameFn    ?? throw new ArgumentNullException(nameof(pageNameFn));
        Factory        = factory       ?? throw new ArgumentNullException(nameof(factory));
        CategoryIcon   = categoryIcon;
        SearchKeywords = searchKeywords;
    }

    /// <summary>
    /// Convenience constructor for cases where the label is a fixed string
    /// (e.g. dynamically registered plugin pages that supply their own translated string).
    /// </summary>
    public OptionsPageDescriptor(
        string category,
        string pageName,
        Func<UserControl> factory,
        string? categoryIcon = null,
        string[]? searchKeywords = null)
        : this(() => category, () => pageName, factory, categoryIcon, searchKeywords)
    { }
}
