// ==========================================================
// Project: WpfHexEditor.Plugins.DocumentStructure
// File: Commands/StructureNavigateCommand.cs
// Created: 2026-04-05
// Description:
//     Terminal command: structure-navigate <name>
//     Finds the first node matching the name and navigates the editor to it.
// ==========================================================

using WpfHexEditor.Plugins.DocumentStructure.ViewModels;
using WpfHexEditor.SDK.Contracts.Terminal;

namespace WpfHexEditor.Plugins.DocumentStructure.Commands;

public sealed class StructureNavigateCommand : PluginTerminalCommandBase
{
    private readonly DocumentStructureViewModel _vm;

    public override string CommandName  => "structure-navigate";
    public override string Description  => "Navigate to a symbol by name in the document structure";
    public override string Usage        => "structure-navigate <name>";
    public override string? Source      => "Document Structure";

    public StructureNavigateCommand(DocumentStructureViewModel vm) => _vm = vm;

    protected override Task<int> ExecuteCoreAsync(
        string[] args,
        ITerminalOutput output,
        ITerminalContext context,
        CancellationToken ct)
    {
        if (!RequireArgs(1, args, output, Usage)) return Task.FromResult(1);

        var name = string.Join(" ", args);
        var node = FindNode(_vm.RootNodes, name) ?? FindNodeFuzzy(_vm.RootNodes, name);

        if (node is null)
        {
            output.WriteWarning($"No symbol found matching '{name}'.");
            return Task.FromResult(1);
        }

        _vm.OnNodeActivated(node);
        var location = node.StartLine > 0 ? $" at line {node.StartLine}"
                     : node.ByteOffset >= 0 ? $" at offset 0x{node.ByteOffset:X}" : "";
        output.WriteLine($"Navigated to [{node.Kind}] {node.Name}{location}");
        return Task.FromResult(0);
    }

    private static StructureNodeVm? FindNode(IReadOnlyList<StructureNodeVm> nodes, string name)
    {
        foreach (var node in nodes)
        {
            if (node.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) return node;
            var found = FindNode(node.Children, name);
            if (found is not null) return found;
        }
        return null;
    }

    private static StructureNodeVm? FindNodeFuzzy(IReadOnlyList<StructureNodeVm> nodes, string text)
    {
        foreach (var node in nodes)
        {
            if (node.Name.Contains(text, StringComparison.OrdinalIgnoreCase)) return node;
            var found = FindNodeFuzzy(node.Children, text);
            if (found is not null) return found;
        }
        return null;
    }
}
