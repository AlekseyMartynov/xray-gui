using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Windows.Win32.Foundation;

namespace Project;

static class NativeUtils {

    // https://www.rfc-editor.org/rfc/rfc2133.txt
    // https://gitlab.com/wireshark/wireshark/-/blob/v4.2.12/wsutil/inet_addr.h#L18-49
    public const int INET_ADDRSTRLEN = 16;
    public const int INET6_ADDRSTRLEN = 46;

    public static void MustSucceed(BOOL result) {
        if(result.Value == 0) {
            ThrowLastWin32Error();
        }
    }

    public static void MustSucceed(WIN32_ERROR result) {
        MustSucceed((uint)result);
    }

    public static void MustSucceed(uint result) {
        if(result == (uint)WIN32_ERROR.ERROR_ACCESS_DENIED) {
            throw new UIException("Restart as Administrator");
        }
        if(result != 0) {
            throw new Win32Exception((int)result);
        }
    }

    public static unsafe void CheckHandle(HANDLE h) {
        CheckHandle(h.Value);
    }

    public static unsafe void CheckHandle(void* h) {
        if(h == null) {
            ThrowLastWin32Error();
        }
    }

    static void ThrowLastWin32Error() {
        throw new Win32Exception(Marshal.GetLastWin32Error());
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Span<T> Cast<S, T>(scoped ref S source) where S : struct where T : struct {
        var sourceSpan = MemoryMarshal.CreateSpan(ref source, 1);
        return MemoryMarshal.Cast<S, T>(sourceSpan);
    }

    public static Guid ParseGuid(PSTR text) {
        try {
            var span = text.AsSpan();
            var len = span.Length;
            var chars = (stackalloc char[len]);
            for(var i = 0; i < len; i++) {
                chars[i] = (char)span[i];
            }
            return Guid.Parse(chars);
        } catch(FormatException) {
            return Guid.Empty;
        }
    }

    public static unsafe PCWSTR MAKEINTRESOURCE(int id) {
        // https://stackoverflow.com/q/3610565
        return (PCWSTR)(char*)id;
    }
}
