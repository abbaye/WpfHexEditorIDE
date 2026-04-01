// ==========================================================
// Project: WpfHexEditor.Plugins.ClaudeAssistant
// File: ToolCallViewModel.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.6
// Created: 2026-03-31
// License: GNU Affero General Public License v3.0 (AGPL-3.0)
// Description:
//     ViewModel for a tool call widget shown inline in chat messages.
// ==========================================================
using CommunityToolkit.Mvvm.ComponentModel;

namespace WpfHexEditor.Plugins.ClaudeAssistant.Panel.Messages;

public sealed partial class ToolCallViewModel : ObservableObject
{
    [ObservableProperty] private string _toolName = "";
    [ObservableProperty] private string _argsJson = "";
    [ObservableProperty] private string _resultJson = "";
    [ObservableProperty] private ToolCallStatus _status = ToolCallStatus.Pending;
    [ObservableProperty] private bool _isExpanded;

    public bool IsDone => Status is ToolCallStatus.Done or ToolCallStatus.Error;

    public void AppendArgs(string delta)
    {
        ArgsJson += delta;
    }

    public void SetResult(string json, bool isError = false)
    {
        ResultJson = json;
        Status = isError ? ToolCallStatus.Error : ToolCallStatus.Done;
        OnPropertyChanged(nameof(IsDone));
    }
}

public enum ToolCallStatus
{
    Pending,
    Running,
    Done,
    Error
}
