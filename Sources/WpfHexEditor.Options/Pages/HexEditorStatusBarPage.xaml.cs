// ==========================================================
// Project: WpfHexEditor.Options
// File: HexEditorStatusBarPage.xaml.cs
// Author: Auto
// Created: 2026-03-06
// Description:
//     Options page — controls which items are visible inside
//     the HexEditor built-in status bar.
//
// Architecture Notes:
//     Implements IOptionsPage (Load / Flush / Changed pattern).
//     Category: "Hex Editor" | Page: "Status Bar"
// ==========================================================

using System;
using System.Windows;
using System.Windows.Controls;

namespace WpfHexEditor.Options.Pages;

public sealed partial class HexEditorStatusBarPage : UserControl, IOptionsPage
{
    public event EventHandler? Changed;
    private bool _loading;

    public HexEditorStatusBarPage() => InitializeComponent();

    // ── IOptionsPage ──────────────────────────────────────────────────────

    public void Load(AppSettings s)
    {
        _loading = true;
        try
        {
            CheckShowStatusMessage.IsChecked = s.HexEditorDefaults.ShowStatusMessage;
            CheckShowFileSize.IsChecked      = s.HexEditorDefaults.ShowFileSizeInStatusBar;
            CheckShowSelection.IsChecked     = s.HexEditorDefaults.ShowSelectionInStatusBar;
            CheckShowPosition.IsChecked      = s.HexEditorDefaults.ShowPositionInStatusBar;
            CheckShowEditMode.IsChecked      = s.HexEditorDefaults.ShowEditModeInStatusBar;
            CheckShowBytesPerLine.IsChecked  = s.HexEditorDefaults.ShowBytesPerLineInStatusBar;
        }
        finally { _loading = false; }
    }

    public void Flush(AppSettings s)
    {
        s.HexEditorDefaults.ShowStatusMessage        = CheckShowStatusMessage.IsChecked == true;
        s.HexEditorDefaults.ShowFileSizeInStatusBar  = CheckShowFileSize.IsChecked      == true;
        s.HexEditorDefaults.ShowSelectionInStatusBar = CheckShowSelection.IsChecked     == true;
        s.HexEditorDefaults.ShowPositionInStatusBar  = CheckShowPosition.IsChecked      == true;
        s.HexEditorDefaults.ShowEditModeInStatusBar  = CheckShowEditMode.IsChecked      == true;
        s.HexEditorDefaults.ShowBytesPerLineInStatusBar = CheckShowBytesPerLine.IsChecked == true;
    }

    // ── Control handlers ─────────────────────────────────────────────────

    private void OnCheckChanged(object sender, RoutedEventArgs e)
    {
        if (!_loading) Changed?.Invoke(this, EventArgs.Empty);
    }
}
