// ==========================================================
// Project: WpfHexEditor.Plugins.AIAssistant
// File: ThinkingConfig.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.6
// Created: 2026-03-31
// License: GNU Affero General Public License v3.0 (AGPL-3.0)
// Description:
//     Configuration for Claude extended thinking. Ignored by non-Anthropic providers.
// ==========================================================
namespace WpfHexEditor.Plugins.AIAssistant.Api;

public sealed record ThinkingConfig(bool Enabled, int BudgetTokens = 8192);
