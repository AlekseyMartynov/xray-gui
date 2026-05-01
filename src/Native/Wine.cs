using System.Runtime.InteropServices;

namespace Project;

static partial class Wine {
    public static readonly bool IsDetected = Detect();

    static bool Detect() {
#if DEBUG
        try {
            return wine_get_version() != default;
        } catch(EntryPointNotFoundException) {
            return false;
        } catch(DllNotFoundException) {
            return false;
        }
#else
        return false;
#endif
    }

    [LibraryImport("ntdll", SetLastError = true)]
    private static partial nint wine_get_version();
}
