// ==========================================================
// Project: WpfHexEditor.Plugins.AssemblyExplorer
// File: Controls/FilterBox.xaml.cs
// Author: Derek Tremblay
// Created: 2026-03-08
// Description:
//     Code-behind for the FilterBox composite control.
//     Manages placeholder visibility, clear button, and FilterText DP.
// ==========================================================

using System.Windows;
using System.Windows.Controls;

namespace WpfHexEditor.Plugins.AssemblyExplorer.Controls;

/// <summary>
/// Composite filter TextBox with inline clear button and placeholder text.
/// Exposes <see cref="FilterText"/> as a bindable dependency property.
/// </summary>
public partial class FilterBox : UserControl
{
    // ── Dependency Properties ─────────────────────────────────────────────────

    public static readonly DependencyProperty FilterTextProperty =
        DependencyProperty.Register(
            nameof(FilterText),
            typeof(string),
            typeof(FilterBox),
            new FrameworkPropertyMetadata(
                string.Empty,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnFilterTextPropertyChanged));

    public string FilterText
    {
        get => (string)GetValue(FilterTextProperty);
        set => SetValue(FilterTextProperty, value);
    }

    // ── Events ────────────────────────────────────────────────────────────────

    public event EventHandler<string>? FilterChanged;

    // ── Constructor ───────────────────────────────────────────────────────────

    public FilterBox()
        => InitializeComponent();

    // ── Handlers ──────────────────────────────────────────────────────────────

    private void OnTextChanged(object sender, TextChangedEventArgs e)
    {
        var text = FilterTextBox.Text;
        FilterText           = text;
        PlaceholderText.Visibility = string.IsNullOrEmpty(text) ? Visibility.Visible : Visibility.Collapsed;
        ClearButton.Visibility     = string.IsNullOrEmpty(text) ? Visibility.Collapsed : Visibility.Visible;
        FilterChanged?.Invoke(this, text);
    }

    private void OnClearClicked(object sender, RoutedEventArgs e)
    {
        FilterTextBox.Clear();
        FilterTextBox.Focus();
    }

    // ── DP callback — keeps TextBox in sync when bound source changes ─────────

    private static void OnFilterTextPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FilterBox box) return;
        var newText = (string)(e.NewValue ?? string.Empty);
        if (box.FilterTextBox.Text != newText)
            box.FilterTextBox.Text = newText;
    }
}
