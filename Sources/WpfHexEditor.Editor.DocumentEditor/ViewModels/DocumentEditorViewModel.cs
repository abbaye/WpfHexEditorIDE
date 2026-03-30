// ==========================================================
// Project: WpfHexEditor.Editor.DocumentEditor
// File: ViewModels/DocumentEditorViewModel.cs
// Description:
//     Thin ViewModel wrapping DocumentModel for DataContext binding.
//     Exposes scroll ratio for MiniMap sync and navigation helpers.
// ==========================================================

using System.ComponentModel;
using System.Runtime.CompilerServices;
using WpfHexEditor.Editor.DocumentEditor.Core.Model;

namespace WpfHexEditor.Editor.DocumentEditor.ViewModels;

/// <summary>
/// DataContext for <see cref="Controls.DocumentEditorHost"/> and its child panes.
/// </summary>
public sealed class DocumentEditorViewModel : INotifyPropertyChanged
{
    private double _scrollRatio;

    public DocumentEditorViewModel(DocumentModel model)
    {
        Model = model;
    }

    /// <summary>The underlying document model.</summary>
    public DocumentModel Model { get; }

    /// <summary>
    /// Scroll position as a 0–1 ratio (shared between TextPane and MiniMap).
    /// </summary>
    public double ScrollRatio
    {
        get => _scrollRatio;
        set => SetField(ref _scrollRatio, value);
    }

    /// <summary>Navigates both panes to the specified block.</summary>
    public void NavigateToBlock(DocumentBlock block)
    {
        var entry = Model.BinaryMap.EntryOf(block);
        if (entry is null) return;
        ScrollRatio = Model.BinaryMap.TotalMappedLength > 0
            ? (double)entry.Value.Offset / Model.BinaryMap.TotalMappedLength
            : 0;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
