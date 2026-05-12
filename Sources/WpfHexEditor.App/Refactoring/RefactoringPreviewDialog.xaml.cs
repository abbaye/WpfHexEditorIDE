// ==========================================================
// Project: WpfHexEditor.App
// File: Refactoring/RefactoringPreviewDialog.xaml.cs
// Description:
//     Modal preview dialog for a refactoring. Bind a list of
//     RefactoringPreviewRow (from WpfHexEditor.Core.LSP.Refactoring) to
//     show File / Line / Original / Replacement. Returns DialogResult=true
//     when the user clicks Apply.
// ==========================================================

using System.Windows;
using WpfHexEditor.Core.LSP.Refactoring;

namespace WpfHexEditor.App.Refactoring;

public sealed partial class RefactoringPreviewDialog : Window
{
    public RefactoringPreviewDialog(IReadOnlyList<RefactoringPreviewRow> rows)
    {
        InitializeComponent();
        PART_Grid.ItemsSource = rows;
    }

    private void OnApplyClicked(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void OnCancelClicked(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
