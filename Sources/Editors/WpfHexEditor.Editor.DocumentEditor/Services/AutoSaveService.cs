// ==========================================================
// Project: WpfHexEditor.Editor.DocumentEditor
// File: Services/AutoSaveService.cs
// Description:
//     Periodic auto-save to %TEMP%\WpfHexEditor\autosave\.
//     Fires every N seconds (from DocumentEditorOptions.AutoSaveIntervalSeconds).
//     Only saves when the document is dirty. Does NOT overwrite the original file.
//     Recovery: if an autosave file is found on next open, the host prompts the user.
// Architecture:
//     DispatcherTimer owned by DocumentEditorHost. Dispose stops the timer.
// ==========================================================

using System.IO;
using System.Windows.Threading;
using WpfHexEditor.Editor.DocumentEditor.Core;
using WpfHexEditor.Editor.DocumentEditor.Core.Model;

namespace WpfHexEditor.Editor.DocumentEditor.Services;

/// <summary>
/// Writes a temporary recovery copy of the document on a timer.
/// </summary>
internal sealed class AutoSaveService : IDisposable
{
    private readonly DispatcherTimer _timer;
    private readonly Func<DocumentModel?>  _getModel;
    private readonly Func<IDocumentSaver?> _getSaver;

    private bool _disposed;

    public AutoSaveService(
        Func<DocumentModel?>  getModel,
        Func<IDocumentSaver?> getSaver,
        int intervalSeconds)
    {
        _getModel = getModel;
        _getSaver = getSaver;

        _timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(Math.Max(30, intervalSeconds))
        };
        _timer.Tick += OnTick;
    }

    public void Start() => _timer.Start();
    public void Stop()  => _timer.Stop();

    public void UpdateInterval(int intervalSeconds)
    {
        _timer.Interval = TimeSpan.FromSeconds(Math.Max(30, intervalSeconds));
    }

    private async void OnTick(object? sender, EventArgs e)
    {
        var model = _getModel();
        if (model is null || !model.IsDirty || string.IsNullOrEmpty(model.FilePath)) return;

        var saver = _getSaver();
        if (saver is null || !saver.CanSave(model.FilePath)) return;

        var dir  = Path.Combine(Path.GetTempPath(), "WpfHexEditor", "autosave");
        Directory.CreateDirectory(dir);

        var fileName   = Path.GetFileName(model.FilePath);
        var autoSavePath = Path.Combine(dir, fileName + ".autosave");

        try
        {
            await using var fs = File.Create(autoSavePath);
            await saver.SaveAsync(model, fs);
        }
        catch
        {
            // Silent: autosave is best-effort; failures must not disrupt the user.
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _timer.Stop();
        _timer.Tick -= OnTick;
    }
}
