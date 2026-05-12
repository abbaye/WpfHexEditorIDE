// ==========================================================
// Project: WpfHexEditor.Editor.DocumentEditor
// File: Dialogs/DocumentDiffDialog.xaml.cs
// Description:
//     Side-by-side structural diff between the active document
//     and a user-picked file. Loads the second file via the
//     registered IDocumentLoader extensions, then renders the
//     LCS-based block diff (DocumentDiffService) row by row.
// ==========================================================

using System.IO;
using System.Windows;
using Microsoft.Win32;
using WpfHexEditor.Editor.Core.Dialogs;
using WpfHexEditor.Editor.Core.Views;
using WpfHexEditor.Editor.DocumentEditor.Core;
using WpfHexEditor.Editor.DocumentEditor.Core.Model;
using WpfHexEditor.Editor.DocumentEditor.Properties;
using WpfHexEditor.Editor.DocumentEditor.Services;
using WpfHexEditor.SDK.Contracts;

namespace WpfHexEditor.Editor.DocumentEditor.Dialogs;

public partial class DocumentDiffDialog : ThemedDialog
{
    private readonly DocumentModel _left;
    private readonly IIDEHostContext? _host;

    public DocumentDiffDialog(DocumentModel left, IIDEHostContext? host)
    {
        InitializeComponent();
        _left = left ?? throw new ArgumentNullException(nameof(left));
        _host = host;

        PART_LeftPath.Text  = left.FilePath ?? "(unsaved)";
        PART_RightPath.Text = DocumentEditorResources.DocDiffDlg_PickHint;
    }

    private void OnCloseClicked(object sender, RoutedEventArgs e) => Close();

    private async void OnPickRightClicked(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title  = DocumentEditorResources.DocDiffDlg_Title,
            Filter = BuildLoaderFilter(),
        };
        if (dlg.ShowDialog(Window.GetWindow(this)) != true) return;

        try
        {
            var right = await LoadAsync(dlg.FileName);
            PART_RightPath.Text = dlg.FileName;
            Render(right);
        }
        catch (Exception ex)
        {
            IdeMessageBox.Show(ex.Message, DocumentEditorResources.DocDiffDlg_Title,
                MessageBoxButton.OK, MessageBoxImage.Error, Window.GetWindow(this));
        }
    }

    private void Render(DocumentModel right)
    {
        var rows = DocumentDiffService.Diff(_left, right);
        PART_List.ItemsSource = rows;

        int added    = 0, removed = 0, modified = 0, equal = 0;
        foreach (var r in rows)
        {
            switch (r.Kind)
            {
                case DocumentDiffKind.Added:    added++;    break;
                case DocumentDiffKind.Removed:  removed++;  break;
                case DocumentDiffKind.Modified: modified++; break;
                default:                        equal++;    break;
            }
        }
        PART_SummaryLabel.Text = string.Format(
            DocumentEditorResources.DocDiffDlg_SummaryFmt,
            added, removed, modified, equal);
    }

    private async Task<DocumentModel> LoadAsync(string path)
    {
        if (_host?.ExtensionRegistry is null)
            throw new InvalidOperationException(DocumentEditorResources.DocDiffDlg_NoLoaders);
        var loaders = _host.ExtensionRegistry.GetExtensions<IDocumentLoader>();
        var loader  = loaders.FirstOrDefault(l => l.CanLoad(path));
        if (loader is null)
            throw new InvalidOperationException(
                string.Format(DocumentEditorResources.DocDiffDlg_NoLoaderForFmt, Path.GetExtension(path)));

        // Loader contract: it sets FilePath on the model; we don't preset it.
        var model = new DocumentModel();
        // Off-thread parse so large docs don't freeze the dispatcher.
        await Task.Run(async () =>
        {
            await using var stream = File.OpenRead(path);
            await loader.LoadAsync(path, stream, model);
        });
        return model;
    }

    private string BuildLoaderFilter()
    {
        if (_host?.ExtensionRegistry is null) return "All Files|*.*";
        var loaders = _host.ExtensionRegistry.GetExtensions<IDocumentLoader>().ToList();
        var parts = loaders
            .Select(l => $"{l.LoaderName}|*.{string.Join(";*.", l.SupportedExtensions)}")
            .ToList();
        parts.Add("All Files|*.*");
        return string.Join("|", parts);
    }
}
