// ==========================================================
// Project: WpfHexEditor.SDK
// File: AssemblyNavigationRequestedEvent.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude (Anthropic)
// Created: 2026-03-26
// Description:
//     SDK-level event published when Assembly Explorer selects a member
//     with a known PE offset. Allows cross-plugin consumers (ParsedFields,
//     HexEditor, etc.) to react without referencing the AssemblyExplorer plugin.
//
// Architecture Notes:
//     Published alongside the plugin-private AssemblyMemberSelectedEvent.
//     Carries only primitive data — no dependency on AssemblyExplorer types.
// ==========================================================

namespace WpfHexEditor.SDK.Events
{
    /// <summary>
    /// Published when the Assembly Explorer selects a member with a PE file offset.
    /// Cross-plugin consumers can subscribe to navigate to the offset.
    /// </summary>
    public sealed class AssemblyNavigationRequestedEvent
    {
        /// <summary>Absolute path of the PE file being explored.</summary>
        public string FilePath { get; init; } = "";

        /// <summary>Display name of the selected member (e.g. "MyClass.MyMethod()").</summary>
        public string MemberName { get; init; } = "";

        /// <summary>PE file offset of the selected member, or -1 if not applicable.</summary>
        public long PeOffset { get; init; } = -1;

        /// <summary>Node kind: "Type", "Method", "Field", "Property", etc.</summary>
        public string NodeKind { get; init; } = "";
    }
}
