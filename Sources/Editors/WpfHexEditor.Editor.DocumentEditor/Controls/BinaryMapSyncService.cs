// ==========================================================
// Project: WpfHexEditor.Editor.DocumentEditor
// File: Controls/BinaryMapSyncService.cs
// Description:
//     Bidirectional selection bridge between DocumentTextPane and
//     DocumentHexPane. Uses a 150ms DispatcherTimer to throttle
//     rapid selection changes and prevent feedback loops.
// ==========================================================

using System.Windows.Threading;
using WpfHexEditor.Editor.DocumentEditor.Core.Model;

namespace WpfHexEditor.Editor.DocumentEditor.Controls;

/// <summary>
/// Wires <see cref="DocumentTextPane"/> ↔ <see cref="DocumentHexPane"/>
/// so that selecting a block in either view highlights it in the other.
/// </summary>
internal sealed class BinaryMapSyncService : IDisposable
{
    private readonly DocumentModel _model;
    private DocumentTextPane?      _textPane;
    private DocumentHexPane?       _hexPane;
    private volatile bool          _isSyncing;
    private readonly DispatcherTimer _throttle;

    private long           _pendingHexOffset   = -1;
    private DocumentBlock? _pendingTextBlock;

    public BinaryMapSyncService(DocumentModel model)
    {
        _model    = model;
        _throttle = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(150)
        };
        _throttle.Tick += OnThrottleTick;
    }

    // ── Wiring ───────────────────────────────────────────────────────────────

    public void Wire(DocumentTextPane textPane, DocumentHexPane hexPane)
    {
        _textPane = textPane;
        _hexPane  = hexPane;

        textPane.SelectedBlockChanged += OnTextBlockSelected;
        hexPane.HexOffsetSelected     += OnHexOffsetSelected;
    }

    // ── Event handlers ───────────────────────────────────────────────────────

    private void OnTextBlockSelected(object? sender, DocumentBlock? block)
    {
        if (_isSyncing || block is null) return;
        _pendingTextBlock = block;
        _pendingHexOffset = -1;
        Restart();
    }

    private void OnHexOffsetSelected(object? sender, long offset)
    {
        if (_isSyncing) return;
        _pendingHexOffset   = offset;
        _pendingTextBlock   = null;
        Restart();
    }

    private void OnThrottleTick(object? sender, EventArgs e)
    {
        _throttle.Stop();
        _isSyncing = true;

        try
        {
            if (_pendingTextBlock is not null)
            {
                _hexPane?.ScrollToBlock(_pendingTextBlock);
                _pendingTextBlock = null;
            }
            else if (_pendingHexOffset >= 0)
            {
                _textPane?.ScrollToOffset(_pendingHexOffset);
                _pendingHexOffset = -1;
            }
        }
        finally
        {
            _isSyncing = false;
        }
    }

    private void Restart()
    {
        _throttle.Stop();
        _throttle.Start();
    }

    // ── IDisposable ──────────────────────────────────────────────────────────

    public void Dispose()
    {
        _throttle.Stop();
        _throttle.Tick -= OnThrottleTick;

        if (_textPane is not null)
            _textPane.SelectedBlockChanged -= OnTextBlockSelected;
        if (_hexPane is not null)
            _hexPane.HexOffsetSelected -= OnHexOffsetSelected;
    }
}
