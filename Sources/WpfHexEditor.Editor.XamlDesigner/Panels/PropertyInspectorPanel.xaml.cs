// ==========================================================
// Project: WpfHexEditor.Editor.XamlDesigner
// File: PropertyInspectorPanel.xaml.cs
// Author: Derek Tremblay
// Created: 2026-03-16
// Description:
//     Code-behind for the XAML Property Inspector dockable panel.
//     Wires DataTemplateSelector for property value cells and manages
//     ToolbarOverflowManager for the filter toolbar group.
//
// Architecture Notes:
//     VS-Like Panel Pattern. Never nulls _vm on OnUnloaded (MEMORY.md rule).
//     DataTemplateSelector dispatches bool vs. text value editors.
// ==========================================================

using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using WpfHexEditor.Editor.XamlDesigner.Models;
using WpfHexEditor.Editor.XamlDesigner.ViewModels;
using WpfHexEditor.SDK.UI;

namespace WpfHexEditor.Editor.XamlDesigner.Panels;

/// <summary>
/// Property Inspector dockable panel — lists DependencyProperties of the selected element.
/// </summary>
public partial class PropertyInspectorPanel : UserControl
{
    // ── State ─────────────────────────────────────────────────────────────────

    private PropertyInspectorPanelViewModel _vm = new();
    private ToolbarOverflowManager?         _overflowManager;

    // ── Constructor ───────────────────────────────────────────────────────────

    public PropertyInspectorPanel()
    {
        InitializeComponent();
        DataContext = _vm;

        // Wire value cell template selector.
        PropertyList.Resources.Add("PropertyValueTemplateSelector", new PropertyEditorTemplateSelector(this));

        Loaded   += OnLoaded;
        Unloaded += OnUnloaded;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Exposes the ViewModel for external wiring by the plugin.</summary>
    public PropertyInspectorPanelViewModel ViewModel => _vm;

    /// <summary>Updates the "element name" banner at the top of the panel.</summary>
    public void SetElementName(string? name)
        => TbkElementName.Text = string.IsNullOrEmpty(name) ? "(no selection)" : name;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _overflowManager ??= new ToolbarOverflowManager(
            ToolbarBorder,
            ToolbarRightPanel,
            ToolbarOverflowButton,
            null,
            new FrameworkElement[] { TbgFilter },
            leftFixedElements: null);

        Dispatcher.InvokeAsync(
            () => _overflowManager.CaptureNaturalWidths(),
            DispatcherPriority.Loaded);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        // Per MEMORY.md rule: never null _vm on unload.
    }

    // ── Size changes ──────────────────────────────────────────────────────────

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        if (sizeInfo.WidthChanged)
            _overflowManager?.Update();
    }

    // ── Inner: DataTemplateSelector ───────────────────────────────────────────

    private sealed class PropertyEditorTemplateSelector : DataTemplateSelector
    {
        private readonly PropertyInspectorPanel _panel;

        public PropertyEditorTemplateSelector(PropertyInspectorPanel panel)
            => _panel = panel;

        public override DataTemplate? SelectTemplate(object item, DependencyObject container)
        {
            if (item is not PropertyInspectorEntry entry) return null;

            if (entry.PropertyType == typeof(bool))
                return _panel.Resources["BoolPropertyTemplate"] as DataTemplate;

            return _panel.Resources["TextPropertyTemplate"] as DataTemplate;
        }
    }
}
