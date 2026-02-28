# Core/Native

Platform Invoke (P/Invoke) wrappers for native Windows API calls.

## 📁 Contents

- **[NativeMethods.cs](NativeMethods.cs)** - Windows API interop
  - File I/O operations (CreateFile, ReadFile, WriteFile)
  - Memory management (VirtualAlloc, VirtualFree)
  - Window management (SetWindowPos, GetWindowRect)
  - Clipboard operations (OpenClipboard, SetClipboardData)
  - Scrolling and caret positioning
  - High-performance file operations

## 🎯 Purpose

This folder contains P/Invoke declarations for Windows API functions needed for high-performance binary file editing. Native methods are used when managed .NET APIs are insufficient for performance or functionality.

## 🔗 Platform

- **Target**: Windows only (uses kernel32.dll, user32.dll)
- **Architecture**: x86, x64, ARM64 (platform-agnostic P/Invoke)
- **Safety**: Uses `[DllImport]` with appropriate marshalling

## 🎓 Usage Example

```csharp
// File operations with native API
[DllImport("kernel32.dll", SetLastError = true)]
private static extern IntPtr CreateFile(
    string lpFileName,
    uint dwDesiredAccess,
    uint dwShareMode,
    IntPtr lpSecurityAttributes,
    uint dwCreationDisposition,
    uint dwFlagsAndAttributes,
    IntPtr hTemplateFile);

// Usage
IntPtr handle = CreateFile(
    @"C:\data\largefile.bin",
    GENERIC_READ | GENERIC_WRITE,
    FILE_SHARE_READ,
    IntPtr.Zero,
    OPEN_EXISTING,
    FILE_FLAG_SEQUENTIAL_SCAN,
    IntPtr.Zero);

if (handle != INVALID_HANDLE_VALUE)
{
    // Perform operations
    CloseHandle(handle);
}
```

## ⚡ Performance Benefits

### Why Use Native APIs?

1. **Large File Support**: Better memory mapping for multi-GB files
2. **Direct Access**: Bypass managed overhead for I/O operations
3. **Advanced Features**: Access Windows features not in .NET
4. **Optimization**: Fine-tuned control over caching and buffering

### Specific Use Cases:

- **Memory-Mapped Files**: For files larger than available RAM
- **Unbuffered I/O**: Direct disk access for maximum speed
- **File Locking**: Precise control over file sharing modes
- **Caret Position**: Smooth cursor movement in hex view

## 🛡️ Safety Considerations

```csharp
// Always check for errors
IntPtr handle = CreateFile(...);
if (handle == INVALID_HANDLE_VALUE)
{
    int error = Marshal.GetLastWin32Error();
    throw new Win32Exception(error);
}

// Always clean up resources
try
{
    // Use handle
}
finally
{
    if (handle != IntPtr.Zero && handle != INVALID_HANDLE_VALUE)
        CloseHandle(handle);
}

// Or use SafeHandle
public class SafeFileHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    public SafeFileHandle() : base(true) { }

    protected override bool ReleaseHandle() => CloseHandle(handle);
}
```

## 📋 Common P/Invoke Patterns

### String Marshaling:
```csharp
[DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
private static extern bool GetFileAttributesEx(
    string lpFileName,
    GET_FILEEX_INFO_LEVELS fInfoLevelId,
    out WIN32_FILE_ATTRIBUTE_DATA fileData);
```

### Structure Marshaling:
```csharp
[StructLayout(LayoutKind.Sequential)]
public struct RECT
{
    public int Left;
    public int Top;
    public int Right;
    public int Bottom;
}

[DllImport("user32.dll")]
private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
```

### Callback Functions:
```csharp
public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

[DllImport("user32.dll")]
private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
```

## 🔍 Debugging Native Calls

```csharp
// Enable SetLastError for debugging
[DllImport("kernel32.dll", SetLastError = true)]
private static extern IntPtr CreateFile(...);

// Check for errors
IntPtr handle = CreateFile(...);
if (handle == INVALID_HANDLE_VALUE)
{
    int error = Marshal.GetLastWin32Error();
    string message = new Win32Exception(error).Message;
    Debug.WriteLine($"CreateFile failed: {message} (error code: {error})");
}
```

## 📚 Common Windows API Functions Used

### File Operations:
- `CreateFile` - Open/create files with advanced options
- `ReadFile` - Read from file handles
- `WriteFile` - Write to file handles
- `SetFilePointer` - Seek within files
- `GetFileSize` - Get file size

### Memory Operations:
- `VirtualAlloc` - Allocate memory pages
- `VirtualFree` - Free memory pages
- `VirtualLock` - Lock pages in RAM

### Window/UI Operations:
- `GetWindowRect` - Get window dimensions
- `SetWindowPos` - Move/resize windows
- `InvalidateRect` - Request window redraw

## 🎨 Best Practices

1. **Use SafeHandle**: Automatic cleanup, exception-safe
2. **Set CharSet**: Unicode for modern Windows APIs
3. **Set SetLastError**: Enable error checking
4. **Validate Inputs**: Check for invalid handles/pointers
5. **Document Safety**: Comment on thread-safety and reentrancy
6. **Test Thoroughly**: P/Invoke bugs can crash the application

## ⚠️ Security Notes

- **Validate all inputs** before P/Invoke calls
- **Never trust external data** passed to native methods
- **Use SecurityCritical** attribute where appropriate
- **Audit for buffer overflows** and memory corruption
- **Minimize attack surface** by using minimal native APIs

## 🔗 Related Components

- **[ByteProvider.cs](../Bytes/ByteProvider.cs)** - Uses native file I/O
- **[HexEditor.xaml.cs](../../HexEditor.xaml.cs)** - Uses native window APIs
- **[FastTextLine.xaml.cs](../FastTextLine.xaml.cs)** - Uses native text rendering

## 📖 Resources

- [P/Invoke Documentation](https://docs.microsoft.com/en-us/dotnet/standard/native-interop/pinvoke)
- [Windows API Reference](https://docs.microsoft.com/en-us/windows/win32/api/)
- [PInvoke.net](https://pinvoke.net/) - P/Invoke signatures database

---

✨ Native Windows API interop for high-performance binary file operations
