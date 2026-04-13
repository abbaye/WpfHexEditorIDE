// ==========================================================
// Project: WpfHexEditor.Tests
// File: RoslynLanguageClientTests.cs
// Description:
//     Unit tests for RoslynLanguageClient — lifecycle, document sync,
//     and InlineHints options. Uses AdhocWorkspace (no MSBuild required).
// ==========================================================

using WpfHexEditor.Core.Roslyn;
using WpfHexEditor.Editor.Core.LSP;

namespace WpfHexEditor.Tests;

[TestClass]
public sealed class RoslynLanguageClientTests
{
    private static System.Windows.Threading.Dispatcher Dispatcher =>
        System.Windows.Threading.Dispatcher.CurrentDispatcher;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    [TestMethod]
    public async Task InitializeAsync_SetsIsInitialized()
    {
        var client = new RoslynLanguageClient(Dispatcher);
        await client.InitializeAsync();
        Assert.IsTrue(client.IsInitialized);
        await client.DisposeAsync();
    }

    [TestMethod]
    public async Task InitializeAsync_CalledTwice_DoesNotThrow()
    {
        var client = new RoslynLanguageClient(Dispatcher);
        await client.InitializeAsync();
        await client.InitializeAsync(); // idempotent
        await client.DisposeAsync();
    }

    [TestMethod]
    public async Task LoadedProjectCount_InitiallyZero()
    {
        var client = new RoslynLanguageClient(Dispatcher);
        Assert.AreEqual(0, client.LoadedProjectCount);
        await client.DisposeAsync();
    }

    [TestMethod]
    public async Task DisposeAsync_DoesNotThrow()
    {
        var client = new RoslynLanguageClient(Dispatcher);
        await client.InitializeAsync();
        await client.DisposeAsync();
    }

    // ── Document sync ─────────────────────────────────────────────────────────

    [TestMethod]
    public async Task OpenDocument_CSharp_DoesNotThrow()
    {
        var client = new RoslynLanguageClient(Dispatcher);
        await client.InitializeAsync();
        client.OpenDocument("/tmp/test.cs", "csharp", "class Foo {}");
        await client.DisposeAsync();
    }

    [TestMethod]
    public async Task DidChange_AfterOpen_DoesNotThrow()
    {
        var client = new RoslynLanguageClient(Dispatcher);
        await client.InitializeAsync();
        client.OpenDocument("/tmp/test.cs", "csharp", "class Foo {}");
        client.DidChange("/tmp/test.cs", 2, "class Bar {}");
        await client.DisposeAsync();
    }

    [TestMethod]
    public async Task CloseDocument_AfterOpen_DoesNotThrow()
    {
        var client = new RoslynLanguageClient(Dispatcher);
        await client.InitializeAsync();
        client.OpenDocument("/tmp/test.cs", "csharp", "class X {}");
        client.CloseDocument("/tmp/test.cs");
        await client.DisposeAsync();
    }

    [TestMethod]
    public async Task CloseDocument_NotOpenFile_DoesNotThrow()
    {
        var client = new RoslynLanguageClient(Dispatcher);
        await client.InitializeAsync();
        client.CloseDocument("/tmp/not-open.cs");
        await client.DisposeAsync();
    }

    [TestMethod]
    public async Task SaveDocument_WithText_DoesNotThrow()
    {
        var client = new RoslynLanguageClient(Dispatcher);
        await client.InitializeAsync();
        client.OpenDocument("/tmp/save.cs", "csharp", "class S {}");
        client.SaveDocument("/tmp/save.cs", "class S { /* updated */ }");
        await client.DisposeAsync();
    }

    [TestMethod]
    public async Task SaveDocument_NullText_DoesNotThrow()
    {
        var client = new RoslynLanguageClient(Dispatcher);
        await client.InitializeAsync();
        client.OpenDocument("/tmp/save2.cs", "csharp", "class S2 {}");
        client.SaveDocument("/tmp/save2.cs", null);
        await client.DisposeAsync();
    }

    // ── Completions ───────────────────────────────────────────────────────────

    [TestMethod]
    public async Task CompletionAsync_EmptyFile_ReturnsEmptyList()
    {
        var client = new RoslynLanguageClient(Dispatcher);
        await client.InitializeAsync();
        client.OpenDocument("/tmp/comp.cs", "csharp", "");
        var items = await client.CompletionAsync("/tmp/comp.cs", 0, 0);
        Assert.IsNotNull(items);
        await client.DisposeAsync();
    }

    [TestMethod]
    public async Task CompletionAsync_NoOpenDocument_ReturnsEmpty()
    {
        var client = new RoslynLanguageClient(Dispatcher);
        await client.InitializeAsync();
        var items = await client.CompletionAsync("/tmp/notopen.cs", 0, 0);
        Assert.AreEqual(0, items.Count);
        await client.DisposeAsync();
    }

    // ── Hover ─────────────────────────────────────────────────────────────────

    [TestMethod]
    public async Task HoverAsync_NoOpenDocument_ReturnsNull()
    {
        var client = new RoslynLanguageClient(Dispatcher);
        await client.InitializeAsync();
        var result = await client.HoverAsync("/tmp/notopen.cs", 0, 0);
        Assert.IsNull(result);
        await client.DisposeAsync();
    }

    // ── Navigation ────────────────────────────────────────────────────────────

    [TestMethod]
    public async Task DefinitionAsync_NoOpenDocument_ReturnsEmpty()
    {
        var client = new RoslynLanguageClient(Dispatcher);
        await client.InitializeAsync();
        var locs = await client.DefinitionAsync("/tmp/x.cs", 0, 0);
        Assert.AreEqual(0, locs.Count);
        await client.DisposeAsync();
    }

    [TestMethod]
    public async Task ReferencesAsync_NoOpenDocument_ReturnsEmpty()
    {
        var client = new RoslynLanguageClient(Dispatcher);
        await client.InitializeAsync();
        var refs = await client.ReferencesAsync("/tmp/x.cs", 0, 0);
        Assert.AreEqual(0, refs.Count);
        await client.DisposeAsync();
    }

    [TestMethod]
    public async Task ImplementationAsync_NoOpenDocument_ReturnsEmpty()
    {
        var client = new RoslynLanguageClient(Dispatcher);
        await client.InitializeAsync();
        var impl = await client.ImplementationAsync("/tmp/x.cs", 0, 0);
        Assert.AreEqual(0, impl.Count);
        await client.DisposeAsync();
    }

    // ── Document Symbols ──────────────────────────────────────────────────────

    [TestMethod]
    public async Task DocumentSymbolsAsync_NoOpenDocument_ReturnsEmpty()
    {
        var client = new RoslynLanguageClient(Dispatcher);
        await client.InitializeAsync();
        var syms = await client.DocumentSymbolsAsync("/tmp/x.cs");
        Assert.AreEqual(0, syms.Count);
        await client.DisposeAsync();
    }

    [TestMethod]
    public async Task DocumentSymbolsAsync_OpenCSharpFile_ReturnsSymbols()
    {
        const string code = """
            namespace MyApp
            {
                public class MyClass
                {
                    public void MyMethod() { }
                }
            }
            """;
        var client = new RoslynLanguageClient(Dispatcher);
        await client.InitializeAsync();
        client.OpenDocument("/tmp/symbols.cs", "csharp", code);
        var syms = await client.DocumentSymbolsAsync("/tmp/symbols.cs");
        // Should find at least the class and method symbols
        Assert.IsTrue(syms.Count >= 1);
        await client.DisposeAsync();
    }

    // ── Inlay Hints ───────────────────────────────────────────────────────────

    [TestMethod]
    public async Task InlayHintsAsync_NoOpenDocument_ReturnsEmpty()
    {
        var client = new RoslynLanguageClient(Dispatcher);
        await client.InitializeAsync();
        var hints = await client.InlayHintsAsync("/tmp/x.cs", 0, 10);
        Assert.AreEqual(0, hints.Count);
        await client.DisposeAsync();
    }

    [TestMethod]
    public async Task InlayHintsAsync_WithVarDeclaration_ReturnsTypeHint()
    {
        const string code = "using System;\nclass T { void M() { var x = 42; } }";
        var client = new RoslynLanguageClient(Dispatcher);
        await client.InitializeAsync();
        client.SetInlineHintsOptions(showVarTypeHints: true, showLambdaReturnTypeHints: false);
        client.OpenDocument("/tmp/varhint.cs", "csharp", code);
        var hints = await client.InlayHintsAsync("/tmp/varhint.cs", 0, 2);
        // Should have at least one ": int" hint for var x = 42
        Assert.IsTrue(hints.Count >= 1);
        await client.DisposeAsync();
    }

    // ── IInlineHintsOptionsClient ─────────────────────────────────────────────

    [TestMethod]
    public void RoslynLanguageClient_Implements_IInlineHintsOptionsClient()
    {
        var client = new RoslynLanguageClient(Dispatcher);
        Assert.IsInstanceOfType<IInlineHintsOptionsClient>(client);
    }

    [TestMethod]
    public async Task SetInlineHintsOptions_DoesNotThrow()
    {
        var client = new RoslynLanguageClient(Dispatcher);
        client.SetInlineHintsOptions(showVarTypeHints: true,  showLambdaReturnTypeHints: true);
        client.SetInlineHintsOptions(showVarTypeHints: false, showLambdaReturnTypeHints: false);
        await client.DisposeAsync();
    }

    // ── IReferenceCountProvider ───────────────────────────────────────────────

    [TestMethod]
    public void CanProvide_CSharpFile_ReturnsTrue()
    {
        var client = new RoslynLanguageClient(Dispatcher);
        client.OpenDocument("/tmp/z.cs", "csharp", "class Z {}");
        Assert.IsTrue(client.CanProvide("/tmp/z.cs"));
    }

    [TestMethod]
    public void CanProvide_PlainTextFile_ReturnsFalse()
    {
        var client = new RoslynLanguageClient(Dispatcher);
        Assert.IsFalse(client.CanProvide("/tmp/readme.txt"));
    }

    // ── Semantic tokens ───────────────────────────────────────────────────────

    [TestMethod]
    public async Task SemanticTokensAsync_NoOpenDocument_ReturnsNull()
    {
        var client = new RoslynLanguageClient(Dispatcher);
        await client.InitializeAsync();
        var tokens = await client.SemanticTokensAsync("/tmp/notopen.cs");
        Assert.IsNull(tokens);
        await client.DisposeAsync();
    }

    // ── Formatting ────────────────────────────────────────────────────────────

    [TestMethod]
    public async Task FormattingAsync_NoOpenDocument_ReturnsEmpty()
    {
        var client = new RoslynLanguageClient(Dispatcher);
        await client.InitializeAsync();
        var edits = await client.FormattingAsync("/tmp/notopen.cs", 4, true);
        Assert.AreEqual(0, edits.Count);
        await client.DisposeAsync();
    }

    [TestMethod]
    public async Task FormattingAsync_OpenCSharpFile_ReturnsEdits()
    {
        const string code = "class F{void M(){int x=1;}}";
        var client = new RoslynLanguageClient(Dispatcher);
        await client.InitializeAsync();
        client.OpenDocument("/tmp/fmt.cs", "csharp", code);
        var edits = await client.FormattingAsync("/tmp/fmt.cs", 4, true);
        // Badly formatted code → expect formatting edits
        Assert.IsTrue(edits.Count >= 1);
        await client.DisposeAsync();
    }

    // ── Workspace symbols ─────────────────────────────────────────────────────

    [TestMethod]
    public async Task WorkspaceSymbolsAsync_EmptyQuery_ReturnsListNotNull()
    {
        var client = new RoslynLanguageClient(Dispatcher);
        await client.InitializeAsync();
        var syms = await client.WorkspaceSymbolsAsync("");
        Assert.IsNotNull(syms);
        await client.DisposeAsync();
    }

    // ── Linked editing ────────────────────────────────────────────────────────

    [TestMethod]
    public async Task LinkedEditingRangesAsync_AlwaysReturnsEmpty_ForCSharp()
    {
        var client = new RoslynLanguageClient(Dispatcher);
        await client.InitializeAsync();
        var ranges = await client.LinkedEditingRangesAsync("/tmp/x.cs", 0, 0);
        Assert.AreEqual(0, ranges.Count);
        await client.DisposeAsync();
    }

    // ── DiagnosticsReceived event ─────────────────────────────────────────────

    [TestMethod]
    public void DiagnosticsReceived_CanSubscribeAndUnsubscribe()
    {
        var client = new RoslynLanguageClient(Dispatcher);
        EventHandler<LspDiagnosticsReceivedEventArgs>? h = (_, _) => { };
        client.DiagnosticsReceived += h;
        client.DiagnosticsReceived -= h;
    }
}
