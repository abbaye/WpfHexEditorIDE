// Project      : WpfHexEditorControl
// File         : ViewModels/ArchiveExplorerViewModel.cs
// Description  : Main ViewModel for the Archive Explorer panel.
//                Drives async archive loading, tree building, filter, nested
//                archive drill-down navigation, and command execution.
//
// Architecture : INPC / RelayCommand. No direct WPF dependency except
//                ObservableCollection (WindowsBase). All services injected.
//
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6

using System.Collections.ObjectModel;
using System.IO;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using WpfHexEditor.Plugins.ArchiveExplorer.Models;
using WpfHexEditor.Plugins.ArchiveExplorer.Services;
using WpfHexEditor.SDK.Commands;
using WpfHexEditor.SDK.Contracts.Services;
using WpfHexEditor.Core.ViewModels;

namespace WpfHexEditor.Plugins.ArchiveExplorer.ViewModels;

/// <summary>
/// ViewModel bound to <c>ArchiveExplorerPanel</c>.
/// </summary>
public sealed class ArchiveExplorerViewModel : ViewModelBase, IDisposable
{
    // ── Services ───────────────────────────────────────────────────────────
    private PreviewService?  _preview;
    private ExtractService?  _extract;
    private IArchiveReader?  _reader;
    private CancellationTokenSource _cts = new();

    // ── Navigation ─────────────────────────────────────────────────────────
    private readonly Stack<string> _navStack = new();

    // ── Settings surface ───────────────────────────────────────────────────
    public bool ShowCompressionRatio { get; set; } = true;
    public bool ShowFormatBadge      { get; set; } = true;
    public int  MaxFormatDetectionKb { get; set; } = 512;
    public int  PreviewMaxSizeKb     { get; set; } = 5120;

    // ── Observable state ──────────────────────────────────────────────────
    public ObservableCollection<ArchiveNodeViewModel> RootNodes { get; } = [];

    private ArchiveNodeViewModel? _selectedNode;
    public ArchiveNodeViewModel? SelectedNode
    {
        get => _selectedNode;
        set { if (_selectedNode != value) { _selectedNode = value; OnPropertyChanged(); RaiseCanExecuteChanged(); } }
    }

    private string _filterText = string.Empty;
    public string FilterText
    {
        get => _filterText;
        set { if (_filterText != value) { _filterText = value; OnPropertyChanged(); ApplyFilter(); } }
    }

    private string _infoBarText = string.Empty;
    public string InfoBarText
    {
        get => _infoBarText;
        set { if (_infoBarText != value) { _infoBarText = value; OnPropertyChanged(); } }
    }

    private bool _isLoading;
    public bool IsLoading
    {
        get => _isLoading;
        set { if (_isLoading != value) { _isLoading = value; OnPropertyChanged(); RaiseCanExecuteChanged(); } }
    }

    private string? _currentArchivePath;
    public string? CurrentArchivePath
    {
        get => _currentArchivePath;
        private set { if (_currentArchivePath != value) { _currentArchivePath = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanNavigateUp)); } }
    }

    public bool CanNavigateUp => _navStack.Count > 0;

    // ── Commands ───────────────────────────────────────────────────────────
    public ICommand ExtractAllCommand      { get; }
    public ICommand ExtractSelectedCommand { get; }
    public ICommand PreviewCommand         { get; }
    public ICommand OpenRawInHexCommand    { get; }
    public ICommand CopyPathCommand        { get; }
    public ICommand ShowPropertiesCommand  { get; }
    public ICommand ExpandAllCommand       { get; }
    public ICommand CollapseAllCommand     { get; }
    public ICommand DrillIntoCommand       { get; }
    public ICommand NavigateUpCommand      { get; }
    public ICommand RefreshCommand         { get; }
    public ICommand ClearFilterCommand     { get; }

    // ── Constructor ────────────────────────────────────────────────────────
    public ArchiveExplorerViewModel()
    {
        ExtractAllCommand      = new RelayCommand(_ => _ = ExtractAllAsync(),      _ => _reader is not null && !IsLoading);
        ExtractSelectedCommand = new RelayCommand(_ => _ = ExtractSelectedAsync(), _ => SelectedNode is { IsFolder: false } && _reader is not null);
        PreviewCommand         = new RelayCommand(_ => _ = PreviewAsync(),         _ => SelectedNode is { IsFolder: false } && _reader is not null);
        OpenRawInHexCommand    = new RelayCommand(_ => _ = PreviewRawAsync(),      _ => SelectedNode is { IsFolder: false } && _reader is not null);
        CopyPathCommand        = new RelayCommand(_ => CopyPath(),                 _ => SelectedNode is not null);
        ShowPropertiesCommand  = new RelayCommand(_ => ShowProperties(),           _ => SelectedNode is not null);
        ExpandAllCommand       = new RelayCommand(_ => SetExpandAll(true),         _ => RootNodes.Count > 0);
        CollapseAllCommand     = new RelayCommand(_ => SetExpandAll(false),        _ => RootNodes.Count > 0);
        DrillIntoCommand       = new RelayCommand(_ => _ = DrillIntoAsync(),       _ => SelectedNode?.IsArchive == true && _reader is not null);
        NavigateUpCommand      = new RelayCommand(_ => _ = NavigateUpAsync(),      _ => CanNavigateUp);
        RefreshCommand         = new RelayCommand(_ => _ = RefreshAsync(),         _ => CurrentArchivePath is not null && !IsLoading);
        ClearFilterCommand     = new RelayCommand(_ => FilterText = string.Empty,  _ => FilterText.Length > 0);
    }

    // ── Service injection (called by panel after construction) ─────────────
    public void SetServices(PreviewService preview, ExtractService extract)
    {
        _preview = preview;
        _extract = extract;
    }

    // ── Public load API ────────────────────────────────────────────────────

    /// <summary>Loads an archive from disk, replacing any current content.</summary>
    public async Task LoadArchiveAsync(string archivePath, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(archivePath) || IsLoading) return;

        // Cancel any in-flight load
        _cts.Cancel();
        _cts.Dispose();
        _cts = new CancellationTokenSource();
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, ct);

        IsLoading = true;
        RootNodes.Clear();
        InfoBarText = "Loadingâ€¦";
        CurrentArchivePath = archivePath;

        try
        {
            _reader?.Dispose();
            _reader = null;

            (IArchiveReader reader, List<ArchiveNodeViewModel> nodes, ArchiveStats stats) =
                await Task.Run(() =>
            {
                IArchiveReader r = ArchiveReaderFactory.CreateReader(archivePath)
                    ?? throw new NotSupportedException($"Unsupported format: {Path.GetExtension(archivePath)}");
                List<ArchiveNodeViewModel> root = BuildTree(r.Entries, archivePath);
                ArchiveStats s                  = ComputeStats(r.Entries);
                return (r, root, s);
            }, linked.Token).ConfigureAwait(false);

            _reader = reader;

            // Must update UI on the calling (UI) thread â€” await ensures we're back
            foreach (var node in nodes)
                RootNodes.Add(node);

            InfoBarText = BuildInfoBar(reader.Format, stats);

            if (ShowFormatBadge)
                _ = EnrichFormatBadgesAsync(linked.Token);
        }
        catch (OperationCanceledException) { InfoBarText = string.Empty; }
        catch (Exception ex)               { InfoBarText = $"Error: {ex.Message}"; }
        finally                            { IsLoading = false; }
    }

    // ── Command handlers ───────────────────────────────────────────────────

    private async Task ExtractAllAsync()
    {
        if (_reader is null || _extract is null) return;
        await _extract.ExtractAsync(_reader, null, _cts.Token).ConfigureAwait(false);
    }

    private async Task ExtractSelectedAsync()
    {
        if (_reader is null || _extract is null || SelectedNode is null) return;
        var entries = new[] { SelectedNode.Node.Entry! };
        await _extract.ExtractAsync(_reader, entries, _cts.Token).ConfigureAwait(false);
    }

    private async Task PreviewAsync()
    {
        if (_reader is null || _preview is null || SelectedNode is null) return;
        if (SelectedNode.Node.Entry is not { } entry) return;
        await _preview.PreviewAsync(_reader, entry, PreviewMaxSizeKb, _cts.Token).ConfigureAwait(false);
    }

    private async Task PreviewRawAsync()
    {
        if (_reader is null || _preview is null || SelectedNode is null) return;
        if (SelectedNode.Node.Entry is not { } entry) return;
        await _preview.PreviewRawAsync(_reader, entry, _cts.Token).ConfigureAwait(false);
    }

    private void CopyPath()
    {
        if (SelectedNode is null) return;
        System.Windows.Clipboard.SetText(SelectedNode.FullPath);
    }

    private void ShowProperties()
    {
        // Opened from panel code-behind (requires WPF Window)
        PropertiesRequested?.Invoke(this, SelectedNode!);
    }

    private async Task DrillIntoAsync()
    {
        if (_reader is null || SelectedNode?.Node.Entry is not { } entry) return;
        if (CurrentArchivePath is not null)
            _navStack.Push(CurrentArchivePath);
        OnPropertyChanged(nameof(CanNavigateUp));

        // Extract inner archive to temp then load it
        var tempPath = Path.Combine(
            Path.GetTempPath(), "WpfHexEditor", "ArchiveExplorer",
            $"nested_{Guid.NewGuid():N}_{entry.FullPath.Replace('/', '_')}");
        Directory.CreateDirectory(Path.GetDirectoryName(tempPath)!);
        await _reader.ExtractEntryAsync(entry, tempPath, _cts.Token).ConfigureAwait(false);
        await LoadArchiveAsync(tempPath, _cts.Token).ConfigureAwait(false);
    }

    private async Task NavigateUpAsync()
    {
        if (!_navStack.TryPop(out var prev)) return;
        OnPropertyChanged(nameof(CanNavigateUp));
        await LoadArchiveAsync(prev, _cts.Token).ConfigureAwait(false);
    }

    private async Task RefreshAsync()
    {
        if (CurrentArchivePath is null) return;
        var path = CurrentArchivePath;
        CurrentArchivePath = null;
        await LoadArchiveAsync(path, _cts.Token).ConfigureAwait(false);
    }

    private void SetExpandAll(bool expanded)
    {
        foreach (var node in AllNodes(RootNodes))
            node.IsExpanded = expanded;
    }

    // ── Filter ─────────────────────────────────────────────────────────────

    private void ApplyFilter()
    {
        var text = _filterText;
        if (string.IsNullOrWhiteSpace(text))
        {
            foreach (var n in AllNodes(RootNodes)) n.IsVisible = true;
            return;
        }
        foreach (var root in RootNodes)
            FilterNode(root, text);
    }

    private static bool FilterNode(ArchiveNodeViewModel vm, string text)
    {
        bool anyChild = false;
        foreach (var child in vm.Children)
            anyChild |= FilterNode(child, text);

        bool match = vm.Name.Contains(text, StringComparison.OrdinalIgnoreCase);
        vm.IsVisible = match || anyChild;
        if (anyChild) vm.IsExpanded = true;
        return vm.IsVisible;
    }

    // ── Format badge enrichment ────────────────────────────────────────────

    private async Task EnrichFormatBadgesAsync(CancellationToken ct)
    {
        if (_reader is null) return;
        await Task.Yield(); // yield to let UI render first

        foreach (var vm in AllNodes(RootNodes).Where(n => !n.IsFolder))
        {
            ct.ThrowIfCancellationRequested();
            if (vm.Node.Entry is not { } entry) continue;
            if (entry.Size > MaxFormatDetectionKb * 1024L) continue;

            try
            {
                byte[] header;
                await using (var stream = _reader.OpenEntry(entry))
                {
                    var buf = new byte[Math.Min(512, entry.Size)];
                    int read = await stream.ReadAsync(buf.AsMemory(0, buf.Length), ct);
                    header = buf[..read];
                }
                // Heuristic: check magic bytes for common formats
                var badge = DetectBadge(header);
                if (badge is not null)
                    vm.FormatBadge = badge;
            }
            catch { /* best-effort */ }
        }
    }

    private static string? DetectBadge(byte[] data)
    {
        if (data.Length < 4) return null;
        // PE
        if (data[0] == 'M' && data[1] == 'Z') return "PE";
        // ELF
        if (data[0] == 0x7F && data[1] == 'E' && data[2] == 'L' && data[3] == 'F') return "ELF";
        // PNG
        if (data[0] == 0x89 && data[1] == 'P' && data[2] == 'N' && data[3] == 'G') return "PNG";
        // JPEG
        if (data[0] == 0xFF && data[1] == 0xD8) return "JPEG";
        // PDF
        if (data[0] == '%' && data[1] == 'P' && data[2] == 'D' && data[3] == 'F') return "PDF";
        // ZIP
        if (data[0] == 'P' && data[1] == 'K') return "ZIP";
        return null;
    }

    // ── Tree building ──────────────────────────────────────────────────────

    private static List<ArchiveNodeViewModel> BuildTree(
        IReadOnlyList<ArchiveEntry> entries, string archivePath)
    {
        var root     = new ArchiveNode { Name = "root", FullPath = "", IsFolder = true };
        var folderMap = new Dictionary<string, ArchiveNode> { [""] = root };

        foreach (var entry in entries)
        {
            var path = entry.FullPath.TrimEnd('/');
            if (entry.IsDirectory)
            {
                EnsureFolder(path, folderMap, root, archivePath);
            }
            else
            {
                var dir    = GetDirectoryPart(path);
                var parent = EnsureFolder(dir, folderMap, root, archivePath);
                var node   = new ArchiveNode
                {
                    Name              = Path.GetFileName(path),
                    FullPath          = entry.FullPath,
                    IsFolder          = false,
                    Entry             = entry,
                    SourceArchivePath = archivePath,
                    Parent            = parent,
                };
                parent.Children.Add(node);
            }
        }

        // Return top-level children of the synthetic root
        return root.Children.Select(n => new ArchiveNodeViewModel(n)).ToList();
    }

    private static ArchiveNode EnsureFolder(
        string path,
        Dictionary<string, ArchiveNode> map,
        ArchiveNode root,
        string archivePath)
    {
        if (string.IsNullOrEmpty(path)) return root;
        if (map.TryGetValue(path, out var existing)) return existing;

        var parentPath = GetDirectoryPart(path);
        var parent     = EnsureFolder(parentPath, map, root, archivePath);
        var node       = new ArchiveNode
        {
            Name              = GetLastSegment(path),
            FullPath          = path + "/",
            IsFolder          = true,
            SourceArchivePath = archivePath,
            Parent            = parent,
        };
        parent.Children.Add(node);
        map[path] = node;
        return node;
    }

    private static string GetDirectoryPart(string path)
    {
        var idx = path.LastIndexOf('/');
        return idx < 0 ? "" : path[..idx];
    }

    private static string GetLastSegment(string path)
    {
        var idx = path.LastIndexOf('/');
        return idx < 0 ? path : path[(idx + 1)..];
    }

    // ── Statistics ─────────────────────────────────────────────────────────

    private static ArchiveStats ComputeStats(IReadOnlyList<ArchiveEntry> entries)
    {
        int files = 0, folders = 0;
        long total = 0, compressed = 0;
        foreach (var e in entries)
        {
            if (e.IsDirectory) folders++;
            else               { files++; total += e.Size; compressed += e.CompressedSize; }
        }
        return new ArchiveStats
        {
            TotalFiles     = files,
            TotalFolders   = folders,
            TotalSize      = total,
            CompressedSize = compressed,
        };
    }

    private static string BuildInfoBar(ArchiveFormat fmt, ArchiveStats s)
    {
        var fmtStr = fmt.ToString();
        var size   = FormatSize(s.TotalSize);
        var ratio  = s.CompressionRatioPct.ToString("F1");
        return $"{fmtStr.ToUpperInvariant()} | {s.TotalFiles} files | {size} | {ratio}% ratio";
    }

    private static string FormatSize(long bytes) => bytes switch
    {
        < 1024L                => $"{bytes} B",
        < 1024L * 1024         => $"{bytes / 1024.0:F1} KB",
        < 1024L * 1024 * 1024  => $"{bytes / 1024.0 / 1024:F1} MB",
        _                      => $"{bytes / 1024.0 / 1024 / 1024:F2} GB",
    };

    // ── Events ─────────────────────────────────────────────────────────────

    /// <summary>Raised when the user requests to view properties for a node.</summary>
    public event EventHandler<ArchiveNodeViewModel>? PropertiesRequested;

    // ── Helpers ────────────────────────────────────────────────────────────

    private static IEnumerable<ArchiveNodeViewModel> AllNodes(
        IEnumerable<ArchiveNodeViewModel> nodes)
    {
        foreach (var n in nodes)
        {
            yield return n;
            foreach (var child in AllNodes(n.Children))
                yield return child;
        }
    }

    private void RaiseCanExecuteChanged()
        => System.Windows.Input.CommandManager.InvalidateRequerySuggested();

    // ── INotifyPropertyChanged ─────────────────────────────────────────────

    // ── IDisposable ────────────────────────────────────────────────────────
    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
        _reader?.Dispose();
        _reader = null;
    }
}
