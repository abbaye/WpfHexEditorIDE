// ==========================================================
// Project: WpfHexEditor.Plugins.AssemblyExplorer
// File: ViewModels/ReferenceNodeViewModel.cs
// Author: Derek Tremblay
// Created: 2026-03-08
// Description:
//     Tree node for an assembly reference (AssemblyRef metadata row).
// ==========================================================

using System.Windows.Media;
using WpfHexEditor.Core.AssemblyAnalysis.Models;

namespace WpfHexEditor.Plugins.AssemblyExplorer.ViewModels;

/// <summary>Tree node for a single AssemblyRef row.</summary>
public sealed class ReferenceNodeViewModel : AssemblyNodeViewModel
{
    public ReferenceNodeViewModel(AssemblyRef reference)
        => Reference = reference;

    public AssemblyRef Reference { get; }

    public override string DisplayName =>
        Reference.Version is not null
            ? $"{Reference.Name} v{Reference.Version}"
            : Reference.Name;

    public override string IconGlyph  => "\uE71B"; // Reference / link
    public override Brush  IconBrush  => MakeBrush("#9B9B9B"); // Silver

    public override string ToolTipText =>
        $"Assembly Reference: {Reference.Name}"
      + (Reference.Version is not null   ? $"\nVersion: {Reference.Version}"         : string.Empty)
      + (Reference.PublicKeyToken is not null ? $"\nPKT: {Reference.PublicKeyToken}" : string.Empty);
}
