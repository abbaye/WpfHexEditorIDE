//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

// MainWindow — Workspace System Integration
// Partial class responsible for:
//   - New / Open / Save / Save As / Close workspace commands
//   - Capturing the current IDE state into a WorkspaceCapture
//   - Restoring layout + solution + open files when a workspace is opened
//   - Updating the status-bar workspace indicator

using System.IO;
using System.Linq;
using System.Windows;
using Microsoft.Win32;
using WpfHexEditor.Core.Workspaces;
using WpfHexEditor.App.Services;
using WpfHexEditor.Docking.Core.Serialization;

namespace WpfHexEditor.App;

public partial class MainWindow
{
    // ── Workspace fields ─────────────────────────────────────────────────────

    private IWorkspaceManager?  _workspaceManager;
    private WorkspaceServiceImpl? _workspaceServiceImpl;

    private const string WorkspaceFileFilter =
        "WpfHexEditor Workspace (*.whidews)|*.whidews|All Files (*.*)|*.*";

    // ── Initialization ────────────────────────────────────────────────────────

    /// <summary>
    /// Called once from <see cref="InitializePluginSystemAsync"/> after all
    /// services are live. Creates <see cref="IWorkspaceManager"/> and the SDK
    /// adapter, then wires status-bar updates.
    /// </summary>
    private void InitializeWorkspaceSystem()
    {
        _workspaceManager    = new WorkspaceManager();
        _workspaceServiceImpl = new WorkspaceServiceImpl(_workspaceManager);

        _workspaceManager.WorkspaceOpened += (_, e) =>
            Dispatcher.InvokeAsync(() => UpdateWorkspaceStatusBar(e.Name));

        _workspaceManager.WorkspaceClosed += (_, _) =>
            Dispatcher.InvokeAsync(() => UpdateWorkspaceStatusBar(null));
    }

    // ── Command handlers ──────────────────────────────────────────────────────

    // ── XAML event wrappers ───────────────────────────────────────────────────

    private void OnNewWorkspace(object sender, RoutedEventArgs e)     => _ = OnNewWorkspaceAsync();
    private void OnOpenWorkspace(object sender, RoutedEventArgs e)    => _ = OnOpenWorkspaceAsync();
    private void OnSaveWorkspace(object sender, RoutedEventArgs e)    => _ = OnSaveWorkspaceAsync();
    private void OnSaveWorkspaceAs(object sender, RoutedEventArgs e)  => _ = OnSaveWorkspaceAsAsync();
    private void OnCloseWorkspace(object sender, RoutedEventArgs e)   => _ = OnCloseWorkspaceAsync();

    // ── Async handlers ────────────────────────────────────────────────────────

    private async Task OnNewWorkspaceAsync()
    {
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Title            = "New Workspace",
            Filter           = WorkspaceFileFilter,
            DefaultExt       = ".whidews",
            OverwritePrompt  = true
        };

        if (dlg.ShowDialog(this) != true) return;

        var name    = Path.GetFileNameWithoutExtension(dlg.FileName);
        var capture = CaptureWorkspaceState();

        try
        {
            await _workspaceManager!.NewAsync(name, dlg.FileName, capture);
            OutputLogger.PluginInfo($"[Workspace] Created '{name}' → {dlg.FileName}");
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Failed to create workspace:\n{ex.Message}",
                "New Workspace", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task OnOpenWorkspaceAsync()
    {
        var dlg = new OpenFileDialog
        {
            Title  = "Open Workspace",
            Filter = WorkspaceFileFilter
        };

        if (dlg.ShowDialog(this) != true) return;

        try
        {
            var state = await _workspaceManager!.OpenAsync(dlg.FileName);
            await ApplyWorkspaceStateAsync(state);
            OutputLogger.PluginInfo($"[Workspace] Opened '{state.Manifest.Name}' ← {dlg.FileName}");
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Failed to open workspace:\n{ex.Message}",
                "Open Workspace", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task OnSaveWorkspaceAsync()
    {
        if (_workspaceManager is null || !_workspaceManager.IsOpen)
        {
            await OnSaveWorkspaceAsAsync();
            return;
        }

        try
        {
            await _workspaceManager.SaveAsync(CaptureWorkspaceState());
            OutputLogger.PluginInfo($"[Workspace] Saved '{_workspaceManager.CurrentName}'");
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Failed to save workspace:\n{ex.Message}",
                "Save Workspace", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task OnSaveWorkspaceAsAsync()
    {
        var dlg = new SaveFileDialog
        {
            Title           = "Save Workspace As",
            Filter          = WorkspaceFileFilter,
            DefaultExt      = ".whidews",
            OverwritePrompt = true,
            FileName        = _workspaceManager?.CurrentName ?? "workspace"
        };

        if (dlg.ShowDialog(this) != true) return;

        try
        {
            await _workspaceManager!.SaveAsAsync(dlg.FileName, CaptureWorkspaceState());
            OutputLogger.PluginInfo($"[Workspace] Saved as → {dlg.FileName}");
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Failed to save workspace:\n{ex.Message}",
                "Save Workspace As", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task OnCloseWorkspaceAsync()
    {
        if (_workspaceManager is null || !_workspaceManager.IsOpen) return;

        var prefs = WpfHexEditor.Core.Options.AppSettingsService.Instance.Current.Workspace;
        if (prefs.PromptSaveOnClose)
        {
            var result = MessageBox.Show(this,
                $"Save workspace '{_workspaceManager.CurrentName}' before closing?",
                "Close Workspace",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Cancel) return;
            if (result == MessageBoxResult.Yes)
                await OnSaveWorkspaceAsync();
        }

        await _workspaceManager.CloseAsync();
        OutputLogger.PluginInfo("[Workspace] Closed.");
    }

    // ── State capture ──────────────────────────────────────────────────────────

    /// <summary>
    /// Snapshots the current IDE state (layout, solution path, open file paths,
    /// active theme) into a <see cref="WorkspaceCapture"/> record.
    /// </summary>
    private WorkspaceCapture CaptureWorkspaceState()
    {
        SnapshotEditorConfigs();

        var layoutJson  = DockLayoutSerializer.Serialize(_layout);
        var solutionPath = _solutionManager.CurrentSolution?.FilePath;
        var openFiles   = _documentManager.OpenDocuments
                            .Select(d => d.FilePath)
                            .Where(p => p is not null)
                            .Cast<string>()
                            .ToList();
        var themeName   = WpfHexEditor.Core.Options.AppSettingsService.Instance.Current.ActiveThemeName;

        return new WorkspaceCapture(layoutJson, solutionPath, openFiles, themeName);
    }

    // ── State restore ──────────────────────────────────────────────────────────

    /// <summary>
    /// Applies a loaded <see cref="WorkspaceState"/> to the IDE:
    /// restores theme, layout, solution, and open files.
    /// </summary>
    private async Task ApplyWorkspaceStateAsync(WorkspaceState state)
    {
        var prefs = WpfHexEditor.Core.Options.AppSettingsService.Instance.Current.Workspace;

        // 1. Theme
        if (prefs.RestoreThemeOnOpen && !string.IsNullOrWhiteSpace(state.Settings?.ThemeName))
            ApplyTheme($"{state.Settings.ThemeName}.xaml", state.Settings.ThemeName);

        // 2. Layout
        if (!string.IsNullOrWhiteSpace(state.Layout))
        {
            try
            {
                var layoutRoot = DockLayoutSerializer.Deserialize(state.Layout);
                PruneStaleDocumentItems(layoutRoot);
                ApplyLayout(layoutRoot);
            }
            catch (Exception ex)
            {
                OutputLogger.PluginError($"[Workspace] Layout restore failed: {ex.Message}");
            }
        }

        // 3. Solution
        if (prefs.RestoreSolutionOnOpen && !string.IsNullOrWhiteSpace(state.Solution?.SolutionPath))
        {
            try
            {
                await _solutionManager.OpenSolutionAsync(state.Solution.SolutionPath);
            }
            catch (Exception ex)
            {
                OutputLogger.PluginError($"[Workspace] Solution restore failed: {ex.Message}");
            }
        }

        // 4. Open files (skip already open ones)
        if (prefs.RestoreOpenFilesOnOpen && state.Files is { Count: > 0 })
        {
            var alreadyOpen = new HashSet<string>(
                _documentManager.OpenDocuments
                    .Select(d => d.FilePath ?? string.Empty),
                StringComparer.OrdinalIgnoreCase);

            foreach (var entry in state.Files)
            {
                if (string.IsNullOrWhiteSpace(entry.Path)) continue;
                if (alreadyOpen.Contains(entry.Path))      continue;
                if (!File.Exists(entry.Path))              continue;

                OpenStandaloneFileWithEditor(entry.Path, entry.EditorId);
            }
        }
    }

    // ── Status bar ─────────────────────────────────────────────────────────────

    private void UpdateWorkspaceStatusBar(string? workspaceName)
    {
        if (WorkspaceStatusItem is null) return;

        if (string.IsNullOrWhiteSpace(workspaceName))
        {
            WorkspaceStatusItem.Visibility = Visibility.Collapsed;
        }
        else
        {
            WorkspaceStatusText.Text       = workspaceName;
            WorkspaceStatusItem.Visibility = Visibility.Visible;
        }
    }
}
