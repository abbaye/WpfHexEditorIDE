// ==========================================================
// Project: WpfHexEditor.Plugins.AssemblyExplorer
// File: ViewModels/TypeNodeViewModel.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-08
// Updated: 2026-03-23 — ASM-02-D: lazy-load-on-expand pattern.
//     Member groups (Methods, Fields, Properties, Events) are injected
//     after construction via SetMemberGroups or via the lazy factory.
// Description:
//     Tree node for a .NET type definition. Icon varies by TypeKind.
//     Children are method/field/property/event sub-groups.
// ==========================================================

using System.Windows.Media;
using WpfHexEditor.Core.AssemblyAnalysis.Models;

namespace WpfHexEditor.Plugins.AssemblyExplorer.ViewModels;

/// <summary>Tree node for a single .NET type (class/struct/interface/enum/delegate).</summary>
public sealed class TypeNodeViewModel : AssemblyNodeViewModel
{
    private readonly Func<IReadOnlyList<AssemblyNodeViewModel>>? _memberGroupFactory;
    private bool _membersLoaded;

    /// <summary>
    /// Creates a TypeNodeViewModel with eagerly supplied children (legacy path).
    /// </summary>
    public TypeNodeViewModel(TypeModel model)
    {
        Model           = model;
        PeOffset        = model.PeOffset;
        MetadataToken   = model.MetadataToken;
        _membersLoaded  = true; // caller will add children directly
    }

    /// <summary>
    /// Creates a TypeNodeViewModel with lazily-built member groups.
    /// The <paramref name="memberGroupFactory"/> runs synchronously on the UI thread
    /// (it only builds ViewModels, not I/O) on first expand.
    /// A DummyChildNode is inserted so the expand arrow is visible.
    /// </summary>
    public TypeNodeViewModel(TypeModel model,
        Func<IReadOnlyList<AssemblyNodeViewModel>> memberGroupFactory)
        : this(model)
    {
        _memberGroupFactory = memberGroupFactory;
        _membersLoaded      = false;
        InsertDummyChild();
    }

    public TypeModel Model { get; }

    public override string DisplayName => Model.Name;

    public override string IconGlyph => Model.Kind switch
    {
        TypeKind.Interface => "\uE8C1",
        TypeKind.Struct    => "\uE8A0",
        TypeKind.Enum      => "\uE945",
        TypeKind.Delegate  => "\uE8F4",
        _                  => "\uE943"  // Class (default)
    };

    public override Brush IconBrush => Model.Kind switch
    {
        TypeKind.Interface => MakeBrush("#B8D7A3"), // Light green
        TypeKind.Struct    => MakeBrush("#4EC9B0"), // Cyan-green
        TypeKind.Enum      => MakeBrush("#CE9178"), // Orange
        TypeKind.Delegate  => MakeBrush("#C586C0"), // Purple
        _                  => MakeBrush("#4FC1FF")  // Blue — class
    };

    public override bool IsPublic => Model.IsPublic;

    public override string ToolTipText =>
        $"{Model.FullName}\nToken: 0x{Model.MetadataToken:X8}"
      + (Model.PeOffset > 0 ? $"\nPE offset: 0x{Model.PeOffset:X}" : string.Empty);

    /// <summary>
    /// Called when the node is expanded for the first time.
    /// Synchronously invokes the member-group factory and replaces the DummyChildNode.
    /// Subsequent calls are no-ops.
    /// </summary>
    public new bool IsExpanded
    {
        get => base.IsExpanded;
        set
        {
            base.IsExpanded = value;
            if (value && !_membersLoaded && _memberGroupFactory is not null)
                LoadMemberGroups();
        }
    }

    /// <summary>
    /// Synchronously loads member groups from the factory (UI thread only).
    /// Building ViewModels is fast — no I/O — so async is not needed here.
    /// </summary>
    private void LoadMemberGroups()
    {
        if (_membersLoaded || _memberGroupFactory is null) return;
        _membersLoaded = true;

        try
        {
            var groups = _memberGroupFactory();
            RemoveDummyChild();
            foreach (var group in groups)
                Children.Add(group);
        }
        catch
        {
            RemoveDummyChild(); // leave the node empty on failure
        }
    }
}
