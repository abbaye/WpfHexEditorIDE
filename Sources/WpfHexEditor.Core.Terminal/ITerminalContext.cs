//////////////////////////////////////////////
// Apache 2.0  - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

using WpfHexEditor.SDK.Contracts;
using WpfHexEditor.SDK.Contracts.Focus;

namespace WpfHexEditor.Core.Terminal;

/// <summary>
/// Execution context passed to every terminal command.
/// Provides access to IDE services and the current focus state.
/// </summary>
public interface ITerminalContext
{
    /// <summary>Full IDE host context (services, event bus, etc.).</summary>
    IIDEHostContext IDE { get; }

    /// <summary>Currently active document (may be null).</summary>
    IDocument? ActiveDocument { get; }

    /// <summary>Currently active panel (may be null).</summary>
    IPanel? ActivePanel { get; }

    /// <summary>Current working directory for file-system commands.</summary>
    string WorkingDirectory { get; }
}
