// ==========================================================
// Project: WpfHexEditor.PluginHost
// File: Sandbox/SandboxJobObject.cs
// Author: Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
// Created: 2026-03-15
// Description:
//     Process-lifetime guardian for all sandbox child processes.
//     Uses a Win32 Job Object configured with KILL_ON_JOB_CLOSE so that
//     every WpfHexEditor.PluginSandbox.exe is automatically terminated by
//     Windows when the host process exits — regardless of whether the
//     shutdown path (OnWindowClosing / DisposeAsync) runs or not.
//
// Architecture Notes:
//     Singleton — one job object for the lifetime of the host process.
//     The job handle is intentionally kept open until the process exits;
//     Windows then closes it automatically and kills all assigned processes.
//     P/Invoke only — no third-party dependencies.
// ==========================================================

using System.Diagnostics;
using System.Runtime.InteropServices;

namespace WpfHexEditor.PluginHost.Sandbox;

/// <summary>
/// Wraps a Win32 Job Object that kills all assigned child processes when
/// the host process terminates for any reason (crash, debugger stop, kill).
/// </summary>
internal static class SandboxJobObject
{
    // ── Win32 P/Invoke ────────────────────────────────────────────────────────

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern nint CreateJobObject(nint lpJobAttributes, string? lpName);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetInformationJobObject(
        nint hJob,
        JobObjectInfoClass jobObjectInfoClass,
        ref JobObjectExtendedLimitInformation lpJobObjectInfo,
        int cbJobObjectInfoLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AssignProcessToJobObject(nint hJob, nint hProcess);

    private enum JobObjectInfoClass
    {
        ExtendedLimitInformation = 9
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JobObjectExtendedLimitInformation
    {
        public JobObjectBasicLimitInformation BasicLimitInformation;
        public IoCounters IoInfo;
        public nuint ProcessMemoryLimit;
        public nuint JobMemoryLimit;
        public nuint PeakProcessMemoryUsed;
        public nuint PeakJobMemoryUsed;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JobObjectBasicLimitInformation
    {
        public long PerProcessUserTimeLimit;
        public long PerJobUserTimeLimit;
        public uint LimitFlags;
        public nuint MinimumWorkingSetSize;
        public nuint MaximumWorkingSetSize;
        public uint ActiveProcessLimit;
        public nuint Affinity;
        public uint PriorityClass;
        public uint SchedulingClass;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IoCounters
    {
        public ulong ReadOperationCount;
        public ulong WriteOperationCount;
        public ulong OtherOperationCount;
        public ulong ReadTransferCount;
        public ulong WriteTransferCount;
        public ulong OtherTransferCount;
    }

    // Kill all processes in job when the job object handle is closed.
    private const uint JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE = 0x00002000;

    // ── Singleton job handle ──────────────────────────────────────────────────

    private static readonly nint _jobHandle = CreateJobObjectHandle();

    private static nint CreateJobObjectHandle()
    {
        var handle = CreateJobObject(nint.Zero, null);
        if (handle == nint.Zero) return nint.Zero;

        var info = new JobObjectExtendedLimitInformation();
        info.BasicLimitInformation.LimitFlags = JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE;

        SetInformationJobObject(
            handle,
            JobObjectInfoClass.ExtendedLimitInformation,
            ref info,
            Marshal.SizeOf<JobObjectExtendedLimitInformation>());

        return handle;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Assigns <paramref name="process"/> to the sandbox job object so that it is
    /// automatically killed when the host process exits.
    /// Safe to call even if job object creation failed (no-op in that case).
    /// </summary>
    public static void Assign(Process process)
    {
        if (_jobHandle == nint.Zero) return;
        try
        {
            AssignProcessToJobObject(_jobHandle, process.Handle);
        }
        catch
        {
            // Best-effort: if assignment fails (e.g., process already in another job)
            // the graceful shutdown path in ForceKillAsync will still terminate it.
        }
    }
}
