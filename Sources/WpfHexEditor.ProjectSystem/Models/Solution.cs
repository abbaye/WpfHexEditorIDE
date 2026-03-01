//////////////////////////////////////////////
// Apache 2.0  - 2026
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using WpfHexEditor.Editor.Core;

namespace WpfHexEditor.ProjectSystem.Models;

internal sealed class Solution : ISolution, INotifyPropertyChanged
{
    private bool     _isModified;
    private IProject? _startupProject;

    private readonly ObservableCollection<Project> _projects = [];

    public string Name     { get; set; } = "";
    public string FilePath { get; set; } = "";

    public IReadOnlyList<IProject> Projects       => _projects;
    public IProject?               StartupProject => _startupProject;

    public bool IsModified
    {
        get => _isModified || _projects.Any(p => p.IsModified);
        set { _isModified = value; OnPropertyChanged(); }
    }

    internal ObservableCollection<Project> ProjectsMutable => _projects;

    internal void SetStartupProject(IProject? project)
    {
        _startupProject = project;
        OnPropertyChanged(nameof(StartupProject));
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? p = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
}
