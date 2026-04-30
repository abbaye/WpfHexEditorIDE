// ==========================================================
// Project: WpfHexEditor.ProjectSystem
// File: Documents/References/ReferenceManagerViewModel.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-24
// Description:
//     ViewModel for the VS-like Reference Manager document tab.
//     Displays project/assembly/package/analyzer references grouped by category.
//     Supports Add Project Reference, Add Assembly Reference, Remove, and Remove Unused.
//     Mutation is performed via CsprojReferenceWriter (XDocument write-back).
//
// Architecture Notes:
//     Pattern:    MVVM with INotifyPropertyChanged
//     Mutation:   CsprojReferenceWriter (pure XDocument â€” same pattern as CsprojPackageWriter)
//     Event:      ProjectModified raised after every write-back for SE tree refresh.
// ==========================================================

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using WpfHexEditor.Editor.Core;
using WpfHexEditor.Core.ProjectSystem.Properties;
using WpfHexEditor.Core.ProjectSystem.Services.References;
using WpfHexEditor.Core.ViewModels;

namespace WpfHexEditor.Core.ProjectSystem.Documents.References;

// ── Entry ViewModel ───────────────────────────────────────────────────────────

/// <summary>Kind of reference entry displayed in the Reference Manager.</summary>
public enum ReferenceKind { Project, Assembly, Package, Analyzer }

/// <summary>Single reference entry shown in the Reference Manager list.</summary>
public sealed class ReferenceEntryVm : ViewModelBase
{
    private bool _isSelected;

    public ReferenceKind Kind        { get; init; }
    public string        Name        { get; init; } = "";
    public string        Detail      { get; init; } = "";   // version / hint-path / path
    /// <summary>Segoe MDL2 glyph for the kind icon.</summary>
    public string        KindIcon    { get; init; } = "\uE8A5";
    public string        KindColor   { get; init; } = "#9AABB5";
    /// <summary>Raw key used for removal (absolute path for project refs, name for assembly refs).</summary>
    public string        RemoveKey   { get; init; } = "";
    public bool          IsRemovable { get; init; } = true;

    public bool IsSelected
    {
        get => _isSelected;
        set { _isSelected = value; OnPropertyChanged(); }
    }

}

// ── Group ViewModel ───────────────────────────────────────────────────────────

/// <summary>Category group (Project References / Assemblies / Packages / Analyzers).</summary>
public sealed class ReferenceGroupVm : ViewModelBase
{
    private bool _isExpanded = true;

    public string                             Header     { get; init; } = "";
    public string                             Icon       { get; init; } = "\uE8B7";
    public ObservableCollection<ReferenceEntryVm> Items { get; } = [];

    public bool IsExpanded
    {
        get => _isExpanded;
        set { _isExpanded = value; OnPropertyChanged(); }
    }

}

// ── Main ViewModel ────────────────────────────────────────────────────────────

/// <summary>ViewModel for <see cref="ReferenceManagerDocument"/>.</summary>
public sealed class ReferenceManagerViewModel : ViewModelBase
{
    private readonly IProject   _project;
    private readonly ISolution? _solution;
    private string  _searchText  = "";
    private string  _statusText  = "";

    // ── Constructor ───────────────────────────────────────────────────────────

    public ReferenceManagerViewModel(IProject project, ISolution? solution)
    {
        _project  = project;
        _solution = solution;

        AddProjectReferenceCommand  = new RelayCommand(_ => OnAddProjectReference());
        AddAssemblyReferenceCommand = new RelayCommand(_ => OnAddAssemblyReference());
        RemoveSelectedCommand       = new RelayCommand(_ => OnRemoveSelected(),
                                         _ => Groups.SelectMany(g => g.Items).Any(e => e.IsSelected && e.IsRemovable));
        RemoveEntryCommand          = new RelayCommand(p => { if (p is ReferenceEntryVm e) OnRemoveEntry(e); });
        RemoveUnusedCommand         = new RelayCommand(_ => OnRemoveUnused());

        Reload();
    }

    // ── Properties ────────────────────────────────────────────────────────────

    public ObservableCollection<ReferenceGroupVm> Groups { get; } = [];

    public string SearchText
    {
        get => _searchText;
        set
        {
            _searchText = value;
            OnPropertyChanged();
            ApplySearch();
        }
    }

    public string StatusText
    {
        get => _statusText;
        private set { _statusText = value; OnPropertyChanged(); }
    }

    public string ProjectName => _project.Name;

    // ── Commands ──────────────────────────────────────────────────────────────

    public ICommand AddProjectReferenceCommand  { get; }
    public ICommand AddAssemblyReferenceCommand { get; }
    public ICommand RemoveSelectedCommand       { get; }
    public ICommand RemoveEntryCommand          { get; }
    public ICommand RemoveUnusedCommand         { get; }

    public event EventHandler? ProjectModified;

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Reloads all reference groups from the project model.</summary>
    public void Reload()
    {
        Groups.Clear();

        if (_project is not IProjectWithReferences refs)
        {
            StatusText = "This project type does not support reference management.";
            return;
        }

        // Project References
        if (refs.ProjectReferences.Count > 0)
        {
            var g = new ReferenceGroupVm { Header = "Project References", Icon = "\uEA3C" };
            foreach (var path in refs.ProjectReferences)
                g.Items.Add(new ReferenceEntryVm
                {
                    Kind      = ReferenceKind.Project,
                    Name      = Path.GetFileNameWithoutExtension(path),
                    Detail    = path,
                    KindIcon  = "\uEA3C",
                    KindColor = "#6CA8C4",
                    RemoveKey = path,
                });
            Groups.Add(g);
        }

        // Assembly References (non-framework only by default â€” framework refs are system-managed)
        var asmRefs = refs.AssemblyReferences.Where(a => !a.IsFrameworkRef).ToList();
        if (asmRefs.Count > 0)
        {
            var g = new ReferenceGroupVm { Header = "Assemblies", Icon = "\uE7EE" };
            foreach (var asm in asmRefs)
                g.Items.Add(new ReferenceEntryVm
                {
                    Kind      = ReferenceKind.Assembly,
                    Name      = asm.Name,
                    Detail    = asm.HintPath ?? asm.Version ?? "",
                    KindIcon  = "\uE7EE",
                    KindColor = "#9AABB5",
                    RemoveKey = asm.Name,
                });
            Groups.Add(g);
        }

        // Package References (read-only â€” managed via NuGet Manager)
        if (refs.PackageReferences.Count > 0)
        {
            var g = new ReferenceGroupVm { Header = "Packages (NuGet)", Icon = "\uE7B8", IsExpanded = false };
            foreach (var pkg in refs.PackageReferences)
                g.Items.Add(new ReferenceEntryVm
                {
                    Kind        = ReferenceKind.Package,
                    Name        = pkg.Id,
                    Detail      = pkg.Version ?? "",
                    KindIcon    = "\uE7B8",
                    KindColor   = "#C8A018",
                    RemoveKey   = pkg.Id,
                    IsRemovable = false,    // use NuGet Manager for packages
                });
            Groups.Add(g);
        }

        // Analyzers (read-only)
        if (refs.AnalyzerReferences.Count > 0)
        {
            var g = new ReferenceGroupVm { Header = "Analyzers", Icon = "\uE9D9", IsExpanded = false };
            foreach (var az in refs.AnalyzerReferences)
                g.Items.Add(new ReferenceEntryVm
                {
                    Kind        = ReferenceKind.Analyzer,
                    Name        = Path.GetFileNameWithoutExtension(az.HintPath),
                    Detail      = az.HintPath,
                    KindIcon    = "\uE9D9",
                    KindColor   = "#B5CEA8",
                    RemoveKey   = az.HintPath,
                    IsRemovable = false,
                });
            Groups.Add(g);
        }

        UpdateStatus();
    }

    // ── Command handlers ──────────────────────────────────────────────────────

    private void OnAddProjectReference()
    {
        if (_project.ProjectFilePath is null) return;

        var dlg = new OpenFileDialog
        {
            Title            = ProjectSystemResources.Dialog_AddProjectReference,
            Filter           = ProjectSystemResources.Filter_ProjectFiles,
            Multiselect      = true,
            InitialDirectory = Path.GetDirectoryName(_project.ProjectFilePath),
        };

        if (dlg.ShowDialog() != true) return;

        foreach (var path in dlg.FileNames)
            CsprojReferenceWriter.AddProjectReference(_project.ProjectFilePath, path);

        ProjectModified?.Invoke(this, EventArgs.Empty);
        Reload();
    }

    private void OnAddAssemblyReference()
    {
        if (_project.ProjectFilePath is null) return;

        var dlg = new OpenFileDialog
        {
            Title            = ProjectSystemResources.Dialog_AddAssemblyReference,
            Filter           = ProjectSystemResources.Filter_AssemblyFiles,
            Multiselect      = true,
        };

        if (dlg.ShowDialog() != true) return;

        foreach (var path in dlg.FileNames)
        {
            var name = Path.GetFileNameWithoutExtension(path);
            CsprojReferenceWriter.AddAssemblyReference(_project.ProjectFilePath, name, path);
        }

        ProjectModified?.Invoke(this, EventArgs.Empty);
        Reload();
    }

    private void OnRemoveEntry(ReferenceEntryVm entry)
    {
        if (!entry.IsRemovable || _project.ProjectFilePath is null) return;

        switch (entry.Kind)
        {
            case ReferenceKind.Project:
                CsprojReferenceWriter.RemoveProjectReference(_project.ProjectFilePath, entry.RemoveKey);
                break;
            case ReferenceKind.Assembly:
                CsprojReferenceWriter.RemoveAssemblyReference(_project.ProjectFilePath, entry.RemoveKey);
                break;
            default: return;
        }

        ProjectModified?.Invoke(this, EventArgs.Empty);
        Reload();
    }

    private void OnRemoveSelected()
    {
        if (_project.ProjectFilePath is null) return;

        var toRemove = Groups.SelectMany(g => g.Items)
                             .Where(e => e.IsSelected && e.IsRemovable)
                             .ToList();

        if (toRemove.Count == 0) return;

        var msg = toRemove.Count == 1
            ? $"Remove reference '{toRemove[0].Name}'?"
            : $"Remove {toRemove.Count} selected references?";

        if (MessageBox.Show(msg, ProjectSystemResources.Dialog_RemoveReferenceTitle, MessageBoxButton.OKCancel,
                MessageBoxImage.Question) != MessageBoxResult.OK) return;

        foreach (var entry in toRemove)
        {
            switch (entry.Kind)
            {
                case ReferenceKind.Project:
                    CsprojReferenceWriter.RemoveProjectReference(_project.ProjectFilePath, entry.RemoveKey);
                    break;
                case ReferenceKind.Assembly:
                    CsprojReferenceWriter.RemoveAssemblyReference(_project.ProjectFilePath, entry.RemoveKey);
                    break;
            }
        }

        ProjectModified?.Invoke(this, EventArgs.Empty);
        Reload();
    }

    private void OnRemoveUnused()
    {
        if (_project.ProjectFilePath is null) return;
        if (_project is not IProjectWithReferences refs) return;

        // Collect all used assembly names from source files (simple heuristic: "using X" or "X." patterns)
        var projectDir = Path.GetDirectoryName(_project.ProjectFilePath) ?? string.Empty;
        var sourceFiles = Directory.GetFiles(projectDir, "*.cs", SearchOption.AllDirectories)
            .Concat(Directory.GetFiles(projectDir, "*.vb", SearchOption.AllDirectories))
            .ToList();

        var allSource = string.Join("\n", sourceFiles.Select(f =>
        {
            try { return File.ReadAllText(f); }
            catch { return ""; }
        }));

        // Find assembly refs whose simple name never appears in source
        var unused = refs.AssemblyReferences
            .Where(a => !a.IsFrameworkRef)
            .Where(a => !allSource.Contains(a.Name, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (unused.Count == 0)
        {
            MessageBox.Show(ProjectSystemResources.Message_NoUnusedReferences, ProjectSystemResources.Dialog_RemoveUnusedReferencesTitle,
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var names = string.Join("\n  â€¢ ", unused.Select(a => a.Name));
        if (MessageBox.Show($"{ProjectSystemResources.Confirm_RemoveUnusedReferences}\n\n  \u2022 {names}",
                ProjectSystemResources.Dialog_RemoveUnusedReferencesTitle, MessageBoxButton.OKCancel,
                MessageBoxImage.Question) != MessageBoxResult.OK) return;

        foreach (var asm in unused)
            CsprojReferenceWriter.RemoveAssemblyReference(_project.ProjectFilePath, asm.Name);

        ProjectModified?.Invoke(this, EventArgs.Empty);
        Reload();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void ApplySearch()
    {
        var q = _searchText.Trim();
        foreach (var group in Groups)
            foreach (var entry in group.Items)
                entry.IsSelected = false;   // clear selection on new search

        if (string.IsNullOrEmpty(q)) return;

        foreach (var group in Groups)
            foreach (var entry in group.Items)
                if (entry.Name.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    entry.Detail.Contains(q, StringComparison.OrdinalIgnoreCase))
                    entry.IsSelected = true;
    }

    private void UpdateStatus()
    {
        var total = Groups.Sum(g => g.Items.Count);
        var removable = Groups.Sum(g => g.Items.Count(e => e.IsRemovable));
        StatusText = $"{total} reference{(total == 1 ? "" : "s")}  â€¢  {removable} editable";
    }

    // ── INotifyPropertyChanged ────────────────────────────────────────────────


    // ── Inner RelayCommand ────────────────────────────────────────────────────

    private sealed class RelayCommand : ICommand
    {
        private readonly Action<object?> _execute;
        private readonly Func<object?, bool>? _canExecute;

        public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
        {
            _execute    = execute;
            _canExecute = canExecute;
        }

        public bool CanExecute(object? p) => _canExecute?.Invoke(p) ?? true;
        public void Execute(object? p)    => _execute(p);
        public event EventHandler? CanExecuteChanged
        {
            add    => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }
    }
}
