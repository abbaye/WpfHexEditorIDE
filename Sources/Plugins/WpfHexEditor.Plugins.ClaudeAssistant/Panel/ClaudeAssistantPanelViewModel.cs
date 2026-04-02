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
using WpfHexEditor.Plugins.ClaudeAssistant.Providers.ClaudeCode;
using WpfHexEditor.Plugins.ClaudeAssistant.Providers.Ollama;
using WpfHexEditor.Plugins.ClaudeAssistant.Providers.OpenAI;
using WpfHexEditor.Plugins.ClaudeAssistant.Session;

namespace WpfHexEditor.Plugins.ClaudeAssistant.Panel;

public sealed partial class ClaudeAssistantPanelViewModel : ObservableObject
{
    private readonly ModelRegistry _registry = new();

    public ObservableCollection<ConversationTabViewModel> Tabs { get; } = [];
    [ObservableProperty] private ConversationTabViewModel? _activeTab;

    partial void OnActiveTabChanged(ConversationTabViewModel? value)
    {
        foreach (var tab in Tabs)
            tab.IsActive = ReferenceEquals(tab, value);
    }
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
        _registry.Register(new ClaudeCodeModelProvider());
        ProviderIds = _registry.Providers.Select(p => p.ProviderId).ToArray();

        History.OpenSessionRequested += OnOpenSessionFromHistory;
        History.DeleteSessionRequested += OnDeleteSessionFromHistory;

        CreateNewTab();
    }

    public async Task RestoreSessionsAsync()
    {
        var openTabState = await ConversationPersistence.LoadOpenTabsAsync();

        if (openTabState is { OpenSessionIds.Count: > 0 })
        {
            // Load ONLY the sessions that were open — not all conversations on disk
            var sessions = await ConversationPersistence.LoadByIdsAsync(openTabState.OpenSessionIds);
            var toRestore = sessions.Where(s => s.Messages.Count > 0).ToList();

            if (toRestore.Count > 0)
            {
                Tabs.Clear();
                ActiveTab = null;

                foreach (var session in toRestore)
                {
                    var tab = CreateTabForSession(session);
                    foreach (var msg in session.Messages)
                    {
                        tab.Messages.Add(new Messages.ChatMessageViewModel
                        {
                            Role = msg.Role,
                            Text = msg.GetTextContent()
                        });
                    }
                }

                var activeTab = openTabState.ActiveSessionId is not null
                    ? Tabs.FirstOrDefault(t => t.Session.Id == openTabState.ActiveSessionId)
                    : null;
                ActiveTab = activeTab ?? Tabs[0];
            }
        }

        await History.LoadAsync();
    }

    [RelayCommand]
    private void CreateNewTab()
    {
        var opts = ClaudeAssistantOptions.Instance;

        // Auto-fallback to claude-code if default provider has no API key
        var providerId = opts.DefaultProviderId;
        var modelId = opts.DefaultModelId;
        if (providerId != "ollama" && providerId != "claude-code"
            && string.IsNullOrEmpty(opts.GetApiKey(providerId))
            && ClaudeCodeModelProvider.FindClaudeExecutable() is not null)
        {
            providerId = "claude-code";
            modelId = "sonnet";
        }

        var session = new ConversationSession
        {
            ProviderId = providerId,
            ModelId = modelId,
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

        // Save to history (conversation file stays on disk for history panel)
        // but it won't reopen because open-tabs.json won't include it
        if (tab.Session.Messages.Count > 0)
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
        {
            // Only persist sessions that have messages (skip empty "New conversation" tabs)
            if (tab.Session.Messages.Count > 0)
                await ConversationPersistence.SaveAsync(tab.Session);
        }

        // Save which tabs are currently open (so we restore only these)
        var openIds = Tabs
            .Where(t => t.Session.Messages.Count > 0)
            .Select(t => t.Session.Id)
            .ToList();
        await ConversationPersistence.SaveOpenTabsAsync(new OpenTabsState
        {
            OpenSessionIds = openIds,
            ActiveSessionId = ActiveTab?.Session.Id
        });
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

        var sessions = await ConversationPersistence.LoadByIdsAsync([sessionId]);
        var session = sessions.FirstOrDefault();
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
