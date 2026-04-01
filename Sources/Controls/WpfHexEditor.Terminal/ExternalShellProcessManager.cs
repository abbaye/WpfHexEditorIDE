// ==========================================================
// Project: WpfHexEditor.Terminal
// File: ExternalShellProcessManager.cs
// Description:
//     Manages the lifecycle of an external shell process (cmd, pwsh, bash).
//     Handles start, stdin/stdout/stderr piping, prompt-flush heuristic,
//     and cleanup. Outputs are forwarded via a callback delegate so the
//     owning ViewModel stays free of process-management details.
// Architecture:
//     Terminal layer. No SDK/App references.
//     Called exclusively from ShellSessionViewModel.
// ==========================================================

using System.Diagnostics;
using System.IO;
using System.Text;
using WpfHexEditor.Core.Terminal;
using WpfHexEditor.Core.Terminal.ShellSession;

namespace WpfHexEditor.Terminal;

/// <summary>
/// Owns the external shell <see cref="Process"/>, wires stdin/stdout/stderr pipes,
/// and forwards output lines to a callback.
/// </summary>
internal sealed class ExternalShellProcessManager : IDisposable
{
    private readonly ShellSession _session;
    private readonly Action<string, TerminalOutputKind> _output;
    private StreamWriter? _shellInput;

    public ExternalShellProcessManager(ShellSession session, Action<string, TerminalOutputKind> output)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _output  = output  ?? throw new ArgumentNullException(nameof(output));
    }

    /// <summary>The stdin writer for the running shell, or <see langword="null"/> when not started.</summary>
    public StreamWriter? Input => _shellInput;

    /// <summary>Starts the shell process and wires stdout/stderr pipe readers.</summary>
    public async Task StartAsync(string selectedEncoding)
    {
        var (exe, args) = ResolveShell(_session.ShellType);
        if (exe is null)
        {
            _output($"Shell not found for {_session.ShellType}. Falling back to HxTerminal input.",
                    TerminalOutputKind.Error);
            return;
        }

        var encoding = ParseEncoding(selectedEncoding);
        var psi = new ProcessStartInfo(exe)
        {
            Arguments              = args,
            UseShellExecute        = false,
            CreateNoWindow         = true,
            RedirectStandardInput  = true,
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            StandardOutputEncoding = encoding,
            StandardErrorEncoding  = encoding,
            WorkingDirectory       = _session.WorkingDirectory,
        };

        var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        process.Exited += OnProcessExited;

        try
        {
            process.Start();
        }
        catch (Exception ex)
        {
            _output($"Failed to start {_session.ShellType}: {ex.Message}", TerminalOutputKind.Error);
            process.Dispose();
            return;
        }

        _session.ShellProcess = process;
        _session.ShellInput   = process.StandardInput;
        _shellInput           = process.StandardInput;

        _ = PipeReaderAsync(process.StandardOutput, TerminalOutputKind.Standard);
        _ = PipeReaderAsync(process.StandardError,  TerminalOutputKind.Error);

        _output($"[{_session.ShellType} started: {exe}]", TerminalOutputKind.Info);
        await Task.CompletedTask;
    }

    /// <summary>Unwires events, disposes the process, and clears stdin references.</summary>
    public void Cleanup()
    {
        if (_session.ShellProcess is not null)
        {
            _session.ShellProcess.Exited -= OnProcessExited;
            _session.ShellProcess.Dispose();
            _session.ShellProcess = null;
        }
        _shellInput?.Dispose();
        _shellInput         = null;
        _session.ShellInput = null;
    }

    public void Dispose() => Cleanup();

    // ── Private ───────────────────────────────────────────────────────────────

    private void OnProcessExited(object? sender, EventArgs e)
    {
        Cleanup();
        _output($"[{_session.ShellType} process exited]", TerminalOutputKind.Warning);
    }

    private async Task PipeReaderAsync(StreamReader reader, TerminalOutputKind kind)
    {
        const int BufSize       = 4096;
        const int PromptTimeout = 150; // ms — flush incomplete lines (prompts) after this silence

        var charBuf = new char[BufSize];
        var lineBuf = new StringBuilder();

        try
        {
            while (true)
            {
                int read;

                if (lineBuf.Length > 0)
                {
                    // Partial content in buffer: use a timeout so shell prompts
                    // (which have no trailing '\n') are flushed after a brief silence.
                    using var cts = new CancellationTokenSource(PromptTimeout);
                    try
                    {
                        read = await reader.ReadAsync(charBuf.AsMemory(0, BufSize), cts.Token)
                                           .ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        // Timeout: emit the accumulated prompt text as a partial line.
                        _output(lineBuf.ToString(), kind);
                        lineBuf.Clear();
                        continue;
                    }
                }
                else
                {
                    // No pending partial content — wait indefinitely (no CTS overhead).
                    read = await reader.ReadAsync(charBuf.AsMemory(0, BufSize))
                                       .ConfigureAwait(false);
                }

                if (read == 0) break; // EOF / process exited

                for (int i = 0; i < read; i++)
                {
                    char c = charBuf[i];
                    if (c == '\n')
                    {
                        // Strip trailing '\r' for Windows CRLF sequences.
                        if (lineBuf.Length > 0 && lineBuf[^1] == '\r')
                            lineBuf.Length--;
                        _output(lineBuf.ToString(), kind);
                        lineBuf.Clear();
                    }
                    else if (c != '\r')
                    {
                        lineBuf.Append(c);
                    }
                }
            }
        }
        catch { /* stream closed when process exits */ }

        // Flush any remaining partial content.
        if (lineBuf.Length > 0)
            _output(lineBuf.ToString(), kind);
    }

    private static (string? exe, string args) ResolveShell(TerminalShellType shellType) =>
        shellType switch
        {
            TerminalShellType.PowerShell =>
                (SearchInPath("pwsh.exe") ?? SearchInPath("powershell.exe"), "-NoLogo -NoExit"),
            TerminalShellType.Bash =>
                (SearchInPath("bash.exe") ?? SearchInPath("wsl.exe"), string.Empty),
            TerminalShellType.Cmd =>
                (SearchInPath("cmd.exe") ?? Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe"), "/k"),
            _ => (null, string.Empty)
        };

    private static string? SearchInPath(string fileName)
    {
        if (File.Exists(fileName)) return Path.GetFullPath(fileName);
        var paths = Environment.GetEnvironmentVariable("PATH")?.Split(Path.PathSeparator) ?? [];
        foreach (var dir in paths)
        {
            var full = Path.Combine(dir.Trim(), fileName);
            if (File.Exists(full)) return full;
        }
        return null;
    }

    private static Encoding ParseEncoding(string name) => name switch
    {
        "Windows-1252" => Encoding.GetEncoding(1252),
        "ASCII"        => Encoding.ASCII,
        _              => new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)
    };
}
