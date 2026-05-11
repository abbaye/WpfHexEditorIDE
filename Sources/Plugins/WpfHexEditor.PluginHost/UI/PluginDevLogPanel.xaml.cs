// ==========================================================
// Project: WpfHexEditor.PluginHost
// File: UI/PluginDevLogPanel.xaml.cs
// Description:
//     Dockable panel that surfaces a PluginDevLog instance. Filters by
//     category via 6 CheckBox toggles; auto-scrolls to the latest entry
//     when the user is already pinned at the bottom.
// ==========================================================

using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using WpfHexEditor.PluginHost.DevTools;

namespace WpfHexEditor.PluginHost.UI;

public sealed partial class PluginDevLogPanel : UserControl
{
    private PluginDevLog? _log;
    private ICollectionView? _view;
    private Dictionary<PluginDevLogCategory, CheckBox>? _filters;

    public PluginDevLogPanel()
    {
        InitializeComponent();
    }

    /// <summary>Binds the panel to a live <see cref="PluginDevLog"/> instance.</summary>
    public void Bind(PluginDevLog log)
    {
        _log = log;
        _filters = new Dictionary<PluginDevLogCategory, CheckBox>
        {
            [PluginDevLogCategory.Info]      = PART_FilterInfo,
            [PluginDevLogCategory.Load]      = PART_FilterLoad,
            [PluginDevLogCategory.Unload]    = PART_FilterUnload,
            [PluginDevLogCategory.HotReload] = PART_FilterHotReload,
            [PluginDevLogCategory.Crash]     = PART_FilterCrash,
            [PluginDevLogCategory.Slow]      = PART_FilterSlow,
        };
        _view = CollectionViewSource.GetDefaultView(_log.Entries);
        _view.Filter = ShouldShow;
        PART_List.ItemsSource = _view;

        ((INotifyCollectionChanged)_log.Entries).CollectionChanged += OnEntriesChanged;
    }

    private bool _scrollPending;

    private void OnEntriesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action != NotifyCollectionChangedAction.Add) return;
        if (_scrollPending) return;
        _scrollPending = true;
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, () =>
        {
            _scrollPending = false;
            if (PART_List.Items.Count == 0) return;
            PART_List.ScrollIntoView(PART_List.Items[PART_List.Items.Count - 1]);
        });
    }

    private bool ShouldShow(object item)
    {
        if (item is not PluginDevLogEntry e) return false;
        if (_filters is null) return true;
        return !_filters.TryGetValue(e.Category, out var cb) || cb.IsChecked == true;
    }

    private void OnFilterChanged(object sender, RoutedEventArgs e) => _view?.Refresh();
    private void OnClearClicked(object sender, RoutedEventArgs e)  => _log?.Clear();
}
