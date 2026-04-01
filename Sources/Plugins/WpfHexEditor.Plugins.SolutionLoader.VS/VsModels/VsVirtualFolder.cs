// ==========================================================
// Project: WpfHexEditor.Plugins.SolutionLoader.VS
// File: VsModels/VsVirtualFolder.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     Mutable IVirtualFolder implementation used while building the
//     project tree from a parsed .csproj file. Sealed after construction.
// ==========================================================

using WpfHexEditor.Editor.Core;

namespace WpfHexEditor.Plugins.SolutionLoader.VS.VsModels;

internal sealed class VsVirtualFolder : IVirtualFolder
{
    private readonly List<string>          _itemIds  = [];
    private readonly List<IVirtualFolder>  _children = [];

    public string  Id                   { get; }          = Guid.NewGuid().ToString();
    public string  Name                 { get; init; }    = string.Empty;
    public string? PhysicalRelativePath { get; init; }

    public IReadOnlyList<string>         ItemIds  => _itemIds;
    public IReadOnlyList<IVirtualFolder> Children => _children;

    internal void AddItemId(string id)      => _itemIds.Add(id);
    internal void AddChild(VsVirtualFolder child) => _children.Add(child);
}
