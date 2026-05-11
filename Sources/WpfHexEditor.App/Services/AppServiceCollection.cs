// ==========================================================
// Project: WpfHexEditor.App
// File: Services/AppServiceCollection.cs
// Description:
//     DI composition root for the WpfHexEditor.App shell.
//     Registers all service adapters as singletons so that future
//     consumers (e.g. plugins, view-models) can resolve them via
//     IServiceProvider without coupling to concrete types.
//
// Architecture Notes:
//     MainWindowServiceArgs carries the concrete instances that MainWindow
//     already built (they require WPF elements as constructor args and
//     cannot be constructed purely by the container).
//     AddAppServices registers each instance directly, then AddSingleton
//     factory delegates expose the two static singletons (SolutionManager,
//     AppSettingsService).  IDEHostContext constructor is unchanged.
// ==========================================================

using Microsoft.Extensions.DependencyInjection;
using WpfHexEditor.Core.Commands;
using WpfHexEditor.Core.Contracts;
using WpfHexEditor.Core.Definitions;
using WpfHexEditor.Core.Options;
using WpfHexEditor.Core.ProjectSystem.Languages;
using WpfHexEditor.Editor.Core.Dialogs;
using WpfHexEditor.PluginHost.Adapters;
using WpfHexEditor.SDK.Contracts;
using WpfHexEditor.SDK.Contracts.Services;

namespace WpfHexEditor.App.Services;

/// <summary>
/// Carries the already-constructed service adapter instances from <c>MainWindow</c>.
/// All fields are required — the container registers them as-is.
/// </summary>
internal sealed record MainWindowServiceArgs(
    IHexEditorService    HexEditorService,
    IOutputService       OutputService,
    IErrorPanelService   ErrorPanelService,
    IThemeService        ThemeService,
    ITerminalService     TerminalService,
    IDockingAdapter      DockingAdapter,
    IMenuAdapter         MenuAdapter,
    IStatusBarAdapter    StatusBarAdapter,
    IDocumentHostService DocumentHostService,
    ICommandRegistry?    CommandRegistry = null);

/// <summary>
/// Extension method that registers all app-level services as singletons.
/// </summary>
internal static class AppServiceCollection
{
    internal static IServiceCollection AddAppServices(
        this IServiceCollection services,
        MainWindowServiceArgs   args)
    {
        // Adapter singletons — instances are pre-built; register directly.
        services.AddSingleton(args.HexEditorService);
        services.AddSingleton(args.OutputService);
        services.AddSingleton(args.ErrorPanelService);
        services.AddSingleton(args.ThemeService);
        services.AddSingleton(args.TerminalService);
        services.AddSingleton(args.DockingAdapter);
        services.AddSingleton(args.MenuAdapter);
        services.AddSingleton(args.StatusBarAdapter);
        services.AddSingleton(args.DocumentHostService);

        // Stateless services that only need AppSettingsService.Instance.
        services.AddSingleton<EditorSettingsService>();

        // Themed dialog service — replaces System.Windows.MessageBox.
        services.AddSingleton<IDialogService, DialogServiceImpl>();

        services.AddSingleton<IEmbeddedFormatCatalog>(EmbeddedFormatCatalog.Instance);
        services.AddSingleton(LanguageRegistry.Instance);

        // ── IDE foundation (#36/#37/#39) ──────────────────────────────────────
        // CommandRegistry is owned by MainWindow (see MainWindow.Commands.cs).
        // Register the live instance so ICommandBus and other consumers
        // resolve the same registry the menus / toolbar were built against.
        if (args.CommandRegistry is not null)
            services.AddSingleton(args.CommandRegistry);
        else
            services.AddSingleton<ICommandRegistry, CommandRegistry>();

        services.AddSingleton<ICommandBus, CommandBus>();
        services.AddSingleton<IServiceContainer>(sp => new ServiceContainerAdapter(sp));
        services.AddSingleton(PluginSettingsRegistry.Instance);
        services.AddSingleton<PreferencesExportService>();

        return services;
    }
}
