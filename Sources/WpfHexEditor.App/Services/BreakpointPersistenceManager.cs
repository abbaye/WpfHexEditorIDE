// ==========================================================
// Project: WpfHexEditor.App
// File: Services/BreakpointPersistenceManager.cs
// Description:
//     Serializes/deserializes IDE breakpoints to/from AppSettings.
//     Converts between BreakpointLocation (runtime model) and
//     PersistedBreakpoint (settings DTO). Stateless — holds no list.
// Architecture:
//     App layer only. Called by DebuggerServiceImpl after every mutation.
// ==========================================================

using WpfHexEditor.Core.Debugger.Models;
using WpfHexEditor.Core.Options;

namespace WpfHexEditor.App.Services;

/// <summary>
/// Serializes IDE-managed breakpoints to/from <see cref="AppSettings.DebuggerSettings"/>.
/// </summary>
internal sealed class BreakpointPersistenceManager
{
    private readonly AppSettings _settings;

    public BreakpointPersistenceManager(AppSettings settings)
        => _settings = settings ?? throw new ArgumentNullException(nameof(settings));

    /// <summary>Deserializes all persisted breakpoints into runtime <see cref="BreakpointLocation"/> records.</summary>
    public IReadOnlyList<BreakpointLocation> Load() =>
        _settings.Debugger.Breakpoints
            .Select(pb => new BreakpointLocation
            {
                FilePath  = pb.FilePath,
                Line      = pb.Line,
                Condition = pb.Condition,
                IsEnabled = pb.IsEnabled,
            })
            .ToList();

    /// <summary>Overwrites the persisted list with the current runtime breakpoints.</summary>
    public void Save(IEnumerable<BreakpointLocation> breakpoints)
    {
        _settings.Debugger.Breakpoints.Clear();
        _settings.Debugger.Breakpoints.AddRange(
            breakpoints.Select(b => new PersistedBreakpoint
            {
                FilePath  = b.FilePath,
                Line      = b.Line,
                Condition = b.Condition,
                IsEnabled = b.IsEnabled,
            }));
    }
}
