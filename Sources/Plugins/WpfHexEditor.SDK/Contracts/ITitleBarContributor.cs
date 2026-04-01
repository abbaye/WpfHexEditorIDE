// ==========================================================
// Project: WpfHexEditor.SDK
// File: ITitleBarContributor.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.6
// Created: 2026-03-31
// License: GNU Affero General Public License v3.0 (AGPL-3.0)
// Description:
//     Interface for plugins to contribute buttons/icons to the IDE title bar.
// ==========================================================
using System.Windows;

namespace WpfHexEditor.SDK.Contracts;

/// <summary>
/// Allows a plugin to contribute a UI element to the IDE title bar
/// (displayed between the main menu and the notification bell).
/// </summary>
public interface ITitleBarContributor
{
    /// <summary>Unique identifier for this contributor.</summary>
    string ContributorId { get; }

    /// <summary>
    /// Creates the WPF element to display in the title bar.
    /// Typically a Button or Border with an icon and optional badge.
    /// </summary>
    UIElement CreateButton();

    /// <summary>
    /// Display order. Lower values appear closer to the notification bell (right side).
    /// Default: 100. Claude AI uses 10.
    /// </summary>
    int Order { get; }
}
