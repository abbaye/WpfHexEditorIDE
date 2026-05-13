// Project: WpfHexEditor.Plugins.ScreenRecorder
// File: ViewModels/TimelineViewModel.cs
// Description: Manages the ordered collection of captured frames.

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using WpfHexEditor.SDK.Commands;

namespace WpfHexEditor.Plugins.ScreenRecorder.ViewModels;

public sealed class TimelineViewModel : INotifyPropertyChanged
{
    private FrameCardViewModel? _selectedFrame;
    private int                 _globalDelay = 100;

    public ObservableCollection<FrameCardViewModel> Frames { get; } = [];

    public FrameCardViewModel? SelectedFrame
    {
        get => _selectedFrame;
        set
        {
            if (_selectedFrame == value) return;
            _selectedFrame = value;
            OnPropertyChanged();
            Preview?.SetFrame(value);
        }
    }

    public int GlobalDelay
    {
        get => _globalDelay;
        set
        {
            if (_globalDelay == value) return;
            _globalDelay = value;
            OnPropertyChanged();
            foreach (var f in Frames) f.Delay = value;
        }
    }

    public PreviewViewModel? Preview { get; set; }

    public ICommand DeleteSelectedCommand { get; }
    public ICommand InsertBlankCommand    { get; }
    public ICommand DuplicateFrameCommand { get; }

    public TimelineViewModel()
    {
        DeleteSelectedCommand = new RelayCommand(_ => DeleteSelected(), _ => SelectedFrames().Any());
        InsertBlankCommand    = new RelayCommand(_ => InsertBlank());
        DuplicateFrameCommand = new RelayCommand(_ => DuplicateSelected(), _ => SelectedFrame is not null);
    }

    public void AddFrame(FrameCardViewModel frame)
    {
        Frames.Add(frame);
        SelectedFrame = frame;
    }

    public void MoveFrame(int fromIndex, int toIndex)
    {
        if (fromIndex < 0 || fromIndex >= Frames.Count) return;
        if (toIndex   < 0 || toIndex   >= Frames.Count) return;
        if (fromIndex == toIndex) return;
        Frames.Move(fromIndex, toIndex);
        RenumberFrames();
    }

    public IReadOnlyList<FrameCardViewModel> SelectedFrames() =>
        Frames.Where(f => f.IsSelected).ToList();

    private void DeleteSelected()
    {
        foreach (var f in SelectedFrames().ToList()) Frames.Remove(f);
        RenumberFrames();
        SelectedFrame = Frames.LastOrDefault();
    }

    private void InsertBlank()
    {
        var idx = SelectedFrame is null ? Frames.Count : Frames.IndexOf(SelectedFrame) + 1;
        var blank = new FrameCardViewModel(idx, null, _globalDelay);
        Frames.Insert(Math.Min(idx, Frames.Count), blank);
        RenumberFrames();
        SelectedFrame = blank;
    }

    private void DuplicateSelected()
    {
        if (SelectedFrame is null) return;
        var idx  = Frames.IndexOf(SelectedFrame) + 1;
        var copy = SelectedFrame.Clone(idx);
        Frames.Insert(Math.Min(idx, Frames.Count), copy);
        RenumberFrames();
        SelectedFrame = copy;
    }

    private void RenumberFrames()
    {
        for (var i = 0; i < Frames.Count; i++) Frames[i].Index = i;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? p = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
}
