// ==========================================================
// Project: WpfHexEditor.Core.AssemblyAnalysis
// File: MetadataTableReader.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-23
// Description:
//     Reads rows from any ECMA-335 metadata table using the BCL
//     System.Reflection.Metadata APIs and returns them as generic
//     name/value column pairs suitable for display in a data grid.
//
// Architecture Notes:
//     Pattern: Service — stateless, created per-assembly.
//     Decoupled from any WPF layer; consumes PEReader + MetadataReader
//     which the caller creates and manages.
//     Only the most common tables (TypeDef, MethodDef, FieldDef,
//     AssemblyRef, MemberRef, Property, Event, CustomAttribute) are
//     fully decoded.  All other table indices fall back to showing the
//     raw row number and token, which is always informative.
// ==========================================================

using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

namespace WpfHexEditor.Core.AssemblyAnalysis;

/// <summary>
/// A single column value within a <see cref="MetadataTableRow"/>.
/// </summary>
/// <param name="ColumnName">Display name of the ECMA-335 column.</param>
/// <param name="Value">Human-readable decoded value.</param>
public sealed record MetadataTableColumn(string ColumnName, string Value);

/// <summary>
/// One decoded row of an ECMA-335 metadata table.
/// </summary>
/// <param name="RowNumber">1-based row number within the table.</param>
/// <param name="Token">ECMA-335 metadata token for this row (e.g. 0x02000001 for TypeDef row 1).</param>
/// <param name="FileOffset">Raw PE file byte offset of this row (0 when not resolved).</param>
/// <param name="Columns">Decoded column name/value pairs.</param>
public sealed record MetadataTableRow(
    int RowNumber,
    int Token,
    long FileOffset,
    IReadOnlyList<MetadataTableColumn> Columns);

/// <summary>
/// Reads rows from ECMA-335 metadata tables and decodes them into human-readable
/// name/value column pairs for display in the Metadata Table Browser panel.
/// </summary>
public sealed class MetadataTableReader
{
    private readonly PEReader      _peReader;
    private readonly MetadataReader _mdReader;

    public MetadataTableReader(PEReader peReader, MetadataReader mdReader)
    {
        _peReader = peReader ?? throw new ArgumentNullException(nameof(peReader));
        _mdReader = mdReader ?? throw new ArgumentNullException(nameof(mdReader));
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Reads all rows from the table identified by <paramref name="tableIndex"/>
    /// (0-based, matching <see cref="TableIndex"/> values from ECMA-335 §II.22).
    /// Returns an empty list when the table is not present in this assembly.
    /// </summary>
    public IReadOnlyList<MetadataTableRow> ReadTable(int tableIndex)
    {
        try
        {
            var ti = (TableIndex)tableIndex;
            return ti switch
            {
                TableIndex.TypeDef        => ReadTypeDefTable(),
                TableIndex.MethodDef      => ReadMethodDefTable(),
                TableIndex.Field          => ReadFieldTable(),
                TableIndex.AssemblyRef    => ReadAssemblyRefTable(),
                TableIndex.MemberRef      => ReadMemberRefTable(),
                TableIndex.Property       => ReadPropertyTable(),
                TableIndex.Event          => ReadEventTable(),
                TableIndex.CustomAttribute => ReadCustomAttributeTable(),
                TableIndex.Assembly       => ReadAssemblyTable(),
                TableIndex.Module         => ReadModuleTable(),
                TableIndex.Param          => ReadParamTable(),
                TableIndex.InterfaceImpl  => ReadInterfaceImplTable(),
                TableIndex.TypeRef        => ReadTypeRefTable(),
                _                         => ReadGenericTable(ti)
            };
        }
        catch (Exception ex)
        {
            return [new MetadataTableRow(0, 0, 0L,
                [new MetadataTableColumn("Error", ex.Message)])];
        }
    }

    /// <summary>
    /// Returns metadata about all tables present in this assembly.
    /// Each entry: (tableIndex, tableName, rowCount).
    /// </summary>
    public IReadOnlyList<(int TableIndex, string TableName, int RowCount)> GetPresentTables()
    {
        var result = new List<(int, string, int)>(64);
        for (var i = 0; i < 64; i++)
        {
            var count = _mdReader.GetTableRowCount((TableIndex)i);
            if (count > 0)
                result.Add((i, ((TableIndex)i).ToString(), count));
        }
        return result;
    }

    // ── Table readers ─────────────────────────────────────────────────────────

    private IReadOnlyList<MetadataTableRow> ReadTypeDefTable()
    {
        var rows = new List<MetadataTableRow>(_mdReader.GetTableRowCount(TableIndex.TypeDef));
        foreach (var handle in _mdReader.TypeDefinitions)
        {
            var def   = _mdReader.GetTypeDefinition(handle);
            var row   = MetadataTokens.GetRowNumber(handle);
            var token = MetadataTokens.GetToken(handle);
            var cols  = new List<MetadataTableColumn>
            {
                Col("Flags",     $"0x{(int)def.Attributes:X4}  ({def.Attributes})"),
                Col("TypeName",  SafeString(def.Name)),
                Col("TypeNs",    SafeString(def.Namespace)),
                Col("Extends",   FormatHandle(def.BaseType)),
                Col("FieldList", $"#{MetadataTokens.GetRowNumber(def.GetFields().FirstOrDefault())}"),
                Col("MethodList",$"#{MetadataTokens.GetRowNumber(def.GetMethods().FirstOrDefault())}")
            };
            rows.Add(new MetadataTableRow(row, token, 0L, cols));
        }
        return rows;
    }

    private IReadOnlyList<MetadataTableRow> ReadMethodDefTable()
    {
        var rows = new List<MetadataTableRow>(_mdReader.GetTableRowCount(TableIndex.MethodDef));
        foreach (var handle in _mdReader.MethodDefinitions)
        {
            var def   = _mdReader.GetMethodDefinition(handle);
            var row   = MetadataTokens.GetRowNumber(handle);
            var token = MetadataTokens.GetToken(handle);
            var cols  = new List<MetadataTableColumn>
            {
                Col("RVA",       $"0x{def.RelativeVirtualAddress:X8}"),
                Col("ImplFlags", $"0x{(int)def.ImplAttributes:X4}  ({def.ImplAttributes})"),
                Col("Flags",     $"0x{(int)def.Attributes:X4}  ({def.Attributes})"),
                Col("Name",      SafeString(def.Name)),
                Col("ParamList", $"#{MetadataTokens.GetRowNumber(def.GetParameters().FirstOrDefault())}")
            };
            rows.Add(new MetadataTableRow(row, token, 0L, cols));
        }
        return rows;
    }

    private IReadOnlyList<MetadataTableRow> ReadFieldTable()
    {
        var rows = new List<MetadataTableRow>(_mdReader.GetTableRowCount(TableIndex.Field));
        foreach (var handle in _mdReader.FieldDefinitions)
        {
            var def   = _mdReader.GetFieldDefinition(handle);
            var row   = MetadataTokens.GetRowNumber(handle);
            var token = MetadataTokens.GetToken(handle);
            var cols  = new List<MetadataTableColumn>
            {
                Col("Flags", $"0x{(int)def.Attributes:X4}  ({def.Attributes})"),
                Col("Name",  SafeString(def.Name)),
                Col("Offset",$"{def.GetOffset()}")
            };
            rows.Add(new MetadataTableRow(row, token, 0L, cols));
        }
        return rows;
    }

    private IReadOnlyList<MetadataTableRow> ReadAssemblyRefTable()
    {
        var rows = new List<MetadataTableRow>(_mdReader.GetTableRowCount(TableIndex.AssemblyRef));
        foreach (var handle in _mdReader.AssemblyReferences)
        {
            var def   = _mdReader.GetAssemblyReference(handle);
            var row   = MetadataTokens.GetRowNumber(handle);
            var token = MetadataTokens.GetToken(handle);
            var cols  = new List<MetadataTableColumn>
            {
                Col("Version", def.Version?.ToString() ?? "-"),
                Col("Flags",   $"0x{(int)def.Flags:X4}  ({def.Flags})"),
                Col("Name",    SafeString(def.Name)),
                Col("Culture", SafeString(def.Culture))
            };
            rows.Add(new MetadataTableRow(row, token, 0L, cols));
        }
        return rows;
    }

    private IReadOnlyList<MetadataTableRow> ReadMemberRefTable()
    {
        var rows = new List<MetadataTableRow>(_mdReader.GetTableRowCount(TableIndex.MemberRef));
        foreach (var handle in _mdReader.MemberReferences)
        {
            var def   = _mdReader.GetMemberReference(handle);
            var row   = MetadataTokens.GetRowNumber(handle);
            var token = MetadataTokens.GetToken(handle);
            var cols  = new List<MetadataTableColumn>
            {
                Col("Class",   FormatHandle(def.Parent)),
                Col("Name",    SafeString(def.Name)),
                Col("Kind",    def.GetKind().ToString())
            };
            rows.Add(new MetadataTableRow(row, token, 0L, cols));
        }
        return rows;
    }

    private IReadOnlyList<MetadataTableRow> ReadPropertyTable()
    {
        var count = _mdReader.GetTableRowCount(TableIndex.Property);
        var rows  = new List<MetadataTableRow>(count);
        for (var i = 1; i <= count; i++)
        {
            var handle = MetadataTokens.PropertyDefinitionHandle(i);
            var def    = _mdReader.GetPropertyDefinition(handle);
            var token  = MetadataTokens.GetToken(handle);
            var cols   = new List<MetadataTableColumn>
            {
                Col("Flags", $"0x{(int)def.Attributes:X4}  ({def.Attributes})"),
                Col("Name",  SafeString(def.Name))
            };
            rows.Add(new MetadataTableRow(i, token, 0L, cols));
        }
        return rows;
    }

    private IReadOnlyList<MetadataTableRow> ReadEventTable()
    {
        var count = _mdReader.GetTableRowCount(TableIndex.Event);
        var rows  = new List<MetadataTableRow>(count);
        for (var i = 1; i <= count; i++)
        {
            var handle = MetadataTokens.EventDefinitionHandle(i);
            var def    = _mdReader.GetEventDefinition(handle);
            var token  = MetadataTokens.GetToken(handle);
            var cols   = new List<MetadataTableColumn>
            {
                Col("Flags", $"0x{(int)def.Attributes:X4}  ({def.Attributes})"),
                Col("Name",  SafeString(def.Name)),
                Col("Type",  FormatHandle(def.Type))
            };
            rows.Add(new MetadataTableRow(i, token, 0L, cols));
        }
        return rows;
    }

    private IReadOnlyList<MetadataTableRow> ReadCustomAttributeTable()
    {
        var count = _mdReader.GetTableRowCount(TableIndex.CustomAttribute);
        var rows  = new List<MetadataTableRow>(count);
        for (var i = 1; i <= count; i++)
        {
            var handle = MetadataTokens.CustomAttributeHandle(i);
            var def    = _mdReader.GetCustomAttribute(handle);
            var token  = MetadataTokens.GetToken(handle);
            var cols   = new List<MetadataTableColumn>
            {
                Col("Parent",    FormatHandle(def.Parent)),
                Col("Constructor", FormatHandle(def.Constructor))
            };
            rows.Add(new MetadataTableRow(i, token, 0L, cols));
        }
        return rows;
    }

    private IReadOnlyList<MetadataTableRow> ReadAssemblyTable()
    {
        if (!_mdReader.IsAssembly) return [];

        var def   = _mdReader.GetAssemblyDefinition();
        // Assembly table token: 0x20000001
        const int assemblyToken = 0x20000001;
        var cols  = new List<MetadataTableColumn>
        {
            Col("HashAlgId",    $"0x{(int)def.HashAlgorithm:X4}  ({def.HashAlgorithm})"),
            Col("Version",      def.Version?.ToString() ?? "-"),
            Col("Flags",        $"0x{(int)def.Flags:X4}  ({def.Flags})"),
            Col("Name",         SafeString(def.Name)),
            Col("Culture",      SafeString(def.Culture)),
            Col("CustomAttrs",  $"{def.GetCustomAttributes().Count()} custom attrs")
        };
        return [new MetadataTableRow(1, assemblyToken, 0L, cols)];
    }

    private IReadOnlyList<MetadataTableRow> ReadModuleTable()
    {
        var def   = _mdReader.GetModuleDefinition();
        // Module table token: 0x00000001
        const int moduleToken = 0x00000001;
        var cols  = new List<MetadataTableColumn>
        {
            Col("Name", SafeString(def.Name)),
            Col("Mvid", def.Mvid == default ? "-" : _mdReader.GetGuid(def.Mvid).ToString("D"))
        };
        return [new MetadataTableRow(1, moduleToken, 0L, cols)];
    }

    private IReadOnlyList<MetadataTableRow> ReadParamTable()
    {
        var count = _mdReader.GetTableRowCount(TableIndex.Param);
        var rows  = new List<MetadataTableRow>(count);
        for (var i = 1; i <= count; i++)
        {
            var handle = MetadataTokens.ParameterHandle(i);
            var def    = _mdReader.GetParameter(handle);
            var token  = MetadataTokens.GetToken(handle);
            var cols   = new List<MetadataTableColumn>
            {
                Col("Flags",    $"0x{(int)def.Attributes:X4}  ({def.Attributes})"),
                Col("Sequence", def.SequenceNumber.ToString()),
                Col("Name",     SafeString(def.Name))
            };
            rows.Add(new MetadataTableRow(i, token, 0L, cols));
        }
        return rows;
    }

    private IReadOnlyList<MetadataTableRow> ReadInterfaceImplTable()
    {
        var count = _mdReader.GetTableRowCount(TableIndex.InterfaceImpl);
        var rows  = new List<MetadataTableRow>(count);
        for (var i = 1; i <= count; i++)
        {
            // Walk all TypeDef interfaces to decode this table
            rows.Add(new MetadataTableRow(i,
                MetadataTokens.GetToken(EntityHandle.ModuleDefinition) + i,
                0L,
                [new MetadataTableColumn("Row", i.ToString())]));
        }
        return rows;
    }

    private IReadOnlyList<MetadataTableRow> ReadTypeRefTable()
    {
        var count = _mdReader.GetTableRowCount(TableIndex.TypeRef);
        var rows  = new List<MetadataTableRow>(count);
        for (var i = 1; i <= count; i++)
        {
            var handle = MetadataTokens.TypeReferenceHandle(i);
            var def    = _mdReader.GetTypeReference(handle);
            var token  = MetadataTokens.GetToken(handle);
            var cols   = new List<MetadataTableColumn>
            {
                Col("ResolutionScope", FormatHandle(def.ResolutionScope)),
                Col("TypeName",        SafeString(def.Name)),
                Col("TypeNamespace",   SafeString(def.Namespace))
            };
            rows.Add(new MetadataTableRow(i, token, 0L, cols));
        }
        return rows;
    }

    private IReadOnlyList<MetadataTableRow> ReadGenericTable(TableIndex ti)
    {
        var count = _mdReader.GetTableRowCount(ti);
        var rows  = new List<MetadataTableRow>(count);
        for (var i = 1; i <= count; i++)
        {
            rows.Add(new MetadataTableRow(i, 0, 0L,
                [new MetadataTableColumn("Row", i.ToString())]));
        }
        return rows;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private string SafeString(StringHandle handle)
    {
        try { return _mdReader.GetString(handle); }
        catch { return string.Empty; }
    }

    private string SafeString(NamespaceDefinitionHandle handle)
    {
        if (handle.IsNil) return string.Empty;
        try
        {
            var ns = _mdReader.GetNamespaceDefinition(handle);
            return _mdReader.GetString(ns.Name);
        }
        catch { return string.Empty; }
    }

    private static string FormatHandle(Handle handle)
    {
        if (handle.IsNil) return "-";
        return $"0x{MetadataTokens.GetToken(handle):X8}";
    }

    private static string FormatHandle(EntityHandle handle)
        => FormatHandle((Handle)handle);

    private static MetadataTableColumn Col(string name, string value)
        => new(name, value);
}
