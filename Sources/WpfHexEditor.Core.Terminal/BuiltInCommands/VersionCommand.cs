//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using System.Reflection;

namespace WpfHexEditor.Core.Terminal.BuiltInCommands;

public sealed class VersionCommand : ITerminalCommandProvider
{
    public string CommandName => "version";
    public string Description => "Show the IDE and terminal version.";
    public string Usage       => "version";

    public Task<int> ExecuteAsync(string[] args, ITerminalOutput output, ITerminalContext context, CancellationToken ct)
    {
        var version = Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "unknown";
        output.WriteLine($"WpfHexEditor  v{version}");
        output.WriteLine($"Terminal Core v{typeof(VersionCommand).Assembly.GetName().Version}");
        return Task.FromResult(0);
    }
}
