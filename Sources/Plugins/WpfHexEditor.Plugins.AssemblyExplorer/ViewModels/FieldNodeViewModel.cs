// ==========================================================
// Project: WpfHexEditor.Plugins.AssemblyExplorer
// File: ViewModels/FieldNodeViewModel.cs
// Author: Derek Tremblay
// Created: 2026-03-08
// Description:
//     Tree node for a .NET field definition.
// ==========================================================

using WpfHexEditor.Plugins.AssemblyExplorer.Models;

namespace WpfHexEditor.Plugins.AssemblyExplorer.ViewModels;

/// <summary>Tree node for a single field definition.</summary>
public sealed class FieldNodeViewModel : AssemblyNodeViewModel
{
    public FieldNodeViewModel(MemberModel model)
    {
        Model         = model;
        PeOffset      = model.PeOffset;
        MetadataToken = model.MetadataToken;
    }

    public MemberModel Model { get; }

    public override string DisplayName => Model.Name;
    public override string IconGlyph   => "\uE8D2"; // Field

    public override string ToolTipText =>
        $"Field: {Model.Name}\nToken: 0x{Model.MetadataToken:X8}"
      + (Model.IsStatic ? "\n[static]" : string.Empty);
}
