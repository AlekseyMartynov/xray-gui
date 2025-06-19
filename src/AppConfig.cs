namespace Project;

static class AppConfig {
    const string
        KEY_SELECTED_SERVER = "selected_server",
        KEY_PROC_CONSOLE = "proc_console",
        KEY_TAP_MODE = "tap_mode",
        KEY_PROXY = "proxy",
        KEY_SIP003_PORT = "sip003_port";

    public static readonly string FilePath = Path.Join(AppContext.BaseDirectory, "xray-gui.ini");

    static AppConfig() {
        Reset();
    }

    public static int SelectedServerIndex { get; set; }

    public static bool ProcConsole { get; set; }
    public static bool TapMode { get; set; }

    public static string ProxyAddr { get; private set; } = "";
    public static int ProxyPort { get; private set; }
    public static string Proxy => ProxyAddr + ':' + ProxyPort;

    public static string SIP003Addr { get; private set; } = "";
    public static int SIP003Port { get; private set; }

    public static void Reset() {
        SelectedServerIndex = -1;
        ProcConsole = false;
        TapMode = false;
        (ProxyAddr, ProxyPort) = ("127.0.0.1", 1080);
        (SIP003Addr, SIP003Port) = ("127.0.0.1", 1984);
    }

    public static void Load() {
        if(!File.Exists(FilePath)) {
            Save();
        }
        var fileText = File.ReadAllText(FilePath);
        foreach(var line in fileText.AsSpan().EnumerateLines()) {
            if(line.IsWhiteSpace()) {
                continue;
            }
            if(line.TrySplit('=', out var key, out var value)) {
                key = key.Trim();
                value = value.Trim();
                if(key.SequenceEqual(KEY_SELECTED_SERVER)) {
                    if(int.TryParse(value, out var index)) {
                        SelectedServerIndex = index;
                    }
                    continue;
                }
                if(key.SequenceEqual(KEY_PROC_CONSOLE)) {
                    ProcConsole = ParseFlag(value);
                    continue;
                }
                if(key.SequenceEqual(KEY_TAP_MODE)) {
                    TapMode = ParseFlag(value);
                    continue;
                }
                if(key.SequenceEqual(KEY_PROXY)) {
                    if(value.TrySplit(':', out var addr, out var portText)) {
                        ProxyAddr = addr.ToString();
                        if(int.TryParse(portText, out var port)) {
                            ProxyPort = port;
                        }
                    } else {
                        ProxyAddr = value.ToString();
                    }
                    continue;
                }
                if(key.SequenceEqual(KEY_SIP003_PORT)) {
                    if(int.TryParse(value, out var port)) {
                        SIP003Port = port;
                    }
                    continue;
                }
            }
            throw new NotSupportedException();
        }
    }

    public static void Save() {
        File.WriteAllLines(FilePath, [
            KEY_SELECTED_SERVER + " = " + SelectedServerIndex,
            KEY_PROC_CONSOLE + " = " + FormatFlag(ProcConsole),
            KEY_TAP_MODE + " = " + FormatFlag(TapMode),
            KEY_PROXY + " = " + Proxy,
            KEY_SIP003_PORT + " = " + SIP003Port,
        ]);
    }

    static string FormatFlag(bool value) {
        return value ? "1" : "0";
    }

    static bool ParseFlag(ReadOnlySpan<char> text) {
        _ = int.TryParse(text, out var n);
        return n != 0;
    }
}
