// ==========================================================
// Project: WpfHexEditor.Editor.DocumentEditor
// File: ViewModels/DocumentSearchViewModel.cs
// Description:
//     Find & Replace view model for the document editor (Phase 19).
//     Searches DocumentBlock.Text concatenations, fires SetFindResults()
//     on the renderer to drive yellow highlight overlays.
//     Mirrors SearchViewModel / ReplaceViewModel architecture from HexEditor.
// ==========================================================

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Windows.Input;
using WpfHexEditor.Editor.DocumentEditor.Controls;
using WpfHexEditor.Editor.DocumentEditor.Core.Model;

namespace WpfHexEditor.Editor.DocumentEditor.ViewModels;

public sealed class DocumentSearchViewModel : INotifyPropertyChanged
{
    private readonly DocumentModel          _model;
    private readonly DocumentCanvasRenderer _renderer;

    private string _searchText    = string.Empty;
    private string _replaceText   = string.Empty;
    private bool   _matchCase;
    private bool   _wholeWord;
    private bool   _useRegex;
    private bool   _isSearching;
    private string _statusText    = string.Empty;
    private int    _activeCursor  = 0;

    public ObservableCollection<DocumentSearchMatch> Results { get; } = [];

    // ── Properties ────────────────────────────────────────────────────────────

    public string SearchText
    {
        get => _searchText;
        set { SetField(ref _searchText, value); Results.Clear(); _renderer.SetFindResults([], 0); }
    }

    public string ReplaceText
    {
        get => _replaceText;
        set => SetField(ref _replaceText, value);
    }

    public bool MatchCase  { get => _matchCase;  set => SetField(ref _matchCase, value); }
    public bool WholeWord  { get => _wholeWord;  set => SetField(ref _wholeWord, value); }
    public bool UseRegex   { get => _useRegex;   set => SetField(ref _useRegex, value); }
    public bool IsSearching { get => _isSearching; private set => SetField(ref _isSearching, value); }
    public string StatusText { get => _statusText; private set => SetField(ref _statusText, value); }

    // ── Commands ──────────────────────────────────────────────────────────────

    public ICommand FindAllCommand      { get; }
    public ICommand FindNextCommand     { get; }
    public ICommand FindPreviousCommand { get; }
    public ICommand ReplaceNextCommand  { get; }
    public ICommand ReplaceAllCommand   { get; }

    public DocumentSearchViewModel(DocumentModel model, DocumentCanvasRenderer renderer)
    {
        _model    = model;
        _renderer = renderer;

        FindAllCommand      = new RelayCmd(FindAll,      () => !string.IsNullOrEmpty(SearchText));
        FindNextCommand     = new RelayCmd(FindNext,     () => Results.Count > 0);
        FindPreviousCommand = new RelayCmd(FindPrevious, () => Results.Count > 0);
        ReplaceNextCommand  = new RelayCmd(ReplaceNext,  () => Results.Count > 0 && !string.IsNullOrEmpty(ReplaceText));
        ReplaceAllCommand   = new RelayCmd(ReplaceAll,   () => Results.Count > 0 && !string.IsNullOrEmpty(ReplaceText));
    }

    // ── Find ──────────────────────────────────────────────────────────────────

    private void FindAll()
    {
        if (string.IsNullOrEmpty(SearchText)) return;
        Results.Clear();

        var comparison = MatchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        var sw         = System.Diagnostics.Stopwatch.StartNew();

        for (int bi = 0; bi < _model.Blocks.Count; bi++)
        {
            var text = _model.Blocks[bi].Text;
            if (string.IsNullOrEmpty(text)) continue;

            if (UseRegex)
            {
                var opts = MatchCase ? RegexOptions.None : RegexOptions.IgnoreCase;
                try
                {
                    foreach (Match m in Regex.Matches(text, SearchText, opts))
                        Results.Add(new DocumentSearchMatch(bi, m.Index, m.Index + m.Length,
                            GetContext(text, m.Index, m.Length)));
                }
                catch { /* invalid regex */ }
            }
            else
            {
                int idx = 0;
                while ((idx = text.IndexOf(SearchText, idx, comparison)) >= 0)
                {
                    if (!WholeWord || IsWholeWord(text, idx, SearchText.Length))
                        Results.Add(new DocumentSearchMatch(bi, idx, idx + SearchText.Length,
                            GetContext(text, idx, SearchText.Length)));
                    idx++;
                }
            }
        }

        sw.Stop();
        _activeCursor = 0;
        _renderer.SetFindResults(Results, _activeCursor);
        StatusText = Results.Count > 0
            ? $"Found {Results.Count} matches in {sw.ElapsedMilliseconds}ms"
            : "No matches found";
    }

    private void FindNext()
    {
        if (Results.Count == 0) { FindAll(); return; }
        _activeCursor = (_activeCursor + 1) % Results.Count;
        _renderer.SetFindResults(Results, _activeCursor);
        StatusText = $"Match {_activeCursor + 1} of {Results.Count}";
    }

    private void FindPrevious()
    {
        if (Results.Count == 0) return;
        _activeCursor = (_activeCursor - 1 + Results.Count) % Results.Count;
        _renderer.SetFindResults(Results, _activeCursor);
        StatusText = $"Match {_activeCursor + 1} of {Results.Count}";
    }

    // ── Replace ───────────────────────────────────────────────────────────────

    private void ReplaceNext()
    {
        if (Results.Count == 0 || _activeCursor >= Results.Count) return;
        var match = Results[_activeCursor];
        var block = _model.Blocks[match.BlockIndex];
        _model.UndoEngine.Push(new WpfHexEditor.Editor.Core.Undo.CompositeUndoEntry("Replace", []));
        var len = match.EndChar - match.StartChar;
        block.Text = block.Text.Remove(match.StartChar, len).Insert(match.StartChar, ReplaceText);
        Results.RemoveAt(_activeCursor);
        if (_activeCursor >= Results.Count) _activeCursor = 0;
        _renderer.SetFindResults(Results, _activeCursor);
        StatusText = $"{Results.Count} matches remaining";
    }

    private void ReplaceAll()
    {
        if (Results.Count == 0) return;
        int count = Results.Count;
        // Replace from last to first to preserve offsets
        foreach (var match in Results.OrderByDescending(m => m.BlockIndex).ThenByDescending(m => m.StartChar))
        {
            var block = _model.Blocks[match.BlockIndex];
            var len   = match.EndChar - match.StartChar;
            block.Text = block.Text.Remove(match.StartChar, len).Insert(match.StartChar, ReplaceText);
        }
        Results.Clear();
        _renderer.SetFindResults([], 0);
        _model.NotifyBlocksChanged();
        StatusText = $"Replaced {count} occurrences";
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static bool IsWholeWord(string text, int start, int length)
    {
        bool leftOk  = start == 0              || !char.IsLetterOrDigit(text[start - 1]);
        bool rightOk = start + length >= text.Length || !char.IsLetterOrDigit(text[start + length]);
        return leftOk && rightOk;
    }

    private static string GetContext(string text, int start, int length)
    {
        int from = Math.Max(0, start - 20);
        int to   = Math.Min(text.Length, start + length + 20);
        return text[from..to];
    }

    // ── INotifyPropertyChanged ────────────────────────────────────────────────

    public event PropertyChangedEventHandler? PropertyChanged;
    private void SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    // ── Minimal RelayCommand (no SDK dep) ────────────────────────────────────

    private sealed class RelayCmd : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool> _canExecute;
        public RelayCmd(Action execute, Func<bool> canExecute) { _execute = execute; _canExecute = canExecute; }
        public bool CanExecute(object? _) => _canExecute();
        public void Execute(object? _)    => _execute();
        public event EventHandler? CanExecuteChanged { add => CommandManager.RequerySuggested += value; remove => CommandManager.RequerySuggested -= value; }
    }
}

// ── DocumentSearchMatch record ────────────────────────────────────────────────

public sealed record DocumentSearchMatch(int BlockIndex, int StartChar, int EndChar, string Context);
