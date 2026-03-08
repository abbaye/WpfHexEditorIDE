// ==========================================================
// Project: WpfHexEditor.Plugins.AssemblyExplorer
// File: ViewModels/EventNodeViewModel.cs
// Author: Derek Tremblay
// Created: 2026-03-08
// Description:
//     Tree node for a .NET event definition.
// ==========================================================

using WpfHexEditor.Plugins.AssemblyExplorer.Models;

namespace WpfHexEditor.Plugins.AssemblyExplorer.ViewModels;

/// <summary>Tree node for a single event definition.</summary>
public sealed class EventNodeViewModel : AssemblyNodeViewModel
{
    public EventNodeViewModel(MemberModel model)
    {
        Model         = model;
        MetadataToken = model.MetadataToken;
    }

    public MemberModel Model { get; }

    public override string DisplayName => Model.Name;
    public override string IconGlyph   => "\uE7FC"; // Event / lightning bolt

    public override string ToolTipText =>
        $"Event: {Model.Name}\nToken: 0x{Model.MetadataToken:X8}";
}
