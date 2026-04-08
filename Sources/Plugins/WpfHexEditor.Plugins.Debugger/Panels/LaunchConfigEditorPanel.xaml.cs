// ==========================================================
// Project: WpfHexEditor.Plugins.Debugger
// File: Panels/LaunchConfigEditorPanel.xaml.cs
// Description:
//     Panel for managing .whdbg debug launch configuration files.
//     Provides file picker, configuration selector, "Edit in IDE" button
//     (opens the .whdbg in the main CodeEditor for JSON editing with schema
//     validation), and a Launch button that drives IDebuggerService.LaunchAsync.
//
// Architecture Notes:
//     Does NOT embed a second CodeEditor — opens the file via IDocumentHostService
//     so the existing CodeEditor with JSON + whdbg schema validation is reused.
//     All state: _currentFilePath + _currentJson parsed lazily on Launch.
// ==========================================================

using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using WpfHexEditor.SDK.Contracts.Services;

namespace WpfHexEditor.Plugins.Debugger.Panels;

/// <summary>
/// Panel for selecting and launching .whdbg debug configurations.
/// </summary>
public sealed partial class LaunchConfigEditorPanel : UserControl
{
    private readonly IDocumentHostService _documentHost;
    private readonly IDebuggerService?    _debugger;
    private string?                        _currentFilePath;
    private string?                        _currentJson;

    public LaunchConfigEditorPanel(IDocumentHostService documentHost, IDebuggerService? debugger)
    {
        InitializeComponent();
        _documentHost = documentHost;
        _debugger     = debugger;
    }

    // ── Toolbar handlers ──────────────────────────────────────────────────────

    private void OnOpen(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title  = "Open Debug Launch Configuration",
            Filter = "Debug Launch Config (*.whdbg)|*.whdbg|JSON files (*.json)|*.json|All files (*.*)|*.*",
        };

        if (dlg.ShowDialog() != true) return;
        LoadFile(dlg.FileName);
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        if (_currentFilePath is null || _currentJson is null) return;

        try
        {
            File.WriteAllText(_currentFilePath, _currentJson);
            StatusText.Text = $"Saved: {System.IO.Path.GetFileName(_currentFilePath)}";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Save failed: {ex.Message}";
        }
    }

    private void OnEditInIde(object sender, RoutedEventArgs e)
    {
        if (_currentFilePath is null)
        {
            StatusText.Text = "No file loaded. Click Open first.";
            return;
        }

        _documentHost.OpenDocument(_currentFilePath);
    }

    private void OnLaunch(object sender, RoutedEventArgs e)
    {
        if (_debugger is null)
        {
            StatusText.Text = "IDebuggerService not available.";
            return;
        }

        if (_currentJson is null)
        {
            StatusText.Text = "No configuration loaded.";
            return;
        }

        try
        {
            using var doc = JsonDocument.Parse(_currentJson);
            if (!doc.RootElement.TryGetProperty("configurations", out var configs))
            {
                StatusText.Text = "No 'configurations' array found in .whdbg file.";
                return;
            }

            var selectedName = (CboConfigurations.SelectedItem as ConfigEntry)?.Name;
            JsonElement? target = null;

            foreach (var cfg in configs.EnumerateArray())
            {
                if (selectedName is null)
                {
                    target = cfg;
                    break;
                }
                if (cfg.TryGetProperty("name", out var n) && n.GetString() == selectedName)
                {
                    target = cfg;
                    break;
                }
            }

            if (target is null)
            {
                StatusText.Text = "No matching configuration found.";
                return;
            }

            var launchConfig = BuildLaunchConfig(target.Value);
            _ = _debugger.LaunchAsync(launchConfig);
            StatusText.Text = $"Launching [{launchConfig.LanguageId}]: {System.IO.Path.GetFileName(launchConfig.ProgramPath)}";
        }
        catch (JsonException ex)
        {
            StatusText.Text = $"JSON parse error: {ex.Message}";
        }
    }

    private void OnConfigurationSelected(object sender, SelectionChangedEventArgs e)
    {
        // No additional action required — selection is read during Launch
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void LoadFile(string path)
    {
        _currentFilePath = path;
        _currentJson     = File.ReadAllText(path);
        RefreshConfigurationCombo(_currentJson);
        StatusText.Text = System.IO.Path.GetFileName(path);
    }

    private void RefreshConfigurationCombo(string json)
    {
        CboConfigurations.Items.Clear();
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("configurations", out var configs)) return;

            foreach (var cfg in configs.EnumerateArray())
            {
                string name = cfg.TryGetProperty("name", out var n) ? n.GetString() ?? "(unnamed)" : "(unnamed)";
                CboConfigurations.Items.Add(new ConfigEntry(name));
            }

            if (CboConfigurations.Items.Count > 0)
                CboConfigurations.SelectedIndex = 0;
        }
        catch { /* malformed JSON — ignore */ }
    }

    private static WpfHexEditor.Core.Debugger.Models.DebugLaunchConfig BuildLaunchConfig(JsonElement cfg)
    {
        string Get(string key, string def = "") =>
            cfg.TryGetProperty(key, out var v) ? v.GetString() ?? def : def;

        string[] GetArgs() =>
            cfg.TryGetProperty("args", out var arr)
                ? [.. arr.EnumerateArray().Select(a => a.GetString() ?? string.Empty)]
                : [];

        var env = new Dictionary<string, string>();
        if (cfg.TryGetProperty("env", out var envEl))
            foreach (var prop in envEl.EnumerateObject())
                env[prop.Name] = prop.Value.GetString() ?? string.Empty;

        return new()
        {
            LanguageId   = Get("type", "csharp"),
            Request      = Get("request", "launch"),
            ProgramPath  = Get("program"),
            ProjectPath  = Get("projectPath"),
            Args         = GetArgs(),
            WorkDir      = cfg.TryGetProperty("cwd", out var cwd) ? cwd.GetString() : null,
            Env          = env,
            StopAtEntry  = cfg.TryGetProperty("stopAtEntry", out var sae) && sae.GetBoolean(),
            ProcessId    = cfg.TryGetProperty("processId", out var pid) ? pid.GetInt32() : null,
            JustMyCode   = !cfg.TryGetProperty("justMyCode", out var jmc) || jmc.GetBoolean(),
            Console      = Get("console", "internalConsole"),
        };
    }

    // ── Inner types ───────────────────────────────────────────────────────────

    private sealed record ConfigEntry(string Name)
    {
        public override string ToString() => Name;
    }
}
