using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
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
        var disposition = default(REG_CREATE_KEY_DISPOSITION);

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
                &result,
                &disposition
            )
        );

        return result;
    }

    public static unsafe IReadOnlyList<string> GetSubKeyNames(HKEY parent) {
        var result = new List<string>();
        var index = default(uint);
        var keyNameLen = default(uint);
        var keyNameBuf = (stackalloc char[KEY_NAME_CHAR_BUF_LEN]);
        while(true) {
            keyNameLen = KEY_NAME_CHAR_BUF_LEN;
            var error = PInvoke.RegEnumKeyEx(
                parent,
                index,
                keyNameBuf,
                // https://learn.microsoft.com/windows/win32/api/winreg/nf-winreg-regenumkeyexw#parameters
                // in  - should include the terminating null character
                // out -  not including the terminating null character
                ref keyNameLen,
                default,
                default,
                default
            );
            if(error == WIN32_ERROR.ERROR_NO_MORE_ITEMS) {
                break;
            }
            NativeUtils.MustSucceed(error);
            result.Add(keyNameBuf.Slice(0, (int)keyNameLen).ToString());
            index++;
        }
        return result;
    }

    public static object? GetValue(HKEY parent, string subKey, string name) {
        uint size = MAX_ON_STACK_BYTES;

        var ok = TryGetValue(parent, subKey, name, out var result, ref size);

        if(!ok) {
            ok = TryGetValue(parent, subKey, name, out result, ref size);
        }

        if(!ok) {
            throw new InvalidOperationException();
        }

        return result;
    }

    static unsafe bool TryGetValue(HKEY parent, string subKey, string name, out object? result, ref uint size) {
        var buf = size <= MAX_ON_STACK_BYTES ? stackalloc byte[(int)size] : new byte[size];
        var type = default(REG_VALUE_TYPE);
        fixed(uint* sizePtr = &size) {
            var error = PInvoke.RegGetValue(
                parent,
                subKey,
                name,
                REG_ROUTINE_FLAGS.RRF_RT_ANY,
                &type,
                Unsafe.AsPointer(ref buf[0]),
                sizePtr
            );
            if(error == WIN32_ERROR.ERROR_MORE_DATA) {
                result = default;
                return false;
            }
            if(error == WIN32_ERROR.ERROR_FILE_NOT_FOUND) {
                result = default;
                return true;
            }
            NativeUtils.MustSucceed(error);
        }
        if(type == REG_VALUE_TYPE.REG_SZ) {
            var charCount = (int)size / 2 - 1;
            result = MemoryMarshal.Cast<byte, char>(buf)
                .Slice(0, charCount)
                .ToString();
            return true;
        }
        throw new NotSupportedException();
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
