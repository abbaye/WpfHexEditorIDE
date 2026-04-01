// ==========================================================
// Project: WpfHexEditor.Plugins.ClaudeAssistant
// File: ConversationPersistence.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.6
// Created: 2026-03-31
// License: GNU Affero General Public License v3.0 (AGPL-3.0)
// Description:
//     Save/load conversations to JSON files in %AppData%.
// ==========================================================
using System.IO;
using System.Text.Json;
using WpfHexEditor.Plugins.ClaudeAssistant.Api;
using WpfHexEditor.Plugins.ClaudeAssistant.Options;

namespace WpfHexEditor.Plugins.ClaudeAssistant.Session;

public static class ConversationPersistence
{
    private static string Dir => ClaudeAssistantOptions.ConversationsDir;

    private static readonly JsonSerializerOptions s_json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static async Task SaveAsync(ConversationSession session)
    {
        Directory.CreateDirectory(Dir);
        var dto = new ConversationDto
        {
            Id = session.Id,
            Title = session.Title,
            ProviderId = session.ProviderId,
            ModelId = session.ModelId,
            ThinkingEnabled = session.ThinkingEnabled,
            CreatedAt = session.CreatedAt,
            LastModifiedAt = session.LastModifiedAt,
            Messages = session.Messages.Select(m => new MessageDto
            {
                Role = m.Role,
                Text = m.GetTextContent()
            }).ToList()
        };

        var path = Path.Combine(Dir, $"{session.Id}.json");
        var json = JsonSerializer.Serialize(dto, s_json);
        await File.WriteAllTextAsync(path, json);

        await SaveIndexAsync();
    }

    public static async Task<List<ConversationSession>> LoadAllAsync()
    {
        var sessions = new List<ConversationSession>();
        if (!Directory.Exists(Dir)) return sessions;

        foreach (var file in Directory.GetFiles(Dir, "*.json").Where(f => !f.EndsWith("index.json")))
        {
            try
            {
                var json = await File.ReadAllTextAsync(file);
                var dto = JsonSerializer.Deserialize<ConversationDto>(json, s_json);
                if (dto is null) continue;

                var session = new ConversationSession
                {
                    Id = dto.Id,
                    Title = dto.Title,
                    ProviderId = dto.ProviderId,
                    ModelId = dto.ModelId,
                    ThinkingEnabled = dto.ThinkingEnabled,
                    CreatedAt = dto.CreatedAt,
                    LastModifiedAt = dto.LastModifiedAt
                };

                foreach (var msg in dto.Messages)
                {
                    session.Messages.Add(new ChatMessage
                    {
                        Role = msg.Role,
                        Content = [new TextBlock(msg.Text)]
                    });
                }

                sessions.Add(session);
            }
            catch { /* skip corrupted files */ }
        }

        return sessions.OrderByDescending(s => s.LastModifiedAt).ToList();
    }

    public static async Task DeleteAsync(string sessionId)
    {
        var path = Path.Combine(Dir, $"{sessionId}.json");
        if (File.Exists(path)) File.Delete(path);
        await SaveIndexAsync();
    }

    public static async Task<List<SessionMetadata>> LoadIndexAsync()
    {
        var indexPath = Path.Combine(Dir, "index.json");
        if (!File.Exists(indexPath)) return [];

        try
        {
            var json = await File.ReadAllTextAsync(indexPath);
            return JsonSerializer.Deserialize<List<SessionMetadata>>(json, s_json) ?? [];
        }
        catch { return []; }
    }

    private static async Task SaveIndexAsync()
    {
        Directory.CreateDirectory(Dir);
        var entries = new List<SessionMetadata>();

        foreach (var file in Directory.GetFiles(Dir, "*.json").Where(f => !f.EndsWith("index.json")))
        {
            try
            {
                var json = await File.ReadAllTextAsync(file);
                var dto = JsonSerializer.Deserialize<ConversationDto>(json, s_json);
                if (dto is null) continue;
                entries.Add(new SessionMetadata
                {
                    Id = dto.Id,
                    Title = dto.Title,
                    ProviderId = dto.ProviderId,
                    ModelId = dto.ModelId,
                    MessageCount = dto.Messages.Count,
                    CreatedAt = dto.CreatedAt,
                    LastModifiedAt = dto.LastModifiedAt
                });
            }
            catch { /* skip */ }
        }

        var indexPath = Path.Combine(Dir, "index.json");
        var indexJson = JsonSerializer.Serialize(entries.OrderByDescending(e => e.LastModifiedAt).ToList(), s_json);
        await File.WriteAllTextAsync(indexPath, indexJson);
    }

    // DTOs for JSON serialization
    private sealed class ConversationDto
    {
        public string Id { get; set; } = "";
        public string Title { get; set; } = "";
        public string ProviderId { get; set; } = "";
        public string ModelId { get; set; } = "";
        public bool ThinkingEnabled { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime LastModifiedAt { get; set; }
        public List<MessageDto> Messages { get; set; } = [];
    }

    private sealed class MessageDto
    {
        public string Role { get; set; } = "";
        public string Text { get; set; } = "";
    }
}
