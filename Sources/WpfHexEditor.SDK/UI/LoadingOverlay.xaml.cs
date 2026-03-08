// ==========================================================
// Project: WpfHexEditor.SDK
// File: UI/LoadingOverlay.xaml.cs
// Author: Derek Tremblay
// Created: 2026-03-08
// Description:
//     Code-behind for LoadingOverlay UserControl.
//     Exposes IsActive (bool) and Label (string) DependencyProperties.
//     Visibility is toggled via PropertyChangedCallback on IsActive
//     so the host panel needs no style triggers.
//
// Architecture Notes:
//     Pattern: UserControl with DependencyProperty (Proxy/Decorator-free).
//     Thread-safety: DPs are UI-thread-only (standard WPF contract).
// ==========================================================

using System.Windows;
using System.Windows.Controls;

namespace WpfHexEditor.SDK.UI;

/// <summary>
/// Semi-transparent loading scrim with a centered label and indeterminate progress bar.
/// Set <see cref="IsActive"/> to <c>true</c> to show the overlay; <c>false</c> to hide it.
/// </summary>
public partial class LoadingOverlay : UserControl
{
    // ── Dependency Properties ─────────────────────────────────────────────────

    /// <summary>Controls overlay visibility. Bind to a ViewModel's IsLoading property.</summary>
    public static readonly DependencyProperty IsActiveProperty =
        DependencyProperty.Register(
            nameof(IsActive),
            typeof(bool),
            typeof(LoadingOverlay),
            new PropertyMetadata(false, OnIsActiveChanged));

    /// <summary>Text displayed above the progress bar.</summary>
    public static readonly DependencyProperty LabelProperty =
        DependencyProperty.Register(
            nameof(Label),
            typeof(string),
            typeof(LoadingOverlay),
            new PropertyMetadata("Loading...", OnLabelChanged));

    // ── CLR Wrappers ──────────────────────────────────────────────────────────

    /// <summary>Gets or sets whether the overlay is visible.</summary>
    public bool IsActive
    {
        get => (bool)GetValue(IsActiveProperty);
        set => SetValue(IsActiveProperty, value);
    }

    /// <summary>Gets or sets the loading label text.</summary>
    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    // ── Constructor ───────────────────────────────────────────────────────────

    public LoadingOverlay()
    {
        InitializeComponent();
        LabelText.Text = Label;
    }

    // ── Callbacks ─────────────────────────────────────────────────────────────

    private static void OnIsActiveChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is LoadingOverlay overlay)
            overlay.Visibility = (bool)e.NewValue ? Visibility.Visible : Visibility.Collapsed;
    }

    private static void OnLabelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is LoadingOverlay overlay)
            overlay.LabelText.Text = e.NewValue as string ?? "Loading...";
    }
}
