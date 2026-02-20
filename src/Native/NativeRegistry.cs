using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Registry;

namespace Project;

static class NativeRegistry {
    // https://learn.microsoft.com/windows/win32/sysinfo/registry-element-size-limits
    const int KEY_NAME_CHAR_BUF_LEN = 255 + 1;

    const int MAX_ON_STACK_BYTES = 512;

    public static unsafe HKEY OpenOrCreateKey(HKEY parent, string subKey, bool writable = false) {
        var result = default(HKEY);

        var sam = REG_SAM_FLAGS.KEY_READ;
        if(writable) {
            sam |= REG_SAM_FLAGS.KEY_WRITE;
        }

        NativeUtils.MustSucceed(
            PInvoke.RegCreateKeyEx(
                parent,
                subKey,
                default,
                REG_OPEN_CREATE_OPTIONS.REG_OPTION_NON_VOLATILE,
                sam,
                default,
                &result
            )
        );

        return result;
    }

    public static void SetValue(HKEY key, string name, uint value) {
        var bytes = (stackalloc byte[4]);
        BitConverter.TryWriteBytes(bytes, value);
        NativeUtils.MustSucceed(
            PInvoke.RegSetValueEx(key, name, REG_VALUE_TYPE.REG_DWORD, bytes)
        );
    }

    public static unsafe void SetValue(HKEY key, string name, string value) {
        fixed(char* namePtr = name)
        fixed(char* valuePtr = value) {
            // https://github.com/dotnet/runtime/blob/v9.0.6/src/libraries/System.Private.CoreLib/src/Internal/Win32/RegistryKey.cs#L408-L413
            var byteCount = 2 * (value.Length + 1);
            NativeUtils.MustSucceed(
                PInvoke.RegSetValueEx(key, namePtr, default, REG_VALUE_TYPE.REG_SZ, (byte*)valuePtr, (uint)byteCount)
            );
        }
    }

    public static void DeleteTree(HKEY parent, string subKey, bool throwOnMissing) {
        ArgumentException.ThrowIfNullOrWhiteSpace(subKey);

        var error = PInvoke.RegDeleteTree(parent, subKey);

        if(!throwOnMissing && error == WIN32_ERROR.ERROR_FILE_NOT_FOUND) {
            return;
        }

        NativeUtils.MustSucceed(error);
    }
}
