// ==========================================================
// Project: WpfHexEditor.Plugins.AssemblyExplorer
// File: ViewModels/MetadataTableNodeViewModel.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-08
// Updated: 2026-03-23 — ASM-02-B: full metadata table browser with rows + CSV export.
// Description:
//     Tree node representing a single ECMA-335 metadata table entry.
//     Exposes loaded rows as an ObservableCollection for DataGrid binding
//     and provides an ICommand for exporting to CSV.
//
// Architecture Notes:
//     Pattern: MVVM — ViewModel holds row data; the MetadataTableView renders it.
//     Rows are loaded lazily (on first expand) from MetadataTableReader.
// ==========================================================

using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using WpfHexEditor.Core.AssemblyAnalysis;
using WpfHexEditor.SDK.Commands;

namespace WpfHexEditor.Plugins.AssemblyExplorer.ViewModels;

/// <summary>
/// Tree node representing a decoded ECMA-335 metadata table.
/// Loaded rows are exposed as <see cref="Rows"/> for DataGrid binding.
/// </summary>
public sealed class MetadataTableNodeViewModel : AssemblyNodeViewModel
{
    public MetadataTableNodeViewModel(string tableName, int rowCount, long tableOffset = 0L)
    {
        TableName = tableName;
        RowCount  = rowCount;
        PeOffset  = tableOffset;

        ExportCsvCommand = new RelayCommand(_ => ExportToCsv(), _ => Rows.Count > 0);

        // Insert dummy child so the expand arrow is visible even before rows are loaded.
        if (rowCount > 0)
            InsertDummyChild();
    }

    // ── Identity ──────────────────────────────────────────────────────────────

    public string TableName { get; }
    public int    RowCount  { get; }

    public override string DisplayName => $"{TableName} ({RowCount} rows)";
    public override string IconGlyph   => "\uE9D2"; // Table
    public override Brush  IconBrush   => MakeBrush("#9B9B9B"); // Silver

    public override string ToolTipText =>
        $"Metadata table: {TableName}\n{RowCount} rows"
      + (PeOffset > 0 ? $"\nOffset: 0x{PeOffset:X}" : " (offset not resolved)");

    // ── Row data ──────────────────────────────────────────────────────────────

    /// <summary>Decoded rows for the DataGrid. Empty until <see cref="SetRows"/> is called.</summary>
    public ObservableCollection<MetadataTableRow> Rows { get; } = [];

    /// <summary>Command to export loaded rows as a CSV file (opens SaveFileDialog).</summary>
    public ICommand ExportCsvCommand { get; }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Replaces the current row collection with <paramref name="rows"/>.
    /// Must be called on the UI thread.
    /// </summary>
    public void SetRows(IReadOnlyList<MetadataTableRow> rows)
    {
        Rows.Clear();
        foreach (var row in rows)
            Rows.Add(row);
    }

    // ── CSV export ────────────────────────────────────────────────────────────

    private void ExportToCsv()
    {
        if (Rows.Count == 0) return;

        var dlg = new SaveFileDialog
        {
            Title       = $"Export {TableName} table",
            Filter      = "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*",
            DefaultExt  = "csv",
            FileName    = $"{TableName}.csv",
            OverwritePrompt = true
        };

        if (dlg.ShowDialog() != true) return;

        try
        {
            var sb = new StringBuilder();

            // Header row from first entry's column names.
            if (Rows.Count > 0)
            {
                sb.Append("Row,Token");
                foreach (var col in Rows[0].Columns)
                {
                    sb.Append(',');
                    AppendCsvValue(sb, col.ColumnName);
                }
                sb.AppendLine();
            }

            // Data rows.
            foreach (var row in Rows)
            {
                sb.Append(row.RowNumber);
                sb.Append(',');
                sb.Append($"0x{row.Token:X8}");
                foreach (var col in row.Columns)
                {
                    sb.Append(',');
                    AppendCsvValue(sb, col.Value);
                }
                sb.AppendLine();
            }

            File.WriteAllText(dlg.FileName, sb.ToString(), Encoding.UTF8);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"Export failed:\n{ex.Message}",
                "CSV Export",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
        }
    }

    private static void AppendCsvValue(StringBuilder sb, string value)
    {
        // RFC-4180: if value contains comma, quote, or newline — wrap in quotes and escape quotes.
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
        {
            sb.Append('"');
            sb.Append(value.Replace("\"", "\"\""));
            sb.Append('"');
        }
        else
        {
            sb.Append(value);
        }
    }
}
