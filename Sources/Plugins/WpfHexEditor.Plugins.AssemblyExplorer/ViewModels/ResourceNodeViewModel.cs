// ==========================================================
// Project: WpfHexEditor.Plugins.AssemblyExplorer
// File: ViewModels/ResourceNodeViewModel.cs
// Author: Derek Tremblay
// Created: 2026-03-08
// Description:
//     Tree node for a managed resource (ManifestResource row).
// ==========================================================

using WpfHexEditor.Plugins.AssemblyExplorer.Models;

namespace WpfHexEditor.Plugins.AssemblyExplorer.ViewModels;

/// <summary>Tree node for a single ManifestResource entry.</summary>
public sealed class ResourceNodeViewModel : AssemblyNodeViewModel
{
    public ResourceNodeViewModel(ResourceEntry resource)
    {
        Resource = resource;
        PeOffset = resource.Offset;
    }

    public ResourceEntry Resource { get; }

    public override string DisplayName => Resource.Name;
    public override string IconGlyph   => "\uEB9F"; // Resource

    public override string ToolTipText =>
        $"Resource: {Resource.Name}"
      + (Resource.Offset > 0 ? $"\nOffset: 0x{Resource.Offset:X}" : string.Empty)
      + (Resource.Length > 0 ? $"\nLength: {Resource.Length} bytes" : string.Empty);
}
