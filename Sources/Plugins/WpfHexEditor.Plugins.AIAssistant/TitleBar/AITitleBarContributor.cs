// ==========================================================
// Project: WpfHexEditor.Plugins.AIAssistant
// File: AITitleBarContributor.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.6
// Created: 2026-03-31
// License: GNU Affero General Public License v3.0 (AGPL-3.0)
// Description:
//     ITitleBarContributor implementation. Left-click opens command palette,
//     right-click shows context menu with quick actions.
// ==========================================================
using System.Windows;
using WpfHexEditor.Plugins.AIAssistant.Connection;
using WpfHexEditor.SDK.Contracts;

namespace WpfHexEditor.Plugins.AIAssistant.TitleBar;

public sealed class AITitleBarContributor : ITitleBarContributor
{
    private readonly AIConnectionService _connectionService;
    private readonly Action<UIElement?> _showCommandPalette;
    private readonly Action _newTab;
    private readonly Action _fixErrors;
    private readonly Action _openOptions;
    private readonly Action _manageConnections;
    private readonly Action _accountUsage;
    private AITitleBarButton? _button;

    public string ContributorId => "AIAssistant.TitleBar";
    public int Order => 10;

    public AITitleBarContributor(
        AIConnectionService connectionService,
        Action<UIElement?> showCommandPalette,
        Action newTab,
        Action fixErrors,
        Action openOptions,
        Action manageConnections,
        Action accountUsage)
    {
        _connectionService = connectionService;
        _showCommandPalette = showCommandPalette;
        _newTab = newTab;
        _fixErrors = fixErrors;
        _openOptions = openOptions;
        _manageConnections = manageConnections;
        _accountUsage = accountUsage;
        _connectionService.StatusChanged += OnStatusChanged;
    }

    public UIElement CreateButton()
    {
        _button = new AITitleBarButton();
        _button.ShowCommandPaletteRequested += () => SafeGuard.Run(() => _showCommandPalette(_button));
        _button.NewTabRequested += () => SafeGuard.Run(_newTab);
        _button.AskSelectionRequested += () => SafeGuard.Run(() => _showCommandPalette(_button));
        _button.FixErrorsRequested += () => SafeGuard.Run(_fixErrors);
        _button.OpenOptionsRequested += () => SafeGuard.Run(_openOptions);
        _button.ManageConnectionsRequested += () => SafeGuard.Run(_manageConnections);
        _button.AccountUsageRequested += () => SafeGuard.Run(_accountUsage);
        _button.UpdateStatus(_connectionService.Status);
        return _button;
    }

    private void OnStatusChanged(object? sender, AIConnectionStatus status)
    {
        _button?.Dispatcher.InvokeAsync(() => _button.UpdateStatus(status));
    }
}
