# Fix-CA-Headers.ps1 — Replace old-style headers with project convention in ClaudeAssistant plugin
# Convention: // ========== / Project / File / Author / Contributors / Created / Description / ==========

$Root = "$PSScriptRoot\..\Plugins\WpfHexEditor.Plugins.ClaudeAssistant"
$McpRoot = "$PSScriptRoot\..\Plugins\WpfHexEditor.MCP.IDEServers"
$SdkFile = "$PSScriptRoot\..\Plugins\WpfHexEditor.SDK\Contracts\ITitleBarContributor.cs"

$descriptions = @{
    'ClaudeAssistantPlugin.cs' = 'Main plugin entry point. Registers panel, menus, commands, titlebar icon, MCP servers.'
    'Api/ChatMessage.cs' = 'Provider-agnostic chat message model with text, image, tool-use, and thinking blocks.'
    'Api/ChatStreamChunk.cs' = 'Provider-agnostic streaming chunk model for token-by-token delivery.'
    'Api/IModelProvider.cs' = 'Abstraction for AI model backends (Anthropic, OpenAI, Gemini, Ollama).'
    'Api/ModelRegistry.cs' = 'Registry of all available model providers. Providers register at startup.'
    'Api/ThinkingConfig.cs' = 'Configuration for Claude extended thinking. Ignored by non-Anthropic providers.'
    'Connection/ClaudeConnectionService.cs' = 'Background health-check, rate-limit backoff, offline detection. Drives titlebar badge.'
    'Connection/ClaudeConnectionStatus.cs' = 'Connection state enum and ConnectionInfo record.'
    'Connection/NetworkAvailabilityMonitor.cs' = 'Monitors OS network availability; fires event on change.'
    'Connection/RateLimitHandler.cs' = 'Handles API rate limits with exponential backoff and request queuing.'
    'Mcp/Host/IMcpServerManager.cs' = 'Contract for the MCP server orchestrator (stdio + in-process servers).'
    'Mcp/Host/McpServerManager.cs' = 'Orchestrates all MCP servers. Aggregates tool lists, routes tool calls.'
    'Mcp/Host/StdioMcpServerProcess.cs' = 'Manages a Node.js MCP server process via stdio JSON-RPC.'
    'Options/ClaudeAssistantOptions.cs' = 'Persistent settings with DPAPI-encrypted API keys per provider.'
    'Options/ClaudeAssistantOptionsPage.xaml.cs' = 'Options page code-behind. Per-provider API key config + defaults.'
    'Panel/ClaudeAssistantPanel.xaml.cs' = 'Panel code-behind. Tab click handler.'
    'Panel/ClaudeAssistantPanelViewModel.cs' = 'Root ViewModel. Multi-tab conversations, history panel, provider registry.'
    'Panel/History/HistoryPanel.xaml.cs' = 'History panel code-behind. Click opens session in a new tab.'
    'Panel/History/HistoryPanelViewModel.cs' = 'ViewModel for the history panel. Lists past conversations with search.'
    'Panel/Messages/ChatMessageViewModel.cs' = 'ViewModel for a single chat message bubble with streaming support.'
    'Panel/Messages/ToolCallViewModel.cs' = 'ViewModel for a tool call widget shown inline in chat messages.'
    'Panel/Tabs/ConversationTab.xaml.cs' = 'Conversation tab code-behind. Auto-scroll and input key handling.'
    'Panel/Tabs/ConversationTabViewModel.cs' = 'ViewModel for a conversation tab. Streaming loop, Send/Cancel, tool execution.'
    'Providers/Anthropic/AnthropicModelProvider.cs' = 'Anthropic Claude provider. HTTP SSE streaming with tool_use and thinking.'
    'Providers/Google/GeminiModelProvider.cs' = 'Google Gemini provider. HTTP direct streaming, 1M context.'
    'Providers/Ollama/OllamaModelProvider.cs' = 'Ollama local provider. OpenAI-compatible API, auto model discovery.'
    'Providers/OpenAI/OpenAIModelProvider.cs' = 'OpenAI provider. SSE streaming with function_calling normalized to tool_use.'
    'Session/ConversationPersistence.cs' = 'Save/load conversations to JSON files in %AppData%.'
    'Session/ConversationSession.cs' = 'Single conversation with message history, provider, and model state.'
    'Session/SessionMetadata.cs' = 'Lightweight metadata for conversation index (no full message history).'
    'TitleBar/ClaudeTitleBarButton.xaml.cs' = 'Claude icon button for the IDE title bar with animated status badge.'
    'TitleBar/ClaudeTitleBarContributor.cs' = 'ITitleBarContributor implementation. Creates the Claude titlebar button.'
}

function Fix-Header($filePath, $project, $relPath, $desc) {
    $content = Get-Content $filePath -Raw
    $fileName = [System.IO.Path]::GetFileName($filePath)

    $header = @"
// ==========================================================
// Project: $project
// File: $fileName
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.6
// Created: 2026-03-31
// License: GNU Affero General Public License v3.0 (AGPL-3.0)
// Description:
//     $desc
// ==========================================================
"@

    # Remove old header lines (// Project: ... // File: ... // Description: ... // Architecture: ...)
    $content = $content -replace '(?s)^(// [^\r\n]+\r?\n)+\r?\n', ''

    $newContent = $header + "`r`n" + $content
    Set-Content $filePath -Value $newContent -NoNewline
    Write-Host ('OK   ' + $relPath)
}

# Process ClaudeAssistant files
foreach ($rel in $descriptions.Keys) {
    $full = Join-Path $Root $rel
    if (Test-Path $full) {
        Fix-Header $full 'WpfHexEditor.Plugins.ClaudeAssistant' $rel $descriptions[$rel]
    }
}

# Process MCP IDEServers
$mcpDescs = @{
    'Base/IIDEMcpServer.cs' = 'Contract for in-process MCP servers that expose IDE state as tools.'
    'Base/IdeMcpServerBase.cs' = 'Base class for IDE MCP servers with tool registration and dispatch.'
}
foreach ($rel in $mcpDescs.Keys) {
    $full = Join-Path $McpRoot $rel
    if (Test-Path $full) {
        Fix-Header $full 'WpfHexEditor.MCP.IDEServers' $rel $mcpDescs[$rel]
    }
}

# Process SDK file
if (Test-Path $SdkFile) {
    Fix-Header $SdkFile 'WpfHexEditor.SDK' 'Contracts/ITitleBarContributor.cs' 'Interface for plugins to contribute buttons/icons to the IDE title bar.'
}

Write-Host ('Done. Headers fixed.')
