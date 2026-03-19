// ==========================================================
// Project: WpfHexEditor.Shell
// File: DocumentTabHost.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude (Anthropic)
// Created: 2026-03-06
// Description:
//     WPF projection of DocumentHostNode: a specialized tab control for editor
//     documents. Visually distinct from tool panel tabs with a different background
//     and tab style. Supports VS2026-style multi-row tabs, tab colorization via
//     TabColorService, and a settings gear button via TabConfigButton.
//
// Architecture Notes:
//     Inherits DockTabControl. DocumentTabBarSettings shared instance drives tab
//     placement, colorization mode, and multi-row behavior through DependencyProperty.
//     TabColorizerAttached provides the per-tab AccentBrush attached property.
//
// ==========================================================

using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WpfHexEditor.Docking.Core;
using WpfHexEditor.Docking.Core.Nodes;
using WpfHexEditor.Shell.Attached;
using WpfHexEditor.Shell.Controls;
using WpfHexEditor.Shell.Services;

namespace WpfHexEditor.Shell;

/// <summary>
/// WPF projection of <see cref="DocumentHostNode"/>: specialized tab host for documents.
/// Visually distinct from tool panel tabs (different background, tab style).
/// Supports VS2026-style multi-row tabs and a settings gear button via
/// <see cref="Settings"/>.
/// </summary>
public class DocumentTabHost : DockTabControl
{
    // --- Settings DP ---------------------------------------------------------

    public static readonly DependencyProperty SettingsProperty =
        DependencyProperty.Register(
            nameof(Settings),
            typeof(DocumentTabBarSettings),
            typeof(DocumentTabHost),
            new PropertyMetadata(null, OnSettingsChanged));

    /// <summary>
    /// Shared settings object that drives tab bar behaviour (placement, multi-row, etc.).
    /// The same instance should be kept on <c>DockLayoutRoot</c> and <c>DockControl</c>
    /// so in-place mutation propagates everywhere automatically.
    /// </summary>
    public DocumentTabBarSettings? Settings
    {
        get => (DocumentTabBarSettings?)GetValue(SettingsProperty);
        set => SetValue(SettingsProperty, value);
    }

    // --- Constructor ---------------------------------------------------------

    public DocumentTabHost()
    {
        SetResourceReference(StyleProperty, "DocumentTabHostStyle");
    }

    // --- Template wiring -----------------------------------------------------

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        WireTemplateParts();
    }

    private void WireTemplateParts()
    {
        if (GetTemplateChild("PART_ConfigButton") is TabConfigButton configBtn)
        {
            configBtn.Settings = Settings;
            configBtn.OptionsRequested += OnOptionsRequested;
        }

        if (GetTemplateChild("PART_OverflowPanel") is UIElement panel)
            panel.PreviewMouseWheel += OnTabStripMouseWheel;
    }

    // --- Settings change handling ---------------------------------------------

    private static void OnSettingsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not DocumentTabHost host) return;

        if (e.OldValue is DocumentTabBarSettings old)
            old.PropertyChanged -= host.OnSettingPropertyChanged;

        if (e.NewValue is DocumentTabBarSettings newSettings)
            newSettings.PropertyChanged += host.OnSettingPropertyChanged;

        host.ApplySettings();
        host.ApplyTabColors();
    }

    private void OnSettingPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(DocumentTabBarSettings.MultiRowTabs)
                            or nameof(DocumentTabBarSettings.TabPlacement))
            ApplySettings();

        if (e.PropertyName is nameof(DocumentTabBarSettings.ColorMode)
                            or nameof(DocumentTabBarSettings.RegexRules))
            ApplyTabColors();
    }

    private void ApplySettings()
    {
        var styleName = (Settings?.MultiRowTabs == true, Settings?.TabPlacement) switch
        {
            (true, _)                       => "DocumentTabHostMultiRowStyle",
            (_, DocumentTabPlacement.Left)  => "DocumentTabHostLeftStyle",
            (_, DocumentTabPlacement.Right) => "DocumentTabHostRightStyle",
            _                               => "DocumentTabHostStyle"
        };

        SetResourceReference(StyleProperty, styleName);
        // OnApplyTemplate is called automatically after the style change, which re-wires parts.
    }

    // --- Mouse wheel on tab strip ---------------------------------------------

    private void OnTabStripMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (Settings?.MultiRowWithMouseWheel == true)
        {
            Settings.MultiRowTabs = !Settings.MultiRowTabs;
            e.Handled = true;
        }
    }

    // --- Tab colorization -----------------------------------------------------

    protected override void OnItemsChanged(NotifyCollectionChangedEventArgs e)
    {
        base.OnItemsChanged(e);
        if (e.NewItems is not null && Settings?.ColorMode != DocumentTabColorMode.None)
            foreach (TabItem tab in e.NewItems.OfType<TabItem>())
                ApplyTabColor(tab);
    }

    internal void ApplyTabColors()
    {
        foreach (TabItem tab in Items.OfType<TabItem>())
            ApplyTabColor(tab);
    }

    private void ApplyTabColor(TabItem tab)
    {
        var brush = tab.Tag is DockItem item && Settings is not null
            ? TabColorService.GetTabBrush(item, Settings)
            : null;
        TabColorizerAttached.SetAccentBrush(tab, brush ?? Brushes.Transparent);
    }

    // --- Options dialog -------------------------------------------------------

    private void OnOptionsRequested(object? sender, EventArgs e)
    {
        if (Settings is null) return;
        var dlg = new Dialogs.TabSettingsDialog
        {
            Settings = Settings,
            Owner = Window.GetWindow(this)
        };
        dlg.ShowDialog();
    }

    // --- Placeholder ---------------------------------------------------------

    /// <summary>
    /// Shows a placeholder when no documents are open.
    /// </summary>
    public void ShowEmptyPlaceholder()
    {
        Items.Clear();
        var placeholder = new TextBlock
        {
            Text = "Open a document to begin",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = Brushes.Gray,
            FontSize = 14
        };

        // Wrap in a tab to maintain visual consistency
        Items.Add(new TabItem
        {
            Header = "Start",
            Content = placeholder,
            IsEnabled = false
        });
    }
}
