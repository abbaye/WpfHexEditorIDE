// ==========================================================
// Project: WpfHexEditor.Sample.Docking
// File: Panels/PropertiesPanel.xaml.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-07
// Description:
//     Code-behind for PropertiesPanel. Populates the ListView with demo
//     key/value rows that illustrate a typical properties side panel.
//
// Architecture Notes:
//     Theme: DockBackgroundBrush, DockTabTextBrush (dynamic, via XAML bindings).
//     Data is a plain List<PropertyRow> — no MVVM overhead for this demo.
// ==========================================================

using System.Windows.Controls;

namespace WpfHexEditor.Sample.Docking.Panels;

/// <summary>
/// A single property row displayed in the PropertiesPanel grid.
/// </summary>
public sealed record PropertyRow(string Key, string Value);

public partial class PropertiesPanel : UserControl
{
    public PropertiesPanel()
    {
        InitializeComponent();

        PropertyList.ItemsSource = new List<PropertyRow>
        {
            new("Framework",     ".NET 8.0-windows"),
            new("Docking",       "WpfHexEditor.Docking.Wpf"),
            new("Themes",        "Dark · Office Light"),
            new("Layout",        "Persistent (JSON)"),
            new("Float",         "Yes — drag any tab"),
            new("Auto-hide",     "Yes — right-click tab"),
            new("External NuGet","None"),
        };
    }
}
