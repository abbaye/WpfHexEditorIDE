// ==========================================================
// Project: WpfHexEditor.Editor.ImageViewer
// File: ResizeImageDialog.xaml.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-06
// Description:
//     Code-behind for ResizeImageDialog. Handles aspect-ratio
//     locking, numeric-only validation, and result exposure.
//
// Architecture Notes:
//     Extends ThemedDialog (custom chrome).
//     Pattern: Modal dialog returning typed result via DialogResult.
//     Aspect-ratio locked via double _aspectRatio field.
//
// ==========================================================

using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WpfHexEditor.Editor.Core.Views;
using WpfHexEditor.Editor.ImageViewer.Transforms;

namespace WpfHexEditor.Editor.ImageViewer.Dialogs;

public partial class ResizeImageDialog : ThemedDialog
{
    private double _aspectRatio = 1.0;
    private bool   _updatingFields;

    // -- Results exposed to caller ----------------------------------------

    public int TargetWidth  { get; private set; }
    public int TargetHeight { get; private set; }
    public ResizeAlgorithm Algorithm { get; private set; }

    // -- Constructor -------------------------------------------------------

    public ResizeImageDialog(int currentWidth, int currentHeight)
    {
        InitializeComponent();

        _aspectRatio = currentWidth > 0 && currentHeight > 0
            ? (double)currentWidth / currentHeight
            : 1.0;

        CurrentSizeHint.Text = $"Current size: {currentWidth} Ã— {currentHeight} px";
        WidthBox.Text        = currentWidth.ToString();
        HeightBox.Text       = currentHeight.ToString();

        WidthBox.Focus();
        WidthBox.SelectAll();
    }

    // -- Event handlers ----------------------------------------------------

    private void OnWidthChanged(object sender, TextChangedEventArgs e)
    {
        if (_updatingFields) return;
        if (!int.TryParse(WidthBox.Text, out int w) || w <= 0)
        {
            OkButton.IsEnabled = false;
            return;
        }

        if (LockRatioCheck.IsChecked == true && _aspectRatio > 0)
        {
            _updatingFields = true;
            HeightBox.Text  = Math.Max(1, (int)Math.Round(w / _aspectRatio)).ToString();
            _updatingFields = false;
        }

        OkButton.IsEnabled = IsInputValid();
    }

    private void OnHeightChanged(object sender, TextChangedEventArgs e)
    {
        if (_updatingFields) return;
        if (!int.TryParse(HeightBox.Text, out int h) || h <= 0)
        {
            OkButton.IsEnabled = false;
            return;
        }

        if (LockRatioCheck.IsChecked == true && _aspectRatio > 0)
        {
            _updatingFields = true;
            WidthBox.Text   = Math.Max(1, (int)Math.Round(h * _aspectRatio)).ToString();
            _updatingFields = false;
        }

        OkButton.IsEnabled = IsInputValid();
    }

    private void OnNumericOnly(object sender, TextCompositionEventArgs e) =>
        e.Handled = !Regex.IsMatch(e.Text, @"^\d+$");

    private void OnNumericPaste(object sender, DataObjectPastingEventArgs e)
    {
        if (e.DataObject.GetDataPresent(typeof(string)))
        {
            var text = (string)e.DataObject.GetData(typeof(string));
            if (!Regex.IsMatch(text, @"^\d+$")) e.CancelCommand();
        }
        else
        {
            e.CancelCommand();
        }
    }

    private void OnOk(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(WidthBox.Text,  out int w) || w <= 0) return;
        if (!int.TryParse(HeightBox.Text, out int h) || h <= 0) return;

        TargetWidth  = w;
        TargetHeight = h;
        Algorithm    = (ResizeAlgorithm)AlgorithmBox.SelectedIndex;
        DialogResult = true;
    }

    private void OnCancel(object sender, RoutedEventArgs e) =>
        DialogResult = false;

    // -- Helpers -----------------------------------------------------------

    private bool IsInputValid() =>
        int.TryParse(WidthBox.Text,  out int w) && w > 0 &&
        int.TryParse(HeightBox.Text, out int h) && h > 0;
}
