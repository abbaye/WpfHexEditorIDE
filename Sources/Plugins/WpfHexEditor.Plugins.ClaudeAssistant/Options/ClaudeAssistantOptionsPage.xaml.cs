// ==========================================================
// Project: WpfHexEditor.Plugins.ClaudeAssistant
// File: ClaudeAssistantOptionsPage.xaml.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.6
// Created: 2026-03-31
// License: GNU Affero General Public License v3.0 (AGPL-3.0)
// Description:
//     Options page code-behind. Per-provider API key config, Azure OpenAI,
//     thinking budget, max tokens, tool call display, MCP servers management,
//     and connection testing via IModelProvider.TestConnectionAsync.
// ==========================================================
using System.Collections.ObjectModel;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using WpfHexEditor.Plugins.ClaudeAssistant.Api;

namespace WpfHexEditor.Plugins.ClaudeAssistant.Options;

public partial class ClaudeAssistantOptionsPage : UserControl
{
    private static readonly string[] s_providers =
        ["anthropic", "openai", "azure-openai", "gemini", "ollama"];

    private static readonly Dictionary<string, string[]> s_models = new()
    {
        ["anthropic"] = ["claude-opus-4-6", "claude-sonnet-4-6", "claude-haiku-4-5"],
        ["openai"] = ["gpt-4o", "gpt-4o-mini", "o3", "o4-mini"],
        ["azure-openai"] = ["(uses deployment)"],
        ["gemini"] = ["gemini-2.5-pro", "gemini-2.0-flash"],
        ["ollama"] = ["(auto-detected)"]
    };

    private readonly ObservableCollection<McpServerEntry> _mcpServers = [];
    private bool _loading;

    public ClaudeAssistantOptionsPage()
    {
        InitializeComponent();
        McpListView.ItemsSource = _mcpServers;
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _loading = true;
        try
        {
            var opts = ClaudeAssistantOptions.Instance;

            // Providers
            DefaultProviderCombo.ItemsSource = s_providers;
            DefaultProviderCombo.SelectedItem = opts.DefaultProviderId;
            UpdateModelCombo(opts.DefaultProviderId);
            DefaultModelCombo.SelectedItem = opts.DefaultModelId;

            // API keys — masked placeholder if key exists
            if (!string.IsNullOrEmpty(opts.EncryptedAnthropicKey))
                AnthropicKeyBox.Password = "********";
            if (!string.IsNullOrEmpty(opts.EncryptedOpenAIKey))
                OpenAIKeyBox.Password = "********";
            if (!string.IsNullOrEmpty(opts.EncryptedAzureOpenAIKey))
                AzureKeyBox.Password = "********";
            if (!string.IsNullOrEmpty(opts.EncryptedGeminiKey))
                GeminiKeyBox.Password = "********";

            // Azure OpenAI
            AzureEndpointBox.Text = opts.AzureOpenAIEndpoint;
            AzureDeploymentBox.Text = opts.AzureOpenAIDeployment;

            // Ollama
            OllamaUrlBox.Text = opts.OllamaBaseUrl;

            // Thinking
            ThinkingCheck.IsChecked = opts.DefaultThinkingEnabled;
            ThinkingBudgetSlider.Value = opts.ThinkingBudgetTokens;
            ThinkingBudgetLabel.Text = FormatTokens(opts.ThinkingBudgetTokens);

            // Max conversation tokens
            MaxTokensSlider.Value = opts.MaxConversationTokens;
            MaxTokensLabel.Text = FormatTokens(opts.MaxConversationTokens);

            // Show tool calls
            ShowToolCallsCheck.IsChecked = opts.ShowToolCallsInline;

            // MCP Servers
            _mcpServers.Clear();
            foreach (var srv in opts.McpServers)
                _mcpServers.Add(srv);
        }
        finally
        {
            _loading = false;
        }
    }

    private void UpdateModelCombo(string providerId)
    {
        if (s_models.TryGetValue(providerId, out var models))
            DefaultModelCombo.ItemsSource = models;
    }

    private void OnDefaultProviderChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        if (DefaultProviderCombo.SelectedItem is string providerId)
        {
            UpdateModelCombo(providerId);
            ThinkingCheck.IsEnabled = providerId == "anthropic";
            ThinkingBudgetSlider.IsEnabled = providerId == "anthropic";
        }
    }

    private void OnThinkingBudgetChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (ThinkingBudgetLabel is not null)
            ThinkingBudgetLabel.Text = FormatTokens((int)e.NewValue);
    }

    private void OnMaxTokensChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (MaxTokensLabel is not null)
            MaxTokensLabel.Text = FormatTokens((int)e.NewValue);
    }

    private static string FormatTokens(int tokens) =>
        tokens >= 1000 ? $"{tokens / 1000}K" : tokens.ToString();

    // ────────── MCP Servers ──────────

    private void OnMcpAddClick(object sender, RoutedEventArgs e)
    {
        var name = McpNameBox.Text.Trim();
        var command = McpCommandBox.Text.Trim();
        if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(command)) return;

        var args = string.IsNullOrWhiteSpace(McpArgsBox.Text)
            ? []
            : McpArgsBox.Text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        _mcpServers.Add(new McpServerEntry
        {
            ServerId = name.ToLowerInvariant().Replace(' ', '-'),
            DisplayName = name,
            Command = command,
            Args = args,
            Enabled = true
        });

        McpNameBox.Clear();
        McpCommandBox.Clear();
        McpArgsBox.Clear();
    }

    private void OnMcpRemoveClick(object sender, RoutedEventArgs e)
    {
        if (McpListView.SelectedItem is McpServerEntry entry)
            _mcpServers.Remove(entry);
    }

    // ────────── Flush (save) ──────────

    internal void Flush()
    {
        var opts = ClaudeAssistantOptions.Instance;

        // API keys — only save if user typed something new
        SaveKeyIfChanged(AnthropicKeyBox.Password, "anthropic", opts);
        SaveKeyIfChanged(OpenAIKeyBox.Password, "openai", opts);
        SaveKeyIfChanged(AzureKeyBox.Password, "azure-openai", opts);
        SaveKeyIfChanged(GeminiKeyBox.Password, "gemini", opts);

        // Azure OpenAI
        opts.AzureOpenAIEndpoint = AzureEndpointBox.Text.Trim();
        opts.AzureOpenAIDeployment = AzureDeploymentBox.Text.Trim();

        // Ollama
        opts.OllamaBaseUrl = OllamaUrlBox.Text.Trim();

        // Defaults
        opts.DefaultProviderId = DefaultProviderCombo.SelectedItem as string ?? "anthropic";
        opts.DefaultModelId = DefaultModelCombo.SelectedItem as string ?? "claude-sonnet-4-6";
        opts.DefaultThinkingEnabled = ThinkingCheck.IsChecked == true;
        opts.ThinkingBudgetTokens = (int)ThinkingBudgetSlider.Value;
        opts.MaxConversationTokens = (int)MaxTokensSlider.Value;
        opts.ShowToolCallsInline = ShowToolCallsCheck.IsChecked == true;

        // MCP Servers
        opts.McpServers = [.. _mcpServers];

        opts.Save();
    }

    private static void SaveKeyIfChanged(string password, string providerId, ClaudeAssistantOptions opts)
    {
        if (password is { Length: > 0 } key && key != "********")
            opts.SetApiKey(providerId, key);
    }

    // ────────── Test Connection ──────────

    private async void OnTestAnthropicClick(object sender, RoutedEventArgs e)
        => await TestProviderConnectionAsync("anthropic", TestAnthropicBtn);

    private async void OnTestOpenAIClick(object sender, RoutedEventArgs e)
        => await TestProviderConnectionAsync("openai", TestOpenAIBtn);

    private async void OnTestAzureClick(object sender, RoutedEventArgs e)
        => await TestProviderConnectionAsync("azure-openai", TestAzureBtn);

    private async void OnTestGeminiClick(object sender, RoutedEventArgs e)
        => await TestProviderConnectionAsync("gemini", TestGeminiBtn);

    private async void OnTestOllamaClick(object sender, RoutedEventArgs e)
        => await TestProviderConnectionAsync("ollama", TestOllamaBtn);

    private async Task TestProviderConnectionAsync(string providerId, Button button)
    {
        // First flush current values so keys/URLs are saved
        Flush();

        button.IsEnabled = false;
        var originalContent = button.Content;
        button.Content = "Testing...";

        try
        {
            var registry = BuildTestRegistry();
            var provider = registry.GetProvider(providerId);

            if (provider is null)
            {
                MessageBox.Show($"Provider '{providerId}' is not registered.",
                    "Connection Test", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var success = await provider.TestConnectionAsync(cts.Token);

            MessageBox.Show(
                success
                    ? $"Connection to {provider.DisplayName} successful!"
                    : $"Connection to {provider.DisplayName} failed. Check your API key and settings.",
                "Connection Test",
                MessageBoxButton.OK,
                success ? MessageBoxImage.Information : MessageBoxImage.Warning);
        }
        catch (OperationCanceledException)
        {
            MessageBox.Show($"Connection test timed out after 10 seconds.",
                "Connection Test", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        catch (HttpRequestException ex)
        {
            MessageBox.Show($"Network error: {ex.Message}",
                "Connection Test", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error: {ex.Message}",
                "Connection Test", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            button.Content = originalContent;
            button.IsEnabled = true;
        }
    }

    private static ModelRegistry BuildTestRegistry()
    {
        var registry = new ModelRegistry();
        registry.Register(new Providers.Anthropic.AnthropicModelProvider());
        registry.Register(new Providers.OpenAI.OpenAIModelProvider());
        registry.Register(new Providers.Google.GeminiModelProvider());
        registry.Register(new Providers.Ollama.OllamaModelProvider());
        return registry;
    }
}
