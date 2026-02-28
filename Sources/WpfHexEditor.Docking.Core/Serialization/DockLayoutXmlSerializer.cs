//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.6
//////////////////////////////////////////////

using System.Globalization;
using System.Xml.Linq;
using WpfHexEditor.Docking.Core.Nodes;

namespace WpfHexEditor.Docking.Core.Serialization;

/// <summary>
/// Serializes and deserializes <see cref="DockLayoutRoot"/> to/from XML
/// using <see cref="System.Xml.Linq"/> (no external dependencies).
/// </summary>
public static class DockLayoutXmlSerializer
{
    private const int CurrentVersion = 1;

    public static string Serialize(DockLayoutRoot layout)
    {
        var root = new XElement("DockLayout",
            new XAttribute("Version", CurrentVersion),
            new XElement("RootNode", NodeToXml(layout.RootNode)),
            new XElement("FloatingItems", layout.FloatingItems.Select(ItemToXml)),
            new XElement("AutoHideItems", layout.AutoHideItems.Select(ItemToXml)));

        // Window state
        if (layout.WindowState is not null)
        {
            var ws = new XElement("WindowState",
                new XAttribute("State", layout.WindowState.Value));
            if (layout.WindowLeft is not null) ws.Add(new XAttribute("Left", layout.WindowLeft.Value.ToString(CultureInfo.InvariantCulture)));
            if (layout.WindowTop is not null) ws.Add(new XAttribute("Top", layout.WindowTop.Value.ToString(CultureInfo.InvariantCulture)));
            if (layout.WindowWidth is not null) ws.Add(new XAttribute("Width", layout.WindowWidth.Value.ToString(CultureInfo.InvariantCulture)));
            if (layout.WindowHeight is not null) ws.Add(new XAttribute("Height", layout.WindowHeight.Value.ToString(CultureInfo.InvariantCulture)));
            root.Add(ws);
        }

        return root.ToString(SaveOptions.None);
    }

    public static DockLayoutRoot Deserialize(string xml)
    {
        var root = XElement.Parse(xml);
        var version = (int?)root.Attribute("Version") ?? 1;

        if (version > CurrentVersion)
            throw new NotSupportedException(
                $"Layout XML version {version} is not supported. Maximum: {CurrentVersion}.");

        var layout = new DockLayoutRoot();

        var rootNodeElement = root.Element("RootNode")?.Elements().FirstOrDefault();
        if (rootNodeElement is not null)
            layout.RootNode = NodeFromXml(rootNodeElement, layout.MainDocumentHost);

        foreach (var itemEl in root.Element("FloatingItems")?.Elements("Item") ?? [])
        {
            var item = ItemFromXml(itemEl);
            item.State = DockItemState.Float;
            layout.FloatingItems.Add(item);
        }

        foreach (var itemEl in root.Element("AutoHideItems")?.Elements("Item") ?? [])
        {
            var item = ItemFromXml(itemEl);
            item.State = DockItemState.AutoHide;
            layout.AutoHideItems.Add(item);
        }

        // Window state
        var wsEl = root.Element("WindowState");
        if (wsEl is not null)
        {
            layout.WindowState = (int?)wsEl.Attribute("State");
            layout.WindowLeft = ParseNullableDouble(wsEl, "Left");
            layout.WindowTop = ParseNullableDouble(wsEl, "Top");
            layout.WindowWidth = ParseNullableDouble(wsEl, "Width");
            layout.WindowHeight = ParseNullableDouble(wsEl, "Height");
        }

        return layout;
    }

    private static XElement NodeToXml(DockNode node) => node switch
    {
        DocumentHostNode docHost => new XElement("DocumentHost",
            new XAttribute("Id", docHost.Id),
            new XAttribute("IsMain", docHost.IsMain),
            new XAttribute("LockMode", docHost.LockMode),
            AttrIfNotNull("ActiveItemContentId", docHost.ActiveItem?.ContentId),
            docHost.Items.Select(ItemToXml)),

        DockGroupNode group => new XElement("Group",
            new XAttribute("Id", group.Id),
            new XAttribute("LockMode", group.LockMode),
            AttrIfNotNull("ActiveItemContentId", group.ActiveItem?.ContentId),
            group.Items.Select(ItemToXml)),

        DockSplitNode split => new XElement("Split",
            new XAttribute("Id", split.Id),
            new XAttribute("Orientation", split.Orientation),
            new XAttribute("LockMode", split.LockMode),
            new XAttribute("Ratios", string.Join(",", split.Ratios.Select(r => r.ToString(CultureInfo.InvariantCulture)))),
            split.PixelSizes.Any(s => s.HasValue)
                ? new XAttribute("PixelSizes", string.Join(",", split.PixelSizes.Select(s => s.HasValue ? s.Value.ToString(CultureInfo.InvariantCulture) : "*")))
                : null,
            split.Children.Select(NodeToXml)),

        _ => throw new NotSupportedException($"Unknown node type: {node.GetType()}")
    };

    private static XElement ItemToXml(DockItem item)
    {
        var el = new XElement("Item",
            new XAttribute("Title", item.Title),
            new XAttribute("ContentId", item.ContentId),
            new XAttribute("CanClose", item.CanClose),
            new XAttribute("CanFloat", item.CanFloat),
            new XAttribute("State", item.State),
            new XAttribute("LastDockSide", item.LastDockSide),
            AttrIfNotNull("FloatLeft", item.FloatLeft?.ToString(CultureInfo.InvariantCulture)),
            AttrIfNotNull("FloatTop", item.FloatTop?.ToString(CultureInfo.InvariantCulture)),
            AttrIfNotNull("FloatWidth", item.FloatWidth?.ToString(CultureInfo.InvariantCulture)),
            AttrIfNotNull("FloatHeight", item.FloatHeight?.ToString(CultureInfo.InvariantCulture)));

        if (item.Metadata.Count > 0)
        {
            var metaEl = new XElement("Metadata");
            foreach (var (key, value) in item.Metadata)
                metaEl.Add(new XElement("Entry", new XAttribute("Key", key), new XAttribute("Value", value)));
            el.Add(metaEl);
        }

        return el;
    }

    private static XAttribute? AttrIfNotNull(string name, string? value) =>
        value is not null ? new XAttribute(name, value) : null;

    private static DockNode NodeFromXml(XElement el, DocumentHostNode mainHost)
    {
        return el.Name.LocalName switch
        {
            "DocumentHost" => ParseDocumentHost(el, mainHost),
            "Group" => ParseGroup(el),
            "Split" => ParseSplit(el, mainHost),
            _ => throw new NotSupportedException($"Unknown XML element: {el.Name}")
        };
    }

    private static DocumentHostNode ParseDocumentHost(XElement el, DocumentHostNode mainHost)
    {
        var isMain = (bool?)el.Attribute("IsMain") ?? false;
        var host = isMain ? mainHost : new DocumentHostNode { IsMain = false };
        host.LockMode = ParseEnum<DockLockMode>(el, "LockMode");
        foreach (var itemEl in el.Elements("Item"))
            host.AddItem(ItemFromXml(itemEl));
        var activeId = (string?)el.Attribute("ActiveItemContentId");
        if (activeId is not null)
            host.ActiveItem = host.Items.FirstOrDefault(i => i.ContentId == activeId);
        return host;
    }

    private static DockGroupNode ParseGroup(XElement el)
    {
        var group = new DockGroupNode { LockMode = ParseEnum<DockLockMode>(el, "LockMode") };
        foreach (var itemEl in el.Elements("Item"))
            group.AddItem(ItemFromXml(itemEl));
        var activeId = (string?)el.Attribute("ActiveItemContentId");
        if (activeId is not null)
            group.ActiveItem = group.Items.FirstOrDefault(i => i.ContentId == activeId);
        return group;
    }

    private static DockSplitNode ParseSplit(XElement el, DocumentHostNode mainHost)
    {
        var split = new DockSplitNode
        {
            Orientation = ParseEnum<SplitOrientation>(el, "Orientation"),
            LockMode = ParseEnum<DockLockMode>(el, "LockMode")
        };
        var ratios = ((string?)el.Attribute("Ratios") ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => double.Parse(s, CultureInfo.InvariantCulture))
            .ToList();
        var children = el.Elements().Where(e => e.Name.LocalName != "Item").ToList();
        for (var i = 0; i < children.Count; i++)
        {
            var child = NodeFromXml(children[i], mainHost);
            var ratio = i < ratios.Count ? ratios[i] : 0.5;
            split.AddChild(child, ratio);
        }

        // Restore pixel sizes
        var pixelSizesStr = (string?)el.Attribute("PixelSizes");
        if (pixelSizesStr is not null)
        {
            var parts = pixelSizesStr.Split(',');
            if (parts.Length == split.Children.Count)
            {
                var pixelSizes = parts.Select(s =>
                    s == "*" ? (double?)null : double.Parse(s, CultureInfo.InvariantCulture)).ToArray();
                split.SetPixelSizes(pixelSizes);
            }
        }

        return split;
    }

    private static DockItem ItemFromXml(XElement el)
    {
        var item = new DockItem
        {
            Title = (string)el.Attribute("Title")!,
            ContentId = (string)el.Attribute("ContentId")!,
            CanClose = (bool?)el.Attribute("CanClose") ?? true,
            CanFloat = (bool?)el.Attribute("CanFloat") ?? true,
            State = ParseEnum<DockItemState>(el, "State"),
            LastDockSide = ParseEnum<DockSide>(el, "LastDockSide"),
            FloatLeft = ParseNullableDouble(el, "FloatLeft"),
            FloatTop = ParseNullableDouble(el, "FloatTop"),
            FloatWidth = ParseNullableDouble(el, "FloatWidth"),
            FloatHeight = ParseNullableDouble(el, "FloatHeight")
        };

        var metaEl = el.Element("Metadata");
        if (metaEl is not null)
        {
            foreach (var entry in metaEl.Elements("Entry"))
            {
                var key = (string?)entry.Attribute("Key");
                var value = (string?)entry.Attribute("Value");
                if (key is not null && value is not null)
                    item.Metadata[key] = value;
            }
        }

        return item;
    }

    private static T ParseEnum<T>(XElement el, string attr) where T : struct, Enum =>
        Enum.TryParse<T>((string?)el.Attribute(attr), out var v) ? v : default;

    private static double? ParseNullableDouble(XElement el, string attr)
    {
        var s = (string?)el.Attribute(attr);
        return s is not null && double.TryParse(s, CultureInfo.InvariantCulture, out var v) ? v : null;
    }
}
