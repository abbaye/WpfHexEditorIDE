// Project: WpfHexEditor.Plugins.ScreenRecorder
// File: ViewModels/PreviewViewModel.cs
// Description: Manages selected frame display and zoom state.

using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media.Imaging;

namespace WpfHexEditor.Plugins.ScreenRecorder.ViewModels;

public sealed class PreviewViewModel : INotifyPropertyChanged
{
    private BitmapSource? _currentBitmap;
    private double        _zoomLevel = 1.0;
    private bool          _isZoom1To1;

    public BitmapSource? CurrentBitmap
    {
        get => _currentBitmap;
        private set { if (_currentBitmap == value) return; _currentBitmap = value; OnPropertyChanged(); }
    }

    public double ZoomLevel
    {
        get => _zoomLevel;
        set { if (Math.Abs(_zoomLevel - value) < 0.001) return; _zoomLevel = Math.Max(0.05, Math.Min(value, 8.0)); OnPropertyChanged(); }
    }

    public bool IsZoom1To1
    {
        get => _isZoom1To1;
        set { if (_isZoom1To1 == value) return; _isZoom1To1 = value; ZoomLevel = value ? 1.0 : 0.0; OnPropertyChanged(); }
    }

    public void SetFrame(FrameCardViewModel? card) =>
        CurrentBitmap = card?.Thumbnail;

    public void ZoomIn()  => ZoomLevel *= 1.25;
    public void ZoomOut() => ZoomLevel /= 1.25;
    public void ZoomFit() { IsZoom1To1 = false; ZoomLevel = 0.0; }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? p = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
}
