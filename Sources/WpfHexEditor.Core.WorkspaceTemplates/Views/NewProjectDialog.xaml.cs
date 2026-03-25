// ==========================================================
// Project: WpfHexEditor.Core.WorkspaceTemplates
// File: Views/NewProjectDialog.xaml.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-16
// Description:
//     Code-behind for the 3-step New Project wizard dialog.
//     Manages step visibility transitions and calls the
//     ProjectScaffolder when the user clicks Finish.
// ==========================================================

using System.Windows;
using System.Windows.Forms;
using WpfHexEditor.Core.WorkspaceTemplates.ViewModels;

namespace WpfHexEditor.Core.WorkspaceTemplates.Views;

/// <summary>
/// Code-behind for the New Project wizard dialog.
/// </summary>
public sealed partial class NewProjectDialog : Window
{
    // -----------------------------------------------------------------------
    // Fields + result
    // -----------------------------------------------------------------------

    private readonly NewProjectDialogViewModel _vm;

    /// <summary>Scaffold result set after successful Finish.</summary>
    public ScaffoldResult? Result { get; private set; }

    // -----------------------------------------------------------------------
    // Construction
    // -----------------------------------------------------------------------

    public NewProjectDialog(TemplateManager templateManager)
    {
        InitializeComponent();
        _vm = new NewProjectDialogViewModel(templateManager);
        DataContext = _vm;
        _vm.PropertyChanged += OnVmPropertyChanged;
    }

    // -----------------------------------------------------------------------
    // Step transition
    // -----------------------------------------------------------------------

    private void OnVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(NewProjectDialogViewModel.CurrentStep)
                           or nameof(NewProjectDialogViewModel.IsStep1)
                           or nameof(NewProjectDialogViewModel.IsStep2)
                           or nameof(NewProjectDialogViewModel.IsStep3))
        {
            UpdateStepVisibility();
        }
    }

    private void UpdateStepVisibility()
    {
        Step1Panel.Visibility = _vm.IsStep1 ? Visibility.Visible : Visibility.Collapsed;
        Step2Panel.Visibility = _vm.IsStep2 ? Visibility.Visible : Visibility.Collapsed;
        Step3Panel.Visibility = _vm.IsStep3 ? Visibility.Visible : Visibility.Collapsed;

        // Update step labels.
        StepLabel1.Style = _vm.IsStep1 ? (System.Windows.Style)Resources["ActiveStepStyle"] : (System.Windows.Style)Resources["StepLabelStyle"];
        StepLabel2.Style = _vm.IsStep2 ? (System.Windows.Style)Resources["ActiveStepStyle"] : (System.Windows.Style)Resources["StepLabelStyle"];
        StepLabel3.Style = _vm.IsStep3 ? (System.Windows.Style)Resources["ActiveStepStyle"] : (System.Windows.Style)Resources["StepLabelStyle"];

        // Update title.
        StepTitle.Text = _vm.CurrentStep switch
        {
            1 => "Select a template",
            2 => "Configure your project",
            3 => "Optional plugins",
            _ => string.Empty,
        };

        // Show Finish on last step, Next on earlier steps.
        BtnNext.Visibility   = _vm.CurrentStep < 3 ? Visibility.Visible : Visibility.Collapsed;
        BtnFinish.Visibility = _vm.CurrentStep == 3 ? Visibility.Visible : Visibility.Collapsed;
    }

    // -----------------------------------------------------------------------
    // Button handlers
    // -----------------------------------------------------------------------

    private void OnBack(object sender, RoutedEventArgs e)   => _vm.GoBack();
    private void OnNext(object sender, RoutedEventArgs e)   => _vm.GoNext();
    private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;

    private async void OnFinish(object sender, RoutedEventArgs e)
    {
        BtnFinish.IsEnabled = false;
        try
        {
            Result = await _vm.ScaffoldAsync();
            DialogResult = true;
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"Failed to create project:\n{ex.Message}",
                "New Project",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            BtnFinish.IsEnabled = true;
        }
    }

    private void OnBrowseDirectory(object sender, RoutedEventArgs e)
    {
        using var dlg = new FolderBrowserDialog
        {
            Description         = "Select a parent directory for the new project",
            SelectedPath        = _vm.ParentDirectory,
            UseDescriptionForTitle = true,
        };

        if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            _vm.ParentDirectory = dlg.SelectedPath;
    }
}
