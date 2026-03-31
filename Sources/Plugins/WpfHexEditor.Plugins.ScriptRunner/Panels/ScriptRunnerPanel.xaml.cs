// ==========================================================
// Project: WpfHexEditor.Plugins.ScriptRunner
// File: Panels/ScriptRunnerPanel.xaml.cs
// Description:
//     Code-behind for the ScriptRunner dockable panel.
//     Replaces plain TextBox with CodeEditorSplitHost for syntax highlighting
//     and scriptGlobals SmartComplete (driven from CSharpScript.whfmt).
//     Two-way sync between CodeBox and ScriptRunnerViewModel.Code
//     uses _editorUpdating flag to prevent feedback loops.
// ==========================================================

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WpfHexEditor.Core.ProjectSystem.Languages;
using WpfHexEditor.Plugins.ScriptRunner.ViewModels;

namespace WpfHexEditor.Plugins.ScriptRunner.Panels;

/// <summary>
/// Dockable panel providing a code input area and script output pane.
/// </summary>
public partial class ScriptRunnerPanel : UserControl
{
    private readonly ScriptRunnerViewModel _vm;
    private bool _editorUpdating;

    public ScriptRunnerPanel(ScriptRunnerViewModel vm)
    {
        _vm         = vm;
        DataContext = vm;
        InitializeComponent();
        Loaded += OnLoaded;

        // Auto-scroll output when text changes.
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ScriptRunnerViewModel.Output))
                Dispatcher.InvokeAsync(() => OutputBox.ScrollToEnd());

            // Reflect Code changes from outside (e.g. file load) into the editor.
            if (e.PropertyName == nameof(ScriptRunnerViewModel.Code))
            {
                if (_editorUpdating) return;
                _editorUpdating = true;
                CodeBox.PrimaryEditor.LoadText(_vm.Code ?? string.Empty);
                _editorUpdating = false;
            }
        };
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Wire language → syntax highlighting + scriptGlobals SmartComplete.
        var lang = LanguageRegistry.Instance.FindById("csharp-script");
        if (lang is not null)
            CodeBox.SetLanguage(lang);

        // Sync initial Code value into editor.
        if (!string.IsNullOrEmpty(_vm.Code))
            CodeBox.PrimaryEditor.LoadText(_vm.Code);

        // Push editor changes back to ViewModel.
        CodeBox.PrimaryEditor.ModifiedChanged += (_, _) =>
        {
            if (_editorUpdating) return;
            _editorUpdating = true;
            _vm.Code = CodeBox.PrimaryEditor.GetText();
            _editorUpdating = false;
        };
    }

    // ── Keyboard shortcuts ────────────────────────────────────────────────────

    private void OnCodeBoxKeyDown(object sender, KeyEventArgs e)
    {
        // F5 → Run
        if (e.Key == Key.F5 && Keyboard.Modifiers == ModifierKeys.None)
        {
            if (_vm.RunCommand.CanExecute(null))
                _vm.RunCommand.Execute(null);
            e.Handled = true;
        }
        // Escape → Cancel
        else if (e.Key == Key.Escape)
        {
            if (_vm.CancelCommand.CanExecute(null))
                _vm.CancelCommand.Execute(null);
            e.Handled = true;
        }
    }
}
