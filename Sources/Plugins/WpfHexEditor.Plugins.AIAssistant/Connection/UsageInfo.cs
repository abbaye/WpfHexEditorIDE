// ==========================================================
// Project: WpfHexEditor.Plugins.AIAssistant
// File: UsageInfo.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.6
// Created: 2026-04-02
// License: GNU Affero General Public License v3.0 (AGPL-3.0)
// Description:
//     Data model for provider account info and usage/quota buckets.
// ==========================================================
namespace WpfHexEditor.Plugins.AIAssistant.Connection;

public sealed class ProviderUsageInfo
{
    public required string ProviderId { get; init; }
    public required string DisplayName { get; init; }

    // Account section (nullable — not all providers expose this)
    public string? AuthMethod { get; set; }
    public string? Email { get; set; }
    public string? Plan { get; set; }

    // Usage buckets (providers may have multiple: requests, tokens, etc.)
    public List<UsageBucket> Buckets { get; set; } = [];

    // Dashboard URL for "Manage usage" link
    public string? ManageUrl { get; set; }

    public bool HasUsageData => Buckets.Count > 0;
    public bool HasAccountData => AuthMethod is not null;
    public DateTime LastRefreshed { get; set; }
}

public sealed class UsageBucket
{
    public required string Label { get; init; }
    public required double UsedPercent { get; init; }
    public string? ResetsIn { get; init; }
    public string? DetailText { get; init; }
}
