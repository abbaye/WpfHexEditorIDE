//////////////////////////////////////////////
// Apache 2.0  - 2026
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.Collections.ObjectModel;
using WpfHexEditor.Editor.Core;

namespace WpfHexEditor.ProjectSystem.Models;

internal sealed class VirtualFolder : IVirtualFolder
{
    private readonly ObservableCollection<VirtualFolder> _children = [];
    private readonly ObservableCollection<string>        _itemIds  = [];

    public string Id   { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "";

    public IReadOnlyList<string>         ItemIds  => _itemIds;
    public IReadOnlyList<IVirtualFolder> Children => _children;

    internal ObservableCollection<VirtualFolder> ChildrenMutable => _children;
    internal ObservableCollection<string>        ItemIdsMutable  => _itemIds;
}
