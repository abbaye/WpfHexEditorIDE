// ==========================================================
// Project: WpfHexEditor.Editor.XamlDesigner
// File: FontPickerEditor.xaml.cs
// Author: Derek Tremblay
// Created: 2026-03-17
// Description:
//     Inline font family editor using a ComboBox populated with
//     all system fonts. Each item renders in its own font.
//
// Architecture Notes:
//     UserControl. FamilyName DP bound two-way by PropertyInspectorPanel.
//     System fonts loaded on first show to avoid startup delay.
// ==========================================================

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace WpfHexEditor.Editor.XamlDesigner.Controls.Editors;

/// <summary>
/// Font family picker combo-box for the Property Inspector.
/// </summary>
public partial class FontPickerEditor : UserControl
{
    private static IReadOnlyList<FontFamily>? _systemFonts;

    // ── Dependency Property ───────────────────────────────────────────────────

    public static readonly DependencyProperty FamilyNameProperty =
        DependencyProperty.Register(nameof(FamilyName), typeof(string), typeof(FontPickerEditor),
            new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public string FamilyName
    {
        get => (string)GetValue(FamilyNameProperty);
        set => SetValue(FamilyNameProperty, value);
    }

    // ── Constructor ───────────────────────────────────────────────────────────

    public FontPickerEditor()
    {
        InitializeComponent();
        DataContext = this;
        Loaded += OnLoaded;
    }

    // ── Events ────────────────────────────────────────────────────────────────

    public event EventHandler<FontFamily>? ValueCommitted;

    // ── Private ───────────────────────────────────────────────────────────────

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_systemFonts is null)
        {
            _systemFonts = Fonts.SystemFontFamilies
                .OrderBy(f => f.Source)
                .ToList();
        }

        if (CbFont.ItemsSource is null)
            CbFont.ItemsSource = _systemFonts;
    }

    private void OnFontSelected(object sender, SelectionChangedEventArgs e)
    {
        if (CbFont.SelectedItem is FontFamily ff)
        {
            FamilyName = ff.Source;
            ValueCommitted?.Invoke(this, ff);
        }
    }
}
