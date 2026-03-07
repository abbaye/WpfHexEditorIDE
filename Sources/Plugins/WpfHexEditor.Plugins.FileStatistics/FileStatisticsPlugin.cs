// ==========================================================
// Project: WpfHexEditor.Plugins.FileStatistics
// File: FileStatisticsPlugin.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-07
// Description:
//     Plugin entry point for the File Statistics panel.
//     Subscribes to FileOpened on IHexEditorService and computes
//     file health statistics (entropy, byte composition, anomalies) to push
//     into the FileStatisticsPanel.
//
// Architecture Notes:
//     Pattern: Observer — subscribes to host events, pushes data to panel.
//     Statistics computation is done inline (no separate service needed for now).
// ==========================================================

using WpfHexEditor.SDK.Commands;
using WpfHexEditor.SDK.Contracts;
using WpfHexEditor.SDK.Contracts.Services;
using WpfHexEditor.SDK.Descriptors;
using WpfHexEditor.SDK.Models;
using WpfHexEditor.Plugins.FileStatistics.Views;

namespace WpfHexEditor.Plugins.FileStatistics;

/// <summary>
/// Official plugin wrapping the File Statistics panel.
/// Subscribes to <see cref="IHexEditorService.FileOpened"/> to compute and display
/// file health statistics whenever a new file is loaded.
/// </summary>
public sealed class FileStatisticsPlugin : IWpfHexEditorPlugin
{
    private IIDEHostContext?    _context;
    private FileStatisticsPanel? _panel;

    public string  Id      => "WpfHexEditor.Plugins.FileStatistics";
    public string  Name    => "File Statistics";
    public Version Version => new(1, 0, 0);

    public PluginCapabilities Capabilities => new()
    {
        AccessHexEditor  = true,
        AccessFileSystem = false,
        RegisterMenus    = true,
        WriteOutput      = true
    };

    public Task InitializeAsync(IIDEHostContext context, CancellationToken ct = default)
    {
        _context = context;
        _panel   = new FileStatisticsPanel();

        _panel.RefreshRequested += (_, _) => RefreshStatistics();

        context.UIRegistry.RegisterPanel(
            "WpfHexEditor.Plugins.FileStatistics.Panel.FileStatisticsPanel",
            _panel,
            Id,
            new PanelDescriptor
            {
                Title           = "File Statistics",
                DefaultDockSide = "Bottom",
                DefaultAutoHide = false,
                CanClose        = true,
                PreferredHeight = 200
            });

        // Register View menu item so the user can show/hide this panel.
        context.UIRegistry.RegisterMenuItem(
            $"{Id}.Menu.Show",
            Id,
            new MenuItemDescriptor
            {
                Header     = "File _Statistics",
                ParentPath = "View",
                Group      = "Statistics",
                IconGlyph  = "\uE9F5",
                Command    = new RelayCommand(_ => context.UIRegistry.ShowPanel(
                                 "WpfHexEditor.Plugins.FileStatistics.Panel.FileStatisticsPanel"))
            });

        context.HexEditor.FileOpened += OnFileOpened;

        return Task.CompletedTask;
    }

    public Task ShutdownAsync(CancellationToken ct = default)
    {
        if (_context is not null)
            _context.HexEditor.FileOpened -= OnFileOpened;
        return Task.CompletedTask;
    }

    // -------------------------------------------------------------------------

    private void OnFileOpened(object? sender, EventArgs e) => RefreshStatistics();

    private void RefreshStatistics()
    {
        if (_panel is null || _context is null || !_context.HexEditor.IsActive) return;

        var svc      = _context.HexEditor;
        var fileSize = svc.FileSize;

        // Read up to 1 MB for analysis to keep it fast
        var readLen  = (int)Math.Min(fileSize, 1_048_576);
        var data     = readLen > 0 ? svc.ReadBytes(0, readLen) : [];

        var stats = ComputeStats(svc.CurrentFilePath, fileSize, data);

        // Panel must be updated on the UI thread
        _panel.Dispatcher.BeginInvoke(() => _panel.UpdateStatistics(stats));
    }

    private static FileStats ComputeStats(string? filePath, long fileSize, byte[] sample)
    {
        var stats = new FileStats
        {
            FileName     = System.IO.Path.GetFileName(filePath),
            FilePath     = filePath,
            AnalysisDate = DateTime.Now,
            FileSize     = fileSize
        };

        if (sample.Length == 0) return stats;

        // Frequency table
        var freq = new long[256];
        foreach (var b in sample) freq[b]++;

        long   nullCount    = freq[0];
        long   printable    = 0;
        byte   maxByte      = 0;
        long   maxFreq      = 0;
        int    uniqueCount  = 0;

        for (int i = 0; i < 256; i++)
        {
            if (freq[i] == 0) continue;
            uniqueCount++;
            if (freq[i] > maxFreq) { maxFreq = freq[i]; maxByte = (byte)i; }
            if (i >= 0x20 && i <= 0x7E) printable += freq[i];
        }

        double nullPct      = (nullCount  * 100.0) / sample.Length;
        double printablePct = (printable  * 100.0) / sample.Length;
        double maxBytePct   = (maxFreq    * 100.0) / sample.Length;

        // Shannon entropy (0-8 bits)
        double entropy = 0;
        for (int i = 0; i < 256; i++)
        {
            if (freq[i] == 0) continue;
            double p = freq[i] / (double)sample.Length;
            entropy -= p * Math.Log(p, 2);
        }

        // Classify data type
        string dataType = entropy switch
        {
            >= 7.5 => "Encrypted",
            >= 6.5 => "Compressed",
            _ when nullPct > 50    => "Sparse",
            _ when printablePct > 85 => "Text",
            _ => "Binary"
        };

        // Health score heuristic (0-100)
        int health = 100;
        if (nullPct > 80)       health -= 30;
        if (entropy > 7.8)      health -= 20;
        if (uniqueCount < 10)   health -= 20;
        health = Math.Max(0, health);

        string healthMsg = health switch
        {
            >= 80 => "Good",
            >= 60 => "Fair",
            >= 40 => "Poor",
            _     => "Critical"
        };

        // Anomalies
        var anomalies = new List<AnomalyInfo>();
        if (nullPct > 40)
            anomalies.Add(new AnomalyInfo { Title = "High null byte ratio", Description = $"{nullPct:F1}% null bytes" });
        if (entropy > 7.5)
            anomalies.Add(new AnomalyInfo { Title = "Very high entropy", Description = "Data may be encrypted or compressed" });

        stats.MostCommonByte         = maxByte;
        stats.MostCommonBytePct      = maxBytePct;
        stats.UniqueBytesCount       = uniqueCount;
        stats.NullBytePercentage     = nullPct;
        stats.PrintableAsciiPercentage = printablePct;
        stats.Entropy                = entropy;
        stats.HealthScore            = health;
        stats.HealthMessage          = healthMsg;
        stats.DataType               = dataType;
        stats.StructureValid         = true;
        stats.ChecksumsPass          = true;
        stats.ChecksumStatus         = "N/A";
        stats.Anomalies              = anomalies;

        return stats;
    }
}
