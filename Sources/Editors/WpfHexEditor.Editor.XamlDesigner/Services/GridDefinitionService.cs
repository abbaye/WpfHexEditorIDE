// ==========================================================
// Project: WpfHexEditor.Editor.XamlDesigner
// File: Services/GridDefinitionService.cs
// Author: Derek Tremblay
// Created: 2026-03-18
// Description:
//     Stateless service with two responsibilities:
//       1. Read column/row definitions from a live rendered WPF Grid element
//          (ActualWidth/ActualHeight available post-layout) and return a
//          GridInfo snapshot with pixel geometry attached.
//       2. Apply structural mutations (resize, add, remove) to a raw XAML
//          string via XLinq, returning the updated XAML text.
//
// Architecture Notes:
//     Pure service — no state, no UI dependencies.
//     Element lookup uses pre-order UID traversal matching DesignToXamlSyncService.
//     Preserves all existing XAML whitespace and attribute order.
// ==========================================================

using System.Globalization;
using System.Text;
using System.Windows.Controls;
using System.Xml;
using System.Xml.Linq;
using WpfHexEditor.Editor.XamlDesigner.Models;

namespace WpfHexEditor.Editor.XamlDesigner.Services;

/// <summary>
/// Reads Grid layout geometry and applies column/row definition mutations to XAML text.
/// </summary>
public sealed class GridDefinitionService
{
    // ── Grid info snapshot ─────────────────────────────────────────────────────

    /// <summary>Snapshot of all column and row definitions with rendered pixel geometry.</summary>
    public sealed record GridInfo(
        IReadOnlyList<GridDefinitionModel> Columns,
        IReadOnlyList<GridDefinitionModel> Rows,
        double TotalWidth,
        double TotalHeight);

    /// <summary>
    /// Captures column and row definitions from <paramref name="grid"/> after layout.
    /// Reads <c>ColumnDefinition.ActualWidth</c> and <c>RowDefinition.ActualHeight</c>
    /// which are only valid on the UI thread after the first measure/arrange pass.
    /// </summary>
    public GridInfo GetGridInfo(Grid grid)
    {
        var cols = new List<GridDefinitionModel>();
        double colOffset = 0;
        for (int i = 0; i < grid.ColumnDefinitions.Count; i++)
        {
            var cd  = grid.ColumnDefinitions[i];
            var raw = GetColumnRaw(cd);
            cols.Add(GridDefinitionModel.Parse(i, true, raw, cd.ActualWidth, colOffset));
            colOffset += cd.ActualWidth;
        }

        var rows = new List<GridDefinitionModel>();
        double rowOffset = 0;
        for (int i = 0; i < grid.RowDefinitions.Count; i++)
        {
            var rd  = grid.RowDefinitions[i];
            var raw = GetRowRaw(rd);
            rows.Add(GridDefinitionModel.Parse(i, false, raw, rd.ActualHeight, rowOffset));
            rowOffset += rd.ActualHeight;
        }

        return new GridInfo(cols, rows, grid.ActualWidth, grid.ActualHeight);
    }

    private static string GetColumnRaw(ColumnDefinition cd)
    {
        var w = cd.Width;
        if (w.IsAuto)  return "Auto";
        if (w.IsStar)  return w.Value == 1.0 ? "*" : $"{w.Value.ToString(CultureInfo.InvariantCulture)}*";
        return w.Value.ToString(CultureInfo.InvariantCulture);
    }

    private static string GetRowRaw(RowDefinition rd)
    {
        var h = rd.Height;
        if (h.IsAuto)  return "Auto";
        if (h.IsStar)  return h.Value == 1.0 ? "*" : $"{h.Value.ToString(CultureInfo.InvariantCulture)}*";
        return h.Value.ToString(CultureInfo.InvariantCulture);
    }

    // ── XAML mutations ────────────────────────────────────────────────────────

    /// <summary>
    /// Changes the Width (column) or Height (row) attribute of the definition at
    /// <paramref name="index"/> inside the Grid identified by <paramref name="gridUid"/>.
    /// </summary>
    public string ResizeDefinition(
        string xaml, int gridUid, bool isColumn, int index, string newValue)
    {
        if (string.IsNullOrWhiteSpace(xaml)) return xaml;
        try
        {
            var doc  = XDocument.Parse(xaml, LoadOptions.PreserveWhitespace);
            var grid = FindByUid(doc.Root!, gridUid);
            if (grid is null) return xaml;

            var (containerSuffix, defName, attrName) = Descriptors(isColumn);
            var container = FindContainer(grid, containerSuffix);
            if (container is null) return xaml;

            var defs = container.Elements()
                .Where(e => e.Name.LocalName == defName)
                .ToList();
            if (index < 0 || index >= defs.Count) return xaml;

            defs[index].SetAttributeValue(attrName, newValue);
            return Serialize(doc);
        }
        catch { return xaml; }
    }

    /// <summary>
    /// Inserts a new ColumnDefinition (Width) or RowDefinition (Height) after
    /// <paramref name="insertAfterIndex"/> (use -1 to prepend, Count-1 to append).
    /// Creates &lt;Grid.ColumnDefinitions&gt; / &lt;Grid.RowDefinitions&gt; if absent.
    /// </summary>
    public string AddDefinition(
        string xaml, int gridUid, bool isColumn, int insertAfterIndex, string value = "*")
    {
        if (string.IsNullOrWhiteSpace(xaml)) return xaml;
        try
        {
            var doc  = XDocument.Parse(xaml, LoadOptions.PreserveWhitespace);
            var grid = FindByUid(doc.Root!, gridUid);
            if (grid is null) return xaml;

            var (containerSuffix, defName, attrName) = Descriptors(isColumn);
            var ns        = grid.Name.Namespace;
            var container = FindContainer(grid, containerSuffix);

            if (container is null)
            {
                container = new XElement(ns + $"{grid.Name.LocalName}.{containerSuffix}");
                grid.AddFirst(container);
            }

            var defs   = container.Elements()
                .Where(e => e.Name.LocalName == defName)
                .ToList();
            var newDef = new XElement(ns + defName);
            newDef.SetAttributeValue(attrName, value);

            if (insertAfterIndex < 0 || defs.Count == 0)
                container.AddFirst(newDef);
            else if (insertAfterIndex >= defs.Count - 1)
                container.Add(newDef);
            else
                defs[insertAfterIndex].AddAfterSelf(newDef);

            // Shift Grid.Row (or Grid.Column) on children that come after the insertion point
            // so the existing layout is preserved when a new definition is prepended or inserted.
            ShiftChildrenAttribute(grid, isColumn, insertAfterIndex);

            return Serialize(doc);
        }
        catch { return xaml; }
    }

    /// <summary>
    /// Increments the <c>Grid.Row</c> (or <c>Grid.Column</c>) attached-property attribute
    /// on every direct child whose current value is greater than
    /// <paramref name="insertAfterIndex"/>.
    /// Children that have no explicit attribute are treated as index 0 — they are
    /// shifted too when a definition is prepended (<paramref name="insertAfterIndex"/> == -1).
    /// </summary>
    private static void ShiftChildrenAttribute(XElement gridEl, bool isColumn, int insertAfterIndex)
    {
        var attachedProp = isColumn ? "Grid.Column" : "Grid.Row";

        foreach (var child in gridEl.Elements())
        {
            // Skip XAML property elements (e.g. <Grid.RowDefinitions>).
            if (child.Name.LocalName.Contains('.')) continue;

            var attr    = child.Attribute(attachedProp);
            int current = attr is not null && int.TryParse(attr.Value, out int v) ? v : 0;

            // Increment when current index is strictly after the insertion point.
            // Covers the prepend case (insertAfterIndex == -1) for all children.
            if (current > insertAfterIndex)
                child.SetAttributeValue(attachedProp, current + 1);
        }
    }

    /// <summary>
    /// Removes the ColumnDefinition or RowDefinition at <paramref name="index"/>.
    /// Removes the container element as well when it becomes empty.
    /// </summary>
    public string RemoveDefinition(string xaml, int gridUid, bool isColumn, int index)
    {
        if (string.IsNullOrWhiteSpace(xaml)) return xaml;
        try
        {
            var doc  = XDocument.Parse(xaml, LoadOptions.PreserveWhitespace);
            var grid = FindByUid(doc.Root!, gridUid);
            if (grid is null) return xaml;

            var (containerSuffix, defName, _) = Descriptors(isColumn);
            var container = FindContainer(grid, containerSuffix);
            if (container is null) return xaml;

            var defs = container.Elements()
                .Where(e => e.Name.LocalName == defName)
                .ToList();
            if (index < 0 || index >= defs.Count) return xaml;

            defs[index].Remove();
            if (!container.HasElements)
                container.Remove();

            return Serialize(doc);
        }
        catch { return xaml; }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static (string ContainerSuffix, string DefName, string AttrName)
        Descriptors(bool isColumn)
        => isColumn
            ? ("ColumnDefinitions", "ColumnDefinition", "Width")
            : ("RowDefinitions",    "RowDefinition",    "Height");

    private static XElement? FindContainer(XElement grid, string containerSuffix)
        => grid.Elements()
               .FirstOrDefault(e => e.Name.LocalName.EndsWith('.' + containerSuffix,
                                                               StringComparison.Ordinal));

    // Pre-order element lookup — matches DesignToXamlSyncService.FindElementByUid.
    private static XElement? FindByUid(XElement root, int uid)
    {
        int counter = 0;
        return FindCore(root, uid, ref counter);
    }

    private static XElement? FindCore(XElement el, int uid, ref int counter)
    {
        // Property elements (<Grid.RowDefinitions>, <Button.Style>…) are NOT counted
        // by InjectUids, so we skip them here too to keep UID numbering in sync.
        bool isPropertyElement = el.Name.LocalName.Contains('.');
        if (!isPropertyElement)
        {
            if (counter == uid) return el;
            counter++;
        }
        foreach (var child in el.Elements())
        {
            var found = FindCore(child, uid, ref counter);
            if (found is not null) return found;
        }
        return null;
    }

    private static string Serialize(XDocument doc)
    {
        var sb = new StringBuilder();
        using var writer = XmlWriter.Create(sb, new XmlWriterSettings
        {
            OmitXmlDeclaration = true,
            Indent             = false,
            NewLineHandling    = NewLineHandling.None
        });
        doc.WriteTo(writer);
        writer.Flush();
        return sb.ToString();
    }
}
