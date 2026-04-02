// ==========================================================
// Project: WpfHexEditor.Plugins.AIAssistant
// File: AIAssistantOptions.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.6
// Created: 2026-03-31
// License: GNU Affero General Public License v3.0 (AGPL-3.0)
// Description:
//     Persistent settings with DPAPI-encrypted API keys per provider.
// ==========================================================
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WpfHexEditor.Plugins.AIAssistant.Options;

public sealed class AIAssistantOptions
{
    private static AIAssistantOptions? _instance;
    public static AIAssistantOptions Instance => _instance ??= new AIAssistantOptions();

    // Provider API keys (DPAPI-encrypted base64 in JSON; decrypted on demand)
    public string EncryptedAnthropicKey { get; set; } = "";
    public string EncryptedOpenAIKey { get; set; } = "";
    public string EncryptedAzureOpenAIKey { get; set; } = "";
    public string EncryptedGeminiKey { get; set; } = "";

    // Azure OpenAI specific
    public string AzureOpenAIEndpoint { get; set; } = "";
    public string AzureOpenAIDeployment { get; set; } = "";

    // Ollama
    public string OllamaBaseUrl { get; set; } = "http://localhost:11434";

    // Default provider and model
    public string DefaultProviderId { get; set; } = "anthropic";
    public string DefaultModelId { get; set; } = "claude-sonnet-4-6";
    public bool DefaultThinkingEnabled { get; set; }
    public int ThinkingBudgetTokens { get; set; } = 8192;

    // MCP
    public List<McpServerEntry> McpServers { get; set; } = [];

    // UI
    public bool ShowToolCallsInline { get; set; } = true;
    public int MaxConversationTokens { get; set; } = 100_000;

    [JsonIgnore]
    private static string SettingsDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "WpfHexEditor", "AIAssistant");

    [JsonIgnore]
    private static string SettingsPath => Path.Combine(SettingsDir, "settings.json");

    [JsonIgnore]
    public static string ConversationsDir => Path.Combine(SettingsDir, "conversations");

    [JsonIgnore]
    public static string PresetsPath => Path.Combine(SettingsDir, "presets.json");

    // --- DPAPI helpers ---

    public void SetApiKey(string providerId, string plaintext)
    {
        var encrypted = ProtectString(plaintext);
        switch (providerId)
        {
            case "anthropic": EncryptedAnthropicKey = encrypted; break;
            case "openai": EncryptedOpenAIKey = encrypted; break;
            case "azure-openai": EncryptedAzureOpenAIKey = encrypted; break;
            case "gemini": EncryptedGeminiKey = encrypted; break;
        }
    }

    public string? GetApiKey(string providerId)
    {
        var encrypted = providerId switch
        {
            "anthropic" => EncryptedAnthropicKey,
            "openai" => EncryptedOpenAIKey,
            "azure-openai" => EncryptedAzureOpenAIKey,
            "gemini" => EncryptedGeminiKey,
            _ => ""
        };
        return string.IsNullOrEmpty(encrypted) ? null : UnprotectString(encrypted);
    }

    private static string ProtectString(string plaintext)
    {
        var bytes = Encoding.UTF8.GetBytes(plaintext);
        var encrypted = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(encrypted);
    }

    private static string? UnprotectString(string base64)
    {
        try
        {
            var encrypted = Convert.FromBase64String(base64);
            var decrypted = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(decrypted);
        }
        catch
        {
            return null;
        }
    }

    // --- Persistence ---

    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault
    };

    public void Save()
    {
        Directory.CreateDirectory(SettingsDir);
        Directory.CreateDirectory(ConversationsDir);
        var json = JsonSerializer.Serialize(this, s_jsonOptions);
        File.WriteAllText(SettingsPath, json);
    }

    public void Load()
    {
        if (!File.Exists(SettingsPath)) return;
        try
        {
            var json = File.ReadAllText(SettingsPath);
            var loaded = JsonSerializer.Deserialize<AIAssistantOptions>(json, s_jsonOptions);
            if (loaded is null) return;

            EncryptedAnthropicKey = loaded.EncryptedAnthropicKey;
            EncryptedOpenAIKey = loaded.EncryptedOpenAIKey;
            EncryptedAzureOpenAIKey = loaded.EncryptedAzureOpenAIKey;
            EncryptedGeminiKey = loaded.EncryptedGeminiKey;
            AzureOpenAIEndpoint = loaded.AzureOpenAIEndpoint;
            AzureOpenAIDeployment = loaded.AzureOpenAIDeployment;
            OllamaBaseUrl = loaded.OllamaBaseUrl;
            DefaultProviderId = loaded.DefaultProviderId;
            DefaultModelId = loaded.DefaultModelId;
            DefaultThinkingEnabled = loaded.DefaultThinkingEnabled;
            ThinkingBudgetTokens = loaded.ThinkingBudgetTokens;
            McpServers = loaded.McpServers;
            ShowToolCallsInline = loaded.ShowToolCallsInline;
            MaxConversationTokens = loaded.MaxConversationTokens;
        }
        catch
        {
            // Corrupted file — keep defaults
        }
    }
}

public sealed class McpServerEntry
{
    public string ServerId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Command { get; set; } = "";
    public string[] Args { get; set; } = [];
    public Dictionary<string, string> Env { get; set; } = [];
    public bool Enabled { get; set; } = true;
    public bool IsIdeServer { get; set; }
}
