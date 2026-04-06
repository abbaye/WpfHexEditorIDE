// ==========================================================
// Project: WpfHexEditor.Core.Roslyn
// File: Providers/RoslynReferenceCountProvider.cs
// Contributors: Claude Sonnet 4.6
// Created: 2026-04-06
// Description:
//     Implements IReferenceCountProvider using SymbolFinder.FindReferencesAsync.
//     Returns null on any Roslyn failure so InlineHintsService silently falls
//     back to the regex path.
//
// Architecture Notes:
//     Reuses RoslynWorkspaceManager.GetDocument() and CurrentSolution —
//     same pattern as RoslynNavigationProvider.
// ==========================================================

using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Text;
using WpfHexEditor.Editor.Core.LSP;

namespace WpfHexEditor.Core.Roslyn.Providers;

internal sealed class RoslynReferenceCountProvider : IReferenceCountProvider
{
    private readonly RoslynWorkspaceManager _workspace;

    internal RoslynReferenceCountProvider(RoslynWorkspaceManager workspace)
        => _workspace = workspace;

    // ── IReferenceCountProvider ───────────────────────────────────────────────

    public bool CanProvide(string filePath)
        => _workspace.GetDocument(filePath) is not null;

    public async Task<int?> CountReferencesAsync(
        string filePath, int declarationLine, string symbolName, CancellationToken ct)
    {
        try
        {
            var doc = _workspace.GetDocument(filePath);
            if (doc is null) return null;

            var text = await doc.GetTextAsync(ct).ConfigureAwait(false);
            if (declarationLine >= text.Lines.Count) return null;

            // Find the column of the symbol name on its declaration line.
            var col = text.Lines[declarationLine].ToString()
                          .IndexOf(symbolName, System.StringComparison.Ordinal);
            if (col < 0) return null;

            var pos   = text.Lines.GetPosition(new LinePosition(declarationLine, col));
            var model = await doc.GetSemanticModelAsync(ct).ConfigureAwait(false);
            var root  = await doc.GetSyntaxRootAsync(ct).ConfigureAwait(false);
            if (model is null || root is null) return null;

            var token  = root.FindToken(pos);
            var symbol = model.GetDeclaredSymbol(token.Parent!, ct)
                      ?? model.GetSymbolInfo(token.Parent!, ct).Symbol;
            if (symbol is null) return null;

            var refs = await SymbolFinder.FindReferencesAsync(
                symbol, _workspace.CurrentSolution, ct).ConfigureAwait(false);

            return refs.Sum(r => r.Locations.Count());
        }
        catch (OperationCanceledException) { throw; }
        catch { return null; }
    }
}
