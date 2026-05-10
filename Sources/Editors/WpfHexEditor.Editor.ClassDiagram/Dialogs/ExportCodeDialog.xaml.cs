// ==========================================================
// Project: WpfHexEditor.Editor.ClassDiagram
// File: Dialogs/ExportCodeDialog.xaml.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.7
// Created: 2026-05-08
// Description:
//     Code-behind for the ExportCodeDialog. Wires the dialog
//     to its ViewModel and provides preset shortcut handlers.
// ==========================================================

using System.Windows;
using WpfHexEditor.Editor.ClassDiagram.Core.CodeGen.Options;
using WpfHexEditor.Editor.ClassDiagram.Services;
using WpfHexEditor.Editor.Core.Views;

namespace WpfHexEditor.Editor.ClassDiagram.Dialogs;

/// <summary>Dialog that lets the user customise <see cref="CodeGenOptions"/> before exporting.</summary>
public partial class ExportCodeDialog : ThemedDialog
{
    private readonly ExportCodeDialogViewModel _viewModel;

    /// <summary>Creates a dialog hydrated from <paramref name="initialSettings"/>.</summary>
    public ExportCodeDialog(CodeGenSettings initialSettings)
    {
        ArgumentNullException.ThrowIfNull(initialSettings);
        InitializeComponent();
        _viewModel = new ExportCodeDialogViewModel(initialSettings);
        DataContext = _viewModel;
    }

    /// <summary>The settings selected by the user when the dialog closes with OK.</summary>
    public CodeGenSettings? Result { get; private set; }

    private void OnOkClick(object sender, RoutedEventArgs e)
    {
        Result = _viewModel.BuildSettings();
        DialogResult = true;
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        Result = null;
        DialogResult = false;
    }

    private void OnPresetDefaultClick(object sender, RoutedEventArgs e) =>
        _viewModel.ApplyPreset(CodeGenOptions.Default);

    private void OnPresetModernClick(object sender, RoutedEventArgs e) =>
        _viewModel.ApplyPreset(CodeGenOptions.ModernCSharp);

    private void OnPresetLegacyClick(object sender, RoutedEventArgs e) =>
        _viewModel.ApplyPreset(CodeGenOptions.LegacyCSharp);
}
