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

    // Undo/Redo — each entry is a full snapshot of Frames list (shallow clone).
    // Cheap because BitmapSource is frozen (shared reference, no copy).
    private readonly Stack<List<FrameCardViewModel>> _undoStack = new();
    private readonly Stack<List<FrameCardViewModel>> _redoStack = new();

    public ObservableCollection<FrameCardViewModel> Frames { get; } = [];

    public FrameCardViewModel? SelectedFrame
    {
        get => _selectedFrame;
        set
        {
            if (_selectedFrame == value) return;
            _selectedFrame = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SelectedIndex));
            Preview?.SetFrame(value);
        }
    }

    public int SelectedIndex => _selectedFrame is null ? -1 : Frames.IndexOf(_selectedFrame);

    public bool CanUndo => _undoStack.Count > 0;
    public bool CanRedo => _redoStack.Count > 0;

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

    // ── Undo / Redo ────────────────────────────────────────────────────────────

    private const int MaxUndoDepth = 50;

    public void PushUndoPublic() => PushUndo();

    private void PushUndo()
    {
        _undoStack.Push(Frames.ToList());
        if (_undoStack.Count > MaxUndoDepth)
        {
            // Rebuild without the oldest entry (Stack has no direct remove-bottom).
            var items = _undoStack.ToArray(); // newest-first
            _undoStack.Clear();
            for (var i = items.Length - 2; i >= 0; i--) // skip oldest (last index)
                _undoStack.Push(items[i]);
        }
        var hadRedo = _redoStack.Count > 0;
        _redoStack.Clear();
        OnPropertyChanged(nameof(CanUndo));
        if (hadRedo) OnPropertyChanged(nameof(CanRedo));
    }

    public void Undo()
    {
        if (!CanUndo) return;
        _redoStack.Push(Frames.ToList());
        RestoreSnapshot(_undoStack.Pop());
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
    }

    public void Redo()
    {
        if (!CanRedo) return;
        _undoStack.Push(Frames.ToList());
        RestoreSnapshot(_redoStack.Pop());
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
    }

    private void RestoreSnapshot(List<FrameCardViewModel> snapshot)
    {
        Frames.Clear();
        foreach (var f in snapshot) { WireContextMenuCommands(f); Frames.Add(f); }
        RenumberFrames();
        SelectedFrame = Frames.LastOrDefault();
    }

    // ── Mutations ──────────────────────────────────────────────────────────────

    public void AddFrame(FrameCardViewModel frame)
    {
        WireContextMenuCommands(frame);
        Frames.Add(frame);
        SelectedFrame = frame;
    }

    private void WireContextMenuCommands(FrameCardViewModel frame)
    {
        frame.DuplicateCommand   = DuplicateFrameCommand;
        frame.InsertBlankCommand = InsertBlankCommand;
        frame.DeleteCommand      = DeleteSelectedCommand;
    }

    public void MoveFrame(int fromIndex, int toIndex)
    {
        if (fromIndex < 0 || fromIndex >= Frames.Count) return;
        if (toIndex   < 0 || toIndex   >= Frames.Count) return;
        if (fromIndex == toIndex) return;
        PushUndo();
        Frames.Move(fromIndex, toIndex);
        RenumberFrames();
    }

    public IReadOnlyList<FrameCardViewModel> SelectedFrames() =>
        Frames.Where(f => f.IsSelected).ToList();

    private void DeleteSelected()
    {
        var toDelete = SelectedFrames();
        if (toDelete.Count == 0) return;
        PushUndo();
        foreach (var f in toDelete) Frames.Remove(f);
        RenumberFrames();
        SelectedFrame = Frames.LastOrDefault();
    }

    private void InsertBlank()
    {
        PushUndo();
        var idx = SelectedFrame is null ? Frames.Count : Frames.IndexOf(SelectedFrame) + 1;
        var blank = new FrameCardViewModel(idx, null, _globalDelay);
        WireContextMenuCommands(blank);
        Frames.Insert(Math.Min(idx, Frames.Count), blank);
        RenumberFrames();
        SelectedFrame = blank;
    }

    private void DuplicateSelected()
    {
        if (SelectedFrame is null) return;
        PushUndo();
        var idx  = Frames.IndexOf(SelectedFrame) + 1;
        var copy = SelectedFrame.Clone(idx);
        WireContextMenuCommands(copy);
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
