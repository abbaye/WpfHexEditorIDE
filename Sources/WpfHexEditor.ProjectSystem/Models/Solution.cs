//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
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
    private bool     _isReadOnlyFormat;

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

    // ── Format versioning ─────────────────────────────────────────────────

    public int  SourceFormatVersion  { get; set; }
    public bool FormatUpgradeRequired => SourceFormatVersion > 0 &&
                                         SourceFormatVersion < Serialization.Migration.MigrationPipeline.CurrentVersion;

    public bool IsReadOnlyFormat
    {
        get => _isReadOnlyFormat;
        set { _isReadOnlyFormat = value; OnPropertyChanged(); }
    }

    // ── Internal helpers ──────────────────────────────────────────────────

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
