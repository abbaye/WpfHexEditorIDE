//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////
// Project: WpfHexEditor.Editor.StructureEditor
// File: ViewModels/BlocksViewModel.cs
// Description: VM for the Blocks tab — tree management, selection, add/remove/reorder.
//////////////////////////////////////////////

using System.Collections.ObjectModel;
using System.Windows.Input;
using WpfHexEditor.Core.FormatDetection;
using WpfHexEditor.Core.ViewModels;

namespace WpfHexEditor.Editor.StructureEditor.ViewModels;

internal sealed class BlocksViewModel : ViewModelBase
{
    internal event EventHandler? Changed;

    private BlockViewModel? _selectedBlock;

    public ObservableCollection<BlockViewModel> BlockTree { get; } = [];

    public BlockViewModel? SelectedBlock
    {
        get => _selectedBlock;
        set => SetField(ref _selectedBlock, value);
    }

    // ── Commands ──────────────────────────────────────────────────────────────

    public ICommand RemoveCommand    => new RelayCommand(RemoveSelected,    () => SelectedBlock is not null);
    public ICommand MoveUpCommand    => new RelayCommand(MoveUp,            () => CanMoveUp());
    public ICommand MoveDownCommand  => new RelayCommand(MoveDown,          () => CanMoveDown());
    public ICommand DuplicateCommand => new RelayCommand(DuplicateSelected, () => SelectedBlock is not null);

    // ── Load / Save ───────────────────────────────────────────────────────────

    internal void LoadFrom(List<BlockDefinition>? blocks)
    {
        BlockTree.Clear();
        foreach (var b in blocks ?? [])
        {
            var vm = CreateViewModel(b);
            BlockTree.Add(vm);
        }
    }

    internal List<BlockDefinition> BuildBlocks() =>
        [..BlockTree.Select(vm => vm.Build())];

    // ── Add (called from AddBlockDialog result) ───────────────────────────────

    internal void AddBlock(string blockType, string name)
    {
        var b = new BlockDefinition
        {
            Type  = blockType,
            Name  = name,
            Color = "#4ECDC4",
        };
        var vm = CreateViewModel(b);
        BlockTree.Add(vm);
        SelectedBlock = vm;
        RaiseChanged();
    }

    // ── Toolbar operations ────────────────────────────────────────────────────

    private void RemoveSelected()
    {
        if (SelectedBlock is null) return;
        BlockTree.Remove(SelectedBlock);
        SelectedBlock = BlockTree.Count > 0 ? BlockTree[0] : null;
        RaiseChanged();
    }

    private void MoveUp()
    {
        if (SelectedBlock is null) return;
        var idx = BlockTree.IndexOf(SelectedBlock);
        if (idx <= 0) return;
        BlockTree.Move(idx, idx - 1);
        RaiseChanged();
    }

    private void MoveDown()
    {
        if (SelectedBlock is null) return;
        var idx = BlockTree.IndexOf(SelectedBlock);
        if (idx < 0 || idx >= BlockTree.Count - 1) return;
        BlockTree.Move(idx, idx + 1);
        RaiseChanged();
    }

    private void DuplicateSelected()
    {
        if (SelectedBlock is null) return;
        var json = SelectedBlock.ToRawJson();
        var dup  = new BlockViewModel();
        dup.LoadFromRawJson(json);
        dup.Name = dup.Name + " (copy)";
        var idx = BlockTree.IndexOf(SelectedBlock);
        WireViewModel(dup);
        BlockTree.Insert(idx + 1, dup);
        SelectedBlock = dup;
        RaiseChanged();
    }

    private bool CanMoveUp()
    {
        if (SelectedBlock is null) return false;
        return BlockTree.IndexOf(SelectedBlock) > 0;
    }

    private bool CanMoveDown()
    {
        if (SelectedBlock is null) return false;
        var idx = BlockTree.IndexOf(SelectedBlock);
        return idx >= 0 && idx < BlockTree.Count - 1;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private BlockViewModel CreateViewModel(BlockDefinition b)
    {
        var vm = new BlockViewModel();
        vm.LoadFrom(b);
        WireViewModel(vm);
        return vm;
    }

    private void WireViewModel(BlockViewModel vm) =>
        vm.Changed += (_, _) => RaiseChanged();

    private void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);
}
