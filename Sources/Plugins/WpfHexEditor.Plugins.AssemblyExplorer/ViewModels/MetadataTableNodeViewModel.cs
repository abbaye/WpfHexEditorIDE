// ==========================================================
// Project: WpfHexEditor.Plugins.AssemblyExplorer
// File: ViewModels/MetadataTableNodeViewModel.cs
// Author: Derek Tremblay
// Created: 2026-03-08
// Description:
//     Tree node representing a single ECMA-335 metadata table entry
//     (stub — full table browser deferred to a future phase).
// ==========================================================

using System.Windows.Media;

namespace WpfHexEditor.Plugins.AssemblyExplorer.ViewModels;

/// <summary>
/// Stub node representing a metadata table (TypeDef, MethodDef, FieldDef, etc.).
/// Full table browser is planned for a future phase.
/// </summary>
public sealed class MetadataTableNodeViewModel : AssemblyNodeViewModel
{
    public MetadataTableNodeViewModel(string tableName, int rowCount, long tableOffset = 0L)
    {
        TableName  = tableName;
        RowCount   = rowCount;
        PeOffset   = tableOffset;
    }

    public string TableName { get; }
    public int    RowCount  { get; }

    public override string DisplayName => $"{TableName} ({RowCount} rows)";
    public override string IconGlyph   => "\uE9D2"; // Table
    public override Brush  IconBrush   => MakeBrush("#9B9B9B"); // Silver

    public override string ToolTipText =>
        $"Metadata table: {TableName}\n{RowCount} rows"
      + (PeOffset > 0 ? $"\nOffset: 0x{PeOffset:X}" : " (offset not resolved)");
}
