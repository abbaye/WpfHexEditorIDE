// ==========================================================
// Project: WpfHexEditor.App
// File: Analysis/UI/ContextMenus/AnalysisContextMenuActions.cs
// Description: Action handlers used by every context menu in the Code
//              Analysis report pane. Pure: no XAML, no DataContext leakage.
// Architecture Notes:
//     Each action takes (item, services) and returns void / does its thing.
//     UI-thread calls only — invoked from MenuItem.Click handlers.
// ==========================================================

using System.Diagnostics;
using System.IO;
using System.Windows;
using WpfHexEditor.App.Analysis.Models;
using WpfHexEditor.App.Analysis.UI.ViewModels;
using WpfHexEditor.SDK.Contracts.Services;

namespace WpfHexEditor.App.Analysis.UI.ContextMenus;

internal static class AnalysisContextMenuActions
{
    // ── Generic ──────────────────────────────────────────────────────────────

    internal static void Copy(string text)
    {
        try { Clipboard.SetText(text ?? string.Empty); } catch { /* clipboard locked */ }
    }

    internal static void OpenFile(IDocumentHostService? docHost, string filePath, int line = 1)
    {
        if (docHost is null || string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return;
        docHost.ActivateAndNavigateTo(filePath, Math.Max(1, line), 1);
    }

    /// <summary>Open a URL in the user's default browser. Silent on failure.</summary>
    internal static void OpenUrl(string url)
    {
        if (string.IsNullOrEmpty(url)) return;
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch { /* browser missing / URL malformed — never crash the menu */ }
    }

    internal static void RevealInExplorer(string filePath)
    {
        if (string.IsNullOrEmpty(filePath)) return;
        try
        {
            if (File.Exists(filePath))
                Process.Start("explorer.exe", $"/select,\"{filePath}\"");
            else if (Directory.Exists(filePath))
                Process.Start("explorer.exe", $"\"{filePath}\"");
        }
        catch { /* explorer missing or path invalid */ }
    }

    internal static void OpenContainingFolder(string filePath)
    {
        var folder = Path.GetDirectoryName(filePath);
        if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder)) return;
        try { Process.Start("explorer.exe", $"\"{folder}\""); } catch { }
    }

    // ── Project ──────────────────────────────────────────────────────────────

    internal static string FormatProjectAsCsv(ProjectMetrics p)
        => string.Join(",",
            p.ProjectName, p.TotalFiles, p.TotalLines, p.TypeCount, p.MethodCount,
            p.AvgCyclomaticComplexity, p.MaxCyclomaticComplexity, p.Score, p.Grade);

    // ── Issue ────────────────────────────────────────────────────────────────

    internal static string FormatIssueAsMarkdown(IssueViewModel i)
        => $"- **[{i.Id}]** ({i.Severity}) `{i.FileName}:{i.Line}` — {i.Message}";

    // ── Suppress (writes a marker comment line above the issue) ──────────────

    internal static bool AddInlineSuppressMarker(string filePath, int line, string ruleId, string reason = "")
    {
        if (!File.Exists(filePath) || line < 1) return false;
        try
        {
            var lines = File.ReadAllLines(filePath).ToList();
            if (line > lines.Count) return false;
            string indent = new(' ', lines[line - 1].TakeWhile(char.IsWhiteSpace).Count());
            string marker = string.IsNullOrEmpty(reason)
                ? $"{indent}// CodeAnalysis: suppress {ruleId}"
                : $"{indent}// CodeAnalysis: suppress {ruleId} — {reason}";
            lines.Insert(line - 1, marker);
            File.WriteAllLines(filePath, lines);
            return true;
        }
        catch { return false; }
    }
}
