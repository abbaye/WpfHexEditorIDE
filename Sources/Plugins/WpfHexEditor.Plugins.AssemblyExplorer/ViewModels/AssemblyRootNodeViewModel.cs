// ==========================================================
// Project: WpfHexEditor.Plugins.AssemblyExplorer
// File: ViewModels/AssemblyRootNodeViewModel.cs
// Author: Derek Tremblay
// Created: 2026-03-08
// Description:
//     Root tree node representing the loaded assembly.
//     Displays name + version and holds the top-level children
//     (Namespaces group, References, Resources, Modules, Metadata).
// ==========================================================

using WpfHexEditor.Plugins.AssemblyExplorer.Models;

namespace WpfHexEditor.Plugins.AssemblyExplorer.ViewModels;

/// <summary>Root node for a loaded assembly. Children are top-level grouping nodes.</summary>
public sealed class AssemblyRootNodeViewModel : AssemblyNodeViewModel
{
    public AssemblyRootNodeViewModel(AssemblyModel model)
    {
        Model     = model;
        IsExpanded = true;
    }

    public AssemblyModel Model { get; }

    public override string DisplayName =>
        Model.Version is not null
            ? $"{Model.Name} v{Model.Version}"
            : Model.Name;

    public override string IconGlyph => "\uE8A5"; // Assembly icon

    public override string ToolTipText =>
        $"{Model.FilePath}\n"
      + (Model.IsManaged
            ? $"Types: {Model.Types.Count}  |  References: {Model.References.Count}"
            : "Native PE — no managed metadata.");
}
