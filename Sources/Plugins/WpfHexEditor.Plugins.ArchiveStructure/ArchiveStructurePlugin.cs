// ==========================================================
// Project: WpfHexEditor.Plugins.ArchiveStructure
// File: ArchiveStructurePlugin.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Description:
//     Plugin entry point for the Archive Structure panel.
//     Auto-loads archive contents when a supported file is opened in the IDE
//     via IIDEEventBus.FileOpenedEvent (ZIP, JAR, NuGet, EPUB, DOCX, XLSX…).
//
// Architecture Notes:
//     Pattern: Observer — IDEEventBus.FileOpenedEvent drives panel updates.
//     Uses System.IO.Compression.ZipFile for ZIP-based formats (no extra NuGet dep).
//     Panel visibility guard skips expensive I/O when the panel is hidden.
// ==========================================================

using System.IO;
using System.IO.Compression;
using System.Windows.Threading;
using WpfHexEditor.Core.Events.IDEEvents;
using WpfHexEditor.SDK.Commands;
using WpfHexEditor.SDK.Contracts;
using WpfHexEditor.SDK.Descriptors;
using WpfHexEditor.SDK.Models;
using WpfHexEditor.Plugins.ArchiveStructure.Views;

namespace WpfHexEditor.Plugins.ArchiveStructure;

/// <summary>
/// Official plugin wrapping the Archive Structure panel.
/// Subscribes to <see cref="FileOpenedEvent"/> on <c>IIDEEventBus</c> to auto-parse
/// ZIP-based archive formats and populate the panel without user interaction.
/// </summary>
public sealed class ArchiveStructurePlugin : IWpfHexEditorPlugin
{
    private const string PanelUiId = "WpfHexEditor.Plugins.ArchiveStructure.Panel.ArchiveStructurePanel";

    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".zip", ".jar", ".war", ".ear", ".nupkg", ".xpi",
        ".epub", ".docx", ".xlsx", ".pptx", ".odt", ".ods"
    };

    private IIDEHostContext?       _context;
    private ArchiveStructurePanel? _panel;
    private IDisposable?           _subFileOpened;
    private CancellationTokenSource? _cts;

    public string  Id      => "WpfHexEditor.Plugins.ArchiveStructure";
    public string  Name    => "Archive Structure";
    public Version Version => new(0, 3, 1);

    public PluginCapabilities Capabilities => new()
    {
        AccessHexEditor  = false,
        AccessFileSystem = true,
        RegisterMenus    = true,
        WriteOutput      = true
    };

    public Task InitializeAsync(IIDEHostContext context, CancellationToken ct = default)
    {
        _context = context;
        _panel   = new ArchiveStructurePanel();

        context.UIRegistry.RegisterPanel(
            PanelUiId,
            _panel,
            Id,
            new PanelDescriptor
            {
                Title           = "Archive Structure",
                DefaultDockSide = "Left",
                DefaultAutoHide = true,
                CanClose        = true
            });

        // Register View menu item so the user can show/hide this panel.
        context.UIRegistry.RegisterMenuItem(
            $"{Id}.Menu.Show",
            Id,
            new MenuItemDescriptor
            {
                Header     = "_Archive Structure",
                ParentPath = "View",
                Group      = "FileTools",
                IconGlyph  = "\uE7C3",
                Command    = new RelayCommand(_ => context.UIRegistry.ShowPanel(PanelUiId))
            });

        // Subscribe to IDE-wide file open events (not HexEditor service — plugin has no hex access).
        _subFileOpened = context.IDEEvents.Subscribe<FileOpenedEvent>(OnFileOpened);

        return Task.CompletedTask;
    }

    public Task ShutdownAsync(CancellationToken ct = default)
    {
        _subFileOpened?.Dispose();
        _cts?.Cancel();
        _cts?.Dispose();
        _cts     = null;
        _panel   = null;
        _context = null;
        // UIRegistry.UnregisterAllForPlugin is called automatically by PluginHost.
        return Task.CompletedTask;
    }

    // ── Event handler ─────────────────────────────────────────────────────────

    private void OnFileOpened(FileOpenedEvent evt)
    {
        if (_panel is null || _context is null) return;
        if (!SupportedExtensions.Contains(evt.FileExtension)) return;

        // Skip expensive I/O when the panel is not visible.
        if (!_context.UIRegistry.IsPanelVisible(PanelUiId)) return;

        // Cancel any previous in-flight parse.
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        var path  = evt.FilePath;
        var panel = _panel;

        _ = Task.Run(() =>
        {
            if (token.IsCancellationRequested || !File.Exists(path)) return;
            var root = ParseZipArchive(path, token);
            if (token.IsCancellationRequested) return;
            panel.Dispatcher.BeginInvoke(() => panel.LoadArchive(root), DispatcherPriority.Background);
        }, token);
    }

    // ── ZIP parsing ───────────────────────────────────────────────────────────

    private static ArchiveNode? ParseZipArchive(string path, CancellationToken ct)
    {
        try
        {
            using var zip = ZipFile.OpenRead(path);

            var root = new ArchiveNode
            {
                Name     = Path.GetFileName(path),
                IsFolder = true,
                Children = new()
            };

            foreach (var entry in zip.Entries)
            {
                if (ct.IsCancellationRequested) return null;
                InsertEntry(root, entry);
            }

            return root;
        }
        catch
        {
            return null;
        }
    }

    private static void InsertEntry(ArchiveNode root, ZipArchiveEntry entry)
    {
        var parts = entry.FullName.TrimEnd('/').Split('/');
        var current = root;

        for (int i = 0; i < parts.Length - 1; i++)
        {
            var part = parts[i];
            var existing = current.Children?.FirstOrDefault(n => n.Name == part && n.IsFolder);
            if (existing is null)
            {
                existing = new ArchiveNode { Name = part, IsFolder = true, Children = new() };
                current.Children!.Add(existing);
            }
            current = existing;
        }

        var isFolder = entry.FullName.EndsWith('/');
        var leafName = parts[^1];
        if (string.IsNullOrEmpty(leafName)) return;

        current.Children!.Add(new ArchiveNode
        {
            Name              = leafName,
            IsFolder          = isFolder,
            Size              = entry.Length,
            CompressedSize    = entry.CompressedLength,
            CompressionMethod = entry.CompressedLength < entry.Length ? "Deflate" : "Stored",
            Children          = isFolder ? new() : null
        });
    }
}
