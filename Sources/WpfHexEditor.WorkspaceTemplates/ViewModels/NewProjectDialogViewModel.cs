// ==========================================================
// Project: WpfHexEditor.WorkspaceTemplates
// File: ViewModels/NewProjectDialogViewModel.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     ViewModel for the New Project dialog (3-step wizard):
//       Step 1 — Template selection
//       Step 2 — Project name + location
//       Step 3 — Optional plugins
//
// Architecture Notes:
//     Pattern: Wizard / Stepper ViewModel.
//     Exposes CurrentStep (1-3), CanGoNext, CanFinish.
//     Scaffolding is triggered by the dialog's OK handler via
//     ProjectScaffolder.ScaffoldAsync().
// ==========================================================

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WpfHexEditor.WorkspaceTemplates.ViewModels;

/// <summary>
/// ViewModel for the 3-step New Project wizard dialog.
/// </summary>
public sealed class NewProjectDialogViewModel : INotifyPropertyChanged
{
    // -----------------------------------------------------------------------
    // Fields
    // -----------------------------------------------------------------------

    private readonly TemplateManager _templateManager;

    private int               _currentStep = 1;
    private IProjectTemplate? _selectedTemplate;
    private string            _projectName       = "MyProject";
    private string            _parentDirectory   = DefaultParentDirectory();
    private string            _selectedLanguage  = "C#";
    private string            _filterText        = string.Empty;

    // -----------------------------------------------------------------------
    // Construction
    // -----------------------------------------------------------------------

    public NewProjectDialogViewModel(TemplateManager templateManager)
    {
        _templateManager = templateManager;
        RefreshTemplates();
    }

    // -----------------------------------------------------------------------
    // Step navigation
    // -----------------------------------------------------------------------

    /// <summary>Current wizard step (1 = template, 2 = name/location, 3 = plugins).</summary>
    public int CurrentStep
    {
        get => _currentStep;
        private set { _currentStep = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanGoBack)); OnPropertyChanged(nameof(CanGoNext)); OnPropertyChanged(nameof(CanFinish)); }
    }

    public bool CanGoBack   => CurrentStep > 1;
    public bool CanGoNext   => CurrentStep < 3 && IsCurrentStepValid();
    public bool CanFinish   => CurrentStep == 3 && IsCurrentStepValid();
    public bool IsStep1     => CurrentStep == 1;
    public bool IsStep2     => CurrentStep == 2;
    public bool IsStep3     => CurrentStep == 3;

    public void GoNext()
    {
        if (CanGoNext) { CurrentStep++; OnPropertyChanged(nameof(IsStep1)); OnPropertyChanged(nameof(IsStep2)); OnPropertyChanged(nameof(IsStep3)); }
    }

    public void GoBack()
    {
        if (CanGoBack) { CurrentStep--; OnPropertyChanged(nameof(IsStep1)); OnPropertyChanged(nameof(IsStep2)); OnPropertyChanged(nameof(IsStep3)); }
    }

    // -----------------------------------------------------------------------
    // Step 1 — Template selection
    // -----------------------------------------------------------------------

    /// <summary>All available template categories for grouping in the list.</summary>
    public ObservableCollection<string> Categories { get; } = [];

    /// <summary>Filtered templates matching <see cref="FilterText"/>.</summary>
    public ObservableCollection<IProjectTemplate> FilteredTemplates { get; } = [];

    /// <summary>Currently highlighted template.</summary>
    public IProjectTemplate? SelectedTemplate
    {
        get => _selectedTemplate;
        set
        {
            _selectedTemplate = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanGoNext));
            OnPropertyChanged(nameof(CanFinish));

            // Pre-fill language from template default.
            if (value is not null) SelectedLanguage = value.DefaultLanguage;
        }
    }

    /// <summary>Text filter applied to template list.</summary>
    public string FilterText
    {
        get => _filterText;
        set { _filterText = value; OnPropertyChanged(); RefreshTemplates(); }
    }

    // -----------------------------------------------------------------------
    // Step 2 — Name + Location
    // -----------------------------------------------------------------------

    /// <summary>Project name entered by the user.</summary>
    public string ProjectName
    {
        get => _projectName;
        set { _projectName = value.Trim(); OnPropertyChanged(); OnPropertyChanged(nameof(ProjectPath)); OnPropertyChanged(nameof(CanGoNext)); OnPropertyChanged(nameof(CanFinish)); }
    }

    /// <summary>Parent directory where the project folder will be created.</summary>
    public string ParentDirectory
    {
        get => _parentDirectory;
        set { _parentDirectory = value.Trim(); OnPropertyChanged(); OnPropertyChanged(nameof(ProjectPath)); }
    }

    /// <summary>Preview of the full project path.</summary>
    public string ProjectPath
        => System.IO.Path.Combine(ParentDirectory, ProjectName);

    /// <summary>Available languages for override.</summary>
    public ObservableCollection<string> Languages { get; } = ["C#", "VB.NET", "F#"];

    /// <summary>Selected language (may override template default).</summary>
    public string SelectedLanguage
    {
        get => _selectedLanguage;
        set { _selectedLanguage = value; OnPropertyChanged(); }
    }

    // -----------------------------------------------------------------------
    // Step 3 — Optional plugins
    // -----------------------------------------------------------------------

    /// <summary>Optional plugin items the user can toggle on/off.</summary>
    public ObservableCollection<OptionalPluginItem> OptionalPlugins { get; } = [];

    // -----------------------------------------------------------------------
    // Scaffold entry point
    // -----------------------------------------------------------------------

    /// <summary>
    /// Initiates scaffolding using <see cref="ProjectScaffolder"/>.
    /// Call from the dialog after the user clicks Finish.
    /// </summary>
    public async Task<ScaffoldResult> ScaffoldAsync(CancellationToken ct = default)
    {
        if (SelectedTemplate is null)
            throw new InvalidOperationException("No template selected.");

        var scaffolder = new ProjectScaffolder();
        return await scaffolder.ScaffoldAsync(
            SelectedTemplate,
            ProjectName,
            ParentDirectory,
            SelectedLanguage,
            ct);
    }

    // -----------------------------------------------------------------------
    // INotifyPropertyChanged
    // -----------------------------------------------------------------------

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private bool IsCurrentStepValid() => CurrentStep switch
    {
        1 => SelectedTemplate is not null,
        2 => !string.IsNullOrWhiteSpace(ProjectName) && !string.IsNullOrWhiteSpace(ParentDirectory),
        3 => true,
        _ => false,
    };

    private void RefreshTemplates()
    {
        var all = _templateManager.GetAll();
        var filter = FilterText.Trim();

        FilteredTemplates.Clear();
        Categories.Clear();

        foreach (var t in all)
        {
            if (!string.IsNullOrEmpty(filter) &&
                !t.Name.Contains(filter, StringComparison.OrdinalIgnoreCase) &&
                !t.Description.Contains(filter, StringComparison.OrdinalIgnoreCase))
                continue;

            FilteredTemplates.Add(t);
            if (!Categories.Contains(t.Category)) Categories.Add(t.Category);
        }

        // Auto-select first template if nothing selected.
        if (SelectedTemplate is null && FilteredTemplates.Count > 0)
            SelectedTemplate = FilteredTemplates[0];
    }

    private static string DefaultParentDirectory()
        => System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "source", "WpfHexEditorProjects");
}

/// <summary>Represents an optional plugin the user may include in the new project.</summary>
public sealed class OptionalPluginItem(string id, string displayName, bool isIncluded = false)
    : INotifyPropertyChanged
{
    private bool _isIncluded = isIncluded;

    public string Id          => id;
    public string DisplayName => displayName;

    public bool IsIncluded
    {
        get => _isIncluded;
        set { _isIncluded = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsIncluded))); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
