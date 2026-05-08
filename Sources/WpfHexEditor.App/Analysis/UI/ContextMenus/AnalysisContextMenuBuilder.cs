// ==========================================================
// Project: WpfHexEditor.App
// File: Analysis/UI/ContextMenus/AnalysisContextMenuBuilder.cs
// Description: Factories that produce ContextMenu instances for each kind of
//              row in the analysis report (project / file / method / issue /
//              coupling / duplication / dead symbol / treemap rectangle).
//              All MenuItem headers are pulled from AppResources via DynamicResource
//              wherever possible — strings here are fallback English.
// Architecture Notes:
//     Each builder takes the row item plus the collaborators it needs (host
//     service for navigation, view-model for filtering, options service for
//     toggling rules, etc.) and returns a ready-to-show ContextMenu.
// ==========================================================

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WpfHexEditor.App.Analysis.Models;
using WpfHexEditor.App.Analysis.UI.ViewModels;
using WpfHexEditor.App.Properties;
using WpfHexEditor.SDK.Contracts.Services;

namespace WpfHexEditor.App.Analysis.UI.ContextMenus;

internal sealed class AnalysisContextMenuBuilder
{
    private readonly IDocumentHostService?         _docHost;
    private readonly CodeAnalysisReportViewModel   _vm;
    private readonly Func<string, Task>?           _scopedReRun;  // (path) → re-run scoped

    internal AnalysisContextMenuBuilder(
        IDocumentHostService?       docHost,
        CodeAnalysisReportViewModel vm,
        Func<string, Task>?         scopedReRun = null)
    {
        _docHost     = docHost;
        _vm          = vm;
        _scopedReRun = scopedReRun;
    }

    // ── Project row ─────────────────────────────────────────────────────────

    internal ContextMenu BuildForProject(ProjectMetrics project)
    {
        var menu = new ContextMenu();
        Add(menu, AppResources.CodeAnalysis_ContextMenu_OpenProjectFolder,   () => AnalysisContextMenuActions.OpenContainingFolder(project.ProjectPath));
        Add(menu, AppResources.CodeAnalysis_ContextMenu_RevealInExplorer,    () => AnalysisContextMenuActions.RevealInExplorer(project.ProjectPath));
        AddSeparator(menu);
        if (_scopedReRun is not null)
            Add(menu, AppResources.CodeAnalysis_ContextMenu_ReanalyzeProject,  () => _ = _scopedReRun(project.ProjectPath));
        Add(menu, AppResources.CodeAnalysis_ContextMenu_FilterAllByProject,    () => _vm.ProjectFilter = project.ProjectName);
        AddSeparator(menu);
        Add(menu, AppResources.CodeAnalysis_ContextMenu_CopyProjectName,       () => AnalysisContextMenuActions.Copy(project.ProjectName));
        Add(menu, AppResources.CodeAnalysis_ContextMenu_CopyMetricsCsv,        () => AnalysisContextMenuActions.Copy(AnalysisContextMenuActions.FormatProjectAsCsv(project)));
        return menu;
    }

    // ── File row (FileMetrics or FileMetricsViewModel) ──────────────────────

    internal ContextMenu BuildForFile(FileMetrics file)
    {
        var menu = new ContextMenu();
        Add(menu, AppResources.CodeAnalysis_ContextMenu_Open,                  () => AnalysisContextMenuActions.OpenFile(_docHost, file.FilePath, 1));
        Add(menu, AppResources.CodeAnalysis_ContextMenu_RevealInExplorer,      () => AnalysisContextMenuActions.RevealInExplorer(file.FilePath));
        Add(menu, AppResources.CodeAnalysis_ContextMenu_OpenContainingFolder,  () => AnalysisContextMenuActions.OpenContainingFolder(file.FilePath));
        AddSeparator(menu);
        if (_scopedReRun is not null)
            Add(menu, AppResources.CodeAnalysis_ContextMenu_ReanalyzeFile,     () => _ = _scopedReRun(file.FilePath));
        Add(menu, AppResources.CodeAnalysis_ContextMenu_FilterIssuesByFile,    () => _vm.IssueFilter = file.FileName);
        AddSeparator(menu);
        Add(menu, AppResources.CodeAnalysis_ContextMenu_CopyFullPath,          () => AnalysisContextMenuActions.Copy(file.FilePath));
        Add(menu, AppResources.CodeAnalysis_ContextMenu_CopyFileName,          () => AnalysisContextMenuActions.Copy(file.FileName));
        return menu;
    }

    internal ContextMenu BuildForFileVm(FileMetricsViewModel fm)
    {
        var menu = new ContextMenu();
        Add(menu, AppResources.CodeAnalysis_ContextMenu_Open,                  () => AnalysisContextMenuActions.OpenFile(_docHost, fm.FilePath, 1));
        Add(menu, AppResources.CodeAnalysis_ContextMenu_RevealInExplorer,      () => AnalysisContextMenuActions.RevealInExplorer(fm.FilePath));
        AddSeparator(menu);
        Add(menu, AppResources.CodeAnalysis_ContextMenu_FilterIssuesByFile,    () => _vm.IssueFilter = fm.FileName);
        Add(menu, AppResources.CodeAnalysis_ContextMenu_CopyFullPath,          () => AnalysisContextMenuActions.Copy(fm.FilePath));
        return menu;
    }

    // ── Method row ──────────────────────────────────────────────────────────

    internal ContextMenu BuildForMethod(MethodMetrics method, string? filePath)
    {
        var menu = new ContextMenu();
        if (!string.IsNullOrEmpty(filePath))
        {
            Add(menu, AppResources.CodeAnalysis_ContextMenu_OpenAtMethodDecl,  () => AnalysisContextMenuActions.OpenFile(_docHost, filePath, method.Line));
            AddSeparator(menu);
        }
        Add(menu, AppResources.CodeAnalysis_ContextMenu_CopyMethodName,        () => AnalysisContextMenuActions.Copy(method.Name));
        Add(menu, AppResources.CodeAnalysis_ContextMenu_CopyFqn,               () => AnalysisContextMenuActions.Copy(method.FullyQualifiedName));
        return menu;
    }

    // ── Issue row ───────────────────────────────────────────────────────────

    internal ContextMenu BuildForIssue(IssueViewModel issue)
    {
        var menu = new ContextMenu();
        Add(menu, AppResources.CodeAnalysis_ContextMenu_OpenAtIssueLocation,   () => AnalysisContextMenuActions.OpenFile(_docHost, issue.FilePath, issue.Line));
        AddSeparator(menu);
        Add(menu, AppResources.CodeAnalysis_ContextMenu_FilterByRule,          () => _vm.IssueFilter = issue.Id);
        Add(menu, AppResources.CodeAnalysis_ContextMenu_FilterByFile,          () => _vm.IssueFilter = issue.FileName);
        AddSeparator(menu);
        Add(menu, AppResources.CodeAnalysis_ContextMenu_SuppressInline,
            () => AnalysisContextMenuActions.AddInlineSuppressMarker(issue.FilePath, issue.Line, issue.Id));
        AddSeparator(menu);
        Add(menu, AppResources.CodeAnalysis_ContextMenu_CopyAsMarkdown,        () => AnalysisContextMenuActions.Copy(AnalysisContextMenuActions.FormatIssueAsMarkdown(issue)));
        Add(menu, AppResources.CodeAnalysis_ContextMenu_CopyIssueId,           () => AnalysisContextMenuActions.Copy(issue.Id));
        return menu;
    }

    // ── Coupling row ────────────────────────────────────────────────────────

    internal ContextMenu BuildForCoupling(CouplingMetrics coupling)
    {
        var menu = new ContextMenu();
        Add(menu, AppResources.CodeAnalysis_ContextMenu_OpenSourceFile,        () => AnalysisContextMenuActions.OpenFile(_docHost, coupling.FilePath, coupling.Line));
        AddSeparator(menu);
        Add(menu, AppResources.CodeAnalysis_ContextMenu_CopyTypeName,          () => AnalysisContextMenuActions.Copy(coupling.TypeName));
        Add(menu, AppResources.CodeAnalysis_ContextMenu_CopyDependencyList,    () => AnalysisContextMenuActions.Copy(string.Join("\n", coupling.DependsOn)));
        return menu;
    }

    // ── Duplication group ──────────────────────────────────────────────────

    internal ContextMenu BuildForDuplication(DuplicationGroup group)
    {
        var menu = new ContextMenu();
        if (group.Occurrences.Count > 0)
        {
            var first = group.Occurrences[0];
            Add(menu, AppResources.CodeAnalysis_ContextMenu_OpenFirstOccurrence, () => AnalysisContextMenuActions.OpenFile(_docHost, first.FilePath, first.StartLine));
        }
        AddSeparator(menu);
        Add(menu, string.Format(AppResources.CodeAnalysis_ContextMenu_CopyLocations, group.Occurrences.Count),
            () => AnalysisContextMenuActions.Copy(string.Join("\n", group.Occurrences.Select(o => $"{o.FilePath}:{o.StartLine}"))));
        return menu;
    }

    // ── Dead symbol ────────────────────────────────────────────────────────

    internal ContextMenu BuildForDeadSymbol(DeadSymbol dead)
    {
        var menu = new ContextMenu();
        Add(menu, AppResources.CodeAnalysis_ContextMenu_OpenAtDeclaration,     () => AnalysisContextMenuActions.OpenFile(_docHost, dead.FilePath, dead.Line));
        AddSeparator(menu);
        Add(menu, AppResources.CodeAnalysis_ContextMenu_CopySymbolName,        () => AnalysisContextMenuActions.Copy(dead.Name));
        return menu;
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static void Add(ContextMenu menu, string header, Action onClick)
    {
        var item = new MenuItem { Header = header };
        item.Click += (_, _) => onClick();
        menu.Items.Add(item);
    }

    private static void AddSeparator(ContextMenu menu)
        => menu.Items.Add(new Separator());
}
