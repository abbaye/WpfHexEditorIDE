// ==========================================================
// Project: WpfHexEditor.Plugins.AssemblyExplorer
// File: ViewModels/AssemblyDetailViewModel.cs
// Author: Derek Tremblay
// Created: 2026-03-08
// Description:
//     ViewModel for the detail pane (bottom split of the Assembly Explorer panel).
//     Phase 4: upgraded to a 4-tab layout —
//       Code  — C# structural skeleton from CSharpSkeletonEmitter
//       IL    — raw IL disassembly from IlTextEmitter (methods only)
//       Info  — metadata token, PE offset, visibility/modifier flags, custom attrs
//       Hex   — 64-byte hex dump at the PE offset (read on demand from file)
//
// Architecture Notes:
//     Pattern: MVVM — populated by AssemblyExplorerViewModel.OnNodeSelected.
//     ShowNode(node, filePath) is the single entry point; filePath is needed
//     to open the PEReader on demand for IL and to read raw hex bytes.
//     All string formatting stays in the VM — the View is binding-only.
// ==========================================================

using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using WpfHexEditor.Core.AssemblyAnalysis.Models;
using WpfHexEditor.Plugins.AssemblyExplorer.Services;

namespace WpfHexEditor.Plugins.AssemblyExplorer.ViewModels;

/// <summary>A key/value row displayed in the Info tab grid.</summary>
public sealed record InfoRow(string Key, string Value);

/// <summary>
/// Provides content for the 4-tab detail pane shown below the tree view.
/// Updated every time the user selects a new tree node.
/// </summary>
public sealed class AssemblyDetailViewModel : AssemblyNodeViewModel
{
    private readonly DecompilerService _decompiler;

    public AssemblyDetailViewModel(DecompilerService decompiler)
        => _decompiler = decompiler;

    // ── Shared header ─────────────────────────────────────────────────────────

    private string _title = string.Empty;
    public string Title
    {
        get => _title;
        set => SetField(ref _title, value);
    }

    private string _metadataInfo = string.Empty;
    public string MetadataInfo
    {
        get => _metadataInfo;
        set => SetField(ref _metadataInfo, value);
    }

    private long _peOffset;
    public long PeOffsetValue
    {
        get => _peOffset;
        set
        {
            SetField(ref _peOffset, value);
            OnPropertyChanged(nameof(HasOffset));
        }
    }

    public bool HasOffset => _peOffset > 0;

    /// <summary>
    /// True when the currently displayed node supports decompilation to C#
    /// and the Extract button should be visible.
    /// </summary>
    private bool _isExtractAvailable;
    public bool IsExtractAvailable
    {
        get => _isExtractAvailable;
        private set => SetField(ref _isExtractAvailable, value);
    }

    // ── Tab 0 — Code ──────────────────────────────────────────────────────────

    private string _detailText = "Select a node to view details.";
    public string DetailText
    {
        get => _detailText;
        set => SetField(ref _detailText, value);
    }

    // ── Tab 1 — IL ────────────────────────────────────────────────────────────

    private string _ilText = string.Empty;
    public string IlText
    {
        get => _ilText;
        set => SetField(ref _ilText, value);
    }

    // ── Tab 2 — Info ──────────────────────────────────────────────────────────

    public ObservableCollection<InfoRow> InfoItems { get; } = [];

    // ── Tab 3 — Hex ───────────────────────────────────────────────────────────

    private string _hexDumpText = "// No PE offset available for this node.";
    public string HexDumpText
    {
        get => _hexDumpText;
        set => SetField(ref _hexDumpText, value);
    }

    // ── Active tab ────────────────────────────────────────────────────────────

    private int _activeTabIndex;
    public int ActiveTabIndex
    {
        get => _activeTabIndex;
        set => SetField(ref _activeTabIndex, value);
    }

    // ── Currently displayed node (for Extract button) ─────────────────────────

    /// <summary>
    /// The node currently shown in the detail pane.
    /// Null after <see cref="Clear"/> is called.
    /// </summary>
    public AssemblyNodeViewModel? CurrentNode { get; private set; }

    // ── AssemblyNodeViewModel overrides (detail pane is not a tree node) ──────

    public override string DisplayName => _title;
    public override string IconGlyph   => "\uE8D6"; // Details

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Updates all detail pane tabs to reflect the selected <paramref name="node"/>.
    /// <paramref name="filePath"/> is required to read IL and raw hex bytes from disk.
    /// Called on the UI thread from AssemblyExplorerViewModel.OnNodeSelected.
    /// </summary>
    public void ShowNode(AssemblyNodeViewModel node, string filePath)
    {
        CurrentNode   = node;
        Title         = node.DisplayName;
        PeOffsetValue = node.PeOffset;
        MetadataInfo  = node.MetadataToken != 0
            ? $"Token: 0x{node.MetadataToken:X8}"
            : string.Empty;

        // Code tab — C# skeleton / assembly info
        DetailText = node switch
        {
            AssemblyRootNodeViewModel root => _decompiler.DecompileAssembly(root.Model),
            TypeNodeViewModel         type => _decompiler.DecompileType(type.Model),
            MethodNodeViewModel       meth => _decompiler.DecompileMethod(meth.Model),
            FieldNodeViewModel        fld  => _decompiler.DecompileMethod(fld.Model),
            PropertyNodeViewModel     prop => _decompiler.DecompileMethod(prop.Model),
            _                              => _decompiler.GetStubText(node.DisplayName)
        };

        // IL tab — method nodes only; empty string for all others
        IlText = node is MethodNodeViewModel m
            ? _decompiler.GetIlText(m.Model, filePath)
            : string.Empty;

        // Info tab — structured metadata key/value grid
        BuildInfoItems(node);

        // Hex tab — 64 bytes at PE offset, formatted as classic hex dump
        HexDumpText = node.PeOffset > 0
            ? FormatHexDump(ReadPeBytes(filePath, node.PeOffset), node.PeOffset)
            : "// No PE offset available for this node.";

        // Extract button visible for Assembly/Type/Method nodes that produce real C# output.
        IsExtractAvailable = node is AssemblyRootNodeViewModel
                                  or TypeNodeViewModel
                                  or MethodNodeViewModel;

        // Do NOT override ActiveTabIndex here — respect the tab the user last picked.
        // Tab is only reset to 0 (Code) by Clear() when the pane is emptied.
    }

    /// <summary>Resets the detail pane to its initial empty state.</summary>
    public void Clear()
    {
        CurrentNode        = null;
        Title              = string.Empty;
        DetailText         = "Select a node to view details.";
        IlText             = string.Empty;
        MetadataInfo       = string.Empty;
        PeOffsetValue      = 0L;
        HexDumpText        = "// No PE offset available for this node.";
        IsExtractAvailable = false;
        InfoItems.Clear();
        ActiveTabIndex = 0;
    }

    // ── Info tab builder ──────────────────────────────────────────────────────

    private void BuildInfoItems(AssemblyNodeViewModel node)
    {
        InfoItems.Clear();

        if (node.MetadataToken != 0)
            InfoItems.Add(new InfoRow("Token", $"0x{node.MetadataToken:X8}"));

        if (node.PeOffset > 0)
            InfoItems.Add(new InfoRow("PE Offset", $"0x{node.PeOffset:X}  ({node.PeOffset:N0})"));

        switch (node)
        {
            case AssemblyRootNodeViewModel root: BuildAssemblyInfo(root.Model);  break;
            case TypeNodeViewModel         type: BuildTypeInfo(type.Model);      break;
            case MethodNodeViewModel       meth: BuildMemberInfo(meth.Model);    break;
            case FieldNodeViewModel        fld:  BuildMemberInfo(fld.Model);     break;
            case PropertyNodeViewModel     prop: BuildMemberInfo(prop.Model);    break;
        }
    }

    private void BuildAssemblyInfo(AssemblyModel model)
    {
        InfoItems.Add(new InfoRow("Name", model.Name));
        if (model.Version is not null)
            InfoItems.Add(new InfoRow("Version", model.Version.ToString()));
        if (!string.IsNullOrEmpty(model.Culture))
            InfoItems.Add(new InfoRow("Culture", model.Culture));
        if (!string.IsNullOrEmpty(model.PublicKeyToken))
            InfoItems.Add(new InfoRow("Public Key Token", model.PublicKeyToken));
        if (!string.IsNullOrEmpty(model.TargetFramework))
            InfoItems.Add(new InfoRow("Target Framework", model.TargetFramework));
        InfoItems.Add(new InfoRow("Managed",    model.IsManaged ? "Yes" : "No (native PE)"));
        InfoItems.Add(new InfoRow("Types",      model.Types.Count.ToString("N0")));
        InfoItems.Add(new InfoRow("Methods",    model.Types.Sum(t => t.Methods.Count).ToString("N0")));
        InfoItems.Add(new InfoRow("References", model.References.Count.ToString("N0")));
    }

    private void BuildTypeInfo(TypeModel model)
    {
        InfoItems.Add(new InfoRow("Kind",       model.Kind.ToString()));
        InfoItems.Add(new InfoRow("Namespace",  string.IsNullOrEmpty(model.Namespace) ? "(global)" : model.Namespace));
        InfoItems.Add(new InfoRow("Visibility", model.IsPublic   ? "Public"  : "Internal / Private"));
        InfoItems.Add(new InfoRow("Abstract",   model.IsAbstract ? "Yes"     : "No"));
        InfoItems.Add(new InfoRow("Sealed",     model.IsSealed   ? "Yes"     : "No"));
        if (!string.IsNullOrEmpty(model.BaseTypeName))
            InfoItems.Add(new InfoRow("Base Type",  model.BaseTypeName));
        if (model.InterfaceNames.Count > 0)
            InfoItems.Add(new InfoRow("Interfaces", string.Join(", ", model.InterfaceNames)));
        if (model.CustomAttributes.Count > 0)
            InfoItems.Add(new InfoRow("Attributes", string.Join(", ", model.CustomAttributes)));
        InfoItems.Add(new InfoRow("Methods",    model.Methods.Count.ToString()));
        InfoItems.Add(new InfoRow("Fields",     model.Fields.Count.ToString()));
        InfoItems.Add(new InfoRow("Properties", model.Properties.Count.ToString()));
    }

    private void BuildMemberInfo(MemberModel model)
    {
        if (!string.IsNullOrEmpty(model.Signature))
            InfoItems.Add(new InfoRow("Signature",  model.Signature));
        InfoItems.Add(new InfoRow("Kind",       model.Kind.ToString()));
        InfoItems.Add(new InfoRow("Visibility", model.IsPublic   ? "Public"     : "Non-public"));
        InfoItems.Add(new InfoRow("Static",     model.IsStatic   ? "Yes"        : "No"));
        InfoItems.Add(new InfoRow("Abstract",   model.IsAbstract ? "Yes"        : "No"));
        InfoItems.Add(new InfoRow("Virtual",    model.IsVirtual  ? "Yes"        : "No"));
        if (model.CustomAttributes.Count > 0)
            InfoItems.Add(new InfoRow("Attributes", string.Join(", ", model.CustomAttributes)));
    }

    // ── Hex tab helpers ───────────────────────────────────────────────────────

    private static byte[]? ReadPeBytes(string filePath, long offset)
    {
        if (offset <= 0 || string.IsNullOrEmpty(filePath)) return null;
        try
        {
            // FileShare.ReadWrite allows concurrent access when HexEditor holds the file open.
            using var fs = new FileStream(
                filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            if (offset >= fs.Length) return null;
            var count = (int)Math.Min(64, fs.Length - offset);
            fs.Seek(offset, SeekOrigin.Begin);
            var buf = new byte[count];
            _ = fs.Read(buf, 0, count);
            return buf;
        }
        catch { return null; }
    }

    private static string FormatHexDump(byte[]? bytes, long baseOffset)
    {
        if (bytes is null || bytes.Length == 0)
            return "// Could not read bytes at this PE offset.";

        var sb = new StringBuilder(bytes.Length * 4);
        for (var i = 0; i < bytes.Length; i += 16)
        {
            sb.Append($"  {baseOffset + i:X8}  ");
            for (var j = 0; j < 16; j++)
            {
                if (i + j < bytes.Length) sb.Append($"{bytes[i + j]:X2} ");
                else                      sb.Append("   ");
                if (j == 7) sb.Append(' ');
            }
            sb.Append("  ");
            for (var j = 0; j < 16 && i + j < bytes.Length; j++)
            {
                var c = (char)bytes[i + j];
                sb.Append(c >= 32 && c < 127 ? c : '.');
            }
            sb.AppendLine();
        }
        return sb.ToString();
    }
}
