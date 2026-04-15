//////////////////////////////////////////////
// Project      : WpfHexEditor.App
// File         : ViewMenuClassifier.cs
// Description  : Auto-classification engine that assigns functional categories
//                to View menu entries based on explicit metadata, Group mapping,
//                and keyword heuristics.
// Architecture : Pure static helper — no state, no WPF dependencies.
//////////////////////////////////////////////

using WpfHexEditor.Core.Options;

namespace WpfHexEditor.App.Services.ViewMenu;

/// <summary>
/// Classifies <see cref="ViewMenuEntry"/> items into functional categories.
/// Classification cascade: user override → SDK Category → Group mapping → keyword heuristics → "Other".
/// </summary>
public static class ViewMenuClassifier
{
    // ── Well-known category names ────────────────────────────────────────────

    public const string CoreIDE       = "Core IDE";
    public const string EditorsCode   = "Editors & Code";
    public const string Analysis      = "Analysis";
    public const string DataFiles     = "Data & Files";
    public const string Debugging     = "Debugging";
    public const string Design        = "Design";
    public const string XamlTools     = "XAML Tools";
    public const string Testing       = "Testing";
    public const string CustomParser  = "Custom Parser";
    public const string Other         = "Other";

    // ── Category icon glyphs (Segoe MDL2 Assets) ────────────────────────────

    private static readonly Dictionary<string, string> _categoryIcons = new(StringComparer.OrdinalIgnoreCase)
    {
        [CoreIDE]      = "\uE737",  // DeveloperTools
        [EditorsCode]  = "\uE8A5",  // Page
        [Analysis]     = "\uE9D9",  // Processing
        [DataFiles]    = "\uE8B7",  // BrowsePhotos (folder-like)
        [Debugging]    = "\uEBE8",  // Bug
        [Design]       = "\uE8D0",  // Design
        [XamlTools]    = "\uE943",  // Code
        [Testing]      = "\uE768",  // Diagnostic
        [CustomParser] = "\uE756",  // Script
        [Other]        = "\uE712",  // More
    };

    /// <summary>Returns the icon glyph for a category name, or null if unknown.</summary>
    public static string? GetCategoryIcon(string categoryName)
        => _categoryIcons.TryGetValue(categoryName, out var icon) ? icon : null;

    /// <summary>Returns all well-known category names in display order.</summary>
    public static IReadOnlyList<string> GetOrderedCategories() => _orderedCategories;

    private static readonly List<string> _orderedCategories =
    [
        CoreIDE, EditorsCode, Analysis, DataFiles, Debugging,
        Design, XamlTools, Testing, CustomParser, Other
    ];

    // ── Group → Category mapping ────────────────────────────────────────────

    private static readonly Dictionary<string, string> _groupToCategoryMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Panels"]           = CoreIDE,       // generic "Panels" group → default to Core IDE
        ["Analysis"]         = Analysis,
        ["Statistics"]       = Analysis,
        ["Diagnostics"]      = Analysis,
        ["FileTools"]        = DataFiles,
        ["Testing"]          = Testing,
        ["Grammar"]          = EditorsCode,
        ["AssemblyExplorer"] = EditorsCode,
        ["Format Editor"]    = EditorsCode,
        ["Tools"]            = CustomParser,
    };

    // ── Keyword → Category rules (checked against stripped header) ──────────

    private static readonly (string[] Keywords, string Category)[] _keywordRules =
    [
        (["Diagram", "Relationship", "Animation", "Design Data", "Design History", "Binding Inspector", "Live Visual Tree"], Design),
        (["XAML"], XamlTools),
        (["Compare", "Diff", "Archive", "Inspector", "Bookmarks"], DataFiles),
        (["Entropy", "Pattern", "Parsed", "Format", "Structure", "Overlay", "File Statistics"], Analysis),
        (["Test"], Testing),
        (["Terminal", "Output", "Error List", "Solution Explorer", "Properties", "Resource Browser"], CoreIDE),
        (["Outline", "Grammar", "Assembly", "Class Outline", "Class Properties"], EditorsCode),
        (["Breakpoint", "Call Stack", "Locals", "Watch"], Debugging),
        (["Script Runner", "Custom Parser"], CustomParser),
    ];

    /// <summary>
    /// Classifies a single entry into a functional category.
    /// </summary>
    /// <param name="entry">The entry to classify.</param>
    /// <param name="settings">Current View menu settings (for user overrides).</param>
    /// <returns>Category name (never null).</returns>
    public static string Classify(ViewMenuEntry entry, ViewMenuSettings settings)
    {
        // 1. User override (highest priority)
        if (settings.CategoryOverrides.TryGetValue(entry.Id, out var userOverride)
            && !string.IsNullOrWhiteSpace(userOverride))
            return userOverride;

        // 2. Explicit SDK Category
        if (!string.IsNullOrWhiteSpace(entry.Category))
            return entry.Category;

        // 3. Group → Category mapping
        if (!string.IsNullOrWhiteSpace(entry.Group)
            && _groupToCategoryMap.TryGetValue(entry.Group, out var groupCat))
        {
            // For the generic "Panels" group, refine using keyword matching
            if (!string.Equals(entry.Group, "Panels", StringComparison.OrdinalIgnoreCase))
                return groupCat;
            // Fall through to keyword matching for "Panels" group
        }

        // 4. Keyword heuristics on the display header
        var header = StripAccessKey(entry.Header);
        foreach (var (keywords, category) in _keywordRules)
        {
            foreach (var kw in keywords)
            {
                if (header.Contains(kw, StringComparison.OrdinalIgnoreCase))
                    return category;
            }
        }

        // 5. Fallback
        return Other;
    }

    /// <summary>Strips the WPF access key prefix (leading underscore) from a header.</summary>
    private static string StripAccessKey(string header)
        => header.StartsWith('_') ? header[1..] : header;
}
