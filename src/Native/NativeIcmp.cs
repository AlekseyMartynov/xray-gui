using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.NetworkManagement.IpHelper;

namespace Project;

class NativeIcmp : IDisposable {
    readonly HANDLE Handle;

    public NativeIcmp() {
        Handle = PInvoke.IcmpCreateFile();
        NativeUtils.CheckHandle(Handle);
    }

    public unsafe bool Ping(NativeIPAddress ip) {
        if(!ip.IsIPv4()) {
            throw new NotSupportedException();
        }

        var dest = default(uint);
        ip.TryWriteBytes(NativeUtils.Cast<uint, byte>(ref dest));

        var reqSize = 4;
        var resSize = Marshal.SizeOf<ICMP_ECHO_REPLY>() + reqSize;

        var req = stackalloc byte[reqSize];
        var res = stackalloc byte[resSize];

        //Console.WriteLine("ping " + ip);

        var replyCount = PInvoke.IcmpSendEcho(
            Handle,
            dest,
            req, (ushort)reqSize,
            default,
            res, (uint)resSize,
            1000
        );

        return replyCount > 0;
    }

    public void Dispose() {
        if(!Handle.IsNull) {
            PInvoke.IcmpCloseHandle(Handle);
        }
    }
}
