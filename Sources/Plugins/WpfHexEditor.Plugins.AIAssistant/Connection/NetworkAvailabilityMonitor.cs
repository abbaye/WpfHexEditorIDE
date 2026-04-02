// ==========================================================
// Project: WpfHexEditor.Plugins.AIAssistant
// File: NetworkAvailabilityMonitor.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.6
// Created: 2026-03-31
// License: GNU Affero General Public License v3.0 (AGPL-3.0)
// Description:
//     Monitors OS network availability; fires event on change.
// ==========================================================
using System.Net.NetworkInformation;

namespace WpfHexEditor.Plugins.AIAssistant.Connection;

public sealed class NetworkAvailabilityMonitor : IDisposable
{
    public bool IsAvailable { get; private set; } = NetworkInterface.GetIsNetworkAvailable();
    public event EventHandler<bool>? AvailabilityChanged;

    public NetworkAvailabilityMonitor()
    {
        NetworkChange.NetworkAvailabilityChanged += OnNetworkChanged;
    }

    private void OnNetworkChanged(object? sender, NetworkAvailabilityEventArgs e)
    {
        IsAvailable = e.IsAvailable;
        AvailabilityChanged?.Invoke(this, e.IsAvailable);
    }

    public void Dispose()
    {
        NetworkChange.NetworkAvailabilityChanged -= OnNetworkChanged;
    }
}
