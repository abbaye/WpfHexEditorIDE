// ==========================================================
// Project: WpfHexEditor.Editor.DocumentEditor
// File: Dialogs/EmbeddedObjectsDialog.xaml.cs
// Description:
//     Forensic review pane: lists images, OLE objects and VBA
//     macros embedded in the active document. Supports extract-
//     to-file and open-in-hex (uses IIDEHostContext.DocumentHost
//     when available to route through the IDE).
// ==========================================================

using System.IO;
using System.IO.Compression;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using WpfHexEditor.Editor.Core.Dialogs;
using WpfHexEditor.Editor.Core.Views;
using WpfHexEditor.Editor.DocumentEditor.Core.Model;
using WpfHexEditor.Editor.DocumentEditor.Properties;
using WpfHexEditor.Editor.DocumentEditor.Services;
using WpfHexEditor.SDK.Contracts;

namespace WpfHexEditor.Editor.DocumentEditor.Dialogs;

public partial class EmbeddedObjectsDialog : ThemedDialog
{
    private readonly DocumentModel _model;
    private readonly IIDEHostContext? _host;
    private readonly List<string> _tempFiles = new();   // tracked for cleanup on Close
    private readonly Dictionary<EmbeddedObjectEntry, string> _tempPathByEntry = new();

    public EmbeddedObjectsDialog(DocumentModel model, IIDEHostContext? host)
    {
        InitializeComponent();
        _model = model ?? throw new ArgumentNullException(nameof(model));
        _host  = host;

        var entries = EmbeddedObjectsScanner.Scan(model);
        PART_List.ItemsSource = entries;
        PART_Subtitle.Text   = model.Metadata?.Title ?? Path.GetFileName(model.FilePath ?? string.Empty);
        PART_CountLabel.Text = string.Format(
            DocumentEditorResources.EmbeddedDlg_CountFmt, entries.Count);

        if (entries.Count > 0)
            PART_List.SelectedIndex = 0;

        PART_SourceSha.Text = DocumentEditorResources.EmbeddedDlg_ShaComputing;
        _ = ComputeSourceShaAsync();

        Closed += OnDialogClosed;
    }

    private async Task ComputeSourceShaAsync()
    {
        if (string.IsNullOrEmpty(_model.FilePath) || !File.Exists(_model.FilePath))
        {
            PART_SourceSha.Text = "—";
            return;
        }
        try
        {
            string hex = await Task.Run(() =>
            {
                using var fs = File.OpenRead(_model.FilePath);
                return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(fs)).ToLowerInvariant();
            });
            PART_SourceSha.Text = hex;
        }
        catch (Exception ex)
        {
            PART_SourceSha.Text = ex.Message;
        }
    }

    private void OnCopyShaClicked(object sender, RoutedEventArgs e)
    {
        try { System.Windows.Clipboard.SetText(PART_SourceSha.Text); }
        catch { /* clipboard busy */ }
    }

    private async void OnHashAllClicked(object sender, RoutedEventArgs e)
    {
        if (PART_List.ItemsSource is not IReadOnlyList<EmbeddedObjectEntry> entries) return;
        try
        {
            await Task.Run(() => HashAllEntriesUsingSharedZip(entries));
            // Refresh so the SHA-256 column re-binds; EmbeddedObjectEntry is a plain CLR
            // object so PropertyChanged would have to be added everywhere — simpler here.
            PART_List.Items.Refresh();
        }
        catch (Exception ex)
        {
            IdeMessageBox.Show(ex.Message, DocumentEditorResources.EmbeddedDlg_Title,
                MessageBoxButton.OK, MessageBoxImage.Error, Window.GetWindow(this));
        }
    }

    /// <summary>
    /// Hashes every entry in one pass, opening the source ZIP at most once.
    /// Inline-data entries and raw-offset entries reuse their bytes directly.
    /// </summary>
    private void HashAllEntriesUsingSharedZip(IReadOnlyList<EmbeddedObjectEntry> entries)
    {
        ZipArchive? sharedZip = null;
        FileStream?  sharedFs  = null;
        bool hasZipSource = !string.IsNullOrEmpty(_model.FilePath) && File.Exists(_model.FilePath);
        try
        {
            foreach (var entry in entries)
            {
                if (!string.IsNullOrEmpty(entry.Sha256)) continue;
                byte[]? bytes = entry.InlineData ?? TryLoadBytesShared(entry, hasZipSource, ref sharedZip, ref sharedFs);
                if (bytes is not null) entry.ComputeHash(bytes);
            }
        }
        finally
        {
            sharedZip?.Dispose();
            sharedFs?.Dispose();
        }
    }

    private byte[]? TryLoadBytesShared(EmbeddedObjectEntry entry, bool hasZipSource,
        ref ZipArchive? sharedZip, ref FileStream? sharedFs)
    {
        try
        {
            if (!string.IsNullOrEmpty(entry.ZipEntryName) && hasZipSource)
            {
                sharedZip ??= ZipFile.OpenRead(_model.FilePath!);
                var ze = sharedZip.GetEntry(entry.ZipEntryName);
                if (ze is null) return null;
                using var s = ze.Open();
                using var ms = new MemoryStream();
                s.CopyTo(ms);
                var bytes = ms.ToArray();
                entry.InlineData = bytes;
                return bytes;
            }
            if (entry.Block is { RawLength: > 0 } blk && hasZipSource)
            {
                sharedFs ??= File.OpenRead(_model.FilePath!);
                sharedFs.Seek(blk.RawOffset, SeekOrigin.Begin);
                var buf = new byte[blk.RawLength];
                sharedFs.ReadExactly(buf);
                entry.InlineData = buf;
                return buf;
            }
            return null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[EmbeddedObjectsDialog] hash skip {entry.Name}: {ex.Message}");
            return null;
        }
    }

    private void OnDialogClosed(object? sender, EventArgs e)
    {
        // Best-effort cleanup — files may still be locked by the hex-editor tab.
        foreach (var path in _tempFiles)
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch { /* still open in another tab; OS will clean up %TEMP% eventually */ }
        }
        _tempFiles.Clear();
        _tempPathByEntry.Clear();
    }

    private void OnItemDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        => OpenSelectedInHex();

    private void OnOpenInHexClicked(object sender, RoutedEventArgs e)
        => OpenSelectedInHex();

    private void OpenSelectedInHex()
    {
        if (PART_List.SelectedItem is not EmbeddedObjectEntry entry) return;
        try
        {
            string tempPath = ExtractToTemp(entry);
            if (_host?.DocumentHost is { } host)
                host.OpenDocument(tempPath, preferredEditorId: "hex-editor");
            else
                ShellOpenSafely(tempPath);
        }
        catch (Exception ex)
        {
            IdeMessageBox.Show(ex.Message, DocumentEditorResources.EmbeddedDlg_Title,
                MessageBoxButton.OK, MessageBoxImage.Error, Window.GetWindow(this));
        }
    }

    /// <summary>
    /// Standalone fallback for opening a temp file when no IDE host is wired.
    /// Restricted to well-known safe extensions to avoid launching arbitrary
    /// executables when the extracted blob happens to carry an .exe/.bat name.
    /// </summary>
    private static void ShellOpenSafely(string path)
    {
        string ext = Path.GetExtension(path).ToLowerInvariant();
        var safe = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".bin", ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".tif", ".tiff",
            ".svg", ".webp", ".ico", ".xml", ".txt", ".json", ".pdf"
        };
        if (!safe.Contains(ext))
        {
            // Open the containing folder rather than running an unknown payload.
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe",
                $"/select,\"{path}\"") { UseShellExecute = true });
            return;
        }
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path)
            { UseShellExecute = true });
    }

    private void OnExtractClicked(object sender, RoutedEventArgs e)
    {
        if (PART_List.SelectedItem is not EmbeddedObjectEntry entry) return;
        var dlg = new SaveFileDialog
        {
            FileName = entry.Name,
            Title    = DocumentEditorResources.EmbeddedDlg_ExtractToolTip
        };
        if (dlg.ShowDialog(Window.GetWindow(this)) != true) return;

        try
        {
            byte[] bytes = LoadBytes(entry);
            File.WriteAllBytes(dlg.FileName, bytes);
        }
        catch (Exception ex)
        {
            IdeMessageBox.Show(ex.Message, DocumentEditorResources.EmbeddedDlg_Title,
                MessageBoxButton.OK, MessageBoxImage.Error, Window.GetWindow(this));
        }
    }

    private void OnCloseClicked(object sender, RoutedEventArgs e) => Close();

    // ── Extraction helpers ────────────────────────────────────────────────

    private byte[] LoadBytes(EmbeddedObjectEntry entry)
    {
        if (entry.InlineData is not null) return entry.InlineData;

        if (!string.IsNullOrEmpty(entry.ZipEntryName) &&
            !string.IsNullOrEmpty(_model.FilePath) &&
            File.Exists(_model.FilePath))
        {
            using var zip = ZipFile.OpenRead(_model.FilePath);
            var ze = zip.GetEntry(entry.ZipEntryName);
            if (ze is null)
                throw new FileNotFoundException(
                    string.Format(DocumentEditorResources.EmbeddedDlg_EntryNotFoundFmt, entry.ZipEntryName));
            using var s  = ze.Open();
            using var ms = new MemoryStream();
            s.CopyTo(ms);
            byte[] bytes = ms.ToArray();
            // Cache so subsequent double-clicks on the same entry don't re-open the ZIP.
            entry.InlineData = bytes;
            return bytes;
        }

        if (entry.Block is not null && entry.Block.RawLength > 0 &&
            !string.IsNullOrEmpty(_model.FilePath) && File.Exists(_model.FilePath))
        {
            using var fs = File.OpenRead(_model.FilePath);
            fs.Seek(entry.Block.RawOffset, SeekOrigin.Begin);
            byte[] buf = new byte[entry.Block.RawLength];
            fs.ReadExactly(buf);
            entry.InlineData = buf;
            return buf;
        }

        throw new InvalidOperationException(DocumentEditorResources.EmbeddedDlg_NoSource);
    }

    private string ExtractToTemp(EmbeddedObjectEntry entry)
    {
        // Reuse the previously-extracted temp file when re-opening the same entry.
        if (_tempPathByEntry.TryGetValue(entry, out var cached) && File.Exists(cached))
            return cached;

        byte[] bytes = LoadBytes(entry);
        string baseName = string.IsNullOrEmpty(entry.Name) ? "embed" : Path.GetFileName(entry.Name);
        string tempPath = Path.Combine(
            Path.GetTempPath(),
            $"whdoc_{Guid.NewGuid():N}_{baseName}");
        File.WriteAllBytes(tempPath, bytes);
        _tempFiles.Add(tempPath);
        _tempPathByEntry[entry] = tempPath;
        return tempPath;
    }
}
