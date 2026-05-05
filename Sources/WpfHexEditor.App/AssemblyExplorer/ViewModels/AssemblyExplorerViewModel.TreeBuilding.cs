// ==========================================================
// Project: WpfHexEditor.App.AssemblyExplorer
// File: ViewModels/AssemblyExplorerViewModel.TreeBuilding.cs
// Description:
//     Tree construction (namespace groups, types, members), filter, rebuild.
// ==========================================================

using WpfHexEditor.Core.AssemblyAnalysis.Models;

namespace WpfHexEditor.App.AssemblyExplorer.ViewModels;

public sealed partial class AssemblyExplorerViewModel
{
    // ── Tree construction ─────────────────────────────────────────────────────

    private void BuildTreeChildren(AssemblyRootNodeViewModel root, AssemblyModel model)
    {
        root.Children.Clear();

        if (model.IsManaged)
        {
            AddNamespaceGroups(root, model);

            if (model.TypeForwarders.Count > 0)
                AddTypeForwardersGroup(root, model);

            if (_showReferences && model.References.Count > 0)
                AddReferencesGroup(root, model);

            if (_showResources && model.Resources.Count > 0)
                AddResourcesGroup(root, model);

            if (_showMetadata)
                AddMetadataGroup(root, model);
        }
        else
        {
            AddSectionsGroup(root, model);
        }
    }

    private void AddNamespaceGroups(AssemblyRootNodeViewModel root, AssemblyModel model)
    {
        var byNs = model.Types
            .GroupBy(t => t.Namespace)
            .OrderBy(g => string.IsNullOrEmpty(g.Key) ? string.Empty : g.Key,
                     StringComparer.OrdinalIgnoreCase);

        foreach (var group in byNs)
        {
            var capturedTypes    = group.ToList();
            var sortAlphabetical = _sortAlphabetical;

            if (capturedTypes.Count > 50)
            {
                var nsNode = new NamespaceNodeViewModel(group.Key, async () =>
                {
                    var ordered = sortAlphabetical
                        ? capturedTypes.OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
                        : (IEnumerable<TypeModel>)capturedTypes.OrderBy(t => t.PeOffset);
                    await Task.Yield();
                    return ordered.Select(BuildTypeNode).ToList<AssemblyNodeViewModel>();
                });
                root.Children.Add(nsNode);
            }
            else
            {
                var nsNode = new NamespaceNodeViewModel(group.Key);
                var types  = sortAlphabetical
                    ? capturedTypes.OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
                    : (IEnumerable<TypeModel>)capturedTypes.OrderBy(t => t.PeOffset);

                foreach (var type in types)
                    nsNode.Children.Add(BuildTypeNode(type));

                root.Children.Add(nsNode);
            }
        }
    }

    private static TypeNodeViewModel BuildTypeNode(TypeModel type)
    {
        var memberCount = type.Methods.Count + type.Fields.Count
                        + type.Properties.Count + type.Events.Count;

        if (memberCount > 30)
            return new TypeNodeViewModel(type, () => BuildMemberGroups(type));

        var typeNode = new TypeNodeViewModel(type);
        foreach (var group in BuildMemberGroups(type))
            typeNode.Children.Add(group);
        return typeNode;
    }

    private static IReadOnlyList<AssemblyNodeViewModel> BuildMemberGroups(TypeModel type)
    {
        var groups = new List<AssemblyNodeViewModel>(5);

        var hasBase       = !string.IsNullOrEmpty(type.BaseTypeName) && type.BaseTypeName != "System.Object";
        var hasInterfaces = type.InterfaceNames.Count > 0;
        if (hasBase || hasInterfaces)
        {
            var inheritsGroup = new NamespaceNodeViewModel("Inherits From");
            if (hasBase)
                inheritsGroup.Children.Add(new MetadataTableNodeViewModel($"\u21B3 {type.BaseTypeName}", 0));
            foreach (var iface in type.InterfaceNames)
                inheritsGroup.Children.Add(new MetadataTableNodeViewModel($"\u21AA {iface}", 0));
            groups.Add(inheritsGroup);
        }

        if (type.Methods.Count > 0)
        {
            var methodsGroup = new NamespaceNodeViewModel("Methods");
            foreach (var m in type.Methods)
                methodsGroup.Children.Add(new MethodNodeViewModel(m));
            groups.Add(methodsGroup);
        }

        if (type.Fields.Count > 0)
        {
            var fieldsGroup = new NamespaceNodeViewModel("Fields");
            foreach (var f in type.Fields)
                fieldsGroup.Children.Add(new FieldNodeViewModel(f));
            groups.Add(fieldsGroup);
        }

        if (type.Properties.Count > 0)
        {
            var propsGroup = new NamespaceNodeViewModel("Properties");
            foreach (var p in type.Properties)
                propsGroup.Children.Add(new PropertyNodeViewModel(p));
            groups.Add(propsGroup);
        }

        if (type.Events.Count > 0)
        {
            var eventsGroup = new NamespaceNodeViewModel("Events");
            foreach (var e in type.Events)
                eventsGroup.Children.Add(new EventNodeViewModel(e));
            groups.Add(eventsGroup);
        }

        return groups;
    }

    private static void AddTypeForwardersGroup(AssemblyRootNodeViewModel root, AssemblyModel model)
    {
        var fwdNode = new NamespaceNodeViewModel($"Type Forwarders ({model.TypeForwarders.Count})");
        foreach (var fwd in model.TypeForwarders.OrderBy(f => f.FullName, StringComparer.OrdinalIgnoreCase))
            fwdNode.Children.Add(new MetadataTableNodeViewModel(fwd.FullName, 0));
        root.Children.Add(fwdNode);
    }

    private static void AddReferencesGroup(AssemblyRootNodeViewModel root, AssemblyModel model)
    {
        var refsNode = new NamespaceNodeViewModel("References");
        foreach (var r in model.References.OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase))
            refsNode.Children.Add(new ReferenceNodeViewModel(r));
        root.Children.Add(refsNode);
    }

    private static void AddResourcesGroup(AssemblyRootNodeViewModel root, AssemblyModel model)
    {
        var resNode = new NamespaceNodeViewModel("Resources");
        foreach (var r in model.Resources)
            resNode.Children.Add(new ResourceNodeViewModel(r));
        root.Children.Add(resNode);
    }

    private static void AddMetadataGroup(AssemblyRootNodeViewModel root, AssemblyModel model)
    {
        var metaNode = new NamespaceNodeViewModel("Metadata Tables");
        metaNode.Children.Add(new MetadataTableNodeViewModel("TypeDef",     model.Types.Count));
        metaNode.Children.Add(new MetadataTableNodeViewModel("MethodDef",   model.Types.Sum(t => t.Methods.Count)));
        metaNode.Children.Add(new MetadataTableNodeViewModel("FieldDef",    model.Types.Sum(t => t.Fields.Count)));
        metaNode.Children.Add(new MetadataTableNodeViewModel("AssemblyRef", model.References.Count));
        root.Children.Add(metaNode);
    }

    private static void AddSectionsGroup(AssemblyRootNodeViewModel root, AssemblyModel model)
    {
        var secNode = new NamespaceNodeViewModel("PE Sections");
        foreach (var s in model.Sections)
            secNode.Children.Add(new MetadataTableNodeViewModel(s.Name, 0, s.RawOffset));
        root.Children.Add(secNode);
    }

    /// <summary>
    /// Recursively sets OwnerFilePath on every descendant node so hex editor
    /// integration can resolve the file without traversing the workspace dictionary.
    /// </summary>
    private static void PropagateOwnerFilePath(AssemblyNodeViewModel node, string filePath)
    {
        node.OwnerFilePath = filePath;
        foreach (var child in node.Children)
            PropagateOwnerFilePath(child, filePath);
    }

    // ── Filter ────────────────────────────────────────────────────────────────

    private void ApplyFilter(string text)
    {
        foreach (var root in RootNodes)
            SetNodeVisibility(root, text);
    }

    /// <summary>
    /// Deep recursive filter: a node is visible when empty filter, its own name
    /// matches, or any descendant matches.
    /// </summary>
    private static bool SetNodeVisibility(AssemblyNodeViewModel node, string text)
    {
        var empty      = string.IsNullOrEmpty(text);
        var selfMatch  = empty || node.DisplayName.Contains(text, StringComparison.OrdinalIgnoreCase);
        var childMatch = false;

        foreach (var child in node.Children)
            childMatch |= SetNodeVisibility(child, text);

        node.IsMatch   = !empty && selfMatch;
        node.IsVisible = empty || selfMatch || childMatch;

        if (!empty && childMatch)
            node.IsExpanded = true;

        return node.IsVisible;
    }

    // ── Rebuild ───────────────────────────────────────────────────────────────

    private void RebuildAllTrees()
    {
        foreach (var entry in _workspace.Values)
        {
            BuildTreeChildren(entry.Root, entry.Model);
            PropagateOwnerFilePath(entry.Root, entry.Model.FilePath);
        }
    }
}
