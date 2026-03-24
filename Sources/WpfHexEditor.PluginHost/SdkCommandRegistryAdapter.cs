//////////////////////////////////////////////
// Project      : WpfHexEditor.PluginHost
// File         : SdkCommandRegistryAdapter.cs
// Description  : Bridges the internal WpfHexEditor.Commands.ICommandRegistry
//                to the plugin-facing WpfHexEditor.SDK.Commands.ICommandRegistry.
//                Enables plugins to register commands that appear in the
//                Command Palette and Keyboard Shortcuts options page.
// Architecture : Adapter pattern. PluginHost owns both sides of the boundary.
//////////////////////////////////////////////

using WpfHexEditor.Core.Commands;
using SdkCmd = WpfHexEditor.SDK.Commands;

namespace WpfHexEditor.PluginHost;

/// <summary>
/// Adapts the internal <see cref="ICommandRegistry"/> to the
/// SDK-facing <see cref="SdkCmd.ICommandRegistry"/> consumed by plugins.
/// </summary>
public sealed class SdkCommandRegistryAdapter : SdkCmd.ICommandRegistry
{
    private readonly ICommandRegistry _inner;

    public SdkCommandRegistryAdapter(ICommandRegistry inner)
        => _inner = inner ?? throw new ArgumentNullException(nameof(inner));

    public void Register(SdkCmd.SdkCommandDefinition def) =>
        _inner.Register(new CommandDefinition(
            def.Id, def.Name, def.Category,
            def.DefaultGesture, def.IconGlyph, def.Command));

    public void Unregister(string id) => _inner.Unregister(id);

    public IReadOnlyList<SdkCmd.SdkCommandDefinition> GetAll() =>
        _inner.GetAll()
              .Select(d => new SdkCmd.SdkCommandDefinition(
                  d.Id, d.Name, d.Category, d.DefaultGesture, d.IconGlyph, d.Command))
              .ToList();

    public SdkCmd.SdkCommandDefinition? Find(string id)
    {
        var d = _inner.Find(id);
        return d is null ? null
            : new SdkCmd.SdkCommandDefinition(
                d.Id, d.Name, d.Category, d.DefaultGesture, d.IconGlyph, d.Command);
    }
}
