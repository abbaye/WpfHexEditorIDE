// ==========================================================
// Project: WpfHexEditor.App
// File: MainWindow.Git.cs
// Description:
//     Git / VCS status bar wiring for MainWindow.
//     Subscribes to GitStatusChangedEvent, GitBlameLoadedEvent,
//     GitOperationStartedEvent, GitOperationCompletedEvent.
//     Branch button click → BranchPickerPopup.
// Architecture Notes:
//     Pattern: identical to MainWindow.Debug.cs — subscribe on bus,
//     update XAML controls on Dispatcher.
// ==========================================================

using WpfHexEditor.Core.Events.IDEEvents;
using WpfHexEditor.Editor.CodeEditor.Controls;
using WpfHexEditor.Editor.Core.LSP;

namespace WpfHexEditor.App;

public partial class MainWindow
{
    private IDisposable[]? _gitSubs;

    // ── Initialization ────────────────────────────────────────────────────────

    private void InitializeGitIntegration()
    {
        if (_ideEventBus is null) return;

        _gitSubs =
        [
            _ideEventBus.Subscribe<GitStatusChangedEvent>(e =>
            {
                Dispatcher.InvokeAsync(() => UpdateGitStatusBar(e));
                return System.Threading.Tasks.Task.CompletedTask;
            }),

            _ideEventBus.Subscribe<GitBlameLoadedEvent>(e =>
            {
                Dispatcher.InvokeAsync(() => OnGitBlameLoaded(e));
                return System.Threading.Tasks.Task.CompletedTask;
            }),

            _ideEventBus.Subscribe<GitBlameToggleRequestedEvent>(_ =>
            {
                Dispatcher.InvokeAsync(() =>
                {
                    var editor = GetActiveCodeEditor();
                    if (editor is not null)
                        editor.ShowBlameGutter = !editor.ShowBlameGutter;
                });
                return System.Threading.Tasks.Task.CompletedTask;
            }),

            _ideEventBus.Subscribe<GitOperationStartedEvent>(e =>
            {
                Dispatcher.InvokeAsync(() =>
                {
                    GitOperationStatusText.Text       = $"Git: {e.OperationName}…";
                    GitOperationStatusText.Visibility = System.Windows.Visibility.Visible;
                });
                return System.Threading.Tasks.Task.CompletedTask;
            }),

            _ideEventBus.Subscribe<GitOperationCompletedEvent>(e =>
            {
                Dispatcher.InvokeAsync(() =>
                {
                    if (e.Success)
                    {
                        GitOperationStatusText.Visibility = System.Windows.Visibility.Collapsed;
                    }
                    else
                    {
                        GitOperationStatusText.Text       = $"Git {e.OperationName} failed";
                        GitOperationStatusText.Visibility = System.Windows.Visibility.Visible;
                    }
                });
                return System.Threading.Tasks.Task.CompletedTask;
            }),
        ];
    }

    /// <summary>Disposes Git event bus subscriptions. Called from ShutdownPluginSystemAsync.</summary>
    private void ShutdownGitIntegration()
    {
        if (_gitSubs is not null)
            foreach (var s in _gitSubs) s.Dispose();
        _gitSubs = null;
    }

    // ── Handlers ─────────────────────────────────────────────────────────────

    private void UpdateGitStatusBar(GitStatusChangedEvent e)
    {
        if (e.Branch is null)
        {
            GitStatusItem.Visibility = System.Windows.Visibility.Collapsed;
            return;
        }

        GitBranchText.Text      = e.Branch;
        GitDirtyDot.Visibility  = e.IsDirty
            ? System.Windows.Visibility.Visible
            : System.Windows.Visibility.Collapsed;
        GitStatusItem.Visibility = System.Windows.Visibility.Visible;
    }

    private async void OnGitBlameLoaded(GitBlameLoadedEvent e)
    {
        // Get the active CodeEditor — only wire blame if it's showing the right file
        var activeEditor = GetActiveCodeEditor();
        if (activeEditor is null) return;

        // Retrieve blame entries from VCS cache
        var vcs = _ideHostContext?.ExtensionRegistry
            .GetExtensions<IVersionControlService>().FirstOrDefault();
        if (vcs is null) return;

        try
        {
            var entries = await vcs.GetBlameAsync(e.FilePath);
            activeEditor.SetBlame(entries);
        }
        catch { /* blame is cosmetic — swallow */ }
    }

    // Returns the active CodeEditor if any document tab hosts one
    private CodeEditor? GetActiveCodeEditor()
    {
        if (_documentManager?.ActiveDocument?.AssociatedEditor
                is WpfHexEditor.Editor.CodeEditor.Controls.CodeEditorSplitHost host)
            return host.PrimaryEditor;
        return null;
    }

    private void OnGitBranchButtonClick(object sender, System.Windows.RoutedEventArgs e)
    {
        // Publish event — GitPlugin subscribes and shows BranchPickerPopup
        // (fully decoupled: App cannot reference Git plugin types directly)
        _ideEventBus?.Publish(new GitBranchClickRequestedEvent(GitBranchButton));
    }
}
