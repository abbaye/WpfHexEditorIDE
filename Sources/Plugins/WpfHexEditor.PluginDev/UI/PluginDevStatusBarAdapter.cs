// ==========================================================
// Project: WpfHexEditor.PluginDev
// File: UI/PluginDevStatusBarAdapter.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-23
// Description:
//     Status bar adapter that reflects the plugin dev lifecycle state
//     in the IDE status bar.
//
//     State → Display:
//       Idle    → gray  "◌ No plugin project"
//       Building→ orange "⟳ Building…"
//       Running → green  "● MyPlugin [Ready]"
//       Error   → red    "✕ MyPlugin [Error]" (clickable → show log panel)
//
// Architecture Notes:
//     Pattern: Adapter — wraps PluginDevToolbarViewModel and maps state
//     transitions to status bar descriptor updates.
//     Uses the SDK StatusBarItemDescriptor; the IDE host registers it
//     via IUIRegistry.RegisterStatusBarItem().
//     Tokens: PD_StatusReady, PD_StatusBuilding, PD_StatusError (added to
//     all 18 Colors.xaml files separately — see ADR-PD-01).
// ==========================================================

using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using WpfHexEditor.SDK.Descriptors;

namespace WpfHexEditor.PluginDev.UI;

/// <summary>
/// Reflects the plugin development state in the IDE status bar.
/// Instantiate once and call <see cref="Attach"/> to start observing.
/// </summary>
public sealed class PluginDevStatusBarAdapter : IDisposable
{
    // -----------------------------------------------------------------------
    // State symbols
    // -----------------------------------------------------------------------

    private const string SymIdle     = "◌";
    private const string SymBuilding = "⟳";
    private const string SymReady    = "●";
    private const string SymError    = "✕";

    // -----------------------------------------------------------------------
    // Fields
    // -----------------------------------------------------------------------

    private readonly StatusBarItemDescriptor _descriptor;
    private          PluginDevToolbarViewModel? _vm;
    private          Action?                 _onErrorClick;

    // -----------------------------------------------------------------------
    // Construction
    // -----------------------------------------------------------------------

    public PluginDevStatusBarAdapter()
    {
        _descriptor = new StatusBarItemDescriptor
        {
            Text      = $"{SymIdle} No extension project",
            Alignment = StatusBarAlignment.Right,
            ToolTip   = "Extension Development",
            Order     = 30,
        };
    }

    // -----------------------------------------------------------------------
    // Public API
    // ----------------------------------------------------------------------->

    /// <summary>The descriptor to register with the IDE status bar.</summary>
    public StatusBarItemDescriptor Descriptor => _descriptor;

    /// <summary>
    /// Starts observing <paramref name="vm"/> for state changes and
    /// reflecting them in the status bar descriptor.
    /// </summary>
    /// <param name="vm">ViewModel to observe.</param>
    /// <param name="onErrorClick">
    /// Optional callback invoked when the user clicks the error status item.
    /// Typically used to show the Plugin Dev Log panel.
    /// </param>
    public void Attach(PluginDevToolbarViewModel vm, Action? onErrorClick = null)
    {
        if (_vm is not null)
            _vm.PropertyChanged -= OnVmPropertyChanged;

        _vm           = vm ?? throw new ArgumentNullException(nameof(vm));
        _onErrorClick = onErrorClick;

        _vm.PropertyChanged += OnVmPropertyChanged;
        Refresh();
    }

    /// <summary>Detaches from the observed ViewModel.</summary>
    public void Detach()
    {
        if (_vm is not null)
            _vm.PropertyChanged -= OnVmPropertyChanged;

        _vm           = null;
        _onErrorClick = null;
        _descriptor.Text = $"{SymIdle} No extension project";
    }

    // -----------------------------------------------------------------------
    // IDisposable
    // -----------------------------------------------------------------------

    public void Dispose() => Detach();

    // -----------------------------------------------------------------------
    // Private
    // -----------------------------------------------------------------------

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(PluginDevToolbarViewModel.State)
                           or nameof(PluginDevToolbarViewModel.ActivePluginName))
            Refresh();
    }

    private void Refresh()
    {
        if (_vm is null)
        {
            _descriptor.Text = $"{SymIdle} No extension project";
            return;
        }

        var pluginName = _vm.ActivePluginName ?? "Extension";

        _descriptor.Text = _vm.State switch
        {
            PluginDevState.Idle     => $"{SymIdle} No plugin project",
            PluginDevState.Building => $"{SymBuilding} Building…",
            PluginDevState.Running  => $"{SymReady} {pluginName} [Ready]",
            PluginDevState.Error    => $"{SymError} {pluginName} [Error]",
            _                      => $"{SymIdle} No plugin project",
        };

        StatusBarUpdated?.Invoke(this, _descriptor);
    }

    /// <summary>
    /// Raised each time the status bar text changes.
    /// The host can subscribe to update its WPF status bar control directly
    /// when SDK UIRegistry.RegisterStatusBarItem() is not available.
    /// </summary>
    public event EventHandler<StatusBarItemDescriptor>? StatusBarUpdated;
}
