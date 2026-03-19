//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.6, Claude Sonnet 4.6
//////////////////////////////////////////////

using System.Collections;
using System.Collections.Specialized;
using WpfHexEditor.Docking.Core;
using WpfHexEditor.Docking.Core.Nodes;

namespace WpfHexEditor.Shell;

/// <summary>
/// Synchronizes an <see cref="IEnumerable"/> of view-model objects with
/// <see cref="DockItem"/> instances in the dock layout.
/// Handles Add/Remove/Reset from <see cref="INotifyCollectionChanged"/>.
/// </summary>
internal sealed class DockItemSourceSynchronizer : IDisposable
{
    private readonly DockControl _host;
    private readonly Func<object, DockItem> _mapper;
    private readonly bool _isDocument;
    private readonly Dictionary<object, DockItem> _vmToItem = new();
    private INotifyCollectionChanged? _observableSource;

    public DockItemSourceSynchronizer(
        DockControl host,
        IEnumerable source,
        Func<object, DockItem> mapper,
        bool isDocument)
    {
        _host = host;
        _mapper = mapper;
        _isDocument = isDocument;

        // Sync initial items
        foreach (var vm in source)
            AddItem(vm);

        // Listen for changes
        if (source is INotifyCollectionChanged ncc)
        {
            _observableSource = ncc;
            _observableSource.CollectionChanged += OnCollectionChanged;
        }
    }

    public void Dispose()
    {
        if (_observableSource is not null)
        {
            _observableSource.CollectionChanged -= OnCollectionChanged;
            _observableSource = null;
        }
        _vmToItem.Clear();
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
                if (e.NewItems is not null)
                    foreach (var vm in e.NewItems)
                        AddItem(vm);
                break;

            case NotifyCollectionChangedAction.Remove:
                if (e.OldItems is not null)
                    foreach (var vm in e.OldItems)
                        RemoveItem(vm);
                break;

            case NotifyCollectionChangedAction.Reset:
                RemoveAll();
                break;

            case NotifyCollectionChangedAction.Replace:
                if (e.OldItems is not null)
                    foreach (var vm in e.OldItems)
                        RemoveItem(vm);
                if (e.NewItems is not null)
                    foreach (var vm in e.NewItems)
                        AddItem(vm);
                break;
        }

        _host.RebuildVisualTree();
    }

    private void AddItem(object vm)
    {
        if (_vmToItem.ContainsKey(vm)) return;

        var item = _mapper(vm);
        item.Tag = vm;
        _vmToItem[vm] = item;

        var engine = _host.Engine;
        if (engine is null || _host.Layout is null) return;

        var layout = _host.Layout;
        var strategy = _host.LayoutUpdateStrategy;

        // Consult the strategy before default insertion
        if (strategy is not null)
        {
            var target = _isDocument
                ? (DockGroupNode)layout.MainDocumentHost
                : layout.MainDocumentHost;

            var handled = _isDocument
                ? strategy.BeforeInsertDocument(layout, item, target)
                : strategy.BeforeInsertAnchorable(layout, item, target);

            if (handled) return;
        }

        if (_isDocument)
            engine.Dock(item, layout.MainDocumentHost, DockDirection.Center);
        else
            engine.Dock(item, layout.MainDocumentHost, DockDirection.Bottom);
    }

    private void RemoveItem(object vm)
    {
        if (!_vmToItem.TryGetValue(vm, out var item)) return;
        _vmToItem.Remove(vm);

        var engine = _host.Engine;
        if (engine is null) return;

        if (item.CanClose)
            engine.Close(item);
        else
            engine.Hide(item);
    }

    private void RemoveAll()
    {
        var items = _vmToItem.Values.ToList();
        _vmToItem.Clear();

        var engine = _host.Engine;
        if (engine is null) return;

        engine.BeginTransaction();
        foreach (var item in items)
        {
            if (item.CanClose)
                engine.Close(item);
            else
                engine.Hide(item);
        }
        engine.CommitTransaction();
    }
}
