using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace PhoenixmlDb.Core.Schema;

/// <summary>
/// The path of a file that is already open, with every link resolved, where the operating system
/// can report it. Deciding on that path, and not on the name a file was opened by, leaves no gap
/// between the check and the open.
/// </summary>
internal static class OpenFilePath
{
    /// <summary>The path, or null where it cannot be had (then the caller falls back).</summary>
    public static string? TryGet(FileStream stream)
    {
        try
        {
            if (OperatingSystem.IsLinux())
            {
                var link = "/proc/self/fd/" + stream.SafeFileHandle.DangerousGetHandle().ToInt64()
                    .ToString(System.Globalization.CultureInfo.InvariantCulture);
                return File.ResolveLinkTarget(link, returnFinalTarget: false)?.FullName;
            }
            if (OperatingSystem.IsWindows())
                return WindowsFinalPath(stream.SafeFileHandle);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
        return null;
    }

    private static string? WindowsFinalPath(SafeFileHandle handle)
    {
        var buffer = new char[1024];
        var length = GetFinalPathNameByHandleW(handle, buffer, (uint)buffer.Length, 0);
        if (length > buffer.Length)
        {
            buffer = new char[length];
            length = GetFinalPathNameByHandleW(handle, buffer, (uint)buffer.Length, 0);
        }
        if (length == 0 || length > buffer.Length)
            return null;
        var path = new string(buffer, 0, (int)length);
        // \\?\C:\dir\file and \\?\UNC\server\share\file
        if (path.StartsWith(@"\\?\UNC\", StringComparison.Ordinal))
            return @"\\" + path[8..];
        return path.StartsWith(@"\\?\", StringComparison.Ordinal) ? path[4..] : path;
    }

#pragma warning disable SYSLIB1054 // LibraryImport needs unsafe code, which this assembly does not allow
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern uint GetFinalPathNameByHandleW(SafeFileHandle hFile, [Out] char[] lpszFilePath,
        uint cchFilePath, uint dwFlags);
#pragma warning restore SYSLIB1054
}
