// ==========================================================
// Project: WpfHexEditor.SDK
// File: Contracts/IExtensionWithContext.cs
// Created: 2026-03-15
// Description:
//     Optional interface for extension point implementations that need IDE services.
//     WpfPluginHost calls Initialize() immediately after instantiating the extension class.
// ==========================================================

namespace WpfHexEditor.SDK.Contracts;

/// <summary>
/// Optional interface that extension point implementations can implement
/// when they need access to IDE services via <see cref="IIDEHostContext"/>.
/// <para>
/// WpfPluginHost calls <see cref="Initialize"/> immediately after creating the extension instance,
/// before it is registered in <see cref="IExtensionRegistry"/>.
/// </para>
/// </summary>
public interface IExtensionWithContext
{
    /// <summary>Called by PluginHost to inject IDE services into the extension implementation.</summary>
    void Initialize(IIDEHostContext context);
}
