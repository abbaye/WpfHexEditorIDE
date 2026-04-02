// ==========================================================
// Project: WpfHexEditor.Plugins.AIAssistant
// File: PromptPresetsService.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.6
// Created: 2026-03-31
// License: GNU Affero General Public License v3.0 (AGPL-3.0)
// Description:
//     Manages prompt presets — load/save from presets.json, includes 6 built-in defaults.
// ==========================================================
using System.IO;
using System.Text.Json;
using WpfHexEditor.Plugins.AIAssistant.Options;

namespace WpfHexEditor.Plugins.AIAssistant.Presets;

public sealed class PromptPresetsService
{
    private static PromptPresetsService? _instance;
    public static PromptPresetsService Instance => _instance ??= new();

    public List<PromptPreset> Presets { get; private set; } = [];

    private static readonly JsonSerializerOptions s_json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task LoadAsync()
    {
        var path = AIAssistantOptions.PresetsPath;
        if (File.Exists(path))
        {
            try
            {
                var json = await File.ReadAllTextAsync(path);
                Presets = JsonSerializer.Deserialize<List<PromptPreset>>(json, s_json) ?? [];
            }
            catch
            {
                Presets = [];
            }
        }

        // Ensure built-in presets exist
        EnsureBuiltIns();
    }

    public async Task SaveAsync()
    {
        var dir = Path.GetDirectoryName(AIAssistantOptions.PresetsPath)!;
        Directory.CreateDirectory(dir);
        var json = JsonSerializer.Serialize(Presets, s_json);
        await File.WriteAllTextAsync(AIAssistantOptions.PresetsPath, json);
    }

    public void Add(PromptPreset preset)
    {
        Presets.Add(preset);
        _ = SaveAsync();
    }

    public void Remove(string presetId)
    {
        Presets.RemoveAll(p => p.Id == presetId && !p.IsBuiltIn);
        _ = SaveAsync();
    }

    private void EnsureBuiltIns()
    {
        var builtIns = GetBuiltInPresets();
        foreach (var bi in builtIns)
        {
            if (!Presets.Any(p => p.Id == bi.Id))
                Presets.Insert(0, bi);
        }
    }

    private static List<PromptPreset> GetBuiltInPresets() =>
    [
        new()
        {
            Id = "builtin-explain",
            Name = "Explain this code",
            Prompt = "Explain this code in detail.",
            AutoInjectMentions = ["@selection"],
            IconGlyph = "\uE946",
            Order = 1,
            IsBuiltIn = true
        },
        new()
        {
            Id = "builtin-tests",
            Name = "Write unit tests",
            Prompt = "Generate comprehensive xUnit tests for this code.",
            AutoInjectMentions = ["@selection"],
            IconGlyph = "\uE9D5",
            Order = 2,
            IsBuiltIn = true
        },
        new()
        {
            Id = "builtin-review",
            Name = "Review for bugs",
            Prompt = "Review this code for bugs, edge cases, and potential improvements.",
            AutoInjectMentions = ["@selection"],
            IconGlyph = "\uE7BA",
            Order = 3,
            IsBuiltIn = true
        },
        new()
        {
            Id = "builtin-docs",
            Name = "Add XML documentation",
            Prompt = "Add complete XML documentation to all public members.",
            AutoInjectMentions = ["@selection"],
            IconGlyph = "\uE8A5",
            Order = 4,
            IsBuiltIn = true
        },
        new()
        {
            Id = "builtin-refactor",
            Name = "Refactor for readability",
            Prompt = "Refactor this code for readability. Keep behavior identical. Show diff.",
            AutoInjectMentions = ["@selection"],
            IconGlyph = "\uE70F",
            Order = 5,
            IsBuiltIn = true
        },
        new()
        {
            Id = "builtin-hex-format",
            Name = "Explain binary format",
            Prompt = "Explain the binary format structure of this data.",
            AutoInjectMentions = ["@hex", "@selection"],
            IconGlyph = "\uE9D9",
            Order = 6,
            IsBuiltIn = true
        }
    ];
}
