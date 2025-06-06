using System.ComponentModel;
using System.Runtime.InteropServices;
using Windows.Win32.Foundation;

namespace Project;

static class NativeUtils {

    public static void MustSucceed(BOOL result) {
        if(result.Value == 0) {
            ThrowLastWin32Error();
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
}
