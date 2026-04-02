// ==========================================================
// Project: WpfHexEditor.Plugins.ClaudeAssistant
// File: ClaudeCodeModelProvider.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Opus 4.6
// Created: 2026-04-02
// License: GNU Affero General Public License v3.0 (AGPL-3.0)
// Description:
//     IModelProvider that shells out to the Claude Code CLI (`claude`).
//     Uses the user's claude.ai subscription — no API key required.
//     Streaming via `claude -p --verbose --output-format stream-json`.
// ==========================================================
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using WpfHexEditor.Plugins.ClaudeAssistant.Api;

namespace WpfHexEditor.Plugins.ClaudeAssistant.Providers.ClaudeCode;

public sealed class ClaudeCodeModelProvider : IModelProvider
{
    public string ProviderId => "claude-code";
    public string DisplayName => "Claude Code (CLI)";
    public string[] AvailableModels => ["sonnet", "opus", "haiku"];
    public bool SupportsTools => false;
    public bool SupportsVision => false;
    public bool SupportsThinking => true;
    public int MaxContextTokens => 200_000;

    private static string? s_cachedExePath;

    /// <summary>Finds the `claude` executable in PATH or known install locations.</summary>
    public static string? FindClaudeExecutable()
    {
        if (s_cachedExePath is not null && File.Exists(s_cachedExePath))
            return s_cachedExePath;

        // Try PATH via `where` on Windows
        try
        {
            var psi = new ProcessStartInfo("where", "claude")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi);
            if (proc is not null)
            {
                var output = proc.StandardOutput.ReadLine()?.Trim();
                proc.WaitForExit(3000);
                if (proc.ExitCode == 0 && !string.IsNullOrEmpty(output) && File.Exists(output))
                {
                    s_cachedExePath = output;
                    return output;
                }
            }
        }
        catch { /* where not available or failed */ }

        // Fallback: WinGet links
        var wingetPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Microsoft", "WinGet", "Links", "claude.exe");
        if (File.Exists(wingetPath))
        {
            s_cachedExePath = wingetPath;
            return wingetPath;
        }

        // Fallback: npm global
        var npmPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "npm", "claude.cmd");
        if (File.Exists(npmPath))
        {
            s_cachedExePath = npmPath;
            return npmPath;
        }

        return null;
    }

    public async Task<bool> TestConnectionAsync(CancellationToken ct = default)
    {
        var exe = FindClaudeExecutable();
        if (exe is null) return false;

        try
        {
            var psi = new ProcessStartInfo(exe, "-p --output-format json \"hi\"")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var proc = Process.Start(psi);
            if (proc is null) return false;

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(15));

            var output = await proc.StandardOutput.ReadToEndAsync(timeoutCts.Token);
            await proc.WaitForExitAsync(timeoutCts.Token);

            if (proc.ExitCode != 0) return false;

            using var doc = JsonDocument.Parse(output);
            return doc.RootElement.TryGetProperty("subtype", out var sub)
                && sub.GetString() == "success";
        }
        catch
        {
            return false;
        }
    }

    public async IAsyncEnumerable<ChatStreamChunk> StreamAsync(
        IReadOnlyList<ChatMessage> messages,
        string modelId,
        IReadOnlyList<ToolDefinition>? tools = null,
        ThinkingConfig? thinking = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var exe = FindClaudeExecutable();
        if (exe is null)
        {
            yield return new ChatStreamChunk(ChunkKind.Error,
                ErrorMessage: "Claude Code CLI not found. Install it via: npm install -g @anthropic-ai/claude-code");
            yield break;
        }

        // Build prompt from conversation history
        var prompt = BuildPrompt(messages);

        // Build arguments
        var args = new StringBuilder();
        args.Append("-p --verbose --output-format stream-json --include-partial-messages");
        args.Append(" --no-session-persistence");
        args.Append($" --model {modelId}");

        var psi = new ProcessStartInfo(exe)
        {
            Arguments = args.ToString(),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8
        };

        Process? proc = null;
        try
        {
            proc = Process.Start(psi);
            if (proc is null)
            {
                yield return new ChatStreamChunk(ChunkKind.Error, ErrorMessage: "Failed to start Claude CLI process");
                yield break;
            }

            // Send prompt via stdin to avoid shell escaping issues
            await proc.StandardInput.WriteAsync(prompt);
            proc.StandardInput.Close();

            // Register cancellation to kill the process
            ct.Register(() =>
            {
                try { if (!proc.HasExited) proc.Kill(entireProcessTree: true); }
                catch { /* already exited */ }
            });

            // Read NDJSON stream line by line
            while (!ct.IsCancellationRequested)
            {
                var line = await proc.StandardOutput.ReadLineAsync(ct);
                if (line is null) break; // EOF

                if (string.IsNullOrWhiteSpace(line)) continue;

                ChatStreamChunk? chunk = null;
                try { chunk = ParseStreamLine(line); }
                catch { /* skip unparseable lines */ }

                if (chunk is not null)
                {
                    yield return chunk;
                    if (chunk.IsFinal) yield break;
                }
            }

            await proc.WaitForExitAsync(ct);

            if (proc.ExitCode != 0 && !ct.IsCancellationRequested)
            {
                var stderr = await proc.StandardError.ReadToEndAsync(ct);
                yield return new ChatStreamChunk(ChunkKind.Error,
                    ErrorMessage: $"Claude CLI exited with code {proc.ExitCode}: {stderr.Trim()}");
            }
        }
        finally
        {
            if (proc is not null && !proc.HasExited)
            {
                try { proc.Kill(entireProcessTree: true); } catch { }
            }
            proc?.Dispose();
        }

        yield return new ChatStreamChunk(ChunkKind.Done, IsFinal: true);
    }

    /// <summary>Builds a single prompt string from conversation messages.</summary>
    private static string BuildPrompt(IReadOnlyList<ChatMessage> messages)
    {
        if (messages.Count == 1)
            return messages[0].GetTextContent();

        var sb = new StringBuilder();
        foreach (var msg in messages)
        {
            var role = msg.Role switch
            {
                "user" => "Human",
                "assistant" => "Assistant",
                "system" => "System",
                _ => msg.Role
            };
            sb.AppendLine($"{role}: {msg.GetTextContent()}");
        }
        return sb.ToString();
    }

    /// <summary>Parses a single NDJSON line from the Claude CLI stream.</summary>
    private static ChatStreamChunk? ParseStreamLine(string line)
    {
        using var doc = JsonDocument.Parse(line);
        var root = doc.RootElement;

        if (!root.TryGetProperty("type", out var typeProp))
            return null;

        var type = typeProp.GetString();

        // Stream event with content delta
        if (type == "stream_event" && root.TryGetProperty("event", out var evt))
        {
            var evtType = evt.TryGetProperty("type", out var et) ? et.GetString() : null;

            if (evtType == "content_block_delta" && evt.TryGetProperty("delta", out var delta))
            {
                var deltaType = delta.TryGetProperty("type", out var dt) ? dt.GetString() : null;

                if (deltaType == "text_delta" && delta.TryGetProperty("text", out var text))
                    return new ChatStreamChunk(ChunkKind.TextDelta, Text: text.GetString());

                if (deltaType == "thinking_delta" && delta.TryGetProperty("thinking", out var thinking))
                    return new ChatStreamChunk(ChunkKind.ThinkingDelta, ThinkingText: thinking.GetString());
            }

            if (evtType == "message_stop")
                return new ChatStreamChunk(ChunkKind.Done, IsFinal: true);
        }

        // Final result
        if (type == "result")
        {
            var isError = root.TryGetProperty("is_error", out var ie) && ie.GetBoolean();
            if (isError)
            {
                var errMsg = root.TryGetProperty("result", out var r) ? r.GetString() : "Unknown error";
                return new ChatStreamChunk(ChunkKind.Error, ErrorMessage: errMsg);
            }
            return new ChatStreamChunk(ChunkKind.Done, IsFinal: true);
        }

        // Error event
        if (type == "error")
        {
            var errMsg = root.TryGetProperty("error", out var e)
                && e.TryGetProperty("message", out var m) ? m.GetString() : "CLI error";
            return new ChatStreamChunk(ChunkKind.Error, ErrorMessage: errMsg);
        }

        return null;
    }
}
