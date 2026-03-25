// ==========================================================
// Project: WpfHexEditor.PluginHost
// File: UI/Options/IDEEventBusOptionsPage.xaml.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-15
// Description:
//     Code-behind for the IDE EventBus options page.
//     Registered in the IDE Options dialog under "Plugin System / Event Bus"
//     via OptionsPageRegistry.RegisterDynamic() in MainWindow.PluginSystem.cs.
//
// Architecture Notes:
//     No separate ViewModel — page is diagnostics-only, no persisted settings.
//     Binds directly to EventRegistryEntry records and a log projection list.
//     Refreshed on demand (Refresh button) and on Loaded.
// ==========================================================

using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using WpfHexEditor.Core.Events;
using WpfHexEditor.PluginHost.Services;

namespace WpfHexEditor.PluginHost.UI.Options;

/// <summary>
/// Options page for the IDE EventBus diagnostics.
/// Accessible via the IDE Options dialog: Plugin System → Event Bus.
/// </summary>
public sealed partial class IDEEventBusOptionsPage : UserControl
{
    private readonly IDEEventBus _eventBus;

    // Observable collections bound to the two list controls.
    private readonly ObservableCollection<EventRegistryEntry> _subscriberRows = [];
    private readonly ObservableCollection<EventLogRow>        _logRows        = [];

    public IDEEventBusOptionsPage(IDEEventBus eventBus)
    {
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        InitializeComponent();

        SubscriberListView.ItemsSource = _subscriberRows;
        EventLogListBox.ItemsSource    = _logRows;

        Loaded += OnLoaded;
    }

    // -- Lifecycle ----------------------------------------------------------------

    private void OnLoaded(object sender, RoutedEventArgs e) => Refresh();

    // -- Button handlers ----------------------------------------------------------

    private void OnRefresh(object sender, RoutedEventArgs e) => Refresh();

    private void OnClearLog(object sender, RoutedEventArgs e)
    {
        _eventBus.ClearLog();
        _logRows.Clear();
    }

    // -- Data refresh -------------------------------------------------------------

    private void Refresh()
    {
        RefreshSubscriberTable();
        RefreshEventLog();
    }

    private void RefreshSubscriberTable()
    {
        _subscriberRows.Clear();
        foreach (var entry in _eventBus.EventRegistry.GetAllEntries())
            _subscriberRows.Add(entry);
    }

    private void RefreshEventLog()
    {
        _logRows.Clear();
        foreach (var evt in _eventBus.GetLog())
            _logRows.Add(new EventLogRow(evt.Timestamp, evt.Source, evt.GetType().Name));
    }

    // -- Inner types --------------------------------------------------------------

    /// <summary>Projection of a raw <see cref="IDEEventBase"/> entry for list display.</summary>
    private sealed record EventLogRow(DateTime Timestamp, string Source, string EventTypeName);
}
