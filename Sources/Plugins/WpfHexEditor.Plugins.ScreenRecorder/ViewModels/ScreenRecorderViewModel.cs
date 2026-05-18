// ==========================================================
// Project: WpfHexEditor.Plugins.ScreenRecorder
// File: ViewModels/ScreenRecorderViewModel.cs
// Description: Root ViewModel for the Screen Recorder document editor.
//              Wires CaptureService, export services, session serializer, and region selector.
// Architecture Notes:
//     CaptureService events are marshalled back to the UI thread via Dispatcher.
//     File dialogs use Microsoft.Win32 (no external dialogs dependency needed).
//     ESC/confirm-cancel is handled here via IdeMessageBox through the plugin's host context;
//     the plugin sets CancelRequested property and the view reacts.
// ==========================================================

using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using WpfHexEditor.Plugins.ScreenRecorder.Models;
using WpfHexEditor.Plugins.ScreenRecorder.Overlay;
using WpfHexEditor.Plugins.ScreenRecorder.Properties;
using WpfHexEditor.Plugins.ScreenRecorder.Options;
using WpfHexEditor.Plugins.ScreenRecorder.Services;
using WpfHexEditor.SDK.Commands;

namespace WpfHexEditor.Plugins.ScreenRecorder.ViewModels;

public sealed class ScreenRecorderViewModel : INotifyPropertyChanged, IDisposable
{
    private RecordingMode _selectedMode   = RecordingMode.Screenshot;
    private bool          _isSessionActive;
    private string        _sessionPath    = string.Empty;

    public TimelineViewModel   Timeline   { get; }
    public PreviewViewModel    Preview    { get; }
    public PropertiesViewModel Properties { get; }
    public CaptureHudViewModel Hud        { get; }

    private readonly CaptureService    _captureService;
    private readonly PlaybackService   _playbackService;
    private CaptureOverlayWindow? _overlay;

    public bool IsPlaying => _playbackService.IsPlaying;

    public RecordingMode SelectedMode
    {
        get => _selectedMode;
        set { if (_selectedMode == value) return; _selectedMode = value; OnPropertyChanged(); }
    }

    public bool IsSessionActive
    {
        get => _isSessionActive;
        private set { if (_isSessionActive == value) return; _isSessionActive = value; OnPropertyChanged(); RefreshCanExecute(); }
    }

    // ── Commands ──────────────────────────────────────────────────────────────

    public ICommand StartCaptureCommand   { get; }
    public ICommand StopCaptureCommand    { get; }
    public ICommand PauseCaptureCommand   { get; }
    public ICommand CaptureFrameCommand   { get; }
    public ICommand SelectRegionCommand   { get; }
    public ICommand SaveSessionCommand    { get; }
    public ICommand OpenSessionCommand    { get; }
    public ICommand ImportImagesCommand   { get; }
    public ICommand ExportGifCommand      { get; }
    public ICommand ExportPngCommand      { get; }
    public ICommand ExportMp4Command      { get; }
    public ICommand PlayCommand           { get; }
    public ICommand StopPlaybackCommand   { get; }

    public ScreenRecorderViewModel()
    {
        var opts = Options.ScreenRecorderOptions.Instance;
        Timeline   = new TimelineViewModel { GlobalDelay = opts.TimerInterval };
        Preview    = new PreviewViewModel();
        Properties = new PropertiesViewModel
        {
            TimerInterval        = opts.TimerInterval,
            RepeatLastFrameDelay = opts.RepeatLastFrameDelay,
            LoopCount            = opts.DefaultLoopCount,
            OutputScale          = opts.DefaultScale,
        };
        Hud        = new CaptureHudViewModel();

        Timeline.Preview = Preview;
        Properties.SelectRegionCommand = new RelayCommand(_ => _ = SelectRegionAsync());
        Properties.ResetRegionCommand  = new RelayCommand(_ =>
        {
            Properties.CaptureRegion = default;
            System.Diagnostics.Debug.WriteLine($"[SR] FullScreen clicked: IsSessionActive={IsSessionActive}, IsPlaying={IsPlaying}");
            if (!IsSessionActive && !IsPlaying)
            {
                try   { StartCapture(); System.Diagnostics.Debug.WriteLine("[SR] StartCapture OK"); }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[SR] StartCapture FAILED: {ex}"); }
            }
        });

        _captureService                  = new CaptureService();
        _captureService.FrameCaptured   += OnFrameCaptured;
        _captureService.SessionStopped  += OnSessionStopped;

        _playbackService                 = new PlaybackService();
        _playbackService.FrameAdvanced  += OnPlaybackFrameAdvanced;
        _playbackService.PlaybackStopped += OnPlaybackStopped;

        StartCaptureCommand  = new RelayCommand(_ => StartCapture(),  _ => !IsSessionActive && !IsPlaying);
        StopCaptureCommand   = new RelayCommand(_ => StopCapture(),   _ => IsSessionActive);
        PauseCaptureCommand  = new RelayCommand(_ => TogglePause(), _ => IsSessionActive);
        CaptureFrameCommand  = new RelayCommand(_ => TriggerF9());
        SelectRegionCommand  = new RelayCommand(_ => _ = SelectRegionAsync());
        SaveSessionCommand   = new RelayCommand(_ => _ = SaveSessionAsync());
        OpenSessionCommand   = new RelayCommand(_ => _ = OpenSessionAsync());
        ImportImagesCommand  = new RelayCommand(_ => _ = ImportImagesAsync());
        ExportGifCommand     = new RelayCommand(_ => _ = ExportGifAsync(), _ => Timeline.Frames.Count > 0);
        ExportPngCommand     = new RelayCommand(_ => _ = ExportPngAsync(), _ => Timeline.Frames.Count > 0);
        ExportMp4Command     = new RelayCommand(_ => _ = ExportMp4Async(), _ => Timeline.Frames.Count > 0 && FfmpegExportService.IsAvailable);
        PlayCommand          = new RelayCommand(_ => StartPlayback(),  _ => Timeline.Frames.Count > 0 && !IsSessionActive && !IsPlaying);
        StopPlaybackCommand  = new RelayCommand(_ => _playbackService.Stop(), _ => IsPlaying);

        Timeline.PlayCommand         = PlayCommand;
        Timeline.StopPlaybackCommand = StopPlaybackCommand;
    }

    // ── Session ────────────────────────────────────────────────────────────────

    private void StartCapture()
    {
        var region = Properties.CaptureRegion.IsEmpty
            ? CaptureRegion.PrimaryScreen()
            : Properties.CaptureRegion;

        var session = new CaptureSession
        {
            Mode                 = SelectedMode,
            Region               = region,
            GlobalDelay          = Properties.TimerInterval,
            LoopCount            = Properties.LoopCount,
            RepeatLastFrameDelay = Properties.RepeatLastFrameDelay,
            OutputScale          = Properties.OutputScale
        };

        _captureService.StartSession(session);
        ShowOverlay(region);
        IsSessionActive     = true;
        Hud.IsRecording     = true;
        Hud.CaptureHotkey   = Options.ScreenRecorderOptions.Instance.HotkeyCapture;
    }

    private void StopCapture() => _captureService.StopSession();

    private void TogglePause()
    {
        if (_captureService.IsPaused) _captureService.ResumeSession();
        else                          _captureService.PauseSession();
    }

    /// <summary>
    /// Called by F9. Behaviour depends on the selected mode:
    /// - Screenshot / Both : if no session is active, starts one then captures a frame;
    ///                        if active, captures a frame immediately.
    /// - TimedInterval      : if no session active, starts the timed session;
    ///                        F9 does nothing extra (timer drives captures).
    /// </summary>
    public void TriggerF9()
    {
        var region = Properties.CaptureRegion.IsEmpty
            ? CaptureRegion.PrimaryScreen()
            : Properties.CaptureRegion;

        if (!IsSessionActive)
        {
            // Always start a session on first F9.
            StartCapture();

            // In Screenshot/Both mode, also capture the first frame immediately
            // (the session was just started — TriggerManualCapture checks Active state).
            if (SelectedMode is RecordingMode.Screenshot or RecordingMode.Both)
                _captureService.TriggerManualCapture();
        }
        else
        {
            // Session running — only capture manually in Screenshot/Both mode.
            if (SelectedMode is RecordingMode.Screenshot or RecordingMode.Both)
                _captureService.TriggerManualCapture();
        }
    }

    private void OnFrameCaptured(object? sender, CaptureFrame frame)
    {
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            var thumb = FrameCaptureEngine.CreateThumbnail(frame.Bitmap);
            var card  = new FrameCardViewModel(frame.Index, thumb, frame.Delay_ms, frame.Bitmap);
            Timeline.AddFrame(card);
            Hud.FrameCount = Timeline.Frames.Count;
            Hud.Elapsed    = _captureService.Elapsed.ToString(@"mm\:ss");
            if (Timeline.Frames.Count == 1) RefreshCanExecute();
        });
    }

    private void OnSessionStopped(object? sender, EventArgs e)
    {
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            IsSessionActive = false;
            Hud.IsRecording = false;
            HideOverlay();
        });
    }

    // ── Overlay ────────────────────────────────────────────────────────────────

    private void ShowOverlay(CaptureRegion region)
    {
        if (_overlay is null)
        {
            _overlay = new Overlay.CaptureOverlayWindow();
            _overlay.Closed                += (_, _) => _overlay = null;
            _overlay.CaptureHotkeyPressed  += (_, _) => TriggerF9();
            _overlay.StopHotkeyPressed     += (_, _) => StopCapture();
        }
        _overlay.ShowOverlay(region, Hud);
        _captureService.SetOverlayHwnd(_overlay.OverlayHwnd);
    }

    private void HideOverlay()
    {
        _overlay?.UnregisterHotkeys();
        _overlay?.HideOverlay();
    }

    // ── Region Selector ────────────────────────────────────────────────────────

    private async Task SelectRegionAsync()
    {
        var region = await RegionSelectorService.SelectRegionAsync();
        if (region is { IsEmpty: false } r)
            Properties.CaptureRegion = r;
    }

    // ── Session serialization ──────────────────────────────────────────────────

    private async Task SaveSessionAsync()
    {
        var dlg = new SaveFileDialog
        {
            Title            = ScreenRecorderResources.ScreenRecorder_SaveSession,
            Filter           = "WpfHexEditor Session (*.whscr)|*.whscr",
            DefaultExt       = ".whscr",
            InitialDirectory = ScreenRecorderOptions.Instance.DefaultSaveFolder
        };
        if (dlg.ShowDialog() != true) return;

        _sessionPath = dlg.FileName;
        var session  = BuildCaptureSession();
        await SessionSerializer.SaveAsync(session, _sessionPath);
        OnPropertyChanged(nameof(LastSavedPath));
    }

    public string LastSavedPath => _sessionPath;

    private async Task OpenSessionAsync()
    {
        var dlg = new OpenFileDialog
        {
            Title            = ScreenRecorderResources.ScreenRecorder_OpenSession,
            Filter           = "WpfHexEditor Session (*.whscr)|*.whscr",
            InitialDirectory = ScreenRecorderOptions.Instance.DefaultSaveFolder
        };
        if (dlg.ShowDialog() != true) return;

        _sessionPath = dlg.FileName;
        var session  = await SessionSerializer.LoadAsync(_sessionPath);

        if (Timeline.Frames.Count > 0) Timeline.PushUndoPublic();
        Timeline.Frames.Clear();
        Timeline.SelectedFrame = null;
        foreach (var frame in session.Frames)
        {
            var thumb = FrameCaptureEngine.CreateThumbnail(frame.Bitmap);
            var card  = new FrameCardViewModel(frame.Index, thumb, frame.Delay_ms, frame.Bitmap);
            Timeline.AddFrame(card);
        }

        Properties.CaptureRegion = session.Region;
        Properties.OutputScale          = session.OutputScale;
        Properties.LoopCount            = session.LoopCount;
        Properties.RepeatLastFrameDelay = session.RepeatLastFrameDelay;
    }

    // ── Export ─────────────────────────────────────────────────────────────────

    private async Task ExportGifAsync()
    {
        var dlg = new SaveFileDialog { Title = ScreenRecorderResources.ScreenRecorder_ExportGif, Filter = "GIF (*.gif)|*.gif", DefaultExt = ".gif" };
        if (dlg.ShowDialog() != true) return;

        var options = new ExportOptions(dlg.FileName, Properties.OutputScale, Properties.LoopCount, Properties.RepeatLastFrameDelay);
        var frames  = BuildFrameList();
        await GifExportService.ExportAsync(frames, options);
    }

    private async Task ExportPngAsync()
    {
        // SaveFileDialog used as directory picker: user selects any filename in target folder;
        // we strip the filename and use the directory.
        var dlg = new SaveFileDialog
        {
            Title      = ScreenRecorderResources.ScreenRecorder_ExportPng,
            Filter     = "PNG Sequence|*.png",
            FileName   = "frames",
            DefaultExt = ".png"
        };
        if (dlg.ShowDialog() != true) return;

        var folder  = Path.GetDirectoryName(dlg.FileName)!;
        var options = new ExportOptions(folder, Properties.OutputScale);
        await PngSequenceExportService.ExportAsync(BuildFrameList(), options);
    }

    private async Task ExportMp4Async()
    {
        var dlg = new SaveFileDialog { Title = ScreenRecorderResources.ScreenRecorder_ExportMp4, Filter = "MP4 (*.mp4)|*.mp4", DefaultExt = ".mp4" };
        if (dlg.ShowDialog() != true) return;

        var options = new ExportOptions(dlg.FileName, Properties.OutputScale);
        await FfmpegExportService.ExportAsync(BuildFrameList(), options);
    }

    // ── Playback ───────────────────────────────────────────────────────────────

    private void StartPlayback()
    {
        if (Timeline.Frames.Count == 0) return;
        var delays = Timeline.Frames.Select(f => f.Delay).ToList();
        var start  = Timeline.SelectedFrame is { } sel ? Timeline.Frames.IndexOf(sel) : 0;
        _playbackService.Play(delays, start);
        Timeline.IsPlaying = true;
        OnPropertyChanged(nameof(IsPlaying));
        RefreshCanExecute();
    }

    private void OnPlaybackFrameAdvanced(object? sender, int index)
    {
        if (index < Timeline.Frames.Count)
            Timeline.SelectedFrame = Timeline.Frames[index];
    }

    private void OnPlaybackStopped(object? sender, EventArgs e)
    {
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            Timeline.IsPlaying = false;
            OnPropertyChanged(nameof(IsPlaying));
            RefreshCanExecute();
        });
    }

    // ── Import ─────────────────────────────────────────────────────────────────

    private async Task ImportImagesAsync()
    {
        var dlg = new OpenFileDialog
        {
            Title      = ScreenRecorderResources.ScreenRecorder_ImportImages,
            Filter     = "Images (*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tiff)|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tiff",
            Multiselect = true
        };
        if (dlg.ShowDialog() != true) return;

        var loadTasks = dlg.FileNames.OrderBy(p => p)
            .Select(path => Task.Run<System.Windows.Media.Imaging.BitmapSource>(() =>
            {
                var img = new System.Windows.Media.Imaging.BitmapImage();
                img.BeginInit();
                using var stream = System.IO.File.OpenRead(path);
                img.StreamSource = stream;
                img.CacheOption  = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                img.EndInit();
                img.Freeze();
                return img;
            }))
            .ToList();

        var bitmaps = await Task.WhenAll(loadTasks);
        var thumbTasks = bitmaps.Select(b => Task.Run(() => FrameCaptureEngine.CreateThumbnail(b))).ToList();
        var thumbs = await Task.WhenAll(thumbTasks);
        for (var i = 0; i < bitmaps.Length; i++)
        {
            var card = new FrameCardViewModel(Timeline.Frames.Count, thumbs[i], Properties.TimerInterval, bitmaps[i]);
            Timeline.AddFrame(card);
        }
        RefreshCanExecute();
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private CaptureSession BuildCaptureSession()
    {
        var session = new CaptureSession
        {
            Mode                 = SelectedMode,
            Region               = Properties.CaptureRegion,
            GlobalDelay          = Properties.TimerInterval,
            LoopCount            = Properties.LoopCount,
            RepeatLastFrameDelay = Properties.RepeatLastFrameDelay,
            OutputScale          = Properties.OutputScale
        };
        foreach (var frame in BuildFrameList())
            session.AddFrame(frame);
        return session;
    }

    private List<CaptureFrame> BuildFrameList() =>
        Timeline.Frames
            .Select(f => new CaptureFrame(
                f.Index,
                f.FullBitmap ?? CreateBlankBitmap(),
                f.Delay, f.Label, DateTimeOffset.UtcNow))
            .ToList();

    private static System.Windows.Media.Imaging.BitmapSource? _blankBitmap;
    private static System.Windows.Media.Imaging.BitmapSource CreateBlankBitmap()
    {
        if (_blankBitmap is not null) return _blankBitmap;
        var wb = new System.Windows.Media.Imaging.WriteableBitmap(1, 1, 96, 96,
            System.Windows.Media.PixelFormats.Bgra32, null);
        wb.Freeze();
        return _blankBitmap = wb;
    }

    private void RefreshCanExecute()
    {
        (StartCaptureCommand  as RelayCommand)?.RaiseCanExecuteChanged();
        (StopCaptureCommand   as RelayCommand)?.RaiseCanExecuteChanged();
        (PauseCaptureCommand  as RelayCommand)?.RaiseCanExecuteChanged();
        (CaptureFrameCommand  as RelayCommand)?.RaiseCanExecuteChanged();
        (PlayCommand          as RelayCommand)?.RaiseCanExecuteChanged();
        (StopPlaybackCommand  as RelayCommand)?.RaiseCanExecuteChanged();
        (ExportGifCommand     as RelayCommand)?.RaiseCanExecuteChanged();
        (ExportPngCommand     as RelayCommand)?.RaiseCanExecuteChanged();
        (ExportMp4Command     as RelayCommand)?.RaiseCanExecuteChanged();
        (ImportImagesCommand  as RelayCommand)?.RaiseCanExecuteChanged();
    }


    public void Dispose()
    {
        _captureService.FrameCaptured   -= OnFrameCaptured;
        _captureService.SessionStopped  -= OnSessionStopped;
        _captureService.Dispose();
        _playbackService.FrameAdvanced  -= OnPlaybackFrameAdvanced;
        _playbackService.PlaybackStopped -= OnPlaybackStopped;
        _playbackService.Dispose();
        _overlay?.Close();
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? p = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
}
