// ==========================================================
// Project: WpfHexEditor.Plugins.AIAssistant
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
using WpfHexEditor.Plugins.AIAssistant.Api;

namespace WpfHexEditor.Plugins.AIAssistant.Providers.ClaudeCode;

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
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        psi.Environment["LANG"] = "en_US.UTF-8";
        psi.Environment["PYTHONIOENCODING"] = "utf-8";

        Process? proc = null;
        try
        {
            proc = Process.Start(psi);
            if (proc is null)
            {
                yield return new ChatStreamChunk(ChunkKind.Error, ErrorMessage: "Failed to start Claude CLI process");
                yield break;
            }

            // Send prompt via stdin in UTF-8 to avoid encoding issues
            using (var utf8Writer = new System.IO.StreamWriter(proc.StandardInput.BaseStream, Encoding.UTF8, leaveOpen: false))
            {
                await utf8Writer.WriteAsync(prompt);
            }

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

    /// <summary>Builds a single prompt string from conversation messages, saving images to temp files.</summary>
    private static string BuildPrompt(IReadOnlyList<ChatMessage> messages)
    {
        if (messages.Count == 1)
            return BuildMessageText(messages[0]);

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
            sb.AppendLine($"{role}: {BuildMessageText(msg)}");
        }
        return sb.ToString();
    }

    private static string BuildMessageText(ChatMessage msg)
    {
        var sb = new StringBuilder();
        foreach (var block in msg.Content)
        {
            switch (block)
            {
                case TextBlock tb:
                    sb.Append(tb.Text);
                    break;
                case ImageBlock img:
                    try
                    {
                        var ext = img.MediaType switch
                        {
                            "image/jpeg" => ".jpg",
                            "image/gif" => ".gif",
                            "image/webp" => ".webp",
                            _ => ".png"
                        };
                        var tmpDir = Path.Combine(Path.GetTempPath(), "claude_assistant");
                        Directory.CreateDirectory(tmpDir);
                        var tmpPath = Path.Combine(tmpDir, $"img_{Guid.NewGuid():N}{ext}");
                        File.WriteAllBytes(tmpPath, Convert.FromBase64String(img.Base64Data));
                        sb.AppendLine();
                        sb.AppendLine($"[Image attached — saved at: {tmpPath}]");
                        sb.AppendLine("Use your Read tool to view this image file.");
                    }
                    catch
                    {
                        sb.Append("\n[Image attachment failed to save]");
                    }
                    break;
            }
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
