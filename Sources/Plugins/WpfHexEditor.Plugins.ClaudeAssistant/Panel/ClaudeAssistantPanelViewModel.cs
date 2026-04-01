// ==========================================================
// Project: WpfHexEditor.Plugins.ClaudeAssistant
// File: ClaudeAssistantPanelViewModel.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.6
// Created: 2026-03-31
// License: GNU Affero General Public License v3.0 (AGPL-3.0)
// Description:
//     Root ViewModel. Multi-tab conversations, history panel, provider registry.
// ==========================================================
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WpfHexEditor.Plugins.ClaudeAssistant.Api;
using WpfHexEditor.Plugins.ClaudeAssistant.Options;
using WpfHexEditor.Plugins.ClaudeAssistant.Panel.History;
using WpfHexEditor.Plugins.ClaudeAssistant.Panel.Tabs;
using WpfHexEditor.Plugins.ClaudeAssistant.Providers.Anthropic;
using WpfHexEditor.Plugins.ClaudeAssistant.Providers.Google;
using WpfHexEditor.Plugins.ClaudeAssistant.Providers.Ollama;
using WpfHexEditor.Plugins.ClaudeAssistant.Providers.OpenAI;
using WpfHexEditor.Plugins.ClaudeAssistant.Session;

namespace WpfHexEditor.Plugins.ClaudeAssistant.Panel;

public sealed partial class ClaudeAssistantPanelViewModel : ObservableObject
{
    private readonly ModelRegistry _registry = new();

    public ObservableCollection<ConversationTabViewModel> Tabs { get; } = [];
    [ObservableProperty] private ConversationTabViewModel? _activeTab;
    [ObservableProperty] private string _statusText = "Ready";
    [ObservableProperty] private bool _isHistoryVisible;

    public HistoryPanelViewModel History { get; } = new();
    public string[] ProviderIds { get; }

    public ClaudeAssistantPanelViewModel()
    {
        _registry.Register(new AnthropicModelProvider());
        _registry.Register(new OpenAIModelProvider());
        _registry.Register(new GeminiModelProvider());
        _registry.Register(new OllamaModelProvider());
        ProviderIds = _registry.Providers.Select(p => p.ProviderId).ToArray();

        History.OpenSessionRequested += OnOpenSessionFromHistory;
        History.DeleteSessionRequested += OnDeleteSessionFromHistory;

        CreateNewTab();
    }

    public async Task RestoreSessionsAsync()
    {
        var sessions = await ConversationPersistence.LoadAllAsync();
        foreach (var session in sessions)
        {
            var tab = CreateTabForSession(session);
            // Restore messages into VM
            foreach (var msg in session.Messages)
            {
                tab.Messages.Add(new Messages.ChatMessageViewModel
                {
                    Role = msg.Role,
                    Text = msg.GetTextContent()
                });
            }
        }

        if (Tabs.Count == 0)
            CreateNewTab();
        else
            ActiveTab = Tabs[0];

        await History.LoadAsync();
    }

    [RelayCommand]
    private void CreateNewTab()
    {
        var opts = ClaudeAssistantOptions.Instance;
        var session = new ConversationSession
        {
            ProviderId = opts.DefaultProviderId,
            ModelId = opts.DefaultModelId,
            ThinkingEnabled = opts.DefaultThinkingEnabled,
            ThinkingBudgetTokens = opts.ThinkingBudgetTokens
        };

        var tab = CreateTabForSession(session);
        ActiveTab = tab;
    }

    [RelayCommand]
    private void CloseTab(ConversationTabViewModel? tab)
    {
        if (tab is null) return;

        // Auto-save before closing
        _ = ConversationPersistence.SaveAsync(tab.Session);

        var idx = Tabs.IndexOf(tab);
        Tabs.Remove(tab);

        if (Tabs.Count == 0)
            CreateNewTab();
        else if (ActiveTab == tab)
            ActiveTab = Tabs[Math.Min(idx, Tabs.Count - 1)];
    }

    [RelayCommand]
    private void ToggleHistory()
    {
        IsHistoryVisible = !IsHistoryVisible;
        if (IsHistoryVisible)
            _ = History.LoadAsync();
    }

    public async Task SaveAllSessionsAsync()
    {
        foreach (var tab in Tabs)
            await ConversationPersistence.SaveAsync(tab.Session);
    }

    public void UpdateModelIds(string providerId)
    {
        // Notify active tab of model list change if needed
    }

    private ConversationTabViewModel CreateTabForSession(ConversationSession session)
    {
        var tab = new ConversationTabViewModel(session, _registry);
        Tabs.Add(tab);
        return tab;
    }

    private async void OnOpenSessionFromHistory(string sessionId)
    {
        // Check if already open
        var existing = Tabs.FirstOrDefault(t => t.Session.Id == sessionId);
        if (existing is not null)
        {
            ActiveTab = existing;
            IsHistoryVisible = false;
            return;
        }

        var sessions = await ConversationPersistence.LoadAllAsync();
        var session = sessions.FirstOrDefault(s => s.Id == sessionId);
        if (session is null) return;

        var tab = CreateTabForSession(session);
        foreach (var msg in session.Messages)
        {
            tab.Messages.Add(new Messages.ChatMessageViewModel
            {
                Role = msg.Role,
                Text = msg.GetTextContent()
            });
        }

        ActiveTab = tab;
        IsHistoryVisible = false;
    }

    private void OnDeleteSessionFromHistory(string sessionId)
    {
        var tab = Tabs.FirstOrDefault(t => t.Session.Id == sessionId);
        if (tab is not null)
            Tabs.Remove(tab);
    }
}
