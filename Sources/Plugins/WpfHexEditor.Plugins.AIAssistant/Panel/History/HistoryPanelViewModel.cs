// ==========================================================
// Project: WpfHexEditor.Plugins.AIAssistant
// File: HistoryPanelViewModel.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.6
// Created: 2026-03-31
// License: GNU Affero General Public License v3.0 (AGPL-3.0)
// Description:
//     ViewModel for the history panel. Lists past conversations with search.
// ==========================================================
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WpfHexEditor.Plugins.AIAssistant.Session;

namespace WpfHexEditor.Plugins.AIAssistant.Panel.History;

public sealed partial class HistoryPanelViewModel : ObservableObject
{
    private List<SessionMetadata> _allEntries = [];

    public ObservableCollection<SessionMetadata> FilteredEntries { get; } = [];

    [ObservableProperty] private string _searchText = "";

    public event Action<string>? OpenSessionRequested;
    public event Action<string>? DeleteSessionRequested;

    public async Task LoadAsync()
    {
        _allEntries = await ConversationPersistence.LoadIndexAsync();
        ApplyFilter();
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    private void ApplyFilter()
    {
        FilteredEntries.Clear();
        var query = SearchText.Trim();
        var items = string.IsNullOrEmpty(query)
            ? _allEntries
            : _allEntries.Where(e => e.Title.Contains(query, StringComparison.OrdinalIgnoreCase));

        foreach (var entry in items)
            FilteredEntries.Add(entry);
    }

    [RelayCommand]
    private void OpenSession(SessionMetadata? entry)
    {
        if (entry is not null)
            OpenSessionRequested?.Invoke(entry.Id);
    }

    [RelayCommand]
    private async Task DeleteSession(SessionMetadata? entry)
    {
        if (entry is null) return;
        await ConversationPersistence.DeleteAsync(entry.Id);
        DeleteSessionRequested?.Invoke(entry.Id);
        _allEntries.RemoveAll(e => e.Id == entry.Id);
        ApplyFilter();
    }

    [RelayCommand]
    private async Task ClearAll()
    {
        foreach (var entry in _allEntries.ToList())
            await ConversationPersistence.DeleteAsync(entry.Id);

        _allEntries.Clear();
        FilteredEntries.Clear();
    }
}
