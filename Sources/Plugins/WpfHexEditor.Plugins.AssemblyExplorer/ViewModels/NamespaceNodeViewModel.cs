// ==========================================================
// Project: WpfHexEditor.Plugins.AssemblyExplorer
// File: ViewModels/NamespaceNodeViewModel.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-08
// Updated: 2026-03-23 — ASM-02-D: lazy-load-on-expand pattern.
//     When a factory func is supplied, children are loaded asynchronously
//     on first IsExpanded=true, gated by a DummyChildNode placeholder.
// Description:
//     Tree node representing a .NET namespace group.
//     Types with empty Namespace are grouped under "(global namespace)".
// ==========================================================

using System.Windows.Media;

namespace WpfHexEditor.Plugins.AssemblyExplorer.ViewModels;

/// <summary>Groups type nodes under a namespace name.</summary>
public sealed class NamespaceNodeViewModel : AssemblyNodeViewModel
{
    private readonly string _namespaceName;
    private readonly Func<Task<IReadOnlyList<AssemblyNodeViewModel>>>? _lazyLoader;
    private bool _childrenLoaded;

    /// <summary>
    /// Creates an eagerly-loaded namespace node (children provided synchronously).
    /// </summary>
    public NamespaceNodeViewModel(string namespaceName)
    {
        _namespaceName  = namespaceName;
        _childrenLoaded = true; // no lazy loader — treat as pre-loaded
        IsExpanded      = false;
    }

    /// <summary>
    /// Creates a lazily-loaded namespace node. Children are fetched from
    /// <paramref name="lazyLoader"/> on first expand, running on a background thread.
    /// A <see cref="DummyChildNode"/> placeholder is inserted so the expand arrow is visible.
    /// </summary>
    public NamespaceNodeViewModel(
        string namespaceName,
        Func<Task<IReadOnlyList<AssemblyNodeViewModel>>> lazyLoader)
    {
        _namespaceName  = namespaceName;
        _lazyLoader     = lazyLoader;
        _childrenLoaded = false;
        IsExpanded      = false;
        InsertDummyChild(); // makes the expand arrow visible
    }

    public override string DisplayName =>
        string.IsNullOrEmpty(_namespaceName) ? "(global namespace)" : _namespaceName;

    public override string IconGlyph  => "\uE8B7"; // Folder icon
    public override Brush  IconBrush  => MakeBrush("#DCDCAA"); // Gold — namespace

    public override string ToolTipText => $"Namespace: {DisplayName}  ({Children.Count} types)";

    /// <summary>
    /// Called when <see cref="AssemblyNodeViewModel.IsExpanded"/> is set to true.
    /// Triggers the lazy child load if children have not been loaded yet.
    /// </summary>
    public new bool IsExpanded
    {
        get => base.IsExpanded;
        set
        {
            base.IsExpanded = value;
            if (value && !_childrenLoaded && _lazyLoader is not null)
                _ = LoadChildrenAsync();
        }
    }

    /// <summary>
    /// Loads children asynchronously via the factory function.
    /// Replaces the DummyChildNode with real children on the UI thread.
    /// Safe to call multiple times — subsequent calls are no-ops.
    /// </summary>
    public async Task LoadChildrenAsync()
    {
        if (_childrenLoaded || _lazyLoader is null) return;
        _childrenLoaded  = true; // prevent re-entry
        IsLoadingChildren = true;

        try
        {
            var loaded = await Task.Run(_lazyLoader);

            // UI thread: remove dummy, add real children.
            RemoveDummyChild();
            foreach (var child in loaded)
                Children.Add(child);
        }
        catch (Exception)
        {
            // On failure, clear the dummy so the node appears empty (not broken).
            RemoveDummyChild();
        }
        finally
        {
            IsLoadingChildren = false;
        }
    }
}
