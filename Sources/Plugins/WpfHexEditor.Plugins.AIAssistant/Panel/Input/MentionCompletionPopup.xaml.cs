// ==========================================================
// Project: WpfHexEditor.Plugins.AIAssistant
// File: MentionCompletionPopup.xaml.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.6
// Created: 2026-04-01
// License: GNU Affero General Public License v3.0 (AGPL-3.0)
// Description:
//     @mention autocomplete popup. Shows available mentions filtered by typed text.
// ==========================================================
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace WpfHexEditor.Plugins.AIAssistant.Panel.Input;

public partial class MentionCompletionPopup : UserControl
{
    public event Action<string>? MentionSelected;

    private static readonly MentionItem[] s_allMentions =
    [
        new("\uE8E5", "@file", "Current file content"),
        new("\uE8C8", "@selection", "Selected code"),
        new("\uE7BA", "@errors", "Build errors"),
        new("\uE8B7", "@solution", "Solution tree"),
        new("\uE9D9", "@hex", "Hex selection")
    ];

    public MentionCompletionPopup()
    {
        InitializeComponent();
        MentionList.ItemsSource = s_allMentions;
    }

    public void Filter(string query)
    {
        if (string.IsNullOrEmpty(query))
            MentionList.ItemsSource = s_allMentions;
        else
            MentionList.ItemsSource = s_allMentions
                .Where(m => m.Token.Contains(query, StringComparison.OrdinalIgnoreCase))
                .ToArray();
    }

    private void OnItemClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: MentionItem item })
            MentionSelected?.Invoke(item.Token + " ");
    }
}

public sealed record MentionItem(string Icon, string Token, string Description);
