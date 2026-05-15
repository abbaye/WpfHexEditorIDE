// ==========================================================
// Project: WpfHexEditor.Plugins.ScreenRecorder
// File: ViewModels/PreviewViewModel.cs
// Description: Manages selected frame display and zoom state for the preview pane.
//              ZoomLevel is a multiplier (0.05–8.0). FitToContainer=true overrides
//              it with Stretch=Uniform so the view fills available space.
// ==========================================================

using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using WpfHexEditor.SDK.Commands;

namespace WpfHexEditor.Plugins.ScreenRecorder.ViewModels;

public sealed class PreviewViewModel : INotifyPropertyChanged
{
    private const double ZoomMin  = 0.05;
    private const double ZoomMax  = 8.0;
    private const double ZoomStep = 1.25;

    private BitmapSource? _currentBitmap;
    private double        _zoomLevel = 1.0;
    private bool          _fitToContainer = true;
    private string?       _frameLabel;

    public BitmapSource? CurrentBitmap
    {
        get => _currentBitmap;
        private set { if (_currentBitmap == value) return; _currentBitmap = value; OnPropertyChanged(); }
    }

    public double ZoomLevel
    {
        get => _zoomLevel;
        set
        {
            var clamped = Math.Max(ZoomMin, Math.Min(value, ZoomMax));
            if (Math.Abs(_zoomLevel - clamped) < 0.001) return;
            _zoomLevel       = clamped;
            _fitToContainer  = false;
            OnPropertyChanged();
            OnPropertyChanged(nameof(FitToContainer));
        }
    }

    public bool FitToContainer
    {
        get => _fitToContainer;
        set { if (_fitToContainer == value) return; _fitToContainer = value; OnPropertyChanged(); }
    }

    public string? FrameLabel
    {
        get => _frameLabel;
        private set { if (_frameLabel == value) return; _frameLabel = value; OnPropertyChanged(); }
    }

    public ICommand ZoomInCommand  { get; }
    public ICommand ZoomOutCommand { get; }
    public ICommand Zoom1To1Command { get; }
    public ICommand ZoomFitCommand { get; }

    public PreviewViewModel()
    {
        ZoomInCommand   = new RelayCommand(_ => ZoomIn(),  _ => _currentBitmap is not null);
        ZoomOutCommand  = new RelayCommand(_ => ZoomOut(), _ => _currentBitmap is not null);
        Zoom1To1Command = new RelayCommand(_ => Zoom1To1(), _ => _currentBitmap is not null);
        ZoomFitCommand  = new RelayCommand(_ => ZoomFit(), _ => _currentBitmap is not null);
    }

    public void SetFrame(FrameCardViewModel? card)
    {
        CurrentBitmap = card?.FullBitmap;
        FrameLabel    = card?.DisplayLabel;
    }

    public void ZoomIn()   => ZoomLevel *= ZoomStep;
    public void ZoomOut()  => ZoomLevel /= ZoomStep;
    public void Zoom1To1() { _zoomLevel = 1.0; FitToContainer = false; OnPropertyChanged(nameof(ZoomLevel)); }
    public void ZoomFit()  { FitToContainer = true; OnPropertyChanged(nameof(ZoomLevel)); }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? p = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
}
