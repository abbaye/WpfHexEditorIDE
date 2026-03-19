// ==========================================================
// Project: WpfHexEditor.Editor.XamlDesigner
// File: PropertyInspectorPanel.xaml.cs
// Author: Derek Tremblay
// Created: 2026-03-16
// Updated: 2026-03-19 — Added BindingBadgeClicked event, OnBindingBadgeMouseDown handler,
//                        OnResetValueClick handler, BrushPropertyTemplate registration in
//                        PropertyEditorTemplateSelector, ToggleGroupButton wiring.
// Description:
//     Code-behind for the XAML Property Inspector dockable panel.
//     Wires DataTemplateSelector for property value cells and manages
//     ToolbarOverflowManager for the filter toolbar group.
//
// Architecture Notes:
//     VS-Like Panel Pattern. Never nulls _vm on OnUnloaded (MEMORY.md rule).
//     Phase D: DataTemplateSelector dispatches to rich editors based on
//     PropertyInspectorEntry.PropertyType: Bool, Thickness, Enum, Numeric,
//     Color, FontFamily, Brush — falls back to TextPropertyTemplate for all others.
//     Phase upgrade 2026-03-19:
//       · BindingBadgeClicked — raised when the {B} badge is clicked.
//       · OnResetValueClick   — calls entry.ResetToDefault() if defined.
//       · ToggleGroupButton   — wired to vm.ToggleGroupCommand.
// ==========================================================

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
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

        // Wire the DataTemplateSelector directly to the GridViewColumn.
        ValueColumn.CellTemplateSelector = new PropertyEditorTemplateSelector(this);

        Loaded   += OnLoaded;
        Unloaded += OnUnloaded;
    }

    // ── Events ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Raised when the user clicks the {B} binding badge on a property row.
    /// Allows the host to open the Binding Inspector for the specific property.
    /// </summary>
    public event EventHandler<PropertyInspectorEntry>? BindingBadgeClicked;

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

    // ── Binding badge handler ─────────────────────────────────────────────────

    /// <summary>
    /// Handles MouseDown on the {B} binding badge TextBlock.
    /// Fires <see cref="BindingBadgeClicked"/> with the associated entry.
    /// Tag is set in XAML to {Binding} so it carries the PropertyInspectorEntry.
    /// </summary>
    internal void OnBindingBadgeMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left) return;

        if (sender is FrameworkElement fe && fe.Tag is PropertyInspectorEntry entry)
        {
            BindingBadgeClicked?.Invoke(this, entry);
            e.Handled = true;
        }
    }

    // ── Reset value handler ───────────────────────────────────────────────────

    /// <summary>
    /// Handles Click on the inline Reset (✕) button inside TextPropertyTemplate
    /// and BrushPropertyTemplate.
    /// Currently resets via clearing the local value by setting Value to null.
    /// A future iteration may call a dedicated ResetToDefault() on the entry.
    /// </summary>
    internal void OnResetValueClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is PropertyInspectorEntry entry)
        {
            // Clear the locally set value — triggers write-back pipeline via INPC.
            entry.Value = null;
            e.Handled = true;
        }
    }

    // ── Size changes ──────────────────────────────────────────────────────────

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        if (sizeInfo.WidthChanged)
            _overflowManager?.Update();
    }

    // ── Inner: DataTemplateSelector ───────────────────────────────────────────

    /// <summary>
    /// Selects the appropriate DataTemplate for a property value cell
    /// based on <see cref="PropertyInspectorEntry.PropertyType"/>.
    /// Dispatch priority (first match wins):
    ///   bool                → BoolPropertyTemplate
    ///   Thickness           → ThicknessPropertyTemplate
    ///   Enum                → EnumPropertyTemplate
    ///   double/float/int    → NumericPropertyTemplate
    ///   Color (struct)      → ColorPropertyTemplate
    ///   Brush (hierarchy)   → BrushPropertyTemplate   ← added 2026-03-19
    ///   FontFamily          → FontPropertyTemplate
    ///   (default)           → TextPropertyTemplate
    /// </summary>
    private sealed class PropertyEditorTemplateSelector : DataTemplateSelector
    {
        private readonly PropertyInspectorPanel _panel;

        public PropertyEditorTemplateSelector(PropertyInspectorPanel panel)
            => _panel = panel;

        public override DataTemplate? SelectTemplate(object item, DependencyObject container)
        {
            if (item is not PropertyInspectorEntry entry) return null;

            var key = ResolveTemplateKey(entry.PropertyType);
            return _panel.Resources[key] as DataTemplate;
        }

        private static string ResolveTemplateKey(Type propertyType)
        {
            if (propertyType == typeof(bool))
                return "BoolPropertyTemplate";

            if (propertyType == typeof(Thickness))
                return "ThicknessPropertyTemplate";

            if (propertyType.IsEnum)
                return "EnumPropertyTemplate";

            if (propertyType == typeof(double) || propertyType == typeof(float) || propertyType == typeof(int))
                return "NumericPropertyTemplate";

            // Color struct → dedicated color picker.
            if (propertyType == typeof(Color))
                return "ColorPropertyTemplate";

            // Brush hierarchy (SolidColorBrush, LinearGradientBrush, etc.) → swatch + hex editor.
            if (typeof(Brush).IsAssignableFrom(propertyType))
                return "BrushPropertyTemplate";

            if (propertyType == typeof(FontFamily))
                return "FontPropertyTemplate";

            return "TextPropertyTemplate";
        }
    }
}
