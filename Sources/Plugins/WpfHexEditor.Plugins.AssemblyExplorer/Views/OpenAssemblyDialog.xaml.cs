// ==========================================================
// Project: WpfHexEditor.Plugins.AssemblyExplorer
// File: Views/OpenAssemblyDialog.xaml.cs
// Author: Derek Tremblay
// Created: 2026-03-08
// Description:
//     Code-behind for the "Open Assembly" redesigned dialog.
//     Inherits ThemedDialog for VS2022-style custom chrome.
//     Input paths via: drop zone drag-and-drop (whole window),
//     Ctrl+V clipboard paste (Explorer FileDrop or raw text),
//     Browse button (OpenFileDialog), Recent tab double-click,
//     .NET Runtimes tab single-click leaf selection / double-click.
//
// Architecture Notes:
//     Pattern: Dialog (modal) — returns selected path via SelectedFilePath.
//     Data models: RecentFileItem, RuntimeGroupNode, VersionGroupNode, AssemblyFileNode
//       are file-scoped display models; no MVVM overhead for a single-use dialog.
//     .NET runtime discovery: enumerates %ProgramFiles%\dotnet\shared\ (BCL only).
// ==========================================================

using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using WpfHexEditor.Editor.Core.Views;
using WpfHexEditor.Plugins.AssemblyExplorer.Options;

namespace WpfHexEditor.Plugins.AssemblyExplorer.Views;

// ── Display models ─────────────────────────────────────────────────────────────

/// <summary>Display model for the Recent tab list items.</summary>
internal sealed class RecentFileItem
{
    public string FileName { get; init; } = string.Empty;
    public string FullPath { get; init; } = string.Empty;
}

/// <summary>Leaf node: a single .dll file under a versioned runtime directory.</summary>
internal sealed class AssemblyFileNode
{
    public string FileName { get; init; } = string.Empty;
    public string FullPath { get; init; } = string.Empty;
}

/// <summary>Intermediate node: a specific .NET runtime version (e.g. "8.0.13").</summary>
internal sealed class VersionGroupNode
{
    public string                   Version      { get; init; } = string.Empty;
    public List<AssemblyFileNode>   Assemblies   { get; init; } = [];
    public int                      AssemblyCount => Assemblies.Count;
}

/// <summary>Root node: a .NET runtime pack (e.g. "Microsoft.NETCore.App").</summary>
internal sealed class RuntimeGroupNode
{
    public string                   Name     { get; init; } = string.Empty;
    public List<VersionGroupNode>   Versions { get; init; } = [];
}

// ── Dialog ─────────────────────────────────────────────────────────────────────

/// <summary>
/// Dialog to select a .dll or .exe for analysis.
/// Show with <see cref="ShowDialog"/>; read <see cref="SelectedFilePath"/> on true result.
/// </summary>
public partial class OpenAssemblyDialog : ThemedDialog
{
    // ── Public output ──────────────────────────────────────────────────────────

    /// <summary>
    /// All selected paths (1..N). Valid only when <see cref="ShowDialog"/> returns true.
    /// Contains more than one item when the user selected a runtime group or version node.
    /// </summary>
    public List<string> SelectedFilePaths { get; private set; } = [];

    /// <summary>First selected path — convenience accessor for single-file callers.</summary>
    public string SelectedFilePath => SelectedFilePaths.Count > 0 ? SelectedFilePaths[0] : string.Empty;

    // ── Private state ──────────────────────────────────────────────────────────

    /// <summary>All leaf nodes from all runtimes — used for the flat filtered list.</summary>
    private List<AssemblyFileNode> _allRuntimeAssemblies = [];

    /// <summary>
    /// When a non-leaf tree node is selected, holds all its descendant DLL paths so
    /// <see cref="Accept"/> can return them all at once. Null for single-file selection.
    /// </summary>
    private List<string>? _pendingMultiplePaths;

    // ── Construction ──────────────────────────────────────────────────────────

    public OpenAssemblyDialog()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    /// <summary>Optional pre-fill: set the path box text before the dialog is shown.</summary>
    public void PreFillPath(string path)
    {
        // Called before ShowDialog() — InitializeComponent not yet done.
        // Store and apply in Loaded handler if needed, or set directly if already loaded.
        if (IsLoaded)
            SetPath(path);
        else
            Loaded += (_, _) => SetPath(path);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        PopulateRecentList();
        PopulateRuntimeTree();
        PathBox.Focus();
    }

    // ── Recent list ────────────────────────────────────────────────────────────

    private void PopulateRecentList()
    {
        var items = AssemblyExplorerOptions.Instance.RecentFiles
            .Where(File.Exists)
            .Select(p => new RecentFileItem { FileName = Path.GetFileName(p), FullPath = p })
            .ToList();

        RecentList.ItemsSource      = items;
        RecentEmptyLabel.Visibility = items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnRecentDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (RecentList.SelectedItem is RecentFileItem item)
            Accept(item.FullPath);
    }

    private void OnRemoveRecentClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string path })
        {
            AssemblyExplorerOptions.Instance.RecentFiles
                .RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
            AssemblyExplorerOptions.Instance.Save();
            PopulateRecentList();
        }
    }

    // ── .NET Runtimes tree ─────────────────────────────────────────────────────

    private void PopulateRuntimeTree()
    {
        var groups = DiscoverDotNetRuntimes().ToList();

        _allRuntimeAssemblies = groups
            .SelectMany(g => g.Versions)
            .SelectMany(v => v.Assemblies)
            .ToList();

        RuntimeTree.ItemsSource    = groups;
        RuntimeEmptyLabel.Visibility = groups.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        RuntimeTree.Visibility       = groups.Count > 0  ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>
    /// Enumerates installed .NET runtimes from %ProgramFiles%\dotnet\shared.
    /// Returns runtime pack groups (e.g. Microsoft.NETCore.App) with version subgroups,
    /// ordered newest version first.
    /// </summary>
    private static IEnumerable<RuntimeGroupNode> DiscoverDotNetRuntimes()
    {
        var dotnetShared = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "dotnet", "shared");

        if (!Directory.Exists(dotnetShared)) yield break;

        foreach (var runtimeDir in Directory.GetDirectories(dotnetShared).OrderBy(Path.GetFileName))
        {
            var versions = new List<VersionGroupNode>();

            // Sort versions descending so the latest is at the top.
            foreach (var versionDir in Directory.GetDirectories(runtimeDir)
                         .OrderByDescending(Path.GetFileName, StringComparer.OrdinalIgnoreCase))
            {
                var assemblies = Directory.GetFiles(versionDir, "*.dll")
                    .Select(f => new AssemblyFileNode { FileName = Path.GetFileName(f), FullPath = f })
                    .OrderBy(a => a.FileName)
                    .ToList();

                if (assemblies.Count > 0)
                    versions.Add(new VersionGroupNode
                    {
                        Version    = Path.GetFileName(versionDir),
                        Assemblies = assemblies
                    });
            }

            if (versions.Count > 0)
                yield return new RuntimeGroupNode
                {
                    Name     = Path.GetFileName(runtimeDir),
                    Versions = versions
                };
        }
    }

    private void OnRuntimeTreeSelectionChanged(object sender,
        RoutedPropertyChangedEventArgs<object> e)
    {
        switch (e.NewValue)
        {
            case AssemblyFileNode file:
                _pendingMultiplePaths = null;
                SetPath(file.FullPath);
                break;

            case VersionGroupNode version:
                _pendingMultiplePaths = version.Assemblies
                    .Select(a => a.FullPath).ToList();
                SetMultiplePathsHint(
                    $"{_pendingMultiplePaths.Count} assemblies — {version.Version}");
                break;

            case RuntimeGroupNode group:
                _pendingMultiplePaths = group.Versions
                    .SelectMany(v => v.Assemblies)
                    .Select(a => a.FullPath).ToList();
                SetMultiplePathsHint(
                    $"{_pendingMultiplePaths.Count} assemblies — {group.Name}");
                break;
        }
    }

    private void SetMultiplePathsHint(string hint)
    {
        PathBox.Text       = hint;
        PathBox.CaretIndex = hint.Length;
        OpenButton.IsEnabled = true;
    }

    private void OnRuntimeFilterChanged(object sender, TextChangedEventArgs e)
    {
        var text = RuntimeFilterBox.Text.Trim();

        if (string.IsNullOrEmpty(text))
        {
            RuntimeTree.Visibility       = Visibility.Visible;
            RuntimeFilterList.Visibility = Visibility.Collapsed;
        }
        else
        {
            var filtered = _allRuntimeAssemblies
                .Where(a => a.FileName.Contains(text, StringComparison.OrdinalIgnoreCase))
                .ToList();

            RuntimeFilterList.ItemsSource = filtered;
            RuntimeTree.Visibility        = Visibility.Collapsed;
            RuntimeFilterList.Visibility  = Visibility.Visible;
        }
    }

    private void OnRuntimeFilterListSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (RuntimeFilterList.SelectedItem is AssemblyFileNode node)
            SetPath(node.FullPath);
    }

    private void OnRuntimeFilterListDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (RuntimeFilterList.SelectedItem is AssemblyFileNode node)
            Accept(node.FullPath);
    }

    // ── Drag and drop ──────────────────────────────────────────────────────────

    private void OnWindowDragOver(object sender, DragEventArgs e)
    {
        var hasFile    = GetDropPath(e) is not null;
        e.Effects      = hasFile ? DragDropEffects.Copy : DragDropEffects.None;
        DragOverlay.Visibility = hasFile ? Visibility.Visible : Visibility.Collapsed;
        e.Handled      = true;
    }

    private void OnWindowDragLeave(object sender, DragEventArgs e)
        => DragOverlay.Visibility = Visibility.Collapsed;

    private void OnWindowDrop(object sender, DragEventArgs e)
    {
        DragOverlay.Visibility = Visibility.Collapsed;
        var path = GetDropPath(e);
        if (path is not null) SetPath(path);
    }

    private static string? GetDropPath(DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return null;

        var files = e.Data.GetData(DataFormats.FileDrop) as string[];
        return files?.FirstOrDefault(f =>
        {
            var ext = Path.GetExtension(f).ToLowerInvariant();
            return ext is ".dll" or ".exe";
        });
    }

    // ── Clipboard / Ctrl+V ────────────────────────────────────────────────────

    private void OnWindowKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.V && Keyboard.Modifiers == ModifierKeys.Control)
        {
            TryPasteFromClipboard();
            e.Handled = true;
        }
    }

    private void OnPathBoxKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Return && OpenButton.IsEnabled)
        {
            Accept(PathBox.Text.Trim());
            e.Handled = true;
        }
    }

    private void TryPasteFromClipboard()
    {
        // Priority 1: Explorer FileDrop (user copied a file in Explorer)
        if (Clipboard.ContainsFileDropList())
        {
            var list = Clipboard.GetFileDropList();
            foreach (string? entry in list)
            {
                if (entry is null) continue;
                var ext = Path.GetExtension(entry).ToLowerInvariant();
                if (ext is ".dll" or ".exe") { SetPath(entry); return; }
            }
        }

        // Priority 2: Plain text path
        if (Clipboard.ContainsText())
        {
            var text = Clipboard.GetText().Trim().Trim('"');
            if (!string.IsNullOrEmpty(text)) { SetPath(text); return; }
        }
    }

    // ── Browse button ─────────────────────────────────────────────────────────

    private void OnBrowseClick(object sender, RoutedEventArgs e)
    {
        var ofd = new OpenFileDialog
        {
            Title            = "Select Assembly",
            Filter           = "Assembly files (*.dll;*.exe)|*.dll;*.exe|All files (*.*)|*.*",
            RestoreDirectory = true
        };

        if (!string.IsNullOrEmpty(PathBox.Text) && File.Exists(PathBox.Text))
            ofd.InitialDirectory = Path.GetDirectoryName(PathBox.Text);

        if (ofd.ShowDialog(this) == true)
            SetPath(ofd.FileName);
    }

    // ── Path validation ───────────────────────────────────────────────────────

    private void OnPathTextChanged(object sender, TextChangedEventArgs e)
        => ValidatePath(PathBox.Text.Trim());

    private void SetPath(string path)
    {
        PathBox.Text       = path;
        PathBox.CaretIndex = path.Length;
        ValidatePath(path);
    }

    private void ValidatePath(string path)
    {
        // Keep Open enabled when a multi-file node is already pending.
        if (_pendingMultiplePaths is { Count: > 0 }) return;
        OpenButton.IsEnabled = !string.IsNullOrEmpty(path) && File.Exists(path);
    }

    // ── Dialog result ─────────────────────────────────────────────────────────

    private void OnOpenClick(object sender, RoutedEventArgs e)
        => Accept(PathBox.Text.Trim());

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void Accept(string path)
    {
        // Multi-file selection (runtime group or version node).
        if (_pendingMultiplePaths is { Count: > 0 })
        {
            SelectedFilePaths = _pendingMultiplePaths;
            foreach (var p in _pendingMultiplePaths)
                AssemblyExplorerOptions.Instance.AddRecentFile(p);
            DialogResult = true;
            Close();
            return;
        }

        // Single-file selection.
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;

        SelectedFilePaths = [path];
        AssemblyExplorerOptions.Instance.AddRecentFile(path);

        DialogResult = true;
        Close();
    }
}
