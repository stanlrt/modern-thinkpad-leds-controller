using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;

namespace ModernThinkPadLEDsController.Hardware.PawnIo;

/// <summary>
/// Provides low-level access to the PawnIO kernel driver.
/// Extracted from LibreHardwareMonitor to avoid full library dependency.
/// </summary>
/// <remarks>
/// This code is derived from LibreHardwareMonitor (MPL-2.0 license).
/// Original source: https://github.com/LibreHardwareMonitor/LibreHardwareMonitor
/// </remarks>
internal sealed partial class PawnIoDriver : IDisposable
{
    private const uint DEVICE_TYPE = 41394u << 16;
    private const int FN_NAME_LENGTH = 32;
    private const uint IOCTL_PIO_EXECUTE_FN = 0x841 << 2;
    private const uint IOCTL_PIO_LOAD_BINARY = 0x821 << 2;

    private readonly SafeFileHandle _handle;
    private bool _disposed;

    static PawnIoDriver()
    {
        using RegistryKey? subKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\PawnIO");

        if (Version.TryParse(subKey?.GetValue("DisplayVersion") as string, out Version? version))
        {
            Version = version;
        }
        else
        {
            using RegistryKey registryKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            using RegistryKey? subKeyWow64 = registryKey.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\PawnIO");

            if (Version.TryParse(subKeyWow64?.GetValue("DisplayVersion") as string, out version))
            {
                Version = version;
            }
        }
    }

    private PawnIoDriver(SafeFileHandle handle) => _handle = handle;

    /// <summary>
    /// Gets a value indicating whether PawnIO is installed on the system.
    /// </summary>
    public static bool IsInstalled => Version is not null;

    /// <summary>
    /// Retrieves the version information for the installed PawnIO.
    /// </summary>
    public static Version? Version { get; }

    /// <summary>
    /// Gets a value indicating whether the underlying handle is currently valid and open.
    /// </summary>
    public bool IsLoaded => _handle is { IsInvalid: false, IsClosed: false };

    /// <summary>
    /// Loads a PawnIO module from an embedded resource.
    /// </summary>
    internal static unsafe PawnIoDriver LoadModuleFromResource(Assembly assembly, string resourceName)
    {
        // Open handle to PawnIO device
        SafeFileHandle handle = CreateFileW(
            @"\\?\GLOBALROOT\Device\PawnIO",
            FileAccess.ReadWrite,
            FileShare.Read | FileShare.Write,
            IntPtr.Zero,
            FileMode.Open,
            0,
            IntPtr.Zero);

        if (handle.IsInvalid)
        {
            return new PawnIoDriver(handle);
        }

        // Load binary module from embedded resource
        using Stream? stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
        {
            return new PawnIoDriver(handle);
        }

        using MemoryStream memory = new();
        stream.CopyTo(memory);
        byte[] bin = memory.ToArray();

        fixed (byte* pIn = bin)
        {
            uint bytesReturned = 0;
            if (DeviceIoControl(handle.DangerousGetHandle(), (uint)ControlCode.LoadBinary, pIn, (uint)bin.Length, null, 0u, ref bytesReturned, IntPtr.Zero))
            {
                return new PawnIoDriver(handle);
            }
        }

        return new PawnIoDriver(new SafeFileHandle(IntPtr.Zero, true));
    }

    /// <summary>
    /// Executes a function on the loaded PawnIO module.
    /// </summary>
    public unsafe long[] Execute(string name, long[] input, int outLength)
    {
        if (!IsLoaded)
        {
            return new long[outLength];
        }

        byte[] output = new byte[outLength * sizeof(long)];
        byte[] totalInput = new byte[(input.Length * sizeof(long)) + FN_NAME_LENGTH];
        Buffer.BlockCopy(Encoding.ASCII.GetBytes(name), 0, totalInput, 0, Math.Min(FN_NAME_LENGTH - 1, name.Length));
        Buffer.BlockCopy(input, 0, totalInput, FN_NAME_LENGTH, input.Length * sizeof(long));

        uint bytesReturned = 0;

        fixed (byte* pIn = totalInput, pOut = output)
        {
            if (DeviceIoControl(_handle.DangerousGetHandle(), (uint)ControlCode.Execute, pIn, (uint)totalInput.Length, pOut, (uint)output.Length, ref bytesReturned, IntPtr.Zero))
            {
                long[] outp = new long[bytesReturned / sizeof(long)];
                Buffer.BlockCopy(output, 0, outp, 0, (int)bytesReturned);
                return outp;
            }
        }

        return new long[outLength];
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (IsLoaded)
        {
            _handle.Close();
        }
    }

    private enum ControlCode : uint
    {
        LoadBinary = DEVICE_TYPE | IOCTL_PIO_LOAD_BINARY,
        Execute = DEVICE_TYPE | IOCTL_PIO_EXECUTE_FN
    }

    /// <summary>
    /// P/Invoke declarations for Windows API functions.
    /// </summary>
    [LibraryImport("kernel32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    private static partial SafeFileHandle CreateFileW(
        string lpFileName,
        FileAccess dwDesiredAccess,
        FileShare dwShareMode,
        IntPtr lpSecurityAttributes,
        FileMode dwCreationDisposition,
        uint dwFlagsAndAttributes,
        IntPtr hTemplateFile);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static unsafe partial bool DeviceIoControl(
        IntPtr hDevice,
        uint dwIoControlCode,
        void* lpInBuffer,
        uint nInBufferSize,
        void* lpOutBuffer,
        uint nOutBufferSize,
        ref uint lpBytesReturned,
        IntPtr lpOverlapped);
}
