namespace Project;

[Flags]
enum AppConfigFlags {
    ProcConsole = 1 << 0,

    TunMode = 1 << 10,
    TunModeIPv6 = 1 << 11,
    TunModeUnsetProxy = 1 << 12,
    TunModeLockdown = 1 << 13,

    BypassRU = 1 << 20,
    BypassPrivate = 1 << 21,
}

static class AppConfig {
    static IAppConfigSource Source = IAppConfigSource.Default;

    public static int SelectedServerIndex {
        get => Source.SelectedServerIndex;
        set => Source.SelectedServerIndex = value;
    }

    public static bool ProcConsole => HasFlag(AppConfigFlags.ProcConsole);
    public static bool TunMode => HasFlag(AppConfigFlags.TunMode);
    public static bool TunModeIPv6 => HasFlag(AppConfigFlags.TunModeIPv6);
    public static bool TunModeUnsetProxy => HasFlag(AppConfigFlags.TunModeUnsetProxy);
    public static bool TunModeLockdown => HasFlag(AppConfigFlags.TunModeLockdown);
    public static bool BypassRU => HasFlag(AppConfigFlags.BypassRU);
    public static bool BypassPrivate => HasFlag(AppConfigFlags.BypassPrivate);

    public static bool HasBypassByIP => BypassRU || BypassPrivate;

    public static string ProxyAddr => Source.ProxyAddr;
    public static int ProxyPort => Source.ProxyPort;
    public static string Proxy => ProxyAddr + ':' + ProxyPort;

    public static string SIP003Addr => Source.SIP003Addr;
    public static int SIP003Port => Source.SIP003Port;

    public static int Mux {
        get => Source.Mux;
        set => Source.Mux = value;
    }

    public static void SetSource(IAppConfigSource source) {
        Source = source;
    }

    public static void Load() {
        Source.Load();
    }

    public static void Save() {
        Source.Save();
    }

    public static bool HasFlag(AppConfigFlags flag) {
        return Source.Flags.HasFlag(flag);
    }

    public static void ToggleFlag(AppConfigFlags flag) {
        Source.Flags ^= flag;
    }
}

class AppConfigFile(string filePath) : IAppConfigSource {
    const string
        KEY_SELECTED_SERVER = "selected_server",
        KEY_PROC_CONSOLE = "proc_console",
        KEY_TUN_MODE = "tun_mode",
        KEY_TUN_MODE_IPv6 = "tun_mode_ipv6",
        KEY_TUN_MODE_UNSET_PROXY = "tun_mode_unset_proxy",
        KEY_TUN_MODE_LOCKDOWN = "tun_mode_lockdown",
        KEY_BYPASS_RU = "bypass_ru",
        KEY_BYPASS_PRIVATE = "bypass_private",
        KEY_PROXY = "proxy",
        KEY_SIP003_PORT = "sip003_port",
        KEY_MUX = "mux";

    readonly string FilePath = filePath;

    public int SelectedServerIndex { get; set; } = IAppConfigSource.Default.SelectedServerIndex;

    public AppConfigFlags Flags { get; set; } = IAppConfigSource.Default.Flags;

    public string ProxyAddr { get; private set; } = IAppConfigSource.Default.ProxyAddr;
    public int ProxyPort { get; private set; } = IAppConfigSource.Default.ProxyPort;

    public string SIP003Addr { get; private set; } = IAppConfigSource.Default.SIP003Addr;
    public int SIP003Port { get; private set; } = IAppConfigSource.Default.SIP003Port;

    public int Mux { get; set; } = IAppConfigSource.Default.Mux;

    public void Load() {
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
                if(key.SequenceEqual(KEY_MUX)) {
                    if(int.TryParse(value, out var mux)) {
                        if(Array.IndexOf(IAppConfigSource.MuxOptions, mux) > -1) {
                            Mux = mux;
                        }
                    }
                    continue;
                }
            }
            throw new NotSupportedException();
        }
    }

    public void Save() {
        File.WriteAllLines(FilePath, [
            KEY_SELECTED_SERVER + " = " + SelectedServerIndex,
            KEY_PROC_CONSOLE + " = " + FormatFlag(AppConfigFlags.ProcConsole),
            KEY_TUN_MODE + " = " + FormatFlag(AppConfigFlags.TunMode),
            KEY_TUN_MODE_IPv6 + " = " + FormatFlag(AppConfigFlags.TunModeIPv6),
            KEY_TUN_MODE_UNSET_PROXY + " = " + FormatFlag(AppConfigFlags.TunModeUnsetProxy),
            KEY_TUN_MODE_LOCKDOWN + " = " + FormatFlag(AppConfigFlags.TunModeLockdown),
            KEY_BYPASS_RU + " = " + FormatFlag(AppConfigFlags.BypassRU),
            KEY_BYPASS_PRIVATE + " = " + FormatFlag(AppConfigFlags.BypassPrivate),
            KEY_PROXY + " = " + ProxyAddr + ':' + ProxyPort,
            KEY_SIP003_PORT + " = " + SIP003Port,
            KEY_MUX + " = " + Mux,
        ]);
    }

    static bool TryParseFlag(ReadOnlySpan<char> text, out AppConfigFlags result) {
        ReadOnlySpan<(string, AppConfigFlags)> known = [
            (KEY_PROC_CONSOLE, AppConfigFlags.ProcConsole),
            (KEY_TUN_MODE, AppConfigFlags.TunMode),
            (KEY_TUN_MODE_IPv6, AppConfigFlags.TunModeIPv6),
            (KEY_TUN_MODE_UNSET_PROXY, AppConfigFlags.TunModeUnsetProxy),
            (KEY_TUN_MODE_LOCKDOWN, AppConfigFlags.TunModeLockdown),
            (KEY_BYPASS_RU, AppConfigFlags.BypassRU),
            (KEY_BYPASS_PRIVATE, AppConfigFlags.BypassPrivate),
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

    string FormatFlag(AppConfigFlags flag) {
        return Flags.HasFlag(flag) ? "1" : "0";
    }
}

interface IAppConfigSource {
    public static readonly int[] MuxOptions = [0, 1, 4];
    public static readonly IAppConfigSource Default = new DefaultImpl();

    int SelectedServerIndex { get; set; }

    AppConfigFlags Flags { get; set; }

    string ProxyAddr { get; }
    int ProxyPort { get; }

    string SIP003Addr { get; }
    int SIP003Port { get; }

    int Mux { get; set; }

    void Load();
    void Save();

    class DefaultImpl : IAppConfigSource {

        public int SelectedServerIndex {
            get => -1;
            set => Throw();
        }

        public AppConfigFlags Flags {
            get => AppConfigFlags.BypassPrivate;
            set => Throw();
        }

        public string ProxyAddr => "127.0.0.1";
        public int ProxyPort => 1080;

        public string SIP003Addr => "127.0.0.1";
        public int SIP003Port => 1984;

        public int Mux {
            get => MuxOptions[^1];
            set => Throw();
        }

        public void Load() {
        }

        public void Save() {
            Throw();
        }

        void Throw() {
            throw new NotSupportedException();
        }
    }
}
