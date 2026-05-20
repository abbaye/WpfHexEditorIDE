//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.Collections.ObjectModel;
using System.Text;
using System.Windows.Input;
using WpfHexEditor.App.BinaryAnalysis.Services;
using WpfHexEditor.Core.ViewModels;
using WpfHexEditor.SDK.Contracts;

namespace WpfHexEditor.App.BinaryAnalysis.ViewModels;

/// <summary>View-model for the XOR/ROT Cipher Decoder panel (#120).</summary>
public sealed class CipherDecoderViewModel : ViewModelBase
{
    private IIDEHostContext? _context;
    private CancellationTokenSource? _cts;

    private string _statusText   = string.Empty;
    private string _previewText  = string.Empty;
    private string _xorKeyHex    = "00";
    private string _rotShift     = "13";
    private int    _modeIndex;       // 0=XOR-single, 1=XOR-rolling, 2=ROT-alpha, 3=ROT-47, 4=Auto-detect
    private bool   _isBusy;
    private long   _rangeStart;
    private long   _rangeLength    = 256;
    private bool   _useSelection;

    // -- Bindable properties --------------------------------------------------

    public string StatusText
    {
        get => _statusText;
        private set { _statusText = value; OnPropertyChanged(); }
    }

    public string PreviewText
    {
        get => _previewText;
        private set { _previewText = value; OnPropertyChanged(); }
    }

    public string XorKeyHex
    {
        get => _xorKeyHex;
        set { _xorKeyHex = value; OnPropertyChanged(); }
    }

    public string RotShift
    {
        get => _rotShift;
        set { _rotShift = value; OnPropertyChanged(); }
    }

    public int ModeIndex
    {
        get => _modeIndex;
        set { _modeIndex = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsXorMode)); OnPropertyChanged(nameof(IsRotMode)); }
    }

    public bool IsXorMode  => _modeIndex is 0 or 1 or 4;
    public bool IsRotMode  => _modeIndex is 2 or 3;

    public bool IsBusy
    {
        get => _isBusy;
        private set { _isBusy = value; OnPropertyChanged(); }
    }

    public long RangeStart
    {
        get => _rangeStart;
        set { _rangeStart = value; OnPropertyChanged(); }
    }

    public long RangeLength
    {
        get => _rangeLength;
        set { _rangeLength = Math.Clamp(value, 1, CipherDecoderService.MaxPreviewBytes); OnPropertyChanged(); }
    }

    public bool UseSelection
    {
        get => _useSelection;
        set { _useSelection = value; OnPropertyChanged(); }
    }

    // Auto-detect results — each item: "0xKK  (score: …) → text…"
    public ObservableCollection<string> AutoResults { get; } = [];

    // -- Commands -------------------------------------------------------------

    public ICommand DecodeCommand  { get; }
    public ICommand CancelCommand  { get; }
    public ICommand CopyCommand    { get; }

    // -- Construction ---------------------------------------------------------

    public CipherDecoderViewModel()
    {
        DecodeCommand = new CipherRelayCommand(async () => await DecodeAsync(), () => !IsBusy);
        CancelCommand = new CipherRelayCommand(() => { _cts?.Cancel(); return Task.CompletedTask; }, () => IsBusy);
        CopyCommand   = new CipherRelayCommand(() =>
        {
            if (!string.IsNullOrEmpty(PreviewText))
                System.Windows.Clipboard.SetText(PreviewText);
            return Task.CompletedTask;
        });
    }

    public void SetContext(IIDEHostContext ctx) => _context = ctx;

    public void OnFileOpened()
    {
        PreviewText  = string.Empty;
        StatusText   = string.Empty;
        AutoResults.Clear();
        RangeStart   = 0;
    }

    // -- Decode ---------------------------------------------------------------

    public async Task DecodeAsync()
    {
        if (_context is null || IsBusy || !_context.HexEditor.IsActive) return;

        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        IsBusy = true;
        PreviewText = string.Empty;
        AutoResults.Clear();
        StatusText  = "Decoding…";

        try
        {
            long selStart  = _context.HexEditor.SelectionStart;
            long selLength = _context.HexEditor.SelectionLength;
            long start  = UseSelection && selStart >= 0 ? selStart : RangeStart;
            long length = UseSelection && selLength > 0
                ? Math.Min(selLength, CipherDecoderService.MaxPreviewBytes)
                : RangeLength;

            if (length <= 0) { StatusText = "Nothing to decode."; return; }

            byte[] raw = await Task.Run(() =>
                _context.HexEditor.ReadBytes(start, (int)length), _cts.Token);

            var token = _cts.Token;
            await Task.Run(() => RunDecode(raw, token), token);

            StatusText = $"Decoded {raw.Length} bytes from 0x{start:X8}.";
        }
        catch (OperationCanceledException) { StatusText = "Cancelled."; }
        catch (Exception ex)               { StatusText = $"Error: {ex.Message}"; }
        finally { IsBusy = false; }
    }

    private void RunDecode(byte[] raw, CancellationToken ct)
    {
        switch (ModeIndex)
        {
            case 0:
            {
                byte key = ParseXorKey();
                SetPreview(CipherDecoderService.XorSingleKey(raw, key));
                break;
            }
            case 1:
            {
                SetPreview(CipherDecoderService.XorRollingKey(raw, ParseRollingKey()));
                break;
            }
            case 2:
            {
                int shift = int.TryParse(_rotShift, out int s) ? s : 13;
                SetPreview(CipherDecoderService.RotAlpha(raw, shift));
                break;
            }
            case 3:
            {
                SetPreview(CipherDecoderService.Rot47(raw));
                break;
            }
            case 4:
            {
                ct.ThrowIfCancellationRequested();
                var ranked = CipherDecoderService.RankXorKeys(raw);
                SetPreview(CipherDecoderService.XorSingleKey(raw, ranked[0].Key));

                var candidates = ranked
                    .Select(r => (r.Key, Decoded: CipherDecoderService.XorSingleKey(raw, r.Key)))
                    .Where(x => CipherDecoderService.LooksLikeText(x.Decoded))
                    .Take(8)
                    .ToList();

                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    AutoResults.Clear();
                    foreach (var (k, d) in candidates)
                    {
                        string preview = Encoding.UTF8.GetString(d, 0, Math.Min(d.Length, 60))
                            .Replace('\n', ' ').Replace('\r', ' ');
                        AutoResults.Add($"0x{k:X2}  →  {preview}");
                    }
                });
                break;
            }
        }
    }

    private void SetPreview(byte[] decoded)
    {
        string text = Encoding.UTF8.GetString(decoded);
        System.Windows.Application.Current.Dispatcher.Invoke(() => PreviewText = text);
    }

    private byte ParseXorKey()
    {
        string hex = _xorKeyHex.Trim().TrimStart('0', 'x', 'X');
        return byte.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out byte b) ? b : (byte)0;
    }

    private byte[] ParseRollingKey()
    {
        string raw = _xorKeyHex.Replace("0x", "").Replace("0X", "").Replace(" ", "");
        if (raw.Length % 2 != 0) raw = "0" + raw;
        var result = new List<byte>();
        for (int i = 0; i < raw.Length; i += 2)
        {
            if (byte.TryParse(raw.AsSpan(i, 2), System.Globalization.NumberStyles.HexNumber, null, out byte b))
                result.Add(b);
        }
        return result.Count > 0 ? [.. result] : [0x00];
    }
}

file sealed class CipherRelayCommand(Func<Task> execute, Func<bool>? canExecute = null) : ICommand
{
    public event EventHandler? CanExecuteChanged;
    public bool CanExecute(object? p) => canExecute?.Invoke() ?? true;
    public async void Execute(object? p) => await execute();
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
