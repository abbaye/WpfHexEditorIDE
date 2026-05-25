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
using WpfHexEditor.Core.Options;
using WpfHexEditor.SDK.Contracts;

namespace WpfHexEditor.App.BinaryAnalysis;

/// <summary>
/// App-layer module providing 8 dockable binary analysis panels:
/// #110 String Extraction, #111 Hash Inspector, #112 File Carver,
/// #114 PE Analyzer, #118 Custom Signature DB, #119 Byte Frequency Heatmap,
/// #119b Byte Bigram Heatmap, #120 XOR/ROT Cipher Decoder.
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
    public const string ContentIdBigram   = "panel-ba-bigram";

    private IIDEHostContext?       _context;
    private HexEditorDefaultSettings? _settings;
    private bool _activated;

    // Shared service (one process-wide signature store)
    private readonly UserSignatureDbStore _sigStore = new();

    // Entropy heatmap service — lifetime tied to this module
    private EntropyHeatmapService? _entropyService;

    // Panels — built once, reused across dock/undock
    private StringExtractionPanel? _stringsPanel;
    private HashInspectorPanel?    _hashPanel;
    private FileCarverPanel?       _carverPanel;
    private SignatureDbPanel?      _sigDbPanel;
    private ByteFrequencyPanel?    _freqPanel;
    private PeAnalyzerPanel?       _pePanel;
    private CipherDecoderPanel?      _cipherPanel;
    private ByteBigramHeatmapPanel?  _bigramPanel;

    public Task InitializeAsync(IIDEHostContext context, HexEditorDefaultSettings settings, CancellationToken ct = default)
    {
        _context  = context;
        _settings = settings;

        context.HexEditor.FileOpened          += OnFileOpened;
        context.HexEditor.ActiveEditorChanged += OnActiveEditorChanged;

        _entropyService = new EntropyHeatmapService(context.HexEditor, settings);
        if (settings.ShowEntropyHeatmap)
            _entropyService.Enable();

        // Eagerly build all panels so that when RefreshModulePanels / RebuildVisualTree
        // fires (immediately after this returns), GetPanel() can return the real panel
        // instead of deferring to a transparent placeholder that never gets replaced on
        // inactive tabs.
        EnsureActivated();

        return Task.CompletedTask;
    }

    /// <summary>Called by the host when the user toggles via context menu or options.</summary>
    public void SetEntropyHeatmapEnabled(bool enabled)
    {
        if (_settings is not null) _settings.ShowEntropyHeatmap = enabled;
        if (enabled) _entropyService?.Enable();
        else         _entropyService?.Disable();
    }

    public bool IsEntropyHeatmapEnabled => _entropyService?.IsEnabled ?? false;

    public void Shutdown()
    {
        if (_context is not null)
        {
            _context.HexEditor.FileOpened          -= OnFileOpened;
            _context.HexEditor.ActiveEditorChanged -= OnActiveEditorChanged;
            _context = null;
        }
        _entropyService?.Dispose();
        _entropyService = null;
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
            ContentIdBigram  => _bigramPanel,
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
        _bigramPanel = new ByteBigramHeatmapPanel();

        // Wire context into all panels
        _stringsPanel.SetContext(_context);
        _hashPanel.SetContext(_context);
        _carverPanel.SetContext(_context);
        _sigDbPanel.SetContext(_context);
        _freqPanel.SetContext(_context);
        _pePanel.SetContext(_context);
        _cipherPanel.SetContext(_context);
        _bigramPanel.SetContext(_context);
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
        _bigramPanel?.OnFileOpened();
    }

    private void OnActiveEditorChanged(object? sender, EventArgs e) => OnFileOpened(sender, e);
}
