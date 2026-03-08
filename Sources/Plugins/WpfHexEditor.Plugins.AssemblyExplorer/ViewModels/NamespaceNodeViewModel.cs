// ==========================================================
// Project: WpfHexEditor.Plugins.AssemblyExplorer
// File: ViewModels/NamespaceNodeViewModel.cs
// Author: Derek Tremblay
// Created: 2026-03-08
// Description:
//     Tree node representing a .NET namespace group.
//     Types with empty Namespace are grouped under "(global namespace)".
// ==========================================================

namespace WpfHexEditor.Plugins.AssemblyExplorer.ViewModels;

/// <summary>Groups type nodes under a namespace name.</summary>
public sealed class NamespaceNodeViewModel : AssemblyNodeViewModel
{
    private readonly string _namespaceName;

    public NamespaceNodeViewModel(string namespaceName)
    {
        _namespaceName = namespaceName;
        IsExpanded = false;
    }

    public override string DisplayName =>
        string.IsNullOrEmpty(_namespaceName) ? "(global namespace)" : _namespaceName;

    public override string IconGlyph => "\uE8B7"; // Folder icon

    public override string ToolTipText => $"Namespace: {DisplayName}  ({Children.Count} types)";
}
