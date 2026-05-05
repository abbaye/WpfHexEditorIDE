// ==========================================================
// Project: WpfHexEditor.Editor.DocumentEditor
// File: ViewModels/DocumentSearchTarget.cs
// Description: ISearchTarget adapter over DocumentSearchViewModel,
//              allowing the standard QuickSearchBar overlay to drive
//              document find/replace without a separate dialog.
// ==========================================================

using System.Windows;
using WpfHexEditor.Editor.Core;

namespace WpfHexEditor.Editor.DocumentEditor.ViewModels;

internal sealed class DocumentSearchTarget : ISearchTarget
{
    private readonly DocumentSearchViewModel _vm;

    public DocumentSearchTarget(DocumentSearchViewModel vm) => _vm = vm;

    public SearchBarCapabilities Capabilities =>
        SearchBarCapabilities.Replace |
        SearchBarCapabilities.CaseSensitive;

    public int MatchCount        => _vm.Results.Count;
    public int CurrentMatchIndex => 0;

    public event EventHandler? SearchResultsChanged;

    public void Find(string query, SearchTargetOptions options = default)
    {
        _vm.SearchText = query;
        _vm.MatchCase  = options.HasFlag(SearchTargetOptions.CaseSensitive);
        _vm.FindAllCommand.Execute(null);
        SearchResultsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void FindNext()
    {
        _vm.FindNextCommand.Execute(null);
        SearchResultsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void FindPrevious()
    {
        _vm.FindPreviousCommand.Execute(null);
        SearchResultsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ClearSearch()
    {
        _vm.SearchText = string.Empty;
        _vm.Results.Clear();
        SearchResultsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Replace(string replacement)
    {
        _vm.ReplaceText = replacement;
        _vm.ReplaceNextCommand.Execute(null);
        SearchResultsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ReplaceAll(string replacement)
    {
        _vm.ReplaceText = replacement;
        _vm.ReplaceAllCommand.Execute(null);
        SearchResultsChanged?.Invoke(this, EventArgs.Empty);
    }

    public UIElement? GetCustomFiltersContent() => null;
}
