// ==========================================================
// Project: WpfHexEditor.Plugins.AssemblyExplorer
// File: ViewModels/TypeNodeViewModel.cs
// Author: Derek Tremblay
// Created: 2026-03-08
// Description:
//     Tree node for a .NET type definition. Icon varies by TypeKind.
//     Children are method/field/property/event sub-groups.
// ==========================================================

using WpfHexEditor.Plugins.AssemblyExplorer.Models;

namespace WpfHexEditor.Plugins.AssemblyExplorer.ViewModels;

/// <summary>Tree node for a single .NET type (class/struct/interface/enum/delegate).</summary>
public sealed class TypeNodeViewModel : AssemblyNodeViewModel
{
    public TypeNodeViewModel(TypeModel model)
    {
        Model         = model;
        PeOffset      = model.PeOffset;
        MetadataToken = model.MetadataToken;
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

    public override string ToolTipText =>
        $"{Model.FullName}\nToken: 0x{Model.MetadataToken:X8}"
      + (Model.PeOffset > 0 ? $"\nPE offset: 0x{Model.PeOffset:X}" : string.Empty);
}
