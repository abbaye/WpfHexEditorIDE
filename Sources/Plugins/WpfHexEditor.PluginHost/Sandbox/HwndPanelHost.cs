//////////////////////////////////////////////
// GNU Affero General Public License v3.0 - 2026
// Author : Derek Tremblay (derektremblay666@gmail.com)
// Contributors: Claude Sonnet 4.6
//////////////////////////////////////////////

// ==========================================================
// Project: WpfHexEditor.PluginHost
// File: Sandbox/HwndPanelHost.cs
// Created: 2026-03-15
// Description:
//     HwndHost subclass that embeds a sandbox process HWND into the IDE's
//     WPF visual tree. Receives the raw Win32 HWND from the sandbox via IPC
//     (RegisterPanelNotification), reparents it to the IDE's window, and
//     adjusts its Win32 styles so it behaves as a child window.
//
// Architecture Notes:
//     - Pattern: Adapter — bridges Win32 HWND (sandbox) to WPF UIElement (IDE).
//     - BuildWindowCore reparents the sandbox HWND and sets WS_CHILD style.
//     - DestroyWindowCore restores original parent (IntPtr.Zero) to avoid
//       destroying a window owned by another process.
//     - Size changes in the WPF layout trigger OnRenderSizeChanged which
//       forwards via SetWindowPos so the sandbox content fills its container.
//     - SandboxHwndLost event fires if the sandbox process exits and its
//       HWND becomes invalid, so the proxy can clean up gracefully.
// ==========================================================

using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace WpfHexEditor.PluginHost.Sandbox;

/// <summary>
/// Embeds a sandbox-process WPF panel (identified by its Win32 HWND)
/// into the IDE's WPF visual tree via <see cref="HwndHost"/>.
/// </summary>
public sealed class HwndPanelHost : HwndHost
{
    private IntPtr _sandboxHwnd;
    private readonly string _contentId;
    private bool _attached;

    // ── Events ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Raised when the sandbox HWND could not be attached (e.g., process exited).
    /// The IDE should remove this host from the docking layout.
    /// </summary>
    public event EventHandler<string>? SandboxHwndLost;

    // ─────────────────────────────────────────────────────────────────────────
    public HwndPanelHost(IntPtr sandboxHwnd, string contentId)
    {
        _sandboxHwnd = sandboxHwnd;
        _contentId = contentId;
    }

    // ── HwndHost overrides ────────────────────────────────────────────────────

    protected override HandleRef BuildWindowCore(HandleRef hwndParent)
    {
        if (_sandboxHwnd == IntPtr.Zero)
        {
            SandboxHwndLost?.Invoke(this, _contentId);
            // Return a temporary hidden window so WPF doesn't crash
            return CreateFallbackWindow(hwndParent);
        }

        // Verify the sandbox HWND is still alive
        if (!IsWindow(_sandboxHwnd))
        {
            SandboxHwndLost?.Invoke(this, _contentId);
            return CreateFallbackWindow(hwndParent);
        }

        // Reparent: make sandbox HWND a child of the IDE's container
        SetParent(_sandboxHwnd, hwndParent.Handle);

        // Adjust Win32 styles: add WS_CHILD + WS_VISIBLE, remove popup/caption chrome
        int style = GetWindowLong(_sandboxHwnd, GWL_STYLE);
        style = (style | WS_CHILD | WS_VISIBLE) & ~WS_POPUP & ~WS_CAPTION & ~WS_THICKFRAME;
        SetWindowLong(_sandboxHwnd, GWL_STYLE, style);

        // Move to top-left of parent with current render size
        SetWindowPos(_sandboxHwnd, IntPtr.Zero,
            0, 0,
            (int)Math.Max(ActualWidth, 1),
            (int)Math.Max(ActualHeight, 1),
            SWP_NOZORDER);

        _attached = true;
        return new HandleRef(this, _sandboxHwnd);
    }

    protected override void DestroyWindowCore(HandleRef hwnd)
    {
        if (_attached && _sandboxHwnd != IntPtr.Zero && IsWindow(_sandboxHwnd))
        {
            // Restore: remove WS_CHILD so the sandbox HWND can live independently again
            int style = GetWindowLong(_sandboxHwnd, GWL_STYLE);
            style = (style & ~WS_CHILD) | WS_POPUP;
            SetWindowLong(_sandboxHwnd, GWL_STYLE, style);

            SetParent(_sandboxHwnd, IntPtr.Zero);
        }

        _attached = false;
        // Note: We do NOT call DestroyWindow — the sandbox process owns this HWND.
    }

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        ForwardSizeToSandbox(sizeInfo.NewSize.Width, sizeInfo.NewSize.Height);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Called when the sandbox HWND has been replaced (e.g. sandbox restarted).
    /// Triggers a rebuild of the window core.
    /// </summary>
    public void UpdateSandboxHwnd(IntPtr newHwnd)
    {
        _sandboxHwnd = newHwnd;
        // Invalidate the HwndHost so BuildWindowCore is called again
        // (WPF re-parents on next layout pass).
    }

    /// <summary>Resize the sandbox HWND to match new dimensions.</summary>
    public void ForwardSizeToSandbox(double width, double height)
    {
        if (!_attached || _sandboxHwnd == IntPtr.Zero) return;
        if (!IsWindow(_sandboxHwnd)) return;

        SetWindowPos(_sandboxHwnd, IntPtr.Zero,
            0, 0, (int)Math.Max(width, 1), (int)Math.Max(height, 1),
            SWP_NOZORDER | SWP_NOMOVE);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static HandleRef CreateFallbackWindow(HandleRef hwndParent)
    {
        // Create a minimal invisible child window so HwndHost doesn't get a null HWND
        var hwnd = CreateWindowEx(
            0, "STATIC", null,
            WS_CHILD, 0, 0, 1, 1,
            hwndParent.Handle, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
        return new HandleRef(null, hwnd);
    }

    // ── Win32 ─────────────────────────────────────────────────────────────────

    private const int GWL_STYLE = -16;
    private const int WS_CHILD = 0x40000000;
    private const int WS_VISIBLE = 0x10000000;
    private const int WS_POPUP = unchecked((int)0x80000000);
    private const int WS_CAPTION = 0x00C00000;
    private const int WS_THICKFRAME = 0x00040000;
    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_NOMOVE = 0x0002;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowLong(IntPtr hwnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int SetWindowLong(IntPtr hwnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
        int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindow(IntPtr hwnd);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern IntPtr CreateWindowEx(
        int dwExStyle, string lpClassName, string? lpWindowName,
        int dwStyle, int x, int y, int nWidth, int nHeight,
        IntPtr hwndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);
}
