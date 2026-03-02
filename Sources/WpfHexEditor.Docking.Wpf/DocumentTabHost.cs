//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.5, Claude Sonnet 4.6
//////////////////////////////////////////////

using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WpfHexEditor.Docking.Core;
using WpfHexEditor.Docking.Core.Nodes;
using WpfHexEditor.Docking.Wpf.Controls;

namespace WpfHexEditor.Docking.Wpf;

/// <summary>
/// WPF projection of <see cref="DocumentHostNode"/>: specialized tab host for documents.
/// Visually distinct from tool panel tabs (different background, tab style).
/// Supports VS2026-style multi-row tabs and a settings gear button via
/// <see cref="Settings"/>.
/// </summary>
public class DocumentTabHost : DockTabControl
{
    // ─── Settings DP ─────────────────────────────────────────────────────────

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

    // ─── Constructor ─────────────────────────────────────────────────────────

    public DocumentTabHost()
    {
        SetResourceReference(StyleProperty, "DocumentTabHostStyle");
    }

    // ─── Template wiring ─────────────────────────────────────────────────────

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        WireTemplateParts();
    }

    private void WireTemplateParts()
    {
        if (GetTemplateChild("PART_ConfigButton") is TabConfigButton configBtn)
            configBtn.Settings = Settings;
    }

    // ─── Settings change handling ─────────────────────────────────────────────

    private static void OnSettingsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not DocumentTabHost host) return;

        if (e.OldValue is DocumentTabBarSettings old)
            old.PropertyChanged -= host.OnSettingPropertyChanged;

        if (e.NewValue is DocumentTabBarSettings newSettings)
            newSettings.PropertyChanged += host.OnSettingPropertyChanged;

        host.ApplySettings();
    }

    private void OnSettingPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(DocumentTabBarSettings.MultiRowTabs))
            ApplySettings();
    }

    private void ApplySettings()
    {
        var styleName = Settings?.MultiRowTabs == true
            ? "DocumentTabHostMultiRowStyle"
            : "DocumentTabHostStyle";

        SetResourceReference(StyleProperty, styleName);
        // OnApplyTemplate is called automatically after the style change, which re-wires parts.
    }

    // ─── Placeholder ─────────────────────────────────────────────────────────

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
