namespace Project;

static class ProcMan {
    static readonly string WorkDir, XrayExePath, Tun2SocksExePath;

    static NativeProcess? SIP003Proc;
    static NativeProcess? XrayProc;
    static NativeProcess? Tun2SocksProc;

    static bool NotifySIP003Exit = false;
    static bool NotifyXrayExit = false;
    static bool NotifyTun2SocksExit = false;

    static ProcMan() {
        WorkDir = AppContext.BaseDirectory;
        XrayExePath = Path.Join(WorkDir, "xray.exe");
        Tun2SocksExePath = Path.Join(WorkDir, "tun2socks.exe");
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
        // There are different tun2socks / tun2proxy projects
        // eycorsican/go-tun2socks repo is abandoned BUT
        // - It is used in Outline: https://github.com/Jigsaw-Code/outline-apps/blob/manager_windows/v1.17.2/go.mod#L9
        // - It uses existing TAP adapters, which is convenient

        if(!File.Exists(Tun2SocksExePath)) {
            throw new UIException(
                "Missing " + Tun2SocksExePath + "\n" +
                "Download it from github.com/eycorsican/go-tun2socks"
            );
        }

        var commandLine = String.Join(' ',
            Tun2SocksExePath.Quote(),
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
            UI.ShowBalloon(Path.GetFileName(Tun2SocksExePath) + " exited unexpectedly", true);
        }
    }
}
