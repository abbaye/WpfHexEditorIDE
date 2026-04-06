// ==========================================================
// Project: WpfHexEditor.ProjectSystem
// File: Documents/ProjectPropertiesViewModel.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     ViewModel for the VS-Like Project Properties document tab.
//     Exposes editable and read-only project metadata; drives the left
//     navigation list, section visibility, nav filter, and save workflow.
//
// Architecture Notes:
//     Pattern: MVVM â€” INotifyPropertyChanged, RelayCommand
//     VS-specific metadata (TargetFramework, AssemblyName, etc.) is
//     read via reflection to avoid a direct dependency on the VS loader
//     plugin assembly.
//     New properties (Phases 4-6): SaveCompleted pulse, NavFilter,
//     FilteredNavItems, GlobalUsings, AppIconPath, AppManifest,
//     LaunchProfiles, EnvironmentVariables, SelectedReference,
//     Add/Remove reference commands.
// ==========================================================

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Microsoft.Win32;
using WpfHexEditor.Editor.Core;
using WpfHexEditor.Core.ProjectSystem.Services;
using WpfHexEditor.Core.ViewModels;

namespace WpfHexEditor.Core.ProjectSystem.Documents;

/// <summary>
/// ViewModel for <see cref="ProjectPropertiesDocument"/>.
/// </summary>
public sealed class ProjectPropertiesViewModel : ViewModelBase
{
    private readonly IProject         _project;
    private readonly ISolutionManager _solutionManager;

    // -----------------------------------------------------------------------
    // Backing fields â€” editable properties
    // -----------------------------------------------------------------------
    private string  _projectName          = "";
    private string  _assemblyName         = "";
    private string  _defaultNs            = "";
    private string  _targetFramework      = "";
    private string  _outputType           = "";
    private string  _configuration        = "Debug";
    private string  _platform             = "Any CPU";
    private string  _outputPath           = @"bin\Debug\net8.0-windows\";
    private bool    _optimizeCode;
    private bool    _treatWarningsAsErrors;
    private bool    _enableCodeAnalysis;
    private bool    _codeAnalysisReleaseOnly;
    private string  _appIconPath          = "";
    private string  _appManifest          = "ParamÃ¨tres par dÃ©faut";
    private string  _packageId            = "";
    private string  _packageVersion       = "1.0.0";
    private string  _packageAuthors       = "";
    private string  _packageDescription   = "";
    private string  _launchArgs           = "";
    private string  _workingDirectory     = "";
    private string? _activeLaunchProfile;
    private bool    _isDirty;
    private bool    _saveCompleted;
    private NavItem? _selectedSection;
    private string  _navFilter            = "";

    // -----------------------------------------------------------------------
    // Constructor
    // -----------------------------------------------------------------------

    public ProjectPropertiesViewModel(IProject project, ISolutionManager solutionManager)
    {
        _project         = project;
        _solutionManager = solutionManager;

        // Read-only display info
        ProjectFilePath  = project.ProjectFilePath;
        ProjectDirectory = Path.GetDirectoryName(project.ProjectFilePath) ?? "";
        ProjectType      = project.ProjectType ?? "WpfHexEditor Project";
        Items            = project.Items;
        ItemCountText    = $"{project.Items.Count} item(s)";

        // Editable baseline
        _projectName = project.Name;

        // VS-specific metadata â€” resolved via reflection to avoid coupling with loader plugin
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

        // References list (editable â€” ObservableCollection)
        var initRefs = IsVsProject
            ? GetList("ProjectReferences")
                .Select(r => new ReferenceEntry(Path.GetFileNameWithoutExtension(r), "Projet"))
                .Concat(GetList("PackageReferences").Select(p => new ReferenceEntry(p, "NuGet")))
            : [];
        References = new ObservableCollection<ReferenceEntry>(initRefs);

        // Global usings (VS only)
        GlobalUsings = IsVsProject ? GetList("GlobalUsings").ToList() : [];

        // Launch profiles (VS only) â€” default list when not available via reflection
        var launchProfiles = IsVsProject
            ? GetList("LaunchProfiles").ToList()
            : new List<string>();
        if (!launchProfiles.Any()) launchProfiles.Add(project.Name);
        LaunchProfiles       = launchProfiles;
        _activeLaunchProfile = LaunchProfiles.FirstOrDefault();

        // Navigation
        NavigationItems  = BuildNavItems(IsVsProject);
        _selectedSection = NavigationItems.FirstOrDefault(n => !n.IsHeader);
        FilterNavItems();

        // Commands
        SaveCommand            = new PropertiesRelayCommand(async () => await SaveAsync(), () => IsDirty);
        AddNuGetCommand        = new PropertiesRelayCommand(async () => await AddNuGetAsync());
        AddProjectRefCommand   = new PropertiesRelayCommand(async () => await AddProjectRefAsync());
        RemoveReferenceCommand = new PropertiesRelayCommand(
            async () => await RemoveReferenceAsync(),
            () => SelectedReference is not null);
    }

    // -----------------------------------------------------------------------
    // Read-only display properties
    // -----------------------------------------------------------------------

    public string                       ProjectFilePath  { get; }
    public string                       ProjectDirectory { get; }
    public string                       ProjectType      { get; }
    public string                       ItemCountText    { get; }
    public IReadOnlyList<IProjectItem>  Items            { get; }
    public IReadOnlyList<string>        GlobalUsings     { get; }
    public List<string>                 LaunchProfiles   { get; }

    /// <summary>True when the loaded project exposes VS-specific metadata.</summary>
    public bool IsVsProject { get; }

    // -----------------------------------------------------------------------
    // Navigation
    // -----------------------------------------------------------------------

    public List<NavItem>                 NavigationItems  { get; }
    public ObservableCollection<NavItem> FilteredNavItems { get; } = [];

    public NavItem? SelectedSection
    {
        get => _selectedSection;
        set { _selectedSection = value; OnPropertyChanged(); OnPropertyChanged(nameof(ActiveSection)); }
    }

    /// <summary>SectionId of the currently selected nav item.</summary>
    public string ActiveSection => _selectedSection?.SectionId ?? "app-general";

    /// <summary>Filter string typed in the nav search box.</summary>
    public string NavFilter
    {
        get => _navFilter;
        set { _navFilter = value; OnPropertyChanged(); FilterNavItems(); }
    }

    private void FilterNavItems()
    {
        FilteredNavItems.Clear();
        foreach (var item in NavigationItems)
        {
            if (item.IsHeader
             || string.IsNullOrEmpty(_navFilter)
             || item.Label.Contains(_navFilter, StringComparison.OrdinalIgnoreCase))
                FilteredNavItems.Add(item);
        }
    }

    // -----------------------------------------------------------------------
    // Editable properties â€” all mark IsDirty on change
    // -----------------------------------------------------------------------

    public string ProjectName
    {
        get => _projectName;
        set { if (_projectName != value) { _projectName = value; MarkDirty(); OnPropertyChanged(); OnPropertyChanged(nameof(ProjectNameError)); } }
    }

    public string AssemblyName
    {
        get => _assemblyName;
        set { if (_assemblyName != value) { _assemblyName = value; MarkDirty(); OnPropertyChanged(); OnPropertyChanged(nameof(AssemblyNameError)); } }
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

    public bool TreatWarningsAsErrors
    {
        get => _treatWarningsAsErrors;
        set { if (_treatWarningsAsErrors != value) { _treatWarningsAsErrors = value; MarkDirty(); OnPropertyChanged(); } }
    }

    public bool EnableCodeAnalysis
    {
        get => _enableCodeAnalysis;
        set { if (_enableCodeAnalysis != value) { _enableCodeAnalysis = value; MarkDirty(); OnPropertyChanged(); } }
    }

    public bool CodeAnalysisReleaseOnly
    {
        get => _codeAnalysisReleaseOnly;
        set { if (_codeAnalysisReleaseOnly != value) { _codeAnalysisReleaseOnly = value; MarkDirty(); OnPropertyChanged(); } }
    }

    // Win32 resources
    public string AppIconPath
    {
        get => _appIconPath;
        set { if (_appIconPath != value) { _appIconPath = value; MarkDirty(); OnPropertyChanged(); } }
    }

    public string AppManifest
    {
        get => _appManifest;
        set { if (_appManifest != value) { _appManifest = value; MarkDirty(); OnPropertyChanged(); } }
    }

    // Package
    public string PackageId
    {
        get => _packageId;
        set { if (_packageId != value) { _packageId = value; MarkDirty(); OnPropertyChanged(); } }
    }

    public string PackageVersion
    {
        get => _packageVersion;
        set { if (_packageVersion != value) { _packageVersion = value; MarkDirty(); OnPropertyChanged(); } }
    }

    public string PackageAuthors
    {
        get => _packageAuthors;
        set { if (_packageAuthors != value) { _packageAuthors = value; MarkDirty(); OnPropertyChanged(); } }
    }

    public string PackageDescription
    {
        get => _packageDescription;
        set { if (_packageDescription != value) { _packageDescription = value; MarkDirty(); OnPropertyChanged(); } }
    }

    // Debug / launch
    public string LaunchArgs
    {
        get => _launchArgs;
        set { if (_launchArgs != value) { _launchArgs = value; MarkDirty(); OnPropertyChanged(); } }
    }

    public string WorkingDirectory
    {
        get => _workingDirectory;
        set { if (_workingDirectory != value) { _workingDirectory = value; MarkDirty(); OnPropertyChanged(); } }
    }

    public string? ActiveLaunchProfile
    {
        get => _activeLaunchProfile;
        set { _activeLaunchProfile = value; OnPropertyChanged(); }
    }

    public ObservableCollection<EnvVarEntry> EnvironmentVariables { get; } = [];

    // References
    public ObservableCollection<ReferenceEntry> References { get; }

    private ReferenceEntry? _selectedReference;
    public ReferenceEntry? SelectedReference
    {
        get => _selectedReference;
        set { _selectedReference = value; OnPropertyChanged(); ((PropertiesRelayCommand)RemoveReferenceCommand).RaiseCanExecuteChanged(); }
    }

    // -----------------------------------------------------------------------
    // State flags
    // -----------------------------------------------------------------------

    public bool IsDirty
    {
        get => _isDirty;
        private set
        {
            _isDirty = value;
            OnPropertyChanged();
            ((PropertiesRelayCommand)SaveCommand).RaiseCanExecuteChanged();
        }
    }

    /// <summary>
    /// Pulsed to true then immediately false after a successful save.
    /// Code-behind watches this to trigger the toast.
    /// </summary>
    public bool SaveCompleted
    {
        get => _saveCompleted;
        private set { _saveCompleted = value; OnPropertyChanged(); }
    }

    // -----------------------------------------------------------------------
    // Validation computed properties
    // -----------------------------------------------------------------------

    public string? ProjectNameError =>
        string.IsNullOrWhiteSpace(ProjectName) ? "Le nom du projet ne peut pas Ãªtre vide." : null;

    public string? AssemblyNameError =>
        string.IsNullOrWhiteSpace(AssemblyName) ? "Le nom d'assembly ne peut pas Ãªtre vide." : null;

    // -----------------------------------------------------------------------
    // Commands
    // -----------------------------------------------------------------------

    public ICommand SaveCommand            { get; }
    public ICommand AddNuGetCommand        { get; }
    public ICommand AddProjectRefCommand   { get; }
    public ICommand RemoveReferenceCommand { get; }

    // -----------------------------------------------------------------------
    // Private helpers
    // -----------------------------------------------------------------------

    private void MarkDirty() => IsDirty = true;

    private async Task SaveAsync()
    {
        if (ProjectNameError is not null || AssemblyNameError is not null) return;

        if (!string.Equals(ProjectName, _project.Name, StringComparison.Ordinal))
            await _solutionManager.RenameProjectAsync(_project, ProjectName);

        IsDirty       = false;
        SaveCompleted = true;   // pulse â€” code-behind shows toast
        SaveCompleted = false;
    }

    private Task AddNuGetAsync()
    {
        // Raise event â€” the host (MainWindow) opens the NuGet Manager document tab.
        ManageNuGetRequested?.Invoke(this, new ManageNuGetRequestedEventArgs { Project = _project });
        return Task.CompletedTask;
    }

    private Task AddProjectRefAsync()
    {
        var ofd = new OpenFileDialog
        {
            Title  = "SÃ©lectionner un projet Ã  rÃ©fÃ©rencer",
            Filter = "Projets C# (*.csproj)|*.csproj|Tous les projets (*.csproj;*.vbproj;*.fsproj)|*.csproj;*.vbproj;*.fsproj"
        };
        if (ofd.ShowDialog() == true)
        {
            References.Add(new ReferenceEntry(
                Path.GetFileNameWithoutExtension(ofd.FileName), "Projet"));
            MarkDirty();
        }
        return Task.CompletedTask;
    }

    private Task RemoveReferenceAsync()
    {
        if (SelectedReference is not null)
        {
            References.Remove(SelectedReference);
            SelectedReference = null;
            MarkDirty();
        }
        return Task.CompletedTask;
    }

    private static List<NavItem> BuildNavItems(bool isVsProject)
    {
        var items = new List<NavItem>
        {
            new("Application",    "",                 IsHeader: true),
            new("GÃ©nÃ©ral",        "app-general",      IsHeader: false),
            new("DÃ©pendances",    "app-dependencies", IsHeader: false),
        };

        if (isVsProject)
        {
            items.Add(new("Ressources Win32",    "app-win32",      IsHeader: false));
            items.Add(new("Utilisations globales", "global-usings", IsHeader: false));
        }

        items.Add(new("Build",      "build",      IsHeader: false));
        items.Add(new("Ã‰lÃ©ments",   "items",      IsHeader: false));
        items.Add(new("RÃ©fÃ©rences", "references", IsHeader: false));

        if (isVsProject)
        {
            items.Add(new("Package",         "package",       IsHeader: false));
            items.Add(new("Analyse du code", "code-analysis", IsHeader: false));
            items.Add(new("DÃ©bogage",        "debug",         IsHeader: false));
        }

        return items;
    }

    // -----------------------------------------------------------------------
    // INotifyPropertyChanged
    // -----------------------------------------------------------------------

    /// <summary>
    /// Raised when the user clicks "+ NuGetâ€¦" so the host can open the NuGet Manager document tab.
    /// </summary>
    public event EventHandler<ManageNuGetRequestedEventArgs>? ManageNuGetRequested;

}

// ---------------------------------------------------------------------------
// Supporting types
// ---------------------------------------------------------------------------

/// <summary>Item in the left navigation list.</summary>
public sealed record NavItem(string Label, string SectionId, bool IsHeader = false);

/// <summary>Flat DTO for the References list.</summary>
public sealed record ReferenceEntry(string Name, string RefType);

/// <summary>Row DTO for the Debug environment variables DataGrid.</summary>
public sealed record EnvVarEntry(string Name, string Value);

/// <summary>Simple async-capable relay command local to this feature.</summary>
internal sealed class PropertiesRelayCommand(Func<Task> executeAsync, Func<bool>? canExecute = null)
    : ICommand
{
    public event EventHandler? CanExecuteChanged;
    public bool CanExecute(object? parameter) => canExecute?.Invoke() ?? true;
    public void Execute(object? parameter)    => executeAsync();
    public void RaiseCanExecuteChanged()      => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}


