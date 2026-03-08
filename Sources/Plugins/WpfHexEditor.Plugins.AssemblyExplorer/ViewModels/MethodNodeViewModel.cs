// ==========================================================
// Project: WpfHexEditor.Plugins.AssemblyExplorer
// File: ViewModels/MethodNodeViewModel.cs
// Author: Derek Tremblay
// Created: 2026-03-08
// Description:
//     Tree node for a .NET method definition.
// ==========================================================

using WpfHexEditor.Plugins.AssemblyExplorer.Models;

namespace WpfHexEditor.Plugins.AssemblyExplorer.ViewModels;

/// <summary>Tree node for a single method definition.</summary>
public sealed class MethodNodeViewModel : AssemblyNodeViewModel
{
    public MethodNodeViewModel(MemberModel model)
    {
        Model         = model;
        PeOffset      = model.PeOffset;
        MetadataToken = model.MetadataToken;
    }

    public MemberModel Model { get; }

    public override string DisplayName =>
        Model.Signature is not null ? Model.Signature : $"{Model.Name}()";

    public override string IconGlyph => "\uE8F4"; // Method

    public override string ToolTipText =>
        $"Method: {Model.Name}\nToken: 0x{Model.MetadataToken:X8}"
      + (Model.IsStatic ? "\n[static]" : string.Empty);
}
