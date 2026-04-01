// ==========================================================
// Project: WpfHexEditor.Editor.DocumentEditor
// File: Controls/DocumentFindReplaceDialog.xaml.cs
// Description:
//     Find & Replace dialog for the document editor (Phase 19).
//     Reuses DocumentSearchViewModel. Ctrl+F → FindOnly, Ctrl+H → with Replace.
//     F3 → FindNext, Shift+F3 → FindPrevious.
// ==========================================================

using System.Windows;
using System.Windows.Input;
using WpfHexEditor.Editor.DocumentEditor.ViewModels;

namespace WpfHexEditor.Editor.DocumentEditor.Controls;

public partial class DocumentFindReplaceDialog : Window
{
    public DocumentFindReplaceDialog(DocumentSearchViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
        KeyDown    += OnKeyDown;
    }

    public bool ShowReplacePanel
    {
        get => PART_ReplaceRow.Visibility == Visibility.Visible;
        set => PART_ReplaceRow.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) { Hide(); e.Handled = true; }
        if (e.Key == Key.F3 && (Keyboard.Modifiers & ModifierKeys.Shift) != 0)
        {
            ((DocumentSearchViewModel)DataContext).FindPreviousCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.F3)
        {
            ((DocumentSearchViewModel)DataContext).FindNextCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void OnCloseClicked(object sender, RoutedEventArgs e) => Hide();
}
