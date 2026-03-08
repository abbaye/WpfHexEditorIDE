// ==========================================================
// Project: WpfHexEditor.Plugins.AssemblyExplorer
// File: ViewModels/PropertyNodeViewModel.cs
// Author: Derek Tremblay
// Created: 2026-03-08
// Description:
//     Tree node for a .NET property definition.
// ==========================================================

using WpfHexEditor.Plugins.AssemblyExplorer.Models;

namespace WpfHexEditor.Plugins.AssemblyExplorer.ViewModels;

/// <summary>Tree node for a single property definition.</summary>
public sealed class PropertyNodeViewModel : AssemblyNodeViewModel
{
    public PropertyNodeViewModel(MemberModel model)
    {
        Model         = model;
        MetadataToken = model.MetadataToken;
    }

    public MemberModel Model { get; }

    public override string DisplayName => Model.Name;
    public override string IconGlyph   => "\uE8EC"; // Property

    public override string ToolTipText =>
        $"Property: {Model.Name}\nToken: 0x{Model.MetadataToken:X8}";
}
