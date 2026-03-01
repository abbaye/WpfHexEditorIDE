//////////////////////////////////////////////
// Apache 2.0  - 2026
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using WpfHexEditor.Editor.Core;

namespace WpfHexEditor.ProjectSystem.Models;

internal sealed class Project : IProject, INotifyPropertyChanged
{
    private bool _isModified;

    private readonly ObservableCollection<ProjectItem>   _items       = [];
    private readonly ObservableCollection<VirtualFolder> _rootFolders = [];

    public string Id              { get; set; } = Guid.NewGuid().ToString();
    public string Name            { get; set; } = "";
    public string ProjectFilePath { get; set; } = "";

    public IReadOnlyList<IProjectItem>   Items       => _items;
    public IReadOnlyList<IVirtualFolder> RootFolders => _rootFolders;

    public bool IsModified
    {
        get => _isModified;
        set { _isModified = value; OnPropertyChanged(); }
    }

    internal ObservableCollection<ProjectItem>   ItemsMutable       => _items;
    internal ObservableCollection<VirtualFolder> RootFoldersMutable => _rootFolders;

    public IProjectItem? FindItem(string id)
        => _items.FirstOrDefault(i => i.Id == id);

    public IProjectItem? FindItemByPath(string absolutePath)
        => _items.FirstOrDefault(i => string.Equals(i.AbsolutePath, absolutePath, StringComparison.OrdinalIgnoreCase));

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? p = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
}
