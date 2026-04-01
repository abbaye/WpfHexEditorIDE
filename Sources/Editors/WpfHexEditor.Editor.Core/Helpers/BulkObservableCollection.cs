// Project      : WpfHexEditorControl
// File         : Helpers/BulkObservableCollection.cs
// Description  : ObservableCollection<T> extension that replaces all items in a single
//                CollectionChanged(Reset) notification — avoids N per-item notifications
//                when loading large datasets (e.g. hex diff rows).
// Architecture : No WPF dependency beyond PresentationFramework; safe to use from any layer
//                that references WpfHexEditor.Editor.Core.

using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace WpfHexEditor.Editor.Core.Helpers;

/// <summary>
/// An <see cref="ObservableCollection{T}"/> that exposes <see cref="ReplaceAll"/> to swap
/// the entire contents in a <b>single</b> <c>CollectionChanged(Reset)</c> notification
/// instead of firing one notification per item.
/// <para>
/// Use this whenever you need to populate a large list bound to a virtualised
/// <c>ItemsControl</c> — replacing 100 K items via individual <c>Add</c> calls would
/// trigger 100 K layout passes in the WPF virtualiser.
/// </para>
/// </summary>
public sealed class BulkObservableCollection<T> : ObservableCollection<T>
{
    /// <summary>
    /// Replaces the entire contents of the collection with <paramref name="newItems"/>
    /// and raises exactly one <c>CollectionChanged(Reset)</c> notification.
    /// Must be called on the UI thread (same contract as <see cref="ObservableCollection{T}"/>).
    /// </summary>
    public void ReplaceAll(IReadOnlyList<T> newItems)
    {
        Items.Clear();
        foreach (var item in newItems)
            Items.Add(item);

        OnPropertyChanged(new PropertyChangedEventArgs("Count"));
        OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }
}
