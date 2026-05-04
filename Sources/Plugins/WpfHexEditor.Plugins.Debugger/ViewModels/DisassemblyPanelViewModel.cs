// ==========================================================
// Project: WpfHexEditor.Plugins.Debugger
// File: ViewModels/DisassemblyPanelViewModel.cs
// Description:
//     VM for the Disassembly window panel.
//     Loads instructions via DAP disassemble request, highlights the current IP line.
// Architecture: stateless VM — refreshed each time execution pauses.
// ==========================================================

using System.Collections.ObjectModel;
using System.Windows.Input;
using WpfHexEditor.Core.ViewModels;
using WpfHexEditor.SDK.Commands;
using WpfHexEditor.SDK.Contracts.Services;

namespace WpfHexEditor.Plugins.Debugger.ViewModels;

public sealed class DisassemblyLine
{
    public string  Address          { get; init; } = string.Empty;
    public string  InstructionBytes { get; init; } = string.Empty;
    public string  Symbol           { get; init; } = string.Empty;
    public string  Instruction      { get; init; } = string.Empty;
    public string? SourceFile       { get; init; }
    public int     SourceLine       { get; init; }

    /// <summary>True for the line at the current instruction pointer.</summary>
    public bool IsCurrentIP { get; init; }
}

public sealed class DisassemblyPanelViewModel : ViewModelBase
{
    private readonly IDebuggerService _debugger;
    private string _addressInput = string.Empty;
    private string _currentMemRef = string.Empty;
    private const int DefaultInstructionCount = 64;

    public ObservableCollection<DisassemblyLine> Lines { get; } = [];

    public string AddressInput
    {
        get => _addressInput;
        set { _addressInput = value; OnPropertyChanged(); }
    }

    public ICommand GoCommand      { get; }
    public ICommand NextPageCommand { get; }
    public ICommand PrevPageCommand { get; }

    public DisassemblyPanelViewModel(IDebuggerService debugger)
    {
        _debugger       = debugger;
        GoCommand       = new RelayCommand(_ => _ = GoToAddressAsync());
        NextPageCommand = new RelayCommand(_ => _ = PageAsync(+DefaultInstructionCount));
        PrevPageCommand = new RelayCommand(_ => _ = PageAsync(-DefaultInstructionCount));
    }

    /// <summary>Refresh disassembly centered on current IP (call after each pause).</summary>
    public async Task RefreshAtCurrentIPAsync(string memRef)
    {
        _currentMemRef = memRef;
        AddressInput   = memRef;
        await LoadAsync(memRef, 0, DefaultInstructionCount, currentIPRef: memRef);
    }

    public void Clear()
        => System.Windows.Application.Current?.Dispatcher.Invoke(Lines.Clear);

    private async Task GoToAddressAsync()
    {
        if (string.IsNullOrWhiteSpace(AddressInput)) return;
        _currentMemRef = AddressInput.Trim();
        await LoadAsync(_currentMemRef, 0, DefaultInstructionCount, currentIPRef: null);
    }

    private async Task PageAsync(int instructionDelta)
    {
        if (string.IsNullOrEmpty(_currentMemRef)) return;
        await LoadAsync(_currentMemRef, instructionDelta, DefaultInstructionCount, currentIPRef: null);
    }

    private async Task LoadAsync(string memRef, int instrOffset, int count, string? currentIPRef)
    {
        if (!_debugger.IsActive) return;
        var instructions = await _debugger.DisassembleAsync(memRef, count);
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            Lines.Clear();
            foreach (var instr in instructions)
            {
                Lines.Add(new DisassemblyLine
                {
                    Address          = instr.Address,
                    InstructionBytes = instr.InstructionBytes ?? string.Empty,
                    Symbol           = instr.Symbol           ?? string.Empty,
                    Instruction      = instr.Instruction,
                    SourceFile       = instr.SourceFile,
                    SourceLine       = instr.SourceLine,
                    IsCurrentIP      = currentIPRef is not null &&
                                       string.Equals(instr.Address, currentIPRef,
                                           StringComparison.OrdinalIgnoreCase),
                });
            }
        });
    }
}
