// ==========================================================
// Project: WpfHexEditor.Plugins.AssemblyExplorer
// File: ViewModels/AssemblyExplorerViewModel.Navigation.cs
// Description:
//     Hex editor navigation, node selection dispatch, decompilation, editor
//     integration, metadata table nav, reverse token nav, search/diff support.
// ==========================================================

using System.IO;
using WpfHexEditor.Core.AssemblyAnalysis.Languages;
using WpfHexEditor.Core.AssemblyAnalysis.Models;
using WpfHexEditor.Core.AssemblyAnalysis.Services;
using WpfHexEditor.Core.ProjectSystem.Languages;
using WpfHexEditor.Plugins.AssemblyExplorer.Events;
using WpfHexEditor.Plugins.AssemblyExplorer.Services;
using WpfHexEditor.SDK.Descriptors;

namespace WpfHexEditor.Plugins.AssemblyExplorer.ViewModels;

public sealed partial class AssemblyExplorerViewModel
{
    // ── Node selection dispatch ───────────────────────────────────────────────

    /// <summary>
    /// Called when the user selects a tree node.
    /// Fast side-effects happen synchronously; decompilation is dispatched async.
    /// </summary>
    public void OnNodeSelected(AssemblyNodeViewModel node)
    {
        NavigateHexEditorToNode(node);
        PublishMemberSelected(node);

        if (node is ReferenceNodeViewModel refNode)
            TryNavigateToReference(refNode);

        StartDecompileAsync(node);
    }

    private void StartDecompileAsync(AssemblyNodeViewModel node)
    {
        _nodeSelectionCts?.Cancel();
        _nodeSelectionCts = new CancellationTokenSource();
        var ct       = _nodeSelectionCts.Token;
        var filePath = node.OwnerFilePath ?? string.Empty;
        _ = SafeShowNodeAsync(node, filePath, ct);
    }

    private async Task SafeShowNodeAsync(AssemblyNodeViewModel node, string filePath, CancellationToken ct)
    {
        try
        {
            await DetailViewModel.ShowNodeAsync(node, filePath, ct);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _output.Write("Plugin System", $"[Assembly Explorer] Decompile error for '{node.DisplayName}': {ex.Message}");
        }
    }

    // ── HexEditor sync ────────────────────────────────────────────────────────

    private void NavigateHexEditorToNode(AssemblyNodeViewModel node, bool force = false)
    {
        if (!force && !_syncWithHexEditor) return;
        if (node.PeOffset <= 0) return;
        if (!_hexEditor.IsActive)
        {
            if (force)
                _output.Write("Plugin System", "[Assembly Explorer] Open the assembly in the HexEditor first.");
            return;
        }

        var hexFile      = _hexEditor.CurrentFilePath;
        var assemblyFile = node.OwnerFilePath;
        if (force
            && !string.IsNullOrEmpty(hexFile)
            && !string.IsNullOrEmpty(assemblyFile)
            && !string.Equals(hexFile, assemblyFile, StringComparison.OrdinalIgnoreCase))
        {
            _output.Write("Plugin System",
                $"[Assembly Explorer] HexEditor has '{Path.GetFileName(hexFile)}' open, " +
                $"but the explorer loaded '{Path.GetFileName(assemblyFile)}'. " +
                $"Navigating anyway — offsets may not match.");
        }

        try { _hexEditor.NavigateTo(node.PeOffset); }
        catch (Exception ex)
        {
            _output.Write("Plugin System", $"[Assembly Explorer] HexEditor navigation failed: {ex.Message}");
        }
    }

    /// <summary>Explicit "Open in HexEditor" — bypasses SyncWithHexEditor toggle.</summary>
    public void NavigateToNodeExplicit(AssemblyNodeViewModel node)
    {
        StartDecompileAsync(node);
        NavigateHexEditorToNode(node, force: true);
    }

    /// <summary>Opens a local source file in the IDE text editor at the given line.</summary>
    public void OpenSourceFileInTextEditor(string filePath, int line)
    {
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return;
        try   { _documentHost?.ActivateAndNavigateTo(filePath, line, column: 1); }
        catch (Exception ex)
        { _output.Write("Plugin System", $"[Assembly Explorer] Failed to open source '{Path.GetFileName(filePath)}': {ex.Message}"); }
    }

    /// <summary>Opens the assembly file in the hex editor at offset 0.</summary>
    public void OpenAssemblyFileInHexEditor(string filePath)
    {
        if (string.IsNullOrEmpty(filePath)) return;
        try   { _documentHost?.OpenDocument(filePath, preferredEditorId: "hex-editor"); }
        catch (Exception ex)
        { _output.Write("Plugin System", $"[Assembly Explorer] Failed to open '{Path.GetFileName(filePath)}': {ex.Message}"); }
    }

    /// <summary>
    /// Opens the assembly file in the hex editor, scrolls to the member's PE offset,
    /// and highlights the member's byte range (if ByteLength > 0).
    /// </summary>
    public async Task OpenMemberInHexEditorAsync(AssemblyNodeViewModel node)
    {
        if (node.PeOffset <= 0) return;
        var filePath = node.OwnerFilePath;
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return;

        try
        {
            _documentHost?.OpenDocument(filePath, preferredEditorId: "hex-editor");

            var tag = $"AsmExplorer.{node.MetadataToken}";
            _hexEditor.ClearCustomBackgroundBlockByTag(tag);
            _hexEditor.NavigateTo(node.PeOffset);

            if (node.ByteLength > 0)
            {
                var brush = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(79, 193, 255));
                var block = new WpfHexEditor.Core.CustomBackgroundBlock
                {
                    StartOffset = node.PeOffset,
                    Length      = node.ByteLength,
                    Color       = brush,
                    Opacity     = 0.25,
                    Description = tag
                };
                _hexEditor.AddCustomBackgroundBlock(block);
            }

            _output.Write("Plugin System",
                $"[Assembly Explorer] Navigated hex editor to '{node.DisplayName}'" +
                $" offset 0x{node.PeOffset:X}" +
                (node.ByteLength > 0 ? $" ({node.ByteLength} bytes)" : string.Empty));
        }
        catch (Exception ex)
        {
            _output.Write("Plugin System", $"[Assembly Explorer] Hex editor navigation failed: {ex.Message}");
        }

        await Task.CompletedTask;
    }

    // ── Cross-assembly reference navigation ───────────────────────────────────

    private void TryNavigateToReference(ReferenceNodeViewModel refNode)
    {
        var refName = refNode.Reference.Name;
        var targetEntry = _workspace.Values
            .FirstOrDefault(e => string.Equals(e.Model.Name, refName, StringComparison.OrdinalIgnoreCase));

        if (targetEntry is not null)
        {
            targetEntry.Root.IsSelected = true;
            targetEntry.Root.IsExpanded = true;
            StatusText = $"Jumped to '{targetEntry.Model.Name}' in workspace.";
        }
        else
        {
            StatusText = $"Assembly '{refName}' is not in the workspace. Use Open Assembly to load it.";
        }
    }

    // ── EventBus publishing ───────────────────────────────────────────────────

    private void PublishMemberSelected(AssemblyNodeViewModel node)
    {
        MemberSelected?.Invoke(this, new AssemblyMemberSelectedEvent
        {
            NodeDisplayName = node.DisplayName,
            MetadataToken   = node.MetadataToken,
            PeOffset        = node.PeOffset,
            NodeKind        = node.GetType().Name.Replace("NodeViewModel", string.Empty)
        });
    }

    // ── Open in Code Editor ───────────────────────────────────────────────────

    private async Task OpenSelectedNodeInEditorAsync()
    {
        if (_selectedNode is null) return;

        var node     = _selectedNode;
        var filePath = node.OwnerFilePath ?? string.Empty;

        var langId   = _decompilerBackend.Options.TargetLanguageId ?? "CSharp";
        var language = DecompilationLanguageRegistry.Get(langId)
                    ?? CSharpDecompilationLanguage.Instance;

        var token = node.MetadataToken;
        var hash  = token != 0 ? token.ToString("X8") : node.DisplayName.GetHashCode().ToString("X8");
        var uiId  = $"doc-plugin-{_pluginId}-decompiled-{hash}-{langId}";
        var title = $"{node.DisplayName} ({language.DisplayName})";

        if (_uiRegistry.Exists(uiId))
            _uiRegistry.UnregisterDocumentTab(uiId);

        bool isIlOutput = string.Equals(language.Id, "IL", StringComparison.OrdinalIgnoreCase);

        string rawText;
        try
        {
            if (isIlOutput)
            {
                rawText = await Task.Run(() =>
                {
                    switch (node)
                    {
                        case MethodNodeViewModel meth:
                            var single = _decompiler.GetIlText(meth.Model, filePath);
                            return string.IsNullOrEmpty(single)
                                ? "// No IL body.\n// Possible causes: abstract, extern, interface, " +
                                  "delegate stub, or reference assembly."
                                : single;

                        case TypeNodeViewModel type:
                            var parts = type.Model.Methods
                                .Select(m => _decompiler.GetIlText(m, filePath))
                                .Where(s => !string.IsNullOrEmpty(s))
                                .ToList();
                            return parts.Count > 0
                                ? string.Join(Environment.NewLine + Environment.NewLine, parts)
                                : "// No IL disassembly available for this type.";

                        default:
                            return _decompiler.GetStubText(node.DisplayName);
                    }
                });
            }
            else
            {
                rawText = await Task.Run(() => node switch
                {
                    AssemblyRootNodeViewModel root => _decompilerBackend.DecompileAssembly(root.Model, filePath),
                    TypeNodeViewModel         type => _decompilerBackend.DecompileType(type.Model, filePath),
                    MethodNodeViewModel       meth => _decompilerBackend.DecompileMethod(meth.Model, filePath),
                    _                              => _decompiler.GetStubText(node.DisplayName)
                });
            }
        }
        catch (Exception ex)
        {
            rawText = $"// Decompilation failed: {ex.Message}";
        }

        string text;
        if (!isIlOutput && _decompilerBackend.OutputIsCSharpOnly && language.Id != "CSharp")
        {
            try
            {
                var (transformed, _) = await language.TransformFromCSharpAsync(rawText, CancellationToken.None);
                text = transformed;
            }
            catch (Exception ex)
            {
                text = $"// {language.DisplayName} transform failed: {ex.Message}\n\n{rawText}";
            }
        }
        else
        {
            text = rawText;
        }

        // Write decompiled text to a temp file and open it in the IDE's CodeEditor.
        var ext = isIlOutput ? ".il"
            : language.Id switch
            {
                "CSharp" => ".cs",
                "VB"     => ".vb",
                _        => ".cs"
            };

        var tempDir = Path.Combine(Path.GetTempPath(), "WpfHexEditor", "Decompiled");
        Directory.CreateDirectory(tempDir);
        var safeName = string.Concat(title.Split(Path.GetInvalidFileNameChars()));
        var tempPath = Path.Combine(tempDir, safeName + ext);

        File.WriteAllText(tempPath, text);
        _documentHost?.OpenDocument(tempPath, WpfHexEditor.SDK.Contracts.WellKnownEditorIds.CodeEditor);
    }

    private void NavigateToTypeName(string fullName)
    {
        foreach (var root in RootNodes)
        {
            foreach (var nsNode in root.Children)
            {
                foreach (var typeNode in nsNode.Children)
                {
                    if (typeNode is TypeNodeViewModel tn && tn.Model.FullName == fullName)
                    {
                        SelectedNode = tn;
                        return;
                    }
                }
            }
        }
    }

    // ── Decompiled text API ───────────────────────────────────────────────────

    /// <summary>
    /// Returns the decompiled C# text for the given node.
    /// </summary>
    public (bool isCSharp, string text) GetDecompiledText(AssemblyNodeViewModel node)
    {
        var filePath = node.OwnerFilePath ?? string.Empty;
        return node switch
        {
            AssemblyRootNodeViewModel root => (true,  _decompilerBackend.DecompileAssembly(root.Model, filePath)),
            TypeNodeViewModel         type => (true,  _decompilerBackend.DecompileType(type.Model, filePath)),
            MethodNodeViewModel       meth => (true,  _decompilerBackend.DecompileMethod(meth.Model, filePath)),
            _                              => (false, _decompiler.GetStubText(node.DisplayName))
        };
    }

    // ── Metadata table navigation ─────────────────────────────────────────────

    public void NavigateToMetadataTable(string tableName, string ownerFilePath)
    {
        var root = RootNodes.OfType<AssemblyRootNodeViewModel>()
                            .FirstOrDefault(r => r.OwnerFilePath == ownerFilePath
                                             || r.Model.FilePath == ownerFilePath);
        if (root is null) return;

        var metaGroup = root.Children.OfType<NamespaceNodeViewModel>()
                            .FirstOrDefault(n => n.DisplayName == "Metadata Tables");
        if (metaGroup is null) return;

        metaGroup.IsExpanded = true;

        var tableNode = metaGroup.Children.OfType<MetadataTableNodeViewModel>()
                                 .FirstOrDefault(t => t.TableName == tableName);
        if (tableNode is null) return;

        SelectedNode = tableNode;
    }

    // ── Reverse Hex → Tree navigation ────────────────────────────────────────

    /// <summary>
    /// Selects the tree node whose MetadataToken matches <paramref name="token"/>
    /// and sets IsReverseHighlighted. Must be called on the UI thread.
    /// </summary>
    public void SelectNode(int? token)
    {
        if (token is null) return;
        var tokenValue = token.Value;
        if (tokenValue == 0) return;

        ClearReverseHighlight(RootNodes);

        foreach (var root in RootNodes)
        {
            var found = FindNodeByTokenRecursive(root.Children, tokenValue);
            if (found is null) continue;

            found.IsReverseHighlighted = true;
            found.IsSelected           = true;
            EnsureAncestorsExpanded(root, found);
            return;
        }
    }

    public void NavigateToOffset(int? token)
    {
        if (token is null) return;
        foreach (var root in RootNodes)
        {
            var node = FindNodeByTokenRecursive(root.Children, token.Value);
            if (node is not null && node.PeOffset > 0)
            {
                NavigateHexEditorToNode(node, force: true);
                return;
            }
        }
    }

    private static void ClearReverseHighlight(IEnumerable<AssemblyNodeViewModel> nodes)
    {
        foreach (var node in nodes)
        {
            if (node.IsReverseHighlighted) node.IsReverseHighlighted = false;
            ClearReverseHighlight(node.Children);
        }
    }

    private static bool EnsureAncestorsExpanded(AssemblyNodeViewModel current, AssemblyNodeViewModel target)
    {
        if (ReferenceEquals(current, target)) return true;

        foreach (var child in current.Children)
        {
            if (EnsureAncestorsExpanded(child, target))
            {
                current.IsExpanded = true;
                return true;
            }
        }

        return false;
    }

    // ── Search / Diff support ─────────────────────────────────────────────────

    public IReadOnlyList<WpfHexEditor.Core.AssemblyAnalysis.Models.AssemblyModel> GetLoadedAssemblyModels()
        => _workspace.Values.Select(e => e.Model).ToList();

    public AssemblyNodeViewModel? FindNodeByToken(int token, string filePath)
    {
        if (!_workspace.TryGetValue(filePath, out var entry)) return null;
        return FindNodeByTokenRecursive(entry.Root.Children, token);
    }

    private static AssemblyNodeViewModel? FindNodeByTokenRecursive(
        IEnumerable<AssemblyNodeViewModel> nodes, int token)
    {
        foreach (var node in nodes)
        {
            if (node.MetadataToken == token) return node;
            var found = FindNodeByTokenRecursive(node.Children, token);
            if (found is not null) return found;
        }
        return null;
    }
}
