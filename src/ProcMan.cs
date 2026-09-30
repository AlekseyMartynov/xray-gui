namespace Project;

static class ProcMan {
    static readonly string WorkDir;

    static NativeProcess? SIP003Proc;
    static NativeProcess? XrayProc;

    static bool NotifySIP003Exit = false;
    static bool NotifyXrayExit = false;

    static ProcMan() {
        WorkDir = AppContext.BaseDirectory;
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
        XrayExeHelper.Validate();

        var commandLine = XrayExeHelper.ExePath.Quote() + " -c " + XrayConfig.FilePath.Quote();

        var accessToken = AppConfig.TunMode
            ? NativeRestrictedTokens.FullyTrusted
            : NativeRestrictedTokens.NormalUser;

        NotifyXrayExit = true;
        XrayProc = new(commandLine, WorkDir, default, Xray_Exited, accessToken);
    }

    public static void StopAll() {
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
            UI.ShowBalloon(Path.GetFileName(XrayExeHelper.ExePath) + " exited unexpectedly", true);
        }
    }
}
