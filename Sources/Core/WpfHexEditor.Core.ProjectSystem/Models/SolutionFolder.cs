// ==========================================================
// Project: WpfHexEditor.ProjectSystem
// File: SolutionFolder.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026
// Description:
//     Internal mutable implementation of ISolutionFolder.
//     Represents a VS-like Solution Folder that groups Projects logically.
//
// Architecture Notes:
//     Pattern: Composite — nested Children of SolutionFolder.
//     Immutable public surface via ISolutionFolder; mutations through internal members.
// ==========================================================

using System.Collections.ObjectModel;
using WpfHexEditor.Editor.Core;

namespace WpfHexEditor.Core.ProjectSystem.Models;

internal sealed class SolutionFolder : ISolutionFolder
{
    private readonly ObservableCollection<string>          _projectIds = [];
    private readonly ObservableCollection<SolutionFolder>  _children   = [];
    private readonly ObservableCollection<string>          _fileItems  = [];

    public string Id   { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "";

    public IReadOnlyList<string>          ProjectIds => _projectIds;
    public IReadOnlyList<ISolutionFolder> Children   => _children;
    public IReadOnlyList<string>          FileItems  => _fileItems;

    // -- Internal mutable access ------------------------------------------
    internal ObservableCollection<string>         ProjectIdsMutable => _projectIds;
    internal ObservableCollection<SolutionFolder> ChildrenMutable   => _children;
    internal ObservableCollection<string>         FileItemsMutable  => _fileItems;
}
