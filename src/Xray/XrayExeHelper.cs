using System.Text;

namespace Project;

static class XrayExeHelper {
    const string RequiredVersion = "26.3.27";

    public static readonly string ExePath;

    static readonly string VersionCachePath;

    static XrayExeHelper() {
        var appDir = AppContext.BaseDirectory;
        ExePath = Path.Join(appDir, "xray.exe");
        VersionCachePath = Path.Join(appDir, "xray-version");
    }

    public static void Validate() {
        if(!NativeUtils.TryGetFileBasicInfo(ExePath, out var exeInfo)) {
            throw new UIException(
                "Missing " + ExePath + "\n" +
                "Download it from github.com/XTLS/Xray-core/releases/tag/v" + RequiredVersion
            );
        }
        string version;
        if(NativeUtils.TryGetFileBasicInfo(VersionCachePath, out var versionInfo) && exeInfo.ChangeTime == versionInfo.LastWriteTime) {
            version = File.ReadAllText(VersionCachePath);
        } else {
            version = ExecVersionCommand();
            try {
                File.WriteAllText(VersionCachePath, version);
                File.SetLastWriteTime(VersionCachePath, DateTime.FromFileTimeUtc(exeInfo.ChangeTime));
            } catch {
                // Treat cache persistence as best-effort
            }
        }
        if(version != RequiredVersion) {
            throw new UIException(
                "Xray " + RequiredVersion + " is required\n" +
                "Detected version: " + version
            );
        }
    }

    static string ExecVersionCommand() {
        var buf = (stackalloc byte[16]);
        using var proc = new NativeProcess(
            ExePath.Quote() + " --version",
            accessToken: NativeRestrictedTokens.Constrained,
            redirectOutput: true
        );
        using var stream = proc.OpenOutput();
        if(stream.Read(buf) == buf.Length) {
            if(buf.StartsWith<byte>([88, 114, 97, 121, 32])) {
                buf = buf.Slice(5);
                var endIndex = buf.IndexOf<byte>(32);
                if(endIndex > -1) {
                    return Encoding.ASCII.GetString(buf[..endIndex]);
                }
            }
        }
        return "unknown";
    }
}
