namespace Project;

[Flags]
enum AppConfigFlags {
    ProcConsole = 1,
    TunMode = 2,
    TunModeIPv6 = 4,
    TunModeUnsetProxy = 8,
    BypassRU = 16
}

static class AppConfig {
    const string
        KEY_SELECTED_SERVER = "selected_server",
        KEY_PROC_CONSOLE = "proc_console",
        KEY_TUN_MODE = "tun_mode",
        KEY_TUN_MODE_IPv6 = "tun_mode_ipv6",
        KEY_TUN_MODE_UNSET_PROXY = "tun_mode_unset_proxy",
        KEY_BYPASS_RU = "bypass_ru",
        KEY_PROXY = "proxy",
        KEY_SIP003_PORT = "sip003_port";

    public static readonly string FilePath = Path.Join(AppContext.BaseDirectory, "xray-gui.ini");

    static AppConfig() {
        Reset();
    }

    public static int SelectedServerIndex { get; set; }

    public static bool ProcConsole => HasFlag(AppConfigFlags.ProcConsole);
    public static bool TunMode => HasFlag(AppConfigFlags.TunMode);
    public static bool TunModeIPv6 => HasFlag(AppConfigFlags.TunModeIPv6);
    public static bool TunModeUnsetProxy => HasFlag(AppConfigFlags.TunModeUnsetProxy);
    public static bool BypassRU => HasFlag(AppConfigFlags.BypassRU);

    static AppConfigFlags Flags;

    public static string ProxyAddr { get; private set; } = "";
    public static int ProxyPort { get; private set; }
    public static string Proxy => ProxyAddr + ':' + ProxyPort;

    public static string SIP003Addr { get; private set; } = "";
    public static int SIP003Port { get; private set; }

    public static void Reset() {
        SelectedServerIndex = -1;
        Flags = 0;
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
                if(TryParseFlag(key, out var flag)) {
                    _ = int.TryParse(value, out var n);
                    if(n != 0) {
                        Flags |= flag;
                    } else {
                        Flags &= ~flag;
                    }
                    continue;
                }
                if(key.SequenceEqual(KEY_SELECTED_SERVER)) {
                    if(int.TryParse(value, out var index)) {
                        SelectedServerIndex = index;
                    }
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
            KEY_PROC_CONSOLE + " = " + FormatFlag(AppConfigFlags.ProcConsole),
            KEY_TUN_MODE + " = " + FormatFlag(AppConfigFlags.TunMode),
            KEY_TUN_MODE_IPv6 + " = " + FormatFlag(AppConfigFlags.TunModeIPv6),
            KEY_TUN_MODE_UNSET_PROXY + " = " + FormatFlag(AppConfigFlags.TunModeUnsetProxy),
            KEY_BYPASS_RU + " = " + FormatFlag(AppConfigFlags.BypassRU),
            KEY_PROXY + " = " + Proxy,
            KEY_SIP003_PORT + " = " + SIP003Port,
        ]);
    }

    public static bool HasFlag(AppConfigFlags flag) {
        return Flags.HasFlag(flag);
    }

    public static void ToggleFlag(AppConfigFlags flag) {
        Flags ^= flag;
    }

    static bool TryParseFlag(ReadOnlySpan<char> text, out AppConfigFlags result) {
        ReadOnlySpan<(string, AppConfigFlags)> known = [
            (KEY_PROC_CONSOLE, AppConfigFlags.ProcConsole),
            (KEY_TUN_MODE, AppConfigFlags.TunMode),
            (KEY_TUN_MODE_IPv6, AppConfigFlags.TunModeIPv6),
            (KEY_TUN_MODE_UNSET_PROXY, AppConfigFlags.TunModeUnsetProxy),
            (KEY_BYPASS_RU, AppConfigFlags.BypassRU),
            ("tap_mode", AppConfigFlags.TunMode),
            ("tap_mode_badvpn", default),
            ("tun_mode_bypass_proxy", AppConfigFlags.TunModeUnsetProxy),
            ("tun_mode_bypass_ru", AppConfigFlags.BypassRU),
        ];
        foreach(var (key, flag) in known) {
            if(text.SequenceEqual(key)) {
                result = flag;
                return true;
            }
        }
        result = default;
        return false;
    }

    static string FormatFlag(AppConfigFlags flag) {
        return HasFlag(flag) ? "1" : "0";
    }
}
