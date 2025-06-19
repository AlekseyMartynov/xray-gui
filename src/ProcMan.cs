namespace Project;

static class ProcMan {
    static readonly string WorkDir, XrayExePath, GoTun2SocksExePath, BadVpnTun2SocksExePath;

    static NativeProcess? SIP003Proc;
    static NativeProcess? XrayProc;
    static NativeProcess? Tun2SocksProc;

    static bool NotifySIP003Exit = false;
    static bool NotifyXrayExit = false;
    static bool NotifyTun2SocksExit = false;

    static ProcMan() {
        WorkDir = AppContext.BaseDirectory;
        XrayExePath = Path.Join(WorkDir, "xray.exe");
        GoTun2SocksExePath = Path.Join(WorkDir, "tun2socks.exe");
        BadVpnTun2SocksExePath = Path.Join(WorkDir, "badvpn-tun2socks.exe");
    }

    public static void StartSIP003(SIP003 sip003) {
        var exePath = Path.Join(WorkDir, sip003.PluginName + ".exe");

        if(!File.Exists(exePath)) {
            throw new UIException("Missing " + exePath);
        }

        NotifySIP003Exit = true;
        SIP003Proc = new(exePath.Quote(), WorkDir, sip003.CreateEnv(), SIP003_Exited, NativeRestrictedTokens.Constrained);
    }

    public static void StartXray() {
        if(!File.Exists(XrayExePath)) {
            throw new UIException(
                "Missing " + XrayExePath + "\n" +
                "Download it from github.com/XTLS/Xray-core"
            );
        }

        var commandLine = XrayExePath.Quote() + " -c " + XrayConfig.FilePath.Quote();

        NotifyXrayExit = true;
        XrayProc = new(commandLine, WorkDir, default, Xray_Exited, NativeRestrictedTokens.Constrained);
    }

    public static void StartTun2Socks() {
        if(AppConfig.TapModeBadVpn) {
            StartBadVpnTun2Socks();
        } else {
            StartGoTun2Socks();
        }
    }

    static void StartGoTun2Socks() {
        // There are different tun2socks / tun2proxy projects
        // eycorsican/go-tun2socks repo is abandoned BUT
        // - It is used in Outline: https://github.com/Jigsaw-Code/outline-apps/blob/manager_windows/v1.17.2/go.mod#L9
        // - It uses existing TAP adapters, which is convenient

        if(!File.Exists(GoTun2SocksExePath)) {
            throw new UIException(
                "Missing " + GoTun2SocksExePath + "\n" +
                "Download it from github.com/eycorsican/go-tun2socks"
            );
        }

        var commandLine = String.Join(' ',
            GoTun2SocksExePath.Quote(),
#if FALSE
            // github.com/eycorsican/go-tun2socks/pull/42
            "-dnsFallback",
#endif
            "-tunName", TapModeAdapters.TapName.Quote(),
            "-tunDns", TapModeAdapters.TapDns,
            "-proxyServer", AppConfig.Proxy
        );

        NotifyTun2SocksExit = true;
        Tun2SocksProc = new(commandLine, WorkDir, default, Tun2Socks_Exited, NativeRestrictedTokens.NormalUser);
    }

    static void StartBadVpnTun2Socks() {
        // https://github.com/ambrop72/badvpn
        // - Smaller binary
        // - Works with 'Constrained' restrictions
        // - Previously used in Outline: https://github.com/Jigsaw-Code/outline-apps/issues/776#issuecomment-613117551
        // - Fork used in shadowsocks-android: https://github.com/shadowsocks/badvpn

        if(!File.Exists(BadVpnTun2SocksExePath)) {
            throw new UIException("Missing " + BadVpnTun2SocksExePath);
        }

        var prefix = TapModeAdapters.TapPrefix.ToString();
        var mask = TapModeAdapters.TapMask.ToString();
        var addr = TapModeAdapters.TapAddr.ToString();
        var gateway = TapModeAdapters.TapGateway.ToString();

        var tundev = String.Join(':',
            TapModeAdapters.REQUIRED_TAP_COMPONENT_ID,
            TapModeAdapters.TapName,
            addr, prefix, mask
        );

        var commandLine = String.Join(' ',
            BadVpnTun2SocksExePath.Quote(),
            "--tundev", tundev.Quote(),
            "--netif-ipaddr", gateway,
            "--netif-netmask", mask,
            "--socks-server-addr", AppConfig.Proxy,

            // https://github.com/ambrop72/badvpn/commit/ae4edfb
            "--socks5-udp"
        );

        NotifyTun2SocksExit = true;
        Tun2SocksProc = new(commandLine, WorkDir, default, Tun2Socks_Exited, NativeRestrictedTokens.Constrained);
    }

    public static void StopAll() {
        NotifyTun2SocksExit = false;
        Stop(ref Tun2SocksProc);

        NotifyXrayExit = false;
        Stop(ref XrayProc);

        NotifySIP003Exit = false;
        Stop(ref SIP003Proc);
    }

    static void Stop(ref NativeProcess? proc) {
        if(proc != null) {
            proc.Dispose();
            proc = null;
        }
    }

    static void SIP003_Exited() {
        if(NotifySIP003Exit) {
            UI.ShowBalloon("SIP003 plugin process exited unexpectedly", true);
        }
    }

    static void Xray_Exited() {
        if(NotifyXrayExit) {
            UI.ShowBalloon(Path.GetFileName(XrayExePath) + " exited unexpectedly", true);
        }
    }

    static void Tun2Socks_Exited() {
        if(NotifyTun2SocksExit) {
            UI.ShowBalloon("tun2socks exited unexpectedly", true);
        }
    }
}
