// ==========================================================
// Project: WpfHexEditor.Plugins.Debugger
// File: ViewModels/MemoryWindowViewModel.cs
// Description:
//     VM for the Memory window panel — shows debuggee memory in hex + ASCII like VS2022.
//     Reads memory via DAP readMemory; 16 bytes per row.
// Architecture: ReadMemoryAsync returns raw bytes; VM formats rows as hex + printable ASCII.
// ==========================================================

using System.Collections.ObjectModel;
using System.Text;
using System.Windows.Input;
using WpfHexEditor.Core.ViewModels;
using WpfHexEditor.SDK.Commands;
using WpfHexEditor.SDK.Contracts.Services;

namespace WpfHexEditor.Plugins.Debugger.ViewModels;

public sealed class MemoryRow
{
    public string Address { get; init; } = string.Empty;
    public string Hex     { get; init; } = string.Empty;
    public string Ascii   { get; init; } = string.Empty;
}

public sealed class MemoryWindowViewModel : ViewModelBase
{
    private readonly IDebuggerService _debugger;
    private string _addressInput = string.Empty;
    private string _currentRef   = string.Empty;
    private const int BytesPerRow  = 16;
    private const int DefaultRows  = 32;

    public ObservableCollection<MemoryRow> Rows { get; } = [];

    public string AddressInput
    {
        get => _addressInput;
        set { _addressInput = value; OnPropertyChanged(); }
    }

    public ICommand GoCommand      { get; }
    public ICommand NextPageCommand { get; }
    public ICommand PrevPageCommand { get; }

    public MemoryWindowViewModel(IDebuggerService debugger)
    {
        _debugger       = debugger;
        GoCommand       = new RelayCommand(_ => _ = GoToAddressAsync());
        NextPageCommand = new RelayCommand(_ => _ = PageAsync(+DefaultRows * BytesPerRow));
        PrevPageCommand = new RelayCommand(_ => _ = PageAsync(-DefaultRows * BytesPerRow));
    }

    public async Task RefreshAsync(string? memRef = null)
    {
        if (memRef is not null) { _currentRef = memRef; AddressInput = memRef; }
        if (string.IsNullOrEmpty(_currentRef)) return;
        await LoadAsync(_currentRef, DefaultRows * BytesPerRow);
    }

    public void Clear()
        => System.Windows.Application.Current?.Dispatcher.Invoke(Rows.Clear);

    private async Task GoToAddressAsync()
    {
        if (string.IsNullOrWhiteSpace(AddressInput)) return;
        _currentRef = AddressInput.Trim();
        await LoadAsync(_currentRef, DefaultRows * BytesPerRow);
    }

    private async Task PageAsync(int bytesDelta)
    {
        if (string.IsNullOrEmpty(_currentRef)) return;
        // Adjust address by delta — requires parsing the hex address
        if (TryParseAddress(_currentRef, out long addr))
        {
            addr += bytesDelta;
            _currentRef = $"0x{addr:X}";
            AddressInput = _currentRef;
        }
        await LoadAsync(_currentRef, DefaultRows * BytesPerRow);
    }

    private async Task LoadAsync(string memRef, int byteCount)
    {
        var bytes = await _debugger.ReadMemoryAsync(memRef, byteCount);
        if (bytes is null) return;

        if (!TryParseAddress(memRef, out long baseAddr))
            baseAddr = 0;

        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            Rows.Clear();
            for (int i = 0; i < bytes.Length; i += BytesPerRow)
            {
                int len = Math.Min(BytesPerRow, bytes.Length - i);
                var slice = bytes.AsSpan(i, len);

                var hex   = new StringBuilder();
                var ascii = new StringBuilder();
                for (int b = 0; b < len; b++)
                {
                    hex.Append($"{slice[b]:X2} ");
                    ascii.Append(slice[b] >= 0x20 && slice[b] < 0x7F ? (char)slice[b] : '.');
                }
                // Pad short rows
                for (int b = len; b < BytesPerRow; b++) hex.Append("   ");

                Rows.Add(new MemoryRow
                {
                    Address = $"0x{baseAddr + i:X8}",
                    Hex     = hex.ToString().TrimEnd(),
                    Ascii   = ascii.ToString(),
                });
            }
        });
    }

    private static bool TryParseAddress(string s, out long addr)
    {
        s = s.Trim();
        if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            return long.TryParse(s[2..], System.Globalization.NumberStyles.HexNumber, null, out addr);
        return long.TryParse(s, out addr);
    }
}
