// ==========================================================
// Project: WpfHexEditor.ProjectSystem
// File: Documents/ProjectPropertiesViewModel.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     ViewModel for the VS-Like Project Properties document tab.
//     Exposes editable and read-only project metadata; drives the left
//     navigation list and right-panel section visibility.
//
// Architecture Notes:
//     Pattern: MVVM — INotifyPropertyChanged, RelayCommand
//     VS-specific metadata (TargetFramework, AssemblyName, etc.) is
//     read via reflection to avoid a direct dependency on the VS loader
//     plugin assembly.
// ==========================================================

using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using WpfHexEditor.Editor.Core;
using WpfHexEditor.ProjectSystem.Services;

namespace WpfHexEditor.ProjectSystem.Documents;

/// <summary>
/// ViewModel for <see cref="ProjectPropertiesDocument"/>.
/// </summary>
public sealed class ProjectPropertiesViewModel : INotifyPropertyChanged
{
    private readonly IProject         _project;
    private readonly ISolutionManager _solutionManager;

    // -----------------------------------------------------------------------
    // Backing fields for editable properties
    // -----------------------------------------------------------------------
    private string _projectName     = "";
    private string _assemblyName    = "";
    private string _defaultNs       = "";
    private string _targetFramework = "";
    private string _outputType      = "";
    private string _configuration   = "Debug";
    private string _platform        = "Any CPU";
    private string _outputPath      = @"bin\Debug\net8.0-windows\";
    private bool   _optimizeCode;
    private bool   _isDirty;
    private NavItem? _selectedSection;

    // -----------------------------------------------------------------------
    // Constructor
    // -----------------------------------------------------------------------

    public ProjectPropertiesViewModel(IProject project, ISolutionManager solutionManager)
    {
        _project          = project;
        _solutionManager  = solutionManager;

        // Read-only display info
        ProjectFilePath  = project.ProjectFilePath;
        ProjectDirectory = Path.GetDirectoryName(project.ProjectFilePath) ?? "";
        ProjectType      = project.ProjectType ?? "WpfHexEditor Project";
        Items            = project.Items;
        ItemCountText    = $"{project.Items.Count} item(s)";

        // Editable baseline
        _projectName = project.Name;

        // VS-specific metadata — resolved via reflection to avoid coupling
        var propType = project.GetType();
        string Get(string name)
        {
            try { return propType.GetProperty(name)?.GetValue(project) as string ?? ""; }
            catch { return ""; }
        }
        IEnumerable<string> GetList(string name)
        {
            try { return propType.GetProperty(name)?.GetValue(project) as IEnumerable<string> ?? []; }
            catch { return []; }
        }

        var fx = Get("TargetFramework");
        IsVsProject      = !string.IsNullOrEmpty(fx);
        _targetFramework = IsVsProject ? fx : "net8.0-windows";
        _assemblyName    = Get("AssemblyName") is { Length: > 0 } a ? a : project.Name;
        _defaultNs       = Get("RootNamespace") is { Length: > 0 } ns ? ns : project.Name;
        _outputType      = Get("OutputType")    is { Length: > 0 } o  ? o  : "Library";

        References = IsVsProject
            ? GetList("ProjectReferences")
                .Select(r => new ReferenceEntry(Path.GetFileNameWithoutExtension(r), "Projet"))
                .Concat(GetList("PackageReferences").Select(p => new ReferenceEntry(p, "NuGet")))
                .ToList()
            : [];

        // Navigation
        NavigationItems  = BuildNavItems(IsVsProject);
        _selectedSection = NavigationItems.FirstOrDefault(n => !n.IsHeader);

        SaveCommand = new PropertiesRelayCommand(async () => await SaveAsync(), () => IsDirty);
    }

    // -----------------------------------------------------------------------
    // Read-only display properties
    // -----------------------------------------------------------------------

    public string                       ProjectFilePath  { get; }
    public string                       ProjectDirectory { get; }
    public string                       ProjectType      { get; }
    public string                       ItemCountText    { get; }
    public IReadOnlyList<IProjectItem>  Items            { get; }
    public IReadOnlyList<ReferenceEntry> References      { get; }

    /// <summary>True when the loaded project exposes VS-specific metadata.</summary>
    public bool IsVsProject { get; }

    // -----------------------------------------------------------------------
    // Navigation
    // -----------------------------------------------------------------------

    public List<NavItem> NavigationItems { get; }

    public NavItem? SelectedSection
    {
        get => _selectedSection;
        set { _selectedSection = value; OnPropertyChanged(); OnPropertyChanged(nameof(ActiveSection)); }
    }

    /// <summary>SectionId of the currently selected nav item.</summary>
    public string ActiveSection => _selectedSection?.SectionId ?? "app-general";

    // -----------------------------------------------------------------------
    // Editable properties (all set IsDirty = true on change)
    // -----------------------------------------------------------------------

    public string ProjectName
    {
        get => _projectName;
        set { if (_projectName != value) { _projectName = value; MarkDirty(); OnPropertyChanged(); } }
    }

    public string AssemblyName
    {
        get => _assemblyName;
        set { if (_assemblyName != value) { _assemblyName = value; MarkDirty(); OnPropertyChanged(); } }
    }

    public string DefaultNamespace
    {
        get => _defaultNs;
        set { if (_defaultNs != value) { _defaultNs = value; MarkDirty(); OnPropertyChanged(); } }
    }

    public string TargetFramework
    {
        get => _targetFramework;
        set { if (_targetFramework != value) { _targetFramework = value; MarkDirty(); OnPropertyChanged(); } }
    }

    public string OutputType
    {
        get => _outputType;
        set { if (_outputType != value) { _outputType = value; MarkDirty(); OnPropertyChanged(); } }
    }

    public string Configuration
    {
        get => _configuration;
        set { if (_configuration != value) { _configuration = value; MarkDirty(); OnPropertyChanged(); } }
    }

    public string Platform
    {
        get => _platform;
        set { if (_platform != value) { _platform = value; MarkDirty(); OnPropertyChanged(); } }
    }

    public string OutputPath
    {
        get => _outputPath;
        set { if (_outputPath != value) { _outputPath = value; MarkDirty(); OnPropertyChanged(); } }
    }

    public bool OptimizeCode
    {
        get => _optimizeCode;
        set { if (_optimizeCode != value) { _optimizeCode = value; MarkDirty(); OnPropertyChanged(); } }
    }

    public bool IsDirty
    {
        get => _isDirty;
        private set { _isDirty = value; OnPropertyChanged(); ((PropertiesRelayCommand)SaveCommand).RaiseCanExecuteChanged(); }
    }

    // -----------------------------------------------------------------------
    // Commands
    // -----------------------------------------------------------------------

    public ICommand SaveCommand { get; }

    // -----------------------------------------------------------------------
    // Private helpers
    // -----------------------------------------------------------------------

    private void MarkDirty() => IsDirty = true;

    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(ProjectName)) return;

        if (!string.Equals(ProjectName, _project.Name, StringComparison.Ordinal))
            await _solutionManager.RenameProjectAsync(_project, ProjectName);

        IsDirty = false;
    }

    private static List<NavItem> BuildNavItems(bool isVsProject)
    {
        var items = new List<NavItem>
        {
            new("Application", "",                IsHeader: true),
            new("Général",     "app-general",     IsHeader: false),
            new("Dépendances", "app-dependencies", IsHeader: false),
        };

        if (isVsProject)
            items.Add(new("Ressources Win32", "app-win32", IsHeader: false));

        items.Add(new("Build",      "build",      IsHeader: false));
        items.Add(new("Éléments",   "items",      IsHeader: false));
        items.Add(new("Références", "references", IsHeader: false));

        if (isVsProject)
        {
            items.Add(new("Package",        "package",       IsHeader: false));
            items.Add(new("Analyse du code","code-analysis", IsHeader: false));
            items.Add(new("Débogage",       "debug",         IsHeader: false));
        }

        return items;
    }

    // -----------------------------------------------------------------------
    // INotifyPropertyChanged
    // -----------------------------------------------------------------------

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

// ---------------------------------------------------------------------------
// Supporting types
// ---------------------------------------------------------------------------

/// <summary>Item in the left navigation list.</summary>
public sealed record NavItem(string Label, string SectionId, bool IsHeader = false);

/// <summary>Flat DTO for the References list.</summary>
public sealed record ReferenceEntry(string Name, string RefType);

/// <summary>Simple async-capable relay command local to this feature.</summary>
internal sealed class PropertiesRelayCommand(Func<Task> executeAsync, Func<bool>? canExecute = null)
    : ICommand
{
    public event EventHandler? CanExecuteChanged;
    public bool CanExecute(object? parameter) => canExecute?.Invoke() ?? true;
    public void Execute(object? parameter)    => executeAsync();
    public void RaiseCanExecuteChanged()      => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
