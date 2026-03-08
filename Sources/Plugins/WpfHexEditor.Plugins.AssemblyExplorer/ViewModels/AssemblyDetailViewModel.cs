// ==========================================================
// Project: WpfHexEditor.Plugins.AssemblyExplorer
// File: ViewModels/AssemblyDetailViewModel.cs
// Author: Derek Tremblay
// Created: 2026-03-08
// Description:
//     ViewModel for the detail pane (bottom split of the panel).
//     Displays decompiled / stub text for the currently selected
//     tree node, along with its PE offset and metadata token.
//
// Architecture Notes:
//     Pattern: MVVM — populated by AssemblyExplorerViewModel.OnNodeSelected.
//     All text comes from DecompilerService (stub in Phase 1).
// ==========================================================

using WpfHexEditor.Plugins.AssemblyExplorer.Models;
using WpfHexEditor.Plugins.AssemblyExplorer.Services;

namespace WpfHexEditor.Plugins.AssemblyExplorer.ViewModels;

/// <summary>
/// Provides content for the detail pane shown below the tree view.
/// Updated every time the user selects a new tree node.
/// </summary>
public sealed class AssemblyDetailViewModel : AssemblyNodeViewModel
{
    private readonly DecompilerService _decompiler;

    public AssemblyDetailViewModel(DecompilerService decompiler)
        => _decompiler = decompiler;

    // ── Displayed properties ──────────────────────────────────────────────────

    private string _title = string.Empty;
    public string Title
    {
        get => _title;
        set => SetField(ref _title, value);
    }

    private string _detailText = "Select a node to view details.";
    public string DetailText
    {
        get => _detailText;
        set => SetField(ref _detailText, value);
    }

    private string _metadataInfo = string.Empty;
    public string MetadataInfo
    {
        get => _metadataInfo;
        set => SetField(ref _metadataInfo, value);
    }

    private long _peOffset;
    public long PeOffsetValue
    {
        get => _peOffset;
        set
        {
            SetField(ref _peOffset, value);
            OnPropertyChanged(nameof(HasOffset));
        }
    }

    public bool HasOffset => _peOffset > 0;

    // ── AssemblyNodeViewModel overrides (detail pane is not a tree node) ──────

    public override string DisplayName => _title;
    public override string IconGlyph   => "\uE8D6"; // Details

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Updates the detail pane to reflect the selected <paramref name="node"/>.
    /// Called on the UI thread from AssemblyExplorerViewModel.OnNodeSelected.
    /// </summary>
    public void ShowNode(AssemblyNodeViewModel node)
    {
        Title        = node.DisplayName;
        PeOffsetValue = node.PeOffset;
        MetadataInfo  = node.MetadataToken != 0
            ? $"Token: 0x{node.MetadataToken:X8}"
            : string.Empty;

        DetailText = node switch
        {
            AssemblyRootNodeViewModel root => _decompiler.DecompileAssembly(root.Model),
            TypeNodeViewModel         type => _decompiler.DecompileType(type.Model),
            MethodNodeViewModel       meth => _decompiler.DecompileMethod(meth.Model),
            _                              => _decompiler.GetStubText(node.DisplayName)
        };
    }

    /// <summary>Resets the detail pane to its initial empty state.</summary>
    public void Clear()
    {
        Title        = string.Empty;
        DetailText   = "Select a node to view details.";
        MetadataInfo  = string.Empty;
        PeOffsetValue = 0L;
    }
}
