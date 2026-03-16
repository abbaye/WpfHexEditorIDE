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

using System.Windows.Media;
using WpfHexEditor.Core.AssemblyAnalysis.Models;

namespace WpfHexEditor.Plugins.AssemblyExplorer.ViewModels;

/// <summary>Root node for a loaded assembly. Children are top-level grouping nodes.</summary>
public sealed class AssemblyRootNodeViewModel : AssemblyNodeViewModel
{
    public AssemblyRootNodeViewModel(AssemblyModel model)
    {
        Model      = model;
        IsExpanded = true;
    }

    public AssemblyModel Model { get; }

    private bool _isPinned;

    /// <summary>
    /// When true a pin badge (📌) is shown on the root node header and the assembly
    /// is not evicted when the active editor changes or the workspace limit is reached.
    /// </summary>
    public bool IsPinned
    {
        get => _isPinned;
        set => SetField(ref _isPinned, value);
    }

    /// <summary>
    /// Assembly name + version + optional target framework badge.
    /// Example: "MyLib v1.0.0  [.NET 8.0]"
    /// </summary>
    public override string DisplayName
    {
        get
        {
            var ver   = Model.Version is not null ? $" v{Model.Version}" : string.Empty;
            var badge = !string.IsNullOrEmpty(Model.TargetFramework)
                ? $"  [{Model.TargetFramework}]"
                : string.Empty;
            return $"{Model.Name}{ver}{badge}";
        }
    }

    public override string IconGlyph  => "\uE8A5"; // Assembly icon
    public override Brush  IconBrush  => MakeBrush("#4EC9B0"); // Teal — assembly root

    public override string ToolTipText =>
        $"{Model.FilePath}\n"
      + (Model.IsManaged
            ? $"Types: {Model.Types.Count}  |  References: {Model.References.Count}"
            : "Native PE — no managed metadata.");
}
