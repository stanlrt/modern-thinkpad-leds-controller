using System.Runtime.InteropServices;

namespace ModernThinkPadLEDsController.Hardware;

// InpOutDriver wraps the three functions we need from inpoutx64.dll.
//
// DllImport ("Platform Invoke" / P/Invoke) tells the .NET runtime:
//   "When this method is called, load inpoutx64.dll and call that C++ function."
// The runtime finds the DLL next to the .exe in the output folder.
//
// IsInpOutDriverOpen() also self-installs the kernel service on first call —
// that is why requireAdministrator in the manifest is mandatory.
public sealed class InpOutDriver : IPortIO, IDisposable
{
    [DllImport("inpoutx64.dll", EntryPoint = "IsInpOutDriverOpen")]
    private static extern uint IsInpOutDriverOpen();

    [DllImport("inpoutx64.dll", EntryPoint = "Inp32")]
    private static extern byte Inp32(ushort portAddress);

    [DllImport("inpoutx64.dll", EntryPoint = "Out32")]
    private static extern void Out32(ushort portAddress, byte data);

    [DllImport("inpoutx64.dll", EntryPoint = "CleanupInpOutXLib")]
    private static extern void CleanupInpOutXLib();

    private bool _disposed;

    // TryOpen is the only way to create an InpOutDriver.
    // It returns false if the driver cannot be initialised (e.g. DLL missing).
    // 'out InpOutDriver? driver' is an output parameter — the caller gets the
    // object back through it when the return value is true.
    public static bool TryOpen(out InpOutDriver? driver)
    {
        driver = null;
        try
        {
            if (IsInpOutDriverOpen() == 0)
                return false;

            driver = new InpOutDriver();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private InpOutDriver() { }

    public byte ReadByte(ushort port) => Inp32(port);
    public void WriteByte(ushort port, byte data) => Out32(port, data);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        CleanupInpOutXLib();
    }
}
