// ==========================================================
// Project: WpfHexEditor.Plugins.AIAssistant
// File: AttachmentViewModel.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.6
// Created: 2026-04-02
// License: GNU Affero General Public License v3.0 (AGPL-3.0)
// Description:
//     ViewModel for file/image attachments pending in the input bar.
// ==========================================================
using CommunityToolkit.Mvvm.ComponentModel;

namespace WpfHexEditor.Plugins.AIAssistant.Panel.Messages;

public sealed class AttachmentViewModel : ObservableObject
{
    public required string DisplayName { get; init; }
    public string? FilePath { get; init; }
    public string? Base64Data { get; init; }
    public required string MediaType { get; init; }
    public bool IsImage { get; init; }
}
