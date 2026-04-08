// ==========================================================
// Project: WpfHexEditor.Plugins.AssemblyExplorer
// File: ViewModels/AssemblyDetailViewModel.cs
// Author: Derek Tremblay
// Created: 2026-03-08
// Updated: 2026-03-16 — Phase 8: async background decompilation + LRU cache.
//     ShowNode replaced by ShowNodeAsync (Task.Run + DecompileCache).
//     IsLoading / LoadingMessage drive a progress overlay in the View.
//     Phase 4: CfgViewModel added; LoadForMethodAsync fires for method nodes.
// Description:
//     ViewModel for the detail pane (bottom split of the Assembly Explorer panel).
//     5-tab layout —
//       Code  — C# with real method bodies via IDecompilerBackend (ILSpy)
//       IL    — raw IL disassembly (methods only)
//       Info  — metadata token, PE offset, visibility/modifier flags, custom attrs
//       Hex   — 64-byte hex dump at the PE offset (read on demand from file)
//       CFG   — Control Flow Graph rendered by CfgCanvas (method nodes only)
//
// Architecture Notes:
//     Pattern: MVVM — populated by AssemblyExplorerViewModel.OnNodeSelected.
//     ShowNodeAsync is the single entry point; all decompile calls run on
//     a background thread via Task.Run and are short-circuited by an LRU cache.
//     All string formatting stays in the VM — the View is binding-only.
// ==========================================================

using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using WpfHexEditor.Core.AssemblyAnalysis.Languages;
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
    private readonly IDecompilerBackend _backend;
    private readonly DecompileCache     _cache;

    public AssemblyDetailViewModel(IDecompilerBackend backend, DecompileCache cache)
    {
        _backend = backend;
        _cache   = cache;

        // Wire CFG block clicks → switch to IL tab
        CfgViewModel.BlockOffsetSelected += _ => ActiveTabIndex = 1; // IL tab
    }

    // ── CFG tab (Tab 4) ───────────────────────────────────────────────────────

    /// <summary>View-model for the CFG tab.</summary>
    public CfgViewModel  CfgViewModel  { get; } = new();

    // ── XRefs tab (Tab 5) ─────────────────────────────────────────────────────

    /// <summary>View-model for the XRefs tab.</summary>
    public XRefViewModel XRefViewModel { get; } = new();

    // ── Source tab (Tab 6) ────────────────────────────────────────────────────

    /// <summary>View-model for the Source (PDB/SourceLink) tab.</summary>
    public SourceViewModel SourceViewModel { get; } = new();

    // ── Synchronized Scrolling — Decompiler ↔ Hex (ASM-02-E) ─────────────────

    /// <summary>
    /// When true, scrolling the decompiler pane maps IL offsets to hex positions,
    /// and vice versa.  Controlled by a toolbar toggle button in the detail pane.
    /// </summary>
    private bool _isSyncScrollEnabled;
    public bool IsSyncScrollEnabled
    {
        get => _isSyncScrollEnabled;
        set => SetField(ref _isSyncScrollEnabled, value);
    }

    /// <summary>
    /// Raised by the detail pane when the decompiler scroll position changes and
    /// <see cref="IsSyncScrollEnabled"/> is true.
    /// The caller (AssemblyExplorerPanel) should forward the IL line offset to
    /// <see cref="IHexEditorService.NavigateTo"/> after resolving via PeOffset.
    /// </summary>
    public event EventHandler<long>? SyncScrollToHexRequested;

    /// <summary>
    /// Raised by the detail pane when the hex selection changes and
    /// <see cref="IsSyncScrollEnabled"/> is true.
    /// The caller (AssemblyExplorerPanel) should scroll the decompiler to the
    /// corresponding IL line.
    /// </summary>
    public event EventHandler<long>? SyncScrollToDecompilerRequested;

    /// <summary>
    /// Called by the detail pane view code-behind when the decompiler's visible
    /// line changes and <see cref="IsSyncScrollEnabled"/> is true.
    /// Converts a 0-based decompiler line index to a PE file offset using the
    /// currently displayed node's <see cref="AssemblyNodeViewModel.PeOffset"/>.
    /// </summary>
    public void NotifyDecompilerScrollChanged(int visibleLine)
    {
        if (!IsSyncScrollEnabled) return;
        if (CurrentNode is null) return;

        // Map: decompiler line → PE byte offset.
        // Approximation: each line of IL/C# maps to ~4 bytes of code.
        // The PeOffset is the method body start; we advance by the line index.
        var baseOffset = CurrentNode.PeOffset;
        if (baseOffset <= 0) return;

        const int bytesPerLine = 4;
        var estimatedOffset = baseOffset + (long)visibleLine * bytesPerLine;
        SyncScrollToHexRequested?.Invoke(this, estimatedOffset);
    }

    /// <summary>
    /// Called by the hex editor selection-changed handler (in AssemblyExplorerPanel)
    /// when <see cref="IsSyncScrollEnabled"/> is true.
    /// Raises <see cref="SyncScrollToDecompilerRequested"/> so the view can scroll
    /// the decompiler editor to the corresponding IL line.
    /// </summary>
    public void NotifyHexSelectionChanged(long hexOffset)
    {
        if (!IsSyncScrollEnabled) return;
        if (CurrentNode is null) return;

        var baseOffset = CurrentNode.PeOffset;
        if (baseOffset <= 0 || hexOffset < baseOffset) return;

        SyncScrollToDecompilerRequested?.Invoke(this, hexOffset - baseOffset);
    }

    /// <summary>
    /// Callback wired by the hosting panel to navigate the tree when an XRef entry is clicked.
    /// </summary>
    public Action<int>? OnXRefNavigate
    {
        get => _onXRefNavigate;
        set
        {
            _onXRefNavigate = value;
            XRefViewModel.NavigateRequested -= OnXRefNavigateHandler;
            if (value is not null) XRefViewModel.NavigateRequested += OnXRefNavigateHandler;
        }
    }
    private Action<int>? _onXRefNavigate;
    private void OnXRefNavigateHandler(int token) => _onXRefNavigate?.Invoke(token);

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

    // ── Loading state (Phase 8) ───────────────────────────────────────────────

    private bool _isLoading;
    // Intentionally hides AssemblyNodeViewModel.IsLoading — this property drives
    // the detail-pane loading overlay, not the tree-node visibility state.
    public new bool IsLoading
    {
        get => _isLoading;
        private set => SetField(ref _isLoading, value);
    }

    private string _loadingMessage = string.Empty;
    public string LoadingMessage
    {
        get => _loadingMessage;
        private set => SetField(ref _loadingMessage, value);
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
    /// Asynchronously updates all detail pane tabs to reflect the selected <paramref name="node"/>.
    /// Fast synchronous work (Info, Hex) runs immediately on the UI thread.
    /// Expensive decompilation (Code, IL) runs on a background thread via Task.Run,
    /// short-circuited by the LRU <see cref="DecompileCache"/> on repeated selections.
    /// <paramref name="ct"/> is checked before each UI property update so a new
    /// selection can abort an in-flight decompile without leaving stale content.
    /// </summary>
    public async Task ShowNodeAsync(AssemblyNodeViewModel node, string filePath, CancellationToken ct)
    {
        // ── Synchronous, instant metadata ────────────────────────────────────
        CurrentNode   = node;
        Title         = node.DisplayName;
        PeOffsetValue = node.PeOffset;
        MetadataInfo  = node.MetadataToken != 0
            ? $"Token: 0x{node.MetadataToken:X8}"
            : string.Empty;

        BuildInfoItems(node);

        IsExtractAvailable = node is AssemblyRootNodeViewModel
                                  or TypeNodeViewModel
                                  or MethodNodeViewModel;

        HexDumpText = node.PeOffset > 0
            ? FormatHexDump(ReadPeBytes(filePath, node.PeOffset), node.PeOffset)
            : "// No PE offset available for this node.";

        // ── Clear stale text immediately ──────────────────────────────────────
        LoadingMessage = $"Decompiling {node.DisplayName}…";
        DetailText    = string.Empty;
        IlText        = string.Empty;

        var token = node.MetadataToken;

        // ── Resolve target language ───────────────────────────────────────────
        var langId   = _backend.Options.TargetLanguageId ?? "CSharp";
        var language = DecompilationLanguageRegistry.Get(langId)
                    ?? CSharpDecompilationLanguage.Instance;
        var codeKey  = $"code_{langId}";

        // ── Code tab ─────────────────────────────────────────────────────────
        if (!_cache.TryGet(filePath, token, codeKey, out var code))
        {
            // Step 1 — decompile via backend (C# for ILSpy, language-native for Skeleton)
            string rawCode;
            try
            {
                rawCode = await Task.Run(() => node switch
                {
                    AssemblyRootNodeViewModel root => _backend.DecompileAssembly(root.Model, filePath),
                    TypeNodeViewModel         type => _backend.DecompileType(type.Model, filePath),
                    MethodNodeViewModel       meth => _backend.DecompileMethod(meth.Model, filePath),
                    FieldNodeViewModel        fld  => _backend.DecompileMethod(fld.Model, filePath),
                    PropertyNodeViewModel     prop => _backend.DecompileMethod(prop.Model, filePath),
                    _                              => $"// {node.DisplayName}"
                }, ct);
            }
            catch (OperationCanceledException) { return; }
            catch (Exception ex) { rawCode = $"// Decompilation failed:\n// {ex.Message}"; }

            if (ct.IsCancellationRequested) { return; }

            // Step 2 — post-decompile language transform (only when backend output is C#-only)
            if (_backend.OutputIsCSharpOnly && language.Id != "CSharp")
            {
                LoadingMessage = $"Converting to {language.DisplayName}…";
                try
                {
                    var (transformed, _) = await language.TransformFromCSharpAsync(rawCode, ct);
                    code = transformed;
                }
                catch (OperationCanceledException) { return; }
                catch (Exception ex)
                {
                    code = $"// {language.DisplayName} transform failed: {ex.Message}\n\n{rawCode}";
                }

                if (ct.IsCancellationRequested) { return; }
            }
            else
            {
                code = rawCode;
            }

            // Only cache named tokens; assembly roots (token=0) are omitted to avoid stale info.
            if (token != 0) _cache.Set(filePath, token, codeKey, code);
        }

        DetailText = code;

        // ── IL tab (method nodes only) ────────────────────────────────────────
        if (node is MethodNodeViewModel methodNode)
        {
            if (!_cache.TryGet(filePath, token, "il", out var il))
            {
                LoadingMessage = $"Disassembling IL for {node.DisplayName}…";
                try
                {
                    il = await Task.Run(() => _backend.GetIlText(methodNode.Model, filePath), ct);
                }
                catch (OperationCanceledException) { return; }
                catch (Exception ex) { il = $"// IL disassembly failed:\n// {ex.Message}"; }

                if (ct.IsCancellationRequested) { return; }

                if (token != 0) _cache.Set(filePath, token, "il", il);
            }

            IlText = il;
        }
        else
        {
            IlText = string.Empty;
        }

        // ── CFG tab — async, independent of decompile cache ────────────────────
        if (node is MethodNodeViewModel cfgMethodNode)
            _ = CfgViewModel.LoadForMethodAsync(cfgMethodNode.Model, filePath, ct);
        else
            CfgViewModel.Clear();

        // ── XRefs tab — fire-and-forget scan ──────────────────────────────────
        if (node is MethodNodeViewModel xrefMethodNode && node.OwnerFilePath is { } xrefFilePath)
        {
            _ = XRefViewModel.LoadAsync(xrefMethodNode.Model, xrefFilePath, ct);
        }
        else if (node is FieldNodeViewModel xrefFieldNode && node.OwnerFilePath is { } fieldFilePath)
        {
            _ = XRefViewModel.LoadAsync(xrefFieldNode.Model, fieldFilePath, ct);
        }
        else
        {
            XRefViewModel.Clear();
        }

        // ── Source tab — fire-and-forget PDB load (method nodes only) ──────────
        if (node is MethodNodeViewModel srcMethodNode && !string.IsNullOrEmpty(filePath))
            _ = SourceViewModel.LoadAsync(srcMethodNode.Model, filePath, ct);
        else
            SourceViewModel.Clear();

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
        IsLoading          = false;
        LoadingMessage     = string.Empty;
        InfoItems.Clear();
        CfgViewModel.Clear();
        XRefViewModel.Clear();
        SourceViewModel.Clear();
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
