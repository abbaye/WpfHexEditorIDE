// ==========================================================
// Project: WpfHexEditor.App
// File: Analysis/Services/SarifExporter.cs
// Description: Serializes a CodeAnalysisReport to SARIF 2.1.0 JSON.
//              GitHub Actions and Azure DevOps consume SARIF natively
//              for inline PR annotations and security tab integration.
// Architecture Notes:
//     Stateless. Hand-rolled minimal SARIF emitter — no extra deps.
// ==========================================================

using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using WpfHexEditor.App.Analysis.Models;
using Severity = WpfHexEditor.App.Analysis.Models.DiagnosticSeverity;

namespace WpfHexEditor.App.Analysis.Services;

internal static class SarifExporter
{
    internal static void Export(CodeAnalysisReport report, string outputPath)
    {
        var rules = report.Diagnostics
            .Select(d => d.Id).Distinct().OrderBy(x => x, StringComparer.Ordinal)
            .Select(BuildRuleDescriptor)
            .ToArray();

        var results = new JsonArray();
        foreach (var d in report.Diagnostics)
        {
            results.Add(new JsonObject
            {
                ["ruleId"]  = d.Id,
                ["level"]   = ToSarifLevel(d.Severity),
                ["message"] = new JsonObject { ["text"] = d.Message },
                ["locations"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["physicalLocation"] = new JsonObject
                        {
                            ["artifactLocation"] = new JsonObject { ["uri"] = ToFileUri(d.FilePath) },
                            ["region"]           = new JsonObject
                            {
                                ["startLine"]   = Math.Max(1, d.Line),
                                ["startColumn"] = Math.Max(1, d.Column),
                            },
                        },
                    },
                },
            });
        }

        var rulesArray = new JsonArray();
        foreach (var r in rules) rulesArray.Add(r);

        var sarif = new JsonObject
        {
            ["$schema"] = "https://schemastore.azurewebsites.net/schemas/json/sarif-2.1.0.json",
            ["version"] = "2.1.0",
            ["runs"]    = new JsonArray
            {
                new JsonObject
                {
                    ["tool"] = new JsonObject
                    {
                        ["driver"] = new JsonObject
                        {
                            ["name"]            = "WpfHexEditor.CodeAnalysis",
                            ["version"]         = "1.0.0",
                            ["informationUri"]  = "https://github.com/abbaye/WpfHexEditorControl",
                            ["rules"]           = rulesArray,
                        },
                    },
                    ["results"] = results,
                },
            },
        };

        var json = sarif.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        File.WriteAllText(outputPath, json);
    }

    private static JsonObject BuildRuleDescriptor(string ruleId)
    {
        var info = RuleMetadata.Get(ruleId);
        if (info is null)
        {
            return new JsonObject
            {
                ["id"]   = ruleId,
                ["name"] = ruleId,
                ["shortDescription"]     = new JsonObject { ["text"] = ruleId },
                ["defaultConfiguration"] = new JsonObject { ["level"] = "warning" },
            };
        }

        var tags = new JsonArray();
        foreach (var t in info.Tags) tags.Add(t);

        return new JsonObject
        {
            ["id"]   = ruleId,
            ["name"] = info.Name,
            ["shortDescription"] = new JsonObject { ["text"] = info.ShortDescription },
            ["fullDescription"]  = new JsonObject { ["text"] = info.FullDescription },
            ["helpUri"]          = info.HelpUri,
            ["help"] = new JsonObject
            {
                ["text"]     = info.FullDescription,
                ["markdown"] = $"**{info.Name}** — {info.ShortDescription}\n\n{info.FullDescription}\n\n[Documentation]({info.HelpUri})",
            },
            ["defaultConfiguration"] = new JsonObject { ["level"] = ToSarifLevel(SeverityFor(info.DefaultLevel)) },
            ["properties"]           = new JsonObject
            {
                ["category"] = RuleCategoryHelper.FromRuleId(ruleId).ToString(),
                ["tags"]     = tags,
            },
        };
    }

    private static Severity SeverityFor(RuleSeverity rs) => rs switch
    {
        RuleSeverity.Error   => Severity.Error,
        RuleSeverity.Warning => Severity.Warning,
        _                    => Severity.Info,
    };

    private static string ToSarifLevel(Severity s) => s switch
    {
        Severity.Error   => "error",
        Severity.Warning => "warning",
        _                => "note",
    };

    private static string ToFileUri(string path)
    {
        if (string.IsNullOrEmpty(path)) return "";
        // Diagnostics may carry relative paths (e.g. baseline-relativized) — emit as-is.
        if (!Path.IsPathRooted(path)) return path.Replace('\\', '/');
        try { return new Uri(path).AbsoluteUri; } catch { return path.Replace('\\', '/'); }
    }
}
