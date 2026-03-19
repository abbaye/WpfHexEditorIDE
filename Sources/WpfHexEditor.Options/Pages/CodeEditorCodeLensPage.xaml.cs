// ==========================================================
// Project: WpfHexEditor.Options
// File: CodeEditorCodeLensPage.xaml.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-18
// Description:
//     Options page for the CodeEditor CodeLens feature.
//     Master toggle + per-symbol-kind visibility filters.
//     All kinds enabled by default.
//
// Architecture Notes:
//     Pattern: Options Page (IOptionsPage).
//     CodeLensSymbolKinds bit values are duplicated as private int constants
//     to avoid adding a WpfHexEditor.Editor.Core project reference here.
//     AppSettings stores CodeLensVisibleKinds as int for the same reason.
// ==========================================================

using System;
using System.Windows;
using System.Windows.Controls;

namespace WpfHexEditor.Options.Pages;

public sealed partial class CodeEditorCodeLensPage : UserControl, IOptionsPage
{
    public event EventHandler? Changed;
    private bool _loading;

    // ── Bit constants matching CodeLensSymbolKinds (WpfHexEditor.Editor.Core) ──
    // Kept as int to avoid cross-project enum dependency in WpfHexEditor.Options.

    private const int KindClass       = 1 << 0;   //    1
    private const int KindInterface   = 1 << 1;   //    2
    private const int KindStruct      = 1 << 2;   //    4
    private const int KindEnum        = 1 << 3;   //    8
    private const int KindRecord      = 1 << 4;   //   16
    private const int KindDelegate    = 1 << 5;   //   32
    private const int KindMethod      = 1 << 6;   //   64
    private const int KindConstructor = 1 << 7;   //  128
    private const int KindProperty    = 1 << 8;   //  256
    private const int KindIndexer     = 1 << 9;   //  512
    private const int KindField       = 1 << 10;  // 1024
    private const int KindEvent       = 1 << 11;  // 2048
    private const int KindAll         = (1 << 12) - 1; // 4095

    public CodeEditorCodeLensPage()
    {
        InitializeComponent();
    }

    // -- IOptionsPage ----------------------------------------------------------

    public void Load(AppSettings s)
    {
        _loading = true;
        try
        {
            var ce   = s.CodeEditorDefaults;
            CheckCodeLensEnabled.IsChecked = ce.ShowCodeLens;

            // Treat 0 as All (migration: old settings.json without this field defaults to 0).
            int mask = ce.CodeLensVisibleKinds == 0 ? KindAll : ce.CodeLensVisibleKinds;

            ChkClass.IsChecked       = (mask & KindClass)       != 0;
            ChkInterface.IsChecked   = (mask & KindInterface)   != 0;
            ChkStruct.IsChecked      = (mask & KindStruct)      != 0;
            ChkEnum.IsChecked        = (mask & KindEnum)        != 0;
            ChkRecord.IsChecked      = (mask & KindRecord)      != 0;
            ChkDelegate.IsChecked    = (mask & KindDelegate)    != 0;
            ChkMethod.IsChecked      = (mask & KindMethod)      != 0;
            ChkConstructor.IsChecked = (mask & KindConstructor) != 0;
            ChkProperty.IsChecked    = (mask & KindProperty)    != 0;
            ChkIndexer.IsChecked     = (mask & KindIndexer)     != 0;
            ChkField.IsChecked       = (mask & KindField)       != 0;
            ChkEvent.IsChecked       = (mask & KindEvent)       != 0;
        }
        finally { _loading = false; }
    }

    public void Flush(AppSettings s)
    {
        var ce = s.CodeEditorDefaults;
        ce.ShowCodeLens = CheckCodeLensEnabled.IsChecked == true;

        int mask = 0;
        if (ChkClass.IsChecked       == true) mask |= KindClass;
        if (ChkInterface.IsChecked   == true) mask |= KindInterface;
        if (ChkStruct.IsChecked      == true) mask |= KindStruct;
        if (ChkEnum.IsChecked        == true) mask |= KindEnum;
        if (ChkRecord.IsChecked      == true) mask |= KindRecord;
        if (ChkDelegate.IsChecked    == true) mask |= KindDelegate;
        if (ChkMethod.IsChecked      == true) mask |= KindMethod;
        if (ChkConstructor.IsChecked == true) mask |= KindConstructor;
        if (ChkProperty.IsChecked    == true) mask |= KindProperty;
        if (ChkIndexer.IsChecked     == true) mask |= KindIndexer;
        if (ChkField.IsChecked       == true) mask |= KindField;
        if (ChkEvent.IsChecked       == true) mask |= KindEvent;
        ce.CodeLensVisibleKinds = mask;
    }

    // -- Control handlers ------------------------------------------------------

    private void OnCheckChanged(object sender, RoutedEventArgs e)
    {
        if (!_loading) Changed?.Invoke(this, EventArgs.Empty);
    }

    private void OnSelectAll(object sender, RoutedEventArgs e)
    {
        SetAllKinds(true);
    }

    private void OnSelectNone(object sender, RoutedEventArgs e)
    {
        SetAllKinds(false);
    }

    // -- Helpers ---------------------------------------------------------------

    private void SetAllKinds(bool value)
    {
        ChkClass.IsChecked       = value;
        ChkInterface.IsChecked   = value;
        ChkStruct.IsChecked      = value;
        ChkEnum.IsChecked        = value;
        ChkRecord.IsChecked      = value;
        ChkDelegate.IsChecked    = value;
        ChkMethod.IsChecked      = value;
        ChkConstructor.IsChecked = value;
        ChkProperty.IsChecked    = value;
        ChkIndexer.IsChecked     = value;
        ChkField.IsChecked       = value;
        ChkEvent.IsChecked       = value;
        if (!_loading) Changed?.Invoke(this, EventArgs.Empty);
    }
}
