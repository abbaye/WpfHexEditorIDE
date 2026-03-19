// ==========================================================
// Project: WpfHexEditor.Editor.XamlDesigner
// File: DesignDataPanel.xaml.cs
// Author: Derek Tremblay
// Created: 2026-03-17
// Description:
//     Code-behind for the Design-Time Data panel.
//     Detects d:DesignInstance in the current XAML, attempts Activator.CreateInstance
//     on the type, and displays the resolved instance properties.
//
// Architecture Notes:
//     VS-Like dockable panel.
//     Lifecycle rule: OnUnloaded must NOT null _xamlSource.
//     Delegates parsing to DesignTimeDataService; updates UI directly
//     (no separate ViewModel to avoid over-engineering for a read-only panel).
// ==========================================================

using System.Windows;
using System.Windows.Controls;
using WpfHexEditor.Editor.XamlDesigner.Services;

namespace WpfHexEditor.Editor.XamlDesigner.Panels;

/// <summary>
/// Panel that inspects design-time data (d:DesignInstance) for the current XAML.
/// </summary>
public partial class DesignDataPanel : UserControl
{
    private readonly DesignTimeXamlPreprocessor _preprocessor = new();
    private string _xamlSource = string.Empty;

    // ── Constructor ───────────────────────────────────────────────────────────

    public DesignDataPanel()
    {
        InitializeComponent();
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Sets the XAML source and refreshes the panel display.
    /// Called by the plugin host whenever the XAML changes.
    /// </summary>
    public void SetXamlSource(string xaml)
    {
        _xamlSource = xaml;
        Refresh();
    }

    // ── Event handlers ────────────────────────────────────────────────────────

    private void OnRefreshClick(object sender, RoutedEventArgs e)
        => Refresh();

    // ── Private ───────────────────────────────────────────────────────────────

    private void Refresh()
    {
        if (!DesignTimeXamlPreprocessor.HasDesignNamespace(_xamlSource))
        {
            ShowPlaceholder("No d:DesignInstance detected in current XAML.");
            return;
        }

        _preprocessor.Process(_xamlSource, out object? instance);

        if (instance is null)
        {
            ShowPlaceholder("d:DesignInstance found but type could not be resolved or instantiated.");
            return;
        }

        ShowInstance(instance);
    }

    private void ShowPlaceholder(string message)
    {
        PlaceholderText.Text       = message;
        PlaceholderText.Visibility = Visibility.Visible;
        DataInfoPanel.Visibility   = Visibility.Collapsed;
    }

    private void ShowInstance(object instance)
    {
        PlaceholderText.Visibility = Visibility.Collapsed;
        DataInfoPanel.Visibility   = Visibility.Visible;

        TypeLabel.Text   = instance.GetType().FullName ?? instance.GetType().Name;
        SourceLabel.Text = "d:DesignInstance (auto-detected)";
        StatusLabel.Text = "Resolved";

        // Populate property list via reflection.
        var props = instance.GetType()
            .GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
            .Where(p => p.CanRead && p.GetIndexParameters().Length == 0)
            .Select(p =>
            {
                string? val;
                try   { val = p.GetValue(instance)?.ToString() ?? "(null)"; }
                catch { val = "(error)"; }
                return new { Key = p.Name, Value = val };
            })
            .ToList();

        PropertyList.ItemsSource = props;
    }
}
