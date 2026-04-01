// ==========================================================
// Project: WpfHexEditor.SDK
// File: Contracts/Terminal/ITerminalContext.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-30
// Description:
//     Execution context passed to every terminal command.
//     Moved from WpfHexEditor.Core.Terminal to the SDK so that
//     plugin authors can implement ITerminalCommandProvider without
//     taking a direct dependency on Core.Terminal.
//
// Architecture Notes:
//     WpfHexEditor.Core.Terminal re-exports this type via a global using alias
//     so all existing built-in commands compile without any changes.
// ==========================================================

using WpfHexEditor.SDK.Contracts.Focus;

namespace WpfHexEditor.SDK.Contracts.Terminal;

/// <summary>
/// Execution context passed to every terminal command.
/// Provides access to IDE services and the current session state.
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
