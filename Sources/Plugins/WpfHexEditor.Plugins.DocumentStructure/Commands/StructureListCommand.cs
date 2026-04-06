// ==========================================================
// Project: WpfHexEditor.Plugins.DocumentStructure
// File: Commands/StructureListCommand.cs
// Created: 2026-04-05
// Description:
//     Terminal command: structure-list [--flat] [--filter <text>]
//     Outputs the current document structure tree as indented text.
// ==========================================================

using System.Text;
using WpfHexEditor.Plugins.DocumentStructure.ViewModels;
using WpfHexEditor.SDK.Contracts.Terminal;

namespace WpfHexEditor.Plugins.DocumentStructure.Commands;

public sealed class StructureListCommand : PluginTerminalCommandBase
{
    private readonly DocumentStructureViewModel _vm;

    public override string CommandName  => "structure-list";
    public override string Description  => "List the current document structure tree";
    public override string Usage        => "structure-list [--flat] [--filter <text>]";
    public override string? Source      => "Document Structure";

    public StructureListCommand(DocumentStructureViewModel vm) => _vm = vm;

    protected override Task<int> ExecuteCoreAsync(
        string[] args,
        ITerminalOutput output,
        ITerminalContext context,
        CancellationToken ct)
    {
        var flat = args.Any(a => a == "--flat");
        string? filter = null;
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == "--filter") { filter = args[i + 1]; break; }
        }

        var nodes = flat ? _vm.FlatNodes : (IReadOnlyList<StructureNodeVm>)_vm.RootNodes;
        if (nodes.Count == 0)
        {
            output.WriteWarning("No structure available for the current document.");
            return Task.FromResult(0);
        }

        if (flat)
        {
            foreach (var node in nodes)
            {
                if (filter is not null && !node.Name.Contains(filter, StringComparison.OrdinalIgnoreCase))
                    continue;
                output.WriteLine(FormatNode(node, node.IndentLevel));
            }
        }
        else
        {
            PrintTree(output, _vm.RootNodes, 0, filter);
        }

        return Task.FromResult(0);
    }

    private static void PrintTree(ITerminalOutput output, IReadOnlyList<StructureNodeVm> nodes, int depth, string? filter)
    {
        foreach (var node in nodes)
        {
            if (filter is not null &&
                !node.Name.Contains(filter, StringComparison.OrdinalIgnoreCase) &&
                !HasMatchingDescendant(node, filter))
                continue;

            output.WriteLine(FormatNode(node, depth));
            PrintTree(output, node.Children, depth + 1, filter);
        }
    }

    private static string FormatNode(StructureNodeVm node, int depth)
    {
        var sb = new StringBuilder();
        sb.Append(new string(' ', depth * 2));
        sb.Append($"[{node.Kind}] {node.Name}");
        if (!string.IsNullOrEmpty(node.Detail))
            sb.Append($" — {node.Detail}");
        if (node.StartLine > 0)
            sb.Append($"  (line {node.StartLine})");
        else if (node.ByteOffset >= 0)
            sb.Append($"  (offset 0x{node.ByteOffset:X})");
        return sb.ToString();
    }

    private static bool HasMatchingDescendant(StructureNodeVm node, string filter)
    {
        foreach (var child in node.Children)
        {
            if (child.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)) return true;
            if (HasMatchingDescendant(child, filter)) return true;
        }
        return false;
    }
}
