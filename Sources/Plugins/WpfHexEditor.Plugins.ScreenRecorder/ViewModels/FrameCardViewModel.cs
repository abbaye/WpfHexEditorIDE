// Project: WpfHexEditor.Plugins.ScreenRecorder
// File: ViewModels/FrameCardViewModel.cs
// Description: Single frame card in the timeline — thumbnail, delay, label, selection.

using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using WpfHexEditor.SDK.Commands;

namespace WpfHexEditor.Plugins.ScreenRecorder.ViewModels;

public sealed class FrameCardViewModel : INotifyPropertyChanged
{
    private int     _index;
    private int     _delay;
    private bool    _isSelected;
    private string? _label;

    // Thumbnail (120px) for the timeline card; FullBitmap for preview and export.
    public BitmapSource? Thumbnail   { get; }
    public BitmapSource? FullBitmap  { get; }
    public string?       SourcePath  { get; init; }  // path inside .whscr ZIP

    // Delegated context-menu commands wired by TimelineViewModel.
    public ICommand? DuplicateCommand  { get; set; }
    public ICommand? InsertBlankCommand { get; set; }
    public ICommand? DeleteCommand     { get; set; }

    public int Index
    {
        get => _index;
        set { if (_index == value) return; _index = value; OnPropertyChanged(); OnPropertyChanged(nameof(DisplayLabel)); }
    }

    public int Delay
    {
        get => _delay;
        set { if (_delay == value) return; _delay = value; OnPropertyChanged(); }
    }

    public bool IsSelected
    {
        get => _isSelected;
        set { if (_isSelected == value) return; _isSelected = value; OnPropertyChanged(); }
    }

    public string? Label
    {
        get => _label;
        set { if (_label == value) return; _label = value; OnPropertyChanged(); OnPropertyChanged(nameof(DisplayLabel)); }
    }

    public string DisplayLabel => !string.IsNullOrEmpty(_label)
        ? _label
        : string.Format(Properties.ScreenRecorderResources.ScreenRecorder_FrameLabel, _index);

    public FrameCardViewModel(int index, BitmapSource? thumbnail, int delay, BitmapSource? fullBitmap = null)
    {
        _index     = index;
        Thumbnail  = thumbnail;
        FullBitmap = fullBitmap ?? thumbnail;
        _delay     = delay;
    }

    public FrameCardViewModel Clone(int newIndex) =>
        new(newIndex, Thumbnail, _delay, FullBitmap) { SourcePath = SourcePath, Label = _label };

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? p = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
}
