// ==========================================================
// Project: WpfHexEditor.Editor.XamlDesigner
// File: XamlElementMapper.cs
// Author: Derek Tremblay
// Created: 2026-03-17
// Description:
//     Maps rendered UIElements to their source XElements by reading
//     the Tag="xd_N" UID attribute injected by DesignToXamlSyncService
//     during the render-preparation phase.
//
// Architecture Notes:
//     Utility class — pure static helpers + instance snapshot.
//     Called by DesignCanvas after each successful XamlReader.Parse.
//     Builds two maps:
//       1. uid (int) → XElement from the raw (uninjected) document
//       2. UIElement → int uid  (via FrameworkElement.Tag read-back)
// ==========================================================

using System.Windows;
using System.Windows.Media;
using System.Xml.Linq;

namespace WpfHexEditor.Editor.XamlDesigner.Models;

/// <summary>
/// Holds the post-render mapping between rendered <see cref="UIElement"/> instances
/// and the originating <see cref="XElement"/> nodes in the XAML document.
/// </summary>
public sealed class XamlElementMapper
{
    private readonly Dictionary<int, XElement>   _uidToXml = new();
    private readonly Dictionary<UIElement, int>  _elToUid  = new();

    // ── Population ────────────────────────────────────────────────────────────

    /// <summary>
    /// Initialises the mapper from a UID→XElement map (produced by
    /// <see cref="Services.DesignToXamlSyncService.InjectUids"/>) and the
    /// rendered visual root returned by <c>XamlReader.Parse()</c>.
    /// </summary>
    public void Build(Dictionary<int, XElement> uidToXml, UIElement renderedRoot)
    {
        _uidToXml.Clear();
        _elToUid.Clear();

        foreach (var (uid, xe) in uidToXml)
            _uidToXml[uid] = xe;

        WalkVisualTree(renderedRoot);
    }

    // ── Query ─────────────────────────────────────────────────────────────────

    /// <summary>Returns the XElement for <paramref name="element"/>, or null if unmapped.</summary>
    public XElement? GetXElement(UIElement element)
        => _elToUid.TryGetValue(element, out int uid) && _uidToXml.TryGetValue(uid, out var xe)
            ? xe
            : null;

    /// <summary>Returns the UID for <paramref name="element"/>, or -1 if unmapped.</summary>
    public int GetUid(UIElement element)
        => _elToUid.TryGetValue(element, out int uid) ? uid : -1;

    // ── Private ───────────────────────────────────────────────────────────────

    private void WalkVisualTree(UIElement root)
    {
        if (root is FrameworkElement fe
            && fe.Tag is string tag
            && tag.StartsWith("xd_", StringComparison.Ordinal)
            && int.TryParse(tag.AsSpan(3), out int uid))
        {
            _elToUid[root] = uid;
        }

        int childCount = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < childCount; i++)
        {
            if (VisualTreeHelper.GetChild(root, i) is UIElement child)
                WalkVisualTree(child);
        }
    }
}
