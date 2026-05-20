//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using WpfHexEditor.App.BinaryAnalysis.Panels;
using WpfHexEditor.App.BinaryAnalysis.Services;
using WpfHexEditor.App.BinaryAnalysis.ViewModels;
using WpfHexEditor.SDK.Contracts;

namespace WpfHexEditor.App.BinaryAnalysis;

/// <summary>
/// App-layer module providing 7 dockable binary analysis panels:
/// #110 String Extraction, #111 Hash Inspector, #112 File Carver,
/// #114 PE Analyzer, #118 Custom Signature DB, #119 Byte Frequency Heatmap,
/// #120 XOR/ROT Cipher Decoder.
///
/// Follows the same architecture as <c>AssemblyExplorerModule</c>:
/// panels are built lazily on first <see cref="GetPanel"/> call; no SDK plugin path.
/// </summary>
internal sealed class BinaryAnalysisModule
{
    public const string ContentIdStrings  = "panel-ba-strings";
    public const string ContentIdHash     = "panel-ba-hash";
    public const string ContentIdCarver   = "panel-ba-carver";
    public const string ContentIdSigDb    = "panel-ba-sigdb";
    public const string ContentIdFreq     = "panel-ba-freq";
    public const string ContentIdPe       = "panel-ba-pe";
    public const string ContentIdCipher   = "panel-ba-cipher";

    private IIDEHostContext? _context;
    private bool _activated;

    // Shared service (one process-wide signature store)
    private readonly UserSignatureDbStore _sigStore = new();

    // Panels — built once, reused across dock/undock
    private StringExtractionPanel? _stringsPanel;
    private HashInspectorPanel?    _hashPanel;
    private FileCarverPanel?       _carverPanel;
    private SignatureDbPanel?      _sigDbPanel;
    private ByteFrequencyPanel?    _freqPanel;
    private PeAnalyzerPanel?       _pePanel;
    private CipherDecoderPanel?    _cipherPanel;

    public Task InitializeAsync(IIDEHostContext context, CancellationToken ct = default)
    {
        _context = context;

        context.HexEditor.FileOpened          += OnFileOpened;
        context.HexEditor.ActiveEditorChanged += OnActiveEditorChanged;

        return Task.CompletedTask;
    }

    public void Shutdown()
    {
        if (_context is not null)
        {
            _context.HexEditor.FileOpened          -= OnFileOpened;
            _context.HexEditor.ActiveEditorChanged -= OnActiveEditorChanged;
            _context = null;
        }
    }

    public UIElement? GetPanel(string contentId)
    {
        EnsureActivated();
        return contentId switch
        {
            ContentIdStrings => _stringsPanel,
            ContentIdHash    => _hashPanel,
            ContentIdCarver  => _carverPanel,
            ContentIdSigDb   => _sigDbPanel,
            ContentIdFreq    => _freqPanel,
            ContentIdPe      => _pePanel,
            ContentIdCipher  => _cipherPanel,
            _                => null
        };
    }

    private void EnsureActivated()
    {
        if (_activated || _context is null) return;
        _activated = true;

        _stringsPanel = new StringExtractionPanel();
        _hashPanel    = new HashInspectorPanel();

        var carverVm = new FileCarverViewModel(_sigStore);
        _carverPanel = new FileCarverPanel(carverVm);

        var sigDbVm = new SignatureDbViewModel(_sigStore);
        _sigDbPanel = new SignatureDbPanel(sigDbVm);

        _freqPanel   = new ByteFrequencyPanel();
        _pePanel     = new PeAnalyzerPanel();
        _cipherPanel = new CipherDecoderPanel();

        // Wire context into all panels
        _stringsPanel.SetContext(_context);
        _hashPanel.SetContext(_context);
        _carverPanel.SetContext(_context);
        _sigDbPanel.SetContext(_context);
        _freqPanel.SetContext(_context);
        _pePanel.SetContext(_context);
        _cipherPanel.SetContext(_context);
    }

    private void OnFileOpened(object? sender, EventArgs e)
    {
        if (!_activated) return;
        _stringsPanel?.OnFileOpened();
        _hashPanel?.OnFileOpened();
        _carverPanel?.OnFileOpened();
        _freqPanel?.OnFileOpened();
        _pePanel?.OnFileOpened();
        _cipherPanel?.OnFileOpened();
    }

    private void OnActiveEditorChanged(object? sender, EventArgs e) => OnFileOpened(sender, e);
}
