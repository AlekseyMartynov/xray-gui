using System.Runtime.InteropServices;

#pragma warning disable SYSLIB1054

namespace Project;

static class Wintun {
    public const string Name = "xray-gui";

    public static Guid Guid { get; private set; }

    static nint AdapterHandle;

    public static unsafe void EnsureCreated() {
        if(AdapterHandle != 0) {
            return;
        }
        var guid = new Guid(0xc4c131fd, 0xf701, 0x4210, 0x9c, 0x06, 0xf5, 0x96, 0x01, 0xda, 0x1b, 0x36);
        var result = IntPtr.Zero;
        fixed(char* namePtr = Name) {
            AdapterHandle = WintunCreateAdapter(namePtr, namePtr, &guid);
        }
        NativeUtils.CheckHandle(AdapterHandle);
        Guid = guid;
    }

    public static void EnsureClosed() {
        if(AdapterHandle == 0) {
            return;
        }
        WintunCloseAdapter(AdapterHandle);
        AdapterHandle = 0;
    }

    [DllImport("wintun", ExactSpelling = true, SetLastError = true)]
    static extern unsafe nint WintunCreateAdapter(char* name, char* tunnelType, Guid* guid);

    [DllImport("wintun", ExactSpelling = true, SetLastError = true)]
    static extern unsafe void WintunCloseAdapter(nint h);
}
