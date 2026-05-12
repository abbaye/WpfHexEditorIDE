// ==========================================================
// Project: WpfHexEditor.App
// File: Analysis/Services/CodeAnalysisOptionsService.cs
// Description: Loads and persists CodeAnalysisOptions to/from the IDE
//              settings folder (.ide/code-analysis-options.json).
// ==========================================================

using System.IO;
using System.Text.Json;
using WpfHexEditor.App.Analysis.Models;

namespace WpfHexEditor.App.Analysis.Services;

internal sealed class CodeAnalysisOptionsService
{
    private CodeAnalysisOptions _options = new();

    public CodeAnalysisOptions Options => _options;

    public void Load()
    {
        var path = GetPath();
        if (!File.Exists(path)) return;

        try
        {
            var json = File.ReadAllText(path);
            _options = JsonSerializer.Deserialize<CodeAnalysisOptions>(json, AnalysisJson.Default) ?? new();

            // Ensure new rules added since the user last saved are present
            foreach (var defaultRule in CodeAnalysisOptions.DefaultRules())
            {
                if (!_options.Rules.Any(r => r.RuleId == defaultRule.RuleId))
                    _options.Rules.Add(defaultRule);
            }

            // Phase 11 migration — back-fill or correct any rule whose Category
            // disagrees with the canonical mapping from RuleId. Tolerates both
            // pre-Phase-11 JSON (no Category field → enum default applies) and
            // user-edited rows where the value drifted.
            foreach (var r in _options.Rules)
            {
                var canonical = RuleCategoryHelper.FromRuleId(r.RuleId);
                if (r.Category != canonical) r.Category = canonical;
            }
        }
        catch
        {
            _options = new CodeAnalysisOptions();
        }
    }

    public void Save()
    {
        var path = GetPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(_options, AnalysisJson.Default));
    }

    internal void SetSolutionDirectory(string solutionDir)
    {
        _solutionDir = solutionDir;
    }

    private string _solutionDir = string.Empty;

    private string GetPath()
    {
        var baseDir = string.IsNullOrEmpty(_solutionDir)
            ? AppDomain.CurrentDomain.BaseDirectory
            : _solutionDir;

        return Path.Combine(baseDir, ".ide", "code-analysis-options.json");
    }
}
