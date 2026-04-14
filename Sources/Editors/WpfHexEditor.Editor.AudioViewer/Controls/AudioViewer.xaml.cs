//////////////////////////////////////////////
// Project: WpfHexEditor.Editor.AudioViewer
// File: Controls/AudioViewer.xaml.cs
// Description:
//     Audio waveform viewer — reads WAV/MP3/FLAC/OGG/AIFF headers,
//     builds WaveformPeaks on a background thread, renders via WaveformDrawingCanvas.
//     No audio playback (no NAudio dependency).
//     Fires NavigateToOffsetRequested on click for HexEditor sync.
// Architecture:
//     IDocumentEditor + IOpenableDocument.
//     Header parsing: WAV (RIFF/WAVE fmt+data chunks), MP3 (ID3v2/MPEG frame),
//     FLAC (fLaC STREAMINFO), OGG (OggS), AIFF (FORM/AIFF).
//     Peak computation: max 4096 columns, min/max per channel per column.
//////////////////////////////////////////////

using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using WpfHexEditor.Editor.Core;
using WpfHexEditor.SDK.UI;

namespace WpfHexEditor.Editor.AudioViewer.Controls;

/// <summary>
/// Audio waveform viewer — header-only, no playback.
/// Implements <see cref="IDocumentEditor"/> and <see cref="IOpenableDocument"/>.
/// </summary>
public sealed partial class AudioViewer : UserControl, IDocumentEditor, IOpenableDocument
{
    private string _filePath = string.Empty;
    private CancellationTokenSource? _cts;
    private ToolbarOverflowManager _overflowManager = null!;
    private AudioHeader _header;

    // ── Playback ──────────────────────────────────────────────────────────────
    private MediaPlayer?      _player;
    private DispatcherTimer?  _positionTimer;
    private bool              _isSeeking;
    private bool              _isPlaying;

    private static readonly HashSet<string> _playbackFormats =
        new(StringComparer.OrdinalIgnoreCase) { "WAV", "MP3" };

    public AudioViewer()
    {
        InitializeComponent();

        UndoCommand      = new SDK.Commands.RelayCommand(() => { }, () => false);
        RedoCommand      = new SDK.Commands.RelayCommand(() => { }, () => false);
        SaveCommand      = new SDK.Commands.RelayCommand(() => { }, () => false);
        CopyCommand      = new SDK.Commands.RelayCommand(() => { }, () => false);
        CutCommand       = new SDK.Commands.RelayCommand(() => { }, () => false);
        PasteCommand     = new SDK.Commands.RelayCommand(() => { }, () => false);
        DeleteCommand    = new SDK.Commands.RelayCommand(() => { }, () => false);
        SelectAllCommand = new SDK.Commands.RelayCommand(() => { }, () => false);

        Loaded += (_, _) =>
        {
            _overflowManager = new ToolbarOverflowManager(
                toolbarContainer:      ToolbarBorder,
                alwaysVisiblePanel:    ToolbarRightPanel,
                overflowButton:        ToolbarOverflowButton,
                overflowMenu:          OverflowContextMenu,
                groupsInCollapseOrder: []);
            Dispatcher.InvokeAsync(_overflowManager.CaptureNaturalWidths, DispatcherPriority.Loaded);
        };
    }

    // -- IDocumentEditor — State ------------------------------------------

    public bool   IsDirty    => false;
    public bool   CanUndo    => false;
    public bool   CanRedo    => false;
    public bool   IsReadOnly { get => true; set { } }
    public string Title      { get; private set; } = "";
    public bool   IsBusy     { get; private set; }

    // -- IDocumentEditor — Commands ---------------------------------------

    public ICommand UndoCommand      { get; }
    public ICommand RedoCommand      { get; }
    public ICommand SaveCommand      { get; }
    public ICommand CopyCommand      { get; }
    public ICommand CutCommand       { get; }
    public ICommand PasteCommand     { get; }
    public ICommand DeleteCommand    { get; }
    public ICommand SelectAllCommand { get; }

    // -- IDocumentEditor — Events -----------------------------------------

#pragma warning disable CS0067
    public event EventHandler?         ModifiedChanged;
    public event EventHandler?         CanUndoChanged;
    public event EventHandler?         CanRedoChanged;
    public event EventHandler<string>? TitleChanged;
    public event EventHandler<string>? StatusMessage;
    public event EventHandler<string>? OutputMessage;
    public event EventHandler?         SelectionChanged;
    public event EventHandler<DocumentOperationEventArgs>?          OperationStarted;
    public event EventHandler<DocumentOperationEventArgs>?          OperationProgress;
    public event EventHandler<DocumentOperationCompletedEventArgs>? OperationCompleted;
#pragma warning restore CS0067

    /// <summary>
    /// Raised when the user clicks the waveform.
    /// Argument is the file byte offset — wire to HexEditor.SetPosition().
    /// </summary>
    public event EventHandler<long>? NavigateToOffsetRequested;

    // -- IDocumentEditor — Methods ----------------------------------------

    public void Undo() { }
    public void Redo() { }
    public void Save() { }
    public Task SaveAsync(CancellationToken ct = default) => Task.CompletedTask;
    public Task SaveAsAsync(string filePath, CancellationToken ct = default) => Task.CompletedTask;
    public void Copy()      { }
    public void Cut()       { }
    public void Paste()     { }
    public void Delete()    { }
    public void SelectAll() { }
    public void CancelOperation() => _cts?.Cancel();

    public void Close()
    {
        _cts?.Cancel();
        CleanupPlayer();
        _filePath = string.Empty;
        WaveCanvas.SetPeaks(null);
        FormatInfoLabel.Text = "Audio";
        StatusText.Text      = "Open an audio file";
    }

    // -- IOpenableDocument ------------------------------------------------

    public async Task OpenAsync(string filePath, CancellationToken ct = default)
    {
        _cts?.Cancel();
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        _filePath = filePath;
        Title     = Path.GetFileName(filePath);
        TitleChanged?.Invoke(this, Title);

        SetBusy(true);
        OperationStarted?.Invoke(this, new DocumentOperationEventArgs { Title = "Analyzing…", IsIndeterminate = true });
        StatusMessage?.Invoke(this, $"Reading {Title}…");

        try
        {
            var result = await Task.Run(() => AnalyzeAndBuildPeaks(filePath, _cts.Token), _cts.Token);
            _header = result.header;

            Dispatcher.Invoke(() =>
            {
                WaveCanvas.SetPeaks(result.peaks);
                FormatInfoLabel.Text = BuildFormatLabel(_header);
                StatusText.Text      = BuildStatusLabel(_header, filePath);
                SetBusy(false);
                InitPlayer(filePath, _header);
            });

            StatusMessage?.Invoke(this, $"{_header.FormatName}  ·  {_header.ChannelCount}ch  ·  {_header.SampleRate} Hz");
            OperationCompleted?.Invoke(this, new DocumentOperationCompletedEventArgs { Success = true });
        }
        catch (OperationCanceledException)
        {
            SetBusy(false);
        }
        catch (Exception ex)
        {
            Dispatcher.Invoke(() =>
            {
                StatusText.Text = $"Error: {ex.Message}";
                SetBusy(false);
            });
            StatusMessage?.Invoke(this, $"Error: {ex.Message}");
            OperationCompleted?.Invoke(this, new DocumentOperationCompletedEventArgs { Success = false, ErrorMessage = ex.Message });
        }
    }

    // -- Analysis + Peak Building -----------------------------------------

    private static (AudioHeader header, WaveformPeaks peaks) AnalyzeAndBuildPeaks(
        string filePath, CancellationToken ct)
    {
        using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var header = ParseHeader(fs, filePath);

        ct.ThrowIfCancellationRequested();

        var peaks = BuildPeaks(fs, header, ct);
        return (header, peaks);
    }

    // -- Header Parsing ---------------------------------------------------

    private static AudioHeader ParseHeader(FileStream fs, string filePath)
    {
        var ext = Path.GetExtension(filePath).ToUpperInvariant();
        fs.Seek(0, SeekOrigin.Begin);

        if (fs.Length < 4)
            return AudioHeader.Raw(ext, fs.Length);

        var sig4 = new byte[4];
        fs.Read(sig4, 0, 4);

        // WAV — RIFF....WAVE
        if (sig4[0] == 'R' && sig4[1] == 'I' && sig4[2] == 'F' && sig4[3] == 'F')
            return ParseWav(fs, filePath);

        // FLAC — fLaC
        if (sig4[0] == 'f' && sig4[1] == 'L' && sig4[2] == 'a' && sig4[3] == 'C')
            return ParseFlac(fs, filePath);

        // OGG — OggS
        if (sig4[0] == 'O' && sig4[1] == 'g' && sig4[2] == 'g' && sig4[3] == 'S')
            return new AudioHeader("OGG Vorbis", ext, 2, 44100, 16, TimeSpan.Zero, 4, fs.Length - 4, fs.Length);

        // AIFF — FORM....AIFF
        if (sig4[0] == 'F' && sig4[1] == 'O' && sig4[2] == 'R' && sig4[3] == 'M')
            return ParseAiff(fs, filePath);

        // MP3 — ID3v2 or MPEG frame sync
        if (sig4[0] == 'I' && sig4[1] == 'D' && sig4[2] == '3')
            return ParseMp3Id3(fs, filePath);
        if ((sig4[0] == 0xFF) && ((sig4[1] & 0xE0) == 0xE0))
            return ParseMp3Frame(sig4, fs, filePath);

        // Raw fallback
        return AudioHeader.Raw(ext, fs.Length);
    }

    private static AudioHeader ParseWav(FileStream fs, string filePath)
    {
        fs.Seek(0, SeekOrigin.Begin);
        using var br = new BinaryReader(fs, Encoding.ASCII, leaveOpen: true);

        br.ReadBytes(12); // RIFF + size + WAVE
        int  channels     = 1;
        int  sampleRate   = 44100;
        int  bitsPerSample = 16;
        long dataOffset   = 0;
        long dataLength   = 0;

        while (fs.Position + 8 <= fs.Length)
        {
            var id   = new string(br.ReadChars(4));
            var size = br.ReadUInt32();

            if (id == "fmt ")
            {
                br.ReadInt16();              // audio format
                channels      = br.ReadInt16();
                sampleRate    = br.ReadInt32();
                br.ReadInt32();              // byte rate
                br.ReadInt16();              // block align
                bitsPerSample = br.ReadInt16();
                long extra = (long)size - 16;
                if (extra > 0) fs.Seek(extra, SeekOrigin.Current);
            }
            else if (id == "data")
            {
                dataOffset = fs.Position;
                dataLength = Math.Min(size, fs.Length - fs.Position);
                break;
            }
            else
            {
                fs.Seek(size, SeekOrigin.Current);
            }
        }

        double durationSec = (sampleRate > 0 && bitsPerSample > 0 && channels > 0 && dataLength > 0)
            ? (double)dataLength / (sampleRate * channels * (bitsPerSample / 8))
            : 0;

        return new AudioHeader(
            "WAV", "WAV", channels, sampleRate, bitsPerSample,
            TimeSpan.FromSeconds(durationSec),
            dataOffset, dataLength, fs.Length);
    }

    private static AudioHeader ParseFlac(FileStream fs, string filePath)
    {
        // fLaC + STREAMINFO metadata block (min 38 bytes total)
        fs.Seek(4, SeekOrigin.Begin);
        if (fs.Length < 42) return AudioHeader.Raw("FLAC", fs.Length);

        var buf = new byte[38];
        fs.Read(buf, 0, buf.Length);

        // Block header: 1 byte type+last, 3 bytes size
        // STREAMINFO: min block size (2), max block size (2), min frame size (3),
        //   max frame size (3), sample rate (20 bits) | channels (3 bits) | bps (5 bits),
        //   total samples (36 bits), MD5 (16 bytes)
        // Offsets in buf (after 4-byte block header at buf[0..3]):
        int sampleRate    = ((buf[4] << 12) | (buf[5] << 4) | (buf[6] >> 4)) & 0xFFFFF;
        int channels      = ((buf[6] >> 1) & 0x7) + 1;
        int bitsPerSample = (((buf[6] & 1) << 4) | (buf[7] >> 4)) + 1;
        long totalSamples = ((long)(buf[7] & 0x0F) << 32)
                          | ((long)buf[8] << 24)
                          | ((long)buf[9] << 16)
                          | ((long)buf[10] << 8)
                          |  (long)buf[11];

        double durationSec = (sampleRate > 0 && totalSamples > 0)
            ? (double)totalSamples / sampleRate
            : 0;

        // Data starts after all metadata blocks — approximate with 8KB offset
        long dataOffset = Math.Min(8192, fs.Length / 2);
        long dataLength = fs.Length - dataOffset;

        return new AudioHeader(
            "FLAC", "FLAC", channels, sampleRate, bitsPerSample,
            TimeSpan.FromSeconds(durationSec),
            dataOffset, dataLength, fs.Length);
    }

    private static AudioHeader ParseAiff(FileStream fs, string filePath)
    {
        fs.Seek(12, SeekOrigin.Begin);
        using var br = new BinaryReader(fs, Encoding.ASCII, leaveOpen: true);

        int  channels     = 1;
        int  sampleRate   = 44100;
        int  bitsPerSample = 16;
        long dataOffset   = 0;
        long dataLength   = 0;

        while (fs.Position + 8 <= fs.Length)
        {
            var id   = new string(br.ReadChars(4));
            var size = (long)(uint)ReadBigEndianInt32(br);

            if (id == "COMM")
            {
                channels      = ReadBigEndianInt16(br);
                br.ReadInt32();  // sample frames
                bitsPerSample = ReadBigEndianInt16(br);
                // 80-bit extended float for sample rate — read first 2 bytes for exponent
                int exp = ReadBigEndianInt16(br) & 0x7FFF;
                uint mantHi = (uint)ReadBigEndianInt32(br);
                br.ReadInt32(); // mantissa low
                sampleRate = (int)(mantHi >> (16382 - exp));
                long extra = size - 18;
                if (extra > 0) fs.Seek(extra, SeekOrigin.Current);
            }
            else if (id == "SSND")
            {
                br.ReadInt32(); // offset
                br.ReadInt32(); // block size
                dataOffset = fs.Position;
                dataLength = Math.Min(size - 8, fs.Length - fs.Position);
                break;
            }
            else
            {
                fs.Seek(size, SeekOrigin.Current);
            }
        }

        double durationSec = (sampleRate > 0 && bitsPerSample > 0 && channels > 0 && dataLength > 0)
            ? (double)dataLength / (sampleRate * channels * (bitsPerSample / 8))
            : 0;

        return new AudioHeader(
            "AIFF", "AIFF", channels, sampleRate, bitsPerSample,
            TimeSpan.FromSeconds(durationSec),
            dataOffset, dataLength, fs.Length);
    }

    private static AudioHeader ParseMp3Id3(FileStream fs, string filePath)
    {
        // Skip ID3v2 tag to find MPEG frame header
        fs.Seek(6, SeekOrigin.Begin);
        var sizeBuf = new byte[4];
        fs.Read(sizeBuf, 0, 4);
        int id3Size = ((sizeBuf[0] & 0x7F) << 21) | ((sizeBuf[1] & 0x7F) << 14)
                    | ((sizeBuf[2] & 0x7F) << 7)  |  (sizeBuf[3] & 0x7F);
        long frameStart = 10 + id3Size;

        fs.Seek(frameStart, SeekOrigin.Begin);
        var fb = new byte[4];
        fs.Read(fb, 0, 4);

        return ParseMp3Frame(fb, fs, filePath, frameStart);
    }

    private static AudioHeader ParseMp3Frame(byte[] fb, FileStream fs, string filePath, long dataOffset = 0)
    {
        // MPEG frame header: 0xFF 0xEx or 0xFx
        // Byte 2: bitrate index (upper 4 bits) + sample rate index (bits 3-2) + padding (bit 1)
        // Byte 3: channel mode (upper 2 bits)
        int[] bitrateTable = [0, 32, 40, 48, 56, 64, 80, 96, 112, 128, 160, 192, 224, 256, 320, 0];
        int[] sampleRates  = [44100, 48000, 32000, 0];

        int bitrateIdx  = (fb[2] >> 4) & 0xF;
        int srIdx       = (fb[2] >> 2) & 0x3;
        int channelMode = (fb[3] >> 6) & 0x3;
        int channels    = channelMode == 3 ? 1 : 2;
        int sampleRate  = sampleRates[srIdx];
        int bitrate     = bitrateTable[bitrateIdx] * 1000;

        double durationSec = (bitrate > 0 && fs.Length > dataOffset)
            ? (double)((fs.Length - dataOffset) * 8) / bitrate
            : 0;

        return new AudioHeader(
            "MP3", "MP3", channels, sampleRate > 0 ? sampleRate : 44100, 16,
            TimeSpan.FromSeconds(durationSec),
            dataOffset, fs.Length - dataOffset, fs.Length);
    }

    // -- Peak Building ----------------------------------------------------

    private const int MaxCols    = 4096;
    private const int MaxReadLen = 4 * 1024 * 1024; // 4 MB

    private static WaveformPeaks BuildPeaks(FileStream fs, AudioHeader h, CancellationToken ct)
    {
        long readLen = Math.Min(h.DataLength, MaxReadLen);
        if (readLen <= 0 || h.DataOffset >= fs.Length)
        {
            return new WaveformPeaks(
                [0.0], [0.0], null, null, 1, h.DataOffset, h.DataLength);
        }

        fs.Seek(h.DataOffset, SeekOrigin.Begin);
        var raw = new byte[readLen];
        int read = fs.Read(raw, 0, (int)readLen);

        int cols = Math.Min(MaxCols, read);
        if (cols < 1) cols = 1;

        bool stereo = h.ChannelCount == 2;
        var minL = new double[cols];
        var maxL = new double[cols];
        double[]? minR = stereo ? new double[cols] : null;
        double[]? maxR = stereo ? new double[cols] : null;

        int bytesPerSample = Math.Max(1, h.BitsPerSample / 8);
        int frameSize      = bytesPerSample * h.ChannelCount;
        int totalFrames    = read / frameSize;
        int framesPerCol   = Math.Max(1, totalFrames / cols);

        for (int col = 0; col < cols; col++)
        {
            ct.ThrowIfCancellationRequested();

            int frameStart = col * framesPerCol;
            int frameEnd   = Math.Min(frameStart + framesPerCol, totalFrames);

            double loL = 0, hiL = 0, loR = 0, hiR = 0;

            for (int f = frameStart; f < frameEnd; f++)
            {
                int baseIdx = f * frameSize;
                if (baseIdx + frameSize > read) break;

                double sL = ReadSample(raw, baseIdx, h.BitsPerSample);
                loL = Math.Min(loL, sL);
                hiL = Math.Max(hiL, sL);

                if (stereo && h.ChannelCount >= 2)
                {
                    double sR = ReadSample(raw, baseIdx + bytesPerSample, h.BitsPerSample);
                    loR = Math.Min(loR, sR);
                    hiR = Math.Max(hiR, sR);
                }
            }

            minL[col] = loL;
            maxL[col] = hiL;
            if (stereo) { minR![col] = loR; maxR![col] = hiR; }
        }

        return new WaveformPeaks(minL, maxL, minR, maxR,
            h.ChannelCount, h.DataOffset, h.DataLength);
    }

    private static double ReadSample(byte[] buf, int offset, int bitsPerSample)
    {
        if (offset >= buf.Length) return 0;
        return bitsPerSample switch
        {
            8  => (buf[offset] - 128.0) / 128.0,
            16 when offset + 1 < buf.Length
               => (short)(buf[offset] | (buf[offset + 1] << 8)) / 32768.0,
            24 when offset + 2 < buf.Length
               => ((buf[offset] | (buf[offset + 1] << 8) | ((sbyte)buf[offset + 2] << 16))) / 8388608.0,
            32 when offset + 3 < buf.Length
               => BitConverter.ToSingle(buf, offset),
            _  => (buf[offset] - 128.0) / 128.0,
        };
    }

    // -- Helpers ----------------------------------------------------------

    private static int ReadBigEndianInt16(BinaryReader br)
    {
        var b = br.ReadBytes(2);
        return (b[0] << 8) | b[1];
    }

    private static int ReadBigEndianInt32(BinaryReader br)
    {
        var b = br.ReadBytes(4);
        return (b[0] << 24) | (b[1] << 16) | (b[2] << 8) | b[3];
    }

    private static string BuildFormatLabel(AudioHeader h)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append(h.FormatName);
        if (h.SampleRate > 0)   sb.Append($"  ·  {h.SampleRate} Hz");
        if (h.ChannelCount > 0) sb.Append($"  ·  {(h.ChannelCount == 1 ? "Mono" : "Stereo")}");
        if (h.BitsPerSample > 0) sb.Append($"  ·  {h.BitsPerSample}-bit");
        if (h.Duration > TimeSpan.Zero)
            sb.Append($"  ·  {h.Duration:mm\\:ss}");
        return sb.ToString();
    }

    private static string BuildStatusLabel(AudioHeader h, string filePath)
    {
        long fileLen = h.FileSize;
        return $"{filePath}  ·  {fileLen:N0} bytes";
    }

    private void SetBusy(bool busy)
    {
        IsBusy = busy;
        BusyOverlay.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
    }

    // -- Playback ---------------------------------------------------------

    private void InitPlayer(string filePath, AudioHeader header)
    {
        CleanupPlayer();

        if (!_playbackFormats.Contains(header.FormatName))
        {
            TransportBar.Visibility = Visibility.Collapsed;
            return;
        }

        _player = new MediaPlayer();
        _player.MediaOpened += (s, e) =>
        {
            var dur = _player.NaturalDuration;
            if (dur.HasTimeSpan)
            {
                SeekSlider.Maximum  = dur.TimeSpan.TotalSeconds;
                DurationLabel.Text  = FormatTime(dur.TimeSpan);
            }
            TransportBar.Visibility = Visibility.Visible;
        };
        _player.MediaEnded += (s, e) => StopPlayback();
        _player.MediaFailed += (s, e) =>
        {
            TransportBar.Visibility = Visibility.Collapsed;
            StatusText.Text = $"Playback unavailable: {e.ErrorException?.Message}";
        };
        _player.Open(new Uri(filePath, UriKind.Absolute));

        _positionTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        _positionTimer.Tick += (s, e) =>
        {
            if (_isSeeking || _player is null || !_isPlaying) return;
            var pos = _player.Position.TotalSeconds;
            SeekSlider.Value    = pos;
            PositionLabel.Text  = FormatTime(_player.Position);
            double max          = SeekSlider.Maximum;
            WaveCanvas.PlayheadFraction = max > 0 ? pos / max : 0;
        };
    }

    private void CleanupPlayer()
    {
        _positionTimer?.Stop();
        _positionTimer = null;
        _player?.Stop();
        _player?.Close();
        _player    = null;
        _isPlaying = false;
        _isSeeking = false;
    }

    private void StopPlayback()
    {
        _player?.Stop();
        _isPlaying              = false;
        PlayPauseGlyph.Text     = "\uE768"; // ▶
        WaveCanvas.PlayheadFraction = 0;
        SeekSlider.Value        = 0;
        PositionLabel.Text      = "0:00";
        _positionTimer?.Stop();
    }

    private void OnPlayPauseClicked(object sender, RoutedEventArgs e)
    {
        if (_player is null) return;
        if (_isPlaying)
        {
            _player.Pause();
            _isPlaying          = false;
            PlayPauseGlyph.Text = "\uE768"; // ▶
            _positionTimer?.Stop();
        }
        else
        {
            _player.Play();
            _isPlaying          = true;
            PlayPauseGlyph.Text = "\uE769"; // ⏸
            _positionTimer?.Start();
        }
    }

    private void OnStopClicked(object sender, RoutedEventArgs e)
        => StopPlayback();

    private void OnSeekDragStarted(object sender, DragStartedEventArgs e)
        => _isSeeking = true;

    private void OnSeekDragCompleted(object sender, DragCompletedEventArgs e)
    {
        _isSeeking = false;
        if (_player is null) return;
        _player.Position = TimeSpan.FromSeconds(SeekSlider.Value);
        if (_isPlaying) _player.Play();
    }

    private static string FormatTime(TimeSpan t)
        => t.TotalHours >= 1
            ? $"{(int)t.TotalHours}:{t.Minutes:D2}:{t.Seconds:D2}"
            : $"{t.Minutes}:{t.Seconds:D2}";

    // -- Event Handlers ---------------------------------------------------

    private void OnWaveHoverChanged(object? sender, WaveformHoverEventArgs e)
    {
        StatusText.Text = $"Offset: 0x{e.FileOffset:X8}  ·  Amp: {e.Amplitude:F3}";
    }

    private void OnWaveOffsetRequested(object? sender, long offset)
        => NavigateToOffsetRequested?.Invoke(this, offset);

    // ── Toolbar overflow ─────────────────────────────────────────────────────

    private void OnToolbarSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (e.WidthChanged) _overflowManager?.Update();
    }

    private void OnOverflowButtonClick(object sender, RoutedEventArgs e)
    {
        OverflowContextMenu.PlacementTarget = ToolbarOverflowButton;
        OverflowContextMenu.Placement       = System.Windows.Controls.Primitives.PlacementMode.Bottom;
        OverflowContextMenu.IsOpen          = true;
    }

    private void OnOverflowMenuOpened(object sender, RoutedEventArgs e)
    {
        _overflowManager?.SyncMenuVisibility();
    }
}

// -- AudioHeader record -------------------------------------------------------

/// <summary>
/// Parsed audio file metadata (format, channels, sample rate, bit depth, duration).
/// </summary>
internal sealed record AudioHeader(
    string   FormatName,
    string   Extension,
    int      ChannelCount,
    int      SampleRate,
    int      BitsPerSample,
    TimeSpan Duration,
    long     DataOffset,
    long     DataLength,
    long     FileSize)
{
    public static AudioHeader Raw(string ext, long fileSize)
        => new("Raw", ext.TrimStart('.'), 1, 0, 8, TimeSpan.Zero, 0, fileSize, fileSize);
}
