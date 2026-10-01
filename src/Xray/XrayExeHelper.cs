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
            File.WriteAllText(VersionCachePath, version);
            File.SetLastWriteTime(VersionCachePath, DateTime.FromFileTimeUtc(exeInfo.ChangeTime));
        }
        if(version != RequiredVersion) {
            throw new UIException(
                "Xray " + RequiredVersion + " is required\n" +
                "Detected version: " + version
            );
        }
    }

    static string ExecVersionCommand() {
        var bufLen = 16;
        var byteBuf = (stackalloc byte[bufLen]);
        var charBuf = (stackalloc char[bufLen]);
        using var proc = new NativeProcess(
            ExePath.Quote() + " --version",
            accessToken: NativeRestrictedTokens.Constrained,
            redirectOutput: true
        );
        using var stream = proc.OpenOutput();
        if(stream.Read(byteBuf) == bufLen) {
            Encoding.ASCII.GetChars(byteBuf, charBuf);
            var index = 0;
            foreach(var r in MemoryExtensions.Split(charBuf, ' ')) {
                if(index == 0 && !charBuf[r].SequenceEqual("Xray")) {
                    break;
                }
                if(index == 1) {
                    return charBuf[r].ToString();
                }
                index++;
            }
        }
        return "unknown";
    }
}
