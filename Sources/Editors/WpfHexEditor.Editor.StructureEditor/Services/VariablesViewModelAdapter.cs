//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////
// Project: WpfHexEditor.Editor.StructureEditor
// File: Services/VariablesViewModelAdapter.cs
// Description: Bridges VariablesViewModel + BlocksViewModel to IVariableSource.
//              Collects all known variable names: user-defined variables,
//              StoreAs / IndexVar / ActionVariable from block tree (recursive).
// Architecture Notes:
//     Called synchronously on UI thread; all data is in-memory, no I/O.
//////////////////////////////////////////////

using WpfHexEditor.Editor.StructureEditor.ViewModels;

namespace WpfHexEditor.Editor.StructureEditor.Services;

internal sealed class VariablesViewModelAdapter : IVariableSource
{
    private readonly VariablesViewModel _variables;
    private readonly BlocksViewModel?   _blocks;

    internal VariablesViewModelAdapter(VariablesViewModel variables, BlocksViewModel? blocks = null)
    {
        _variables = variables;
        _blocks    = blocks;
    }

    public IReadOnlyList<string> GetVariableNames()
    {
        var names = new HashSet<string>(StringComparer.Ordinal);

        // 1. User-defined variables from VariablesTab
        foreach (var item in _variables.Items)
            if (!string.IsNullOrWhiteSpace(item.Key))
                names.Add(item.Key);

        // 2. Variables derived from block definitions (StoreAs, IndexVar, ActionVariable)
        if (_blocks is not null)
            CollectBlockVars(_blocks.BlockTree, names);

        return [.. names.OrderBy(n => n, StringComparer.OrdinalIgnoreCase)];
    }

    private static void CollectBlockVars(
        IEnumerable<BlockViewModel> blocks,
        HashSet<string> names)
    {
        foreach (var b in blocks)
        {
            AddIfNotEmpty(b.StoreAs,        names);
            AddIfNotEmpty(b.IndexVar,       names);
            AddIfNotEmpty(b.ActionVariable, names);
            AddIfNotEmpty(b.TargetVar,      names);
            AddIfNotEmpty(b.MappedValueStoreAs, names);

            if (b.Children.Count > 0)
                CollectBlockVars(b.Children, names);
        }
    }

    private static void AddIfNotEmpty(string value, HashSet<string> names)
    {
        if (!string.IsNullOrWhiteSpace(value))
            names.Add(value.Trim());
    }
}
