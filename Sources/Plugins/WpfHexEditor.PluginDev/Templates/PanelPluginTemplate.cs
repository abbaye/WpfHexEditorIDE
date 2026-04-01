// ==========================================================
// Project: WpfHexEditor.PluginDev
// File: Templates/PanelPluginTemplate.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-23
// Description:
//     Scaffolds a Panel-style plugin project:
//       - MyPanel UserControl (code-behind only, WPF)
//       - MyPanelViewModel (INotifyPropertyChanged)
//       - MyPanelPlugin (entry point, registers the panel)
//
// Architecture Notes:
//     Pattern: Template Method — fills variable slots in embedded template strings.
//     Each template is self-contained; no external dependencies on TemplateManager.
// ==========================================================

using System.IO;

namespace WpfHexEditor.PluginDev.Templates;

/// <summary>
/// Scaffolds a plugin that contributes a dockable IDE panel.
/// </summary>
public sealed class PanelPluginTemplate : IPluginTemplate
{
    // -----------------------------------------------------------------------
    // IPluginTemplate
    // -----------------------------------------------------------------------

    public string TemplateId    => "panel";
    public string DisplayName   => "Panel Plugin";
    public string Description   => "A dockable IDE panel with its own ViewModel and plugin entry point.";
    public string Icon          => "\uE8A5";

    /// <summary>
    /// Creates all three files inside <paramref name="outputDir"/>:
    /// MyPanel.cs, MyPanelViewModel.cs, MyPanelPlugin.cs.
    /// </summary>
    public async Task ScaffoldAsync(
        string            outputDir,
        string            pluginName,
        string            authorName,
        CancellationToken ct = default)
    {
        var safeName  = PluginTemplateHelpers.MakeSafeName(pluginName);
        var ns        = $"WpfHexEditor.Plugins.{safeName}";
        var date      = DateTime.Now.ToString("yyyy-MM-dd");
        var pluginId  = $"WpfHexEditor.Plugins.{safeName}";

        Directory.CreateDirectory(outputDir);

        await File.WriteAllTextAsync(
            Path.Combine(outputDir, $"{safeName}Panel.cs"),
            BuildPanel(ns, safeName, authorName, date), ct);

        await File.WriteAllTextAsync(
            Path.Combine(outputDir, $"{safeName}PanelViewModel.cs"),
            BuildViewModel(ns, safeName, authorName, date), ct);

        await File.WriteAllTextAsync(
            Path.Combine(outputDir, $"{safeName}Plugin.cs"),
            BuildPlugin(ns, safeName, pluginId, authorName, date), ct);
    }

    // -----------------------------------------------------------------------
    // Template bodies
    // -----------------------------------------------------------------------

    private static string BuildPanel(string ns, string name, string author, string date) => $$"""
// ==========================================================
// Plugin:  {{name}}
// File:    {{name}}Panel.cs
// Author:  {{author}}
// Created: {{date}}
// Description: Dockable panel UserControl (code-behind only).
// ==========================================================

using System.Windows;
using System.Windows.Controls;

namespace {{ns}};

/// <summary>
/// Dockable panel for the {{name}} plugin.
/// Created purely in code — no XAML required.
/// </summary>
public sealed class {{name}}Panel : UserControl
{
    private readonly {{name}}PanelViewModel _vm;

    public {{name}}Panel({{name}}PanelViewModel vm)
    {
        _vm = vm ?? throw new ArgumentNullException(nameof(vm));
        DataContext = _vm;
        BuildUI();
    }

    private void BuildUI()
    {
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        // Toolbar
        var toolbar = new ToolBar();
        var refreshBtn = new Button { Content = "\u27F3  Refresh", Padding = new Thickness(6, 2, 6, 2) };
        refreshBtn.Click += (_, _) => _vm.Refresh();
        toolbar.Items.Add(refreshBtn);
        Grid.SetRow(toolbar, 0);
        root.Children.Add(toolbar);

        // Content
        var content = new TextBlock
        {
            Text       = "{{name}} Panel — replace this with your content.",
            Margin     = new Thickness(8),
            TextWrapping = System.Windows.TextWrapping.Wrap,
        };
        Grid.SetRow(content, 1);
        root.Children.Add(content);

        Content = root;
    }
}
""";

    private static string BuildViewModel(string ns, string name, string author, string date) => $$"""
// ==========================================================
// Plugin:  {{name}}
// File:    {{name}}PanelViewModel.cs
// Author:  {{author}}
// Created: {{date}}
// Description: ViewModel for {{name}}Panel.
// ==========================================================

using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace {{ns}};

/// <summary>
/// ViewModel backing <see cref="{{name}}Panel"/>.
/// </summary>
public sealed class {{name}}PanelViewModel : INotifyPropertyChanged
{
    private string _statusText = "Ready";

    public string StatusText
    {
        get => _statusText;
        set { _statusText = value; OnPropertyChanged(); }
    }

    public void Refresh()
    {
        StatusText = $"Refreshed at {DateTime.Now:HH:mm:ss}";
        // TODO: implement refresh logic.
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
""";

    private static string BuildPlugin(string ns, string name, string pluginId, string author, string date)
    {
        var contentId = $"panel-{name.ToLowerInvariant()}";
        return $$"""
// ==========================================================
// Plugin:  {{name}}
// File:    {{name}}Plugin.cs
// Author:  {{author}}
// Created: {{date}}
// Description: Plugin entry point — registers the {{name}} panel.
// ==========================================================

using WpfHexEditor.SDK.Contracts;
using WpfHexEditor.SDK.Descriptors;
using WpfHexEditor.SDK.Models;

namespace {{ns}};

/// <summary>
/// Entry point for the {{name}} panel plugin.
/// Registered by PluginHost via plugin.manifest.json.
/// </summary>
public sealed class {{name}}Plugin : IWpfHexEditorPlugin
{
    private const string PanelContentId = "{{contentId}}";

    public string  Id      => "{{pluginId}}";
    public string  Name    => "{{name}}";
    public Version Version => new(1, 0, 0);

    public PluginCapabilities Capabilities => new()
    {
        WriteOutput = true,
    };

    public Task InitializeAsync(IIDEHostContext context, CancellationToken ct = default)
    {
        var vm    = new {{name}}PanelViewModel();
        var panel = new {{name}}Panel(vm);

        context.UIRegistry.RegisterPanel(new PanelDescriptor
        {
            ContentId   = PanelContentId,
            Title       = "{{name}}",
            ContentFactory = () => panel,
            DefaultDock = DockHint.Bottom,
        });

        context.Output.WriteLine(Id, $"{{name}} panel registered.");
        return Task.CompletedTask;
    }

    public Task ShutdownAsync(CancellationToken ct = default) => Task.CompletedTask;
}
""";
    }
}
