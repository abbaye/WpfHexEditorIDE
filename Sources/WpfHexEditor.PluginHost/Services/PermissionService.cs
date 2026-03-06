// ==========================================================
// Project: WpfHexEditor.PluginHost
// File: PermissionService.cs
// Author: Auto
// Created: 2026-03-06
// Description:
//     Runtime permission management for installed plugins.
//     Persists granted/revoked permissions to %AppData%/WpfHexEditor/PluginPermissions.json.
//     Thread-safe via ReaderWriterLockSlim for high-read, low-write workloads.
//
// Architecture Notes:
//     Each plugin has an independent permission mask (PluginPermission flags).
//     Write-tmp-then-rename pattern for atomic persistence.
//     PermissionChanged event fires on Dispatcher thread to allow UI bindings.
//
// ==========================================================

using System.Text.Json;
using System.Windows;
using WpfHexEditor.SDK.Contracts;
using WpfHexEditor.SDK.Models;

namespace WpfHexEditor.PluginHost.Services;

/// <summary>
/// Runtime permission management for plugins with persistence.
/// </summary>
public sealed class PermissionService : IPermissionService
{
    private static readonly string PermissionsFilePath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                     "WpfHexEditor", "PluginPermissions.json");

    private readonly ReaderWriterLockSlim _rwLock = new(LockRecursionPolicy.NoRecursion);
    private readonly Dictionary<string, PluginPermission> _permissions = new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public event EventHandler<PermissionChangedEventArgs>? PermissionChanged;

    public PermissionService()
    {
        LoadFromDisk();
    }

    /// <inheritdoc />
    public bool IsGranted(string pluginId, PluginPermission permission)
    {
        _rwLock.EnterReadLock();
        try
        {
            return _permissions.TryGetValue(pluginId, out var granted) && (granted & permission) == permission;
        }
        finally
        {
            _rwLock.ExitReadLock();
        }
    }

    /// <inheritdoc />
    public PluginPermission GetGranted(string pluginId)
    {
        _rwLock.EnterReadLock();
        try
        {
            return _permissions.TryGetValue(pluginId, out var granted) ? granted : PluginPermission.None;
        }
        finally
        {
            _rwLock.ExitReadLock();
        }
    }

    /// <inheritdoc />
    public void Grant(string pluginId, PluginPermission permission)
    {
        PluginPermission previous, updated;
        _rwLock.EnterWriteLock();
        try
        {
            _permissions.TryGetValue(pluginId, out previous);
            updated = previous | permission;
            _permissions[pluginId] = updated;
        }
        finally
        {
            _rwLock.ExitWriteLock();
        }

        SaveToDisk();
        RaisePermissionChanged(pluginId, previous, updated);
    }

    /// <inheritdoc />
    public void Revoke(string pluginId, PluginPermission permission)
    {
        PluginPermission previous, updated;
        _rwLock.EnterWriteLock();
        try
        {
            _permissions.TryGetValue(pluginId, out previous);
            updated = previous & ~permission;
            _permissions[pluginId] = updated;
        }
        finally
        {
            _rwLock.ExitWriteLock();
        }

        SaveToDisk();
        RaisePermissionChanged(pluginId, previous, updated);
    }

    /// <summary>
    /// Initializes a plugin's permissions from its declared capabilities.
    /// Called during plugin load; does not override existing runtime overrides.
    /// </summary>
    public void InitializeForPlugin(string pluginId, PluginPermission declaredPermissions)
    {
        _rwLock.EnterWriteLock();
        try
        {
            if (!_permissions.ContainsKey(pluginId))
                _permissions[pluginId] = declaredPermissions;
        }
        finally
        {
            _rwLock.ExitWriteLock();
        }
    }

    private void RaisePermissionChanged(string pluginId, PluginPermission previous, PluginPermission current)
    {
        var args = new PermissionChangedEventArgs(pluginId, previous, current);
        if (Application.Current?.Dispatcher is { } d && !d.CheckAccess())
            d.InvokeAsync(() => PermissionChanged?.Invoke(this, args));
        else
            PermissionChanged?.Invoke(this, args);
    }

    private void LoadFromDisk()
    {
        try
        {
            if (!File.Exists(PermissionsFilePath)) return;
            var json = File.ReadAllText(PermissionsFilePath);
            var dict = JsonSerializer.Deserialize<Dictionary<string, long>>(json);
            if (dict is null) return;
            foreach (var kvp in dict)
                _permissions[kvp.Key] = (PluginPermission)kvp.Value;
        }
        catch
        {
            // Corrupt permissions file — start fresh; plugin will re-request on next load.
        }
    }

    private void SaveToDisk()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(PermissionsFilePath)!);
            var dict = new Dictionary<string, long>();
            _rwLock.EnterReadLock();
            try
            {
                foreach (var kvp in _permissions)
                    dict[kvp.Key] = (long)kvp.Value;
            }
            finally
            {
                _rwLock.ExitReadLock();
            }

            var json = JsonSerializer.Serialize(dict, new JsonSerializerOptions { WriteIndented = true });
            var tmp = PermissionsFilePath + ".tmp";
            File.WriteAllText(tmp, json);
            File.Move(tmp, PermissionsFilePath, overwrite: true);
        }
        catch
        {
            // Non-fatal — permissions will be re-asked on next session.
        }
    }
}
