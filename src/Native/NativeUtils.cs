using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Windows.Win32.Foundation;

namespace Project;

static class NativeUtils {

    public static void MustSucceed(BOOL result) {
        if(result.Value == 0) {
            ThrowLastWin32Error();
        }
    }

    public static void MustSucceed(WIN32_ERROR result) {
        MustSucceed((uint)result);
    }

    public static void MustSucceed(uint result) {
        if(result != 0) {
            throw new Win32Exception((int)result);
        }
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
}
