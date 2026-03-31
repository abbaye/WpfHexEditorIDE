// ==========================================================
// Project: WpfHexEditor.App
// File: Services/ScriptingServiceImpl.cs
// Description:
//     Bridges IScriptingService (SDK) to RoslynScriptEngine (WpfHexEditor.Scripting).
//     Builds ScriptGlobals from the injected IDE services on each RunAsync call.
//     Raises ScriptExecuted event on the UI thread after each run.
// Architecture:
//     App layer only — no plugin or Core.Scripting assembly referenced from plugins.
//     ScriptResult → IScriptResult adapter via inner ScriptResultAdapter.
// ==========================================================

using System.Windows;
using WpfHexEditor.Core.Scripting;
using WpfHexEditor.SDK.Contracts.Services;

namespace WpfHexEditor.App.Services;

/// <summary>
/// App-level implementation of <see cref="IScriptingService"/>.
/// Wraps <see cref="RoslynScriptEngine"/> and injects IDE services into script globals.
/// </summary>
public sealed class ScriptingServiceImpl : IScriptingService
{
    private readonly RoslynScriptEngine     _engine = new();
    private readonly IHexEditorService      _hexEditor;
    private readonly IDocumentHostService   _documentHost;
    private readonly IOutputService         _output;
    private readonly ITerminalService?      _terminal;

    public event EventHandler<ScriptExecutedEventArgs>? ScriptExecuted;

    public ScriptingServiceImpl(
        IHexEditorService    hexEditor,
        IDocumentHostService documentHost,
        IOutputService       output,
        ITerminalService?    terminal = null)
    {
        _hexEditor    = hexEditor    ?? throw new ArgumentNullException(nameof(hexEditor));
        _documentHost = documentHost ?? throw new ArgumentNullException(nameof(documentHost));
        _output       = output       ?? throw new ArgumentNullException(nameof(output));
        _terminal     = terminal;
    }

    // ── IScriptingService ────────────────────────────────────────────────────

    public async Task<IScriptResult> RunAsync(string code, CancellationToken ct = default)
    {
        var globals = BuildGlobals(ct);
        var result  = await _engine.RunAsync(code, globals, ct).ConfigureAwait(false);
        var adapted = new ScriptResultAdapter(result);

        System.Windows.Threading.Dispatcher.CurrentDispatcher.InvokeAsync(
            () => ScriptExecuted?.Invoke(this, new ScriptExecutedEventArgs(adapted)));

        return adapted;
    }

    public async Task<IScriptResult> ValidateAsync(string code, CancellationToken ct = default)
    {
        var result = await _engine.ValidateAsync(code, ct).ConfigureAwait(false);
        return new ScriptResultAdapter(result);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private ScriptGlobals BuildGlobals(CancellationToken ct) => new()
    {
        HexEditor  = _hexEditor,
        Documents  = _documentHost,
        Output     = _output,
        Terminal   = _terminal,
        CT         = ct,
    };

    // ── Inner adapters (IScriptResult / IScriptDiagnostic) ───────────────────

    private sealed class ScriptResultAdapter(ScriptResult r) : IScriptResult
    {
        public bool     Success   => r.Success;
        public string   Output    => r.Output;
        public bool     HasErrors => r.HasErrors;
        public TimeSpan Duration  => r.Duration;
        public IReadOnlyList<IScriptDiagnostic> Diagnostics { get; } =
            r.Errors.Select(e => (IScriptDiagnostic)new DiagAdapter(e)).ToList();
    }

    private sealed class DiagAdapter(ScriptError e) : IScriptDiagnostic
    {
        public string Message   => e.Message;
        public int    Line      => e.Line;
        public int    Column    => e.Column;
        public bool   IsWarning => e.IsWarning;
    }
}
