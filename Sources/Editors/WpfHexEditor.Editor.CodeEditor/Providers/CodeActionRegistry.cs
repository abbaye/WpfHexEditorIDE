// ==========================================================
// Project: WpfHexEditor.Editor.CodeEditor
// File: Providers/CodeActionRegistry.cs
// Description: Process-global registry of ICodeActionProvider instances queried
//              by every CodeEditor on lightbulb tick and Ctrl+. invocation.
// Architecture Notes:
//     - Static singleton: contributors register once at app startup and the
//       same list is consumed by every CodeEditor instance.
//     - Snapshot-on-read: GetProviders() returns a copy so a slow provider
//       won't hold the lock while we await.
//     - Exception-safe aggregation: a throwing provider is logged and skipped
//       so it never breaks the editor.
// ==========================================================

using System.Diagnostics;
using WpfHexEditor.Editor.Core.LSP;

namespace WpfHexEditor.Editor.CodeEditor.Providers;

public static class CodeActionRegistry
{
    private static readonly object _lock = new();
    private static readonly List<ICodeActionProvider> _providers = [];

    /// <summary>True if at least one provider is registered — used to gate the lightbulb timer.</summary>
    public static bool HasProviders { get { lock (_lock) return _providers.Count > 0; } }

    /// <summary>Register a provider. Idempotent — same instance is added only once.</summary>
    public static void Register(ICodeActionProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        lock (_lock) { if (!_providers.Contains(provider)) _providers.Add(provider); }
    }

    /// <summary>Remove a previously registered provider.</summary>
    public static void Unregister(ICodeActionProvider provider)
    {
        if (provider is null) return;
        lock (_lock) _providers.Remove(provider);
    }

    /// <summary>Aggregate actions from every registered provider for the caret position.</summary>
    public static async Task<IReadOnlyList<LspCodeAction>> CollectAsync(
        string filePath, int line, int column, CancellationToken ct)
    {
        ICodeActionProvider[] snapshot;
        lock (_lock) snapshot = _providers.ToArray();
        if (snapshot.Length == 0) return [];

        var result = new List<LspCodeAction>();
        foreach (var p in snapshot)
        {
            if (ct.IsCancellationRequested) break;
            try
            {
                var actions = await p.GetCodeActionsAsync(filePath, line, column, ct).ConfigureAwait(false);
                if (actions.Count > 0) result.AddRange(actions);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { Debug.WriteLine($"[CodeActionRegistry] {p.GetType().Name}: {ex.Message}"); }
        }
        return result;
    }
}
