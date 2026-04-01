// ==========================================================
// Project: WpfHexEditor.Plugins.ClaudeAssistant
// File: ClaudeAssistantOptionsPage.xaml.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.6
// Created: 2026-03-31
// License: GNU Affero General Public License v3.0 (AGPL-3.0)
// Description:
//     Options page code-behind. Per-provider API key config + defaults.
// ==========================================================
using System.Windows;
using System.Windows.Controls;

namespace WpfHexEditor.Plugins.ClaudeAssistant.Options;

public partial class ClaudeAssistantOptionsPage : UserControl
{
    private static readonly string[] s_providers = ["anthropic", "openai", "gemini", "ollama"];
    private static readonly Dictionary<string, string[]> s_models = new()
    {
        ["anthropic"] = ["claude-opus-4-6", "claude-sonnet-4-6", "claude-haiku-4-5"],
        ["openai"] = ["gpt-4o", "gpt-4o-mini", "o3", "o4-mini"],
        ["gemini"] = ["gemini-2.5-pro", "gemini-2.0-flash"],
        ["ollama"] = ["(auto-detected)"]
    };

    public ClaudeAssistantOptionsPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var opts = ClaudeAssistantOptions.Instance;

        // Populate provider combo
        DefaultProviderCombo.ItemsSource = s_providers;
        DefaultProviderCombo.SelectedItem = opts.DefaultProviderId;

        // Populate model combo
        UpdateModelCombo(opts.DefaultProviderId);
        DefaultModelCombo.SelectedItem = opts.DefaultModelId;

        // Ollama URL
        OllamaUrlBox.Text = opts.OllamaBaseUrl;

        // Thinking
        ThinkingCheck.IsChecked = opts.DefaultThinkingEnabled;

        // API keys — show masked placeholder if a key exists
        if (!string.IsNullOrEmpty(opts.EncryptedAnthropicKey))
            AnthropicKeyBox.Password = "********";
        if (!string.IsNullOrEmpty(opts.EncryptedOpenAIKey))
            OpenAIKeyBox.Password = "********";
        if (!string.IsNullOrEmpty(opts.EncryptedGeminiKey))
            GeminiKeyBox.Password = "********";
    }

    private void UpdateModelCombo(string providerId)
    {
        if (s_models.TryGetValue(providerId, out var models))
            DefaultModelCombo.ItemsSource = models;
    }

    private void OnDefaultProviderChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DefaultProviderCombo.SelectedItem is string providerId)
        {
            UpdateModelCombo(providerId);
            ThinkingCheck.IsEnabled = providerId == "anthropic";
        }
    }

    internal void Flush()
    {
        var opts = ClaudeAssistantOptions.Instance;

        // Only save key if user typed something new (not the mask placeholder)
        if (AnthropicKeyBox.Password is { Length: > 0 } ak && ak != "********")
            opts.SetApiKey("anthropic", ak);
        if (OpenAIKeyBox.Password is { Length: > 0 } ok && ok != "********")
            opts.SetApiKey("openai", ok);
        if (GeminiKeyBox.Password is { Length: > 0 } gk && gk != "********")
            opts.SetApiKey("gemini", gk);

        opts.OllamaBaseUrl = OllamaUrlBox.Text;
        opts.DefaultProviderId = DefaultProviderCombo.SelectedItem as string ?? "anthropic";
        opts.DefaultModelId = DefaultModelCombo.SelectedItem as string ?? "claude-sonnet-4-6";
        opts.DefaultThinkingEnabled = ThinkingCheck.IsChecked == true;

        opts.Save();
    }

    private async void OnTestAnthropicClick(object sender, RoutedEventArgs e)
        => await TestProviderAsync("anthropic", AnthropicKeyBox.Password);

    private async void OnTestOpenAIClick(object sender, RoutedEventArgs e)
        => await TestProviderAsync("openai", OpenAIKeyBox.Password);

    private async void OnTestGeminiClick(object sender, RoutedEventArgs e)
        => await TestProviderAsync("gemini", GeminiKeyBox.Password);

    private async void OnTestOllamaClick(object sender, RoutedEventArgs e)
        => await TestProviderAsync("ollama", OllamaUrlBox.Text);

    private static async Task TestProviderAsync(string providerId, string keyOrUrl)
    {
        // TODO Phase 1: call IModelProvider.TestConnectionAsync
        await Task.Delay(500);
        MessageBox.Show($"Connection test for {providerId}: not yet implemented.",
            "Claude AI Assistant", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
