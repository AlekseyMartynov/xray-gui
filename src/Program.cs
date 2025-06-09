namespace Project;

static partial class Program {
    internal static readonly object SingleInstanceLock = AcquireSingleInstanceLock();

    public static bool Started { get; private set; }

    static void Main() {
        AppDomain.CurrentDomain.UnhandledException += AppDomain_UnhandledException;
        WanInfo.Ready += WanInfo_Ready;

        ProxyBackup.TryRestore();
        TapModeRouting.UndoAll();

        AppConfig.Load();
        ServerList.Load();

        UI.Run();
    }

    public static void Start() {
        EnsureStopped();

        var uri = SelectedServer.GetUri();

        if(AppConfig.TapMode) {
            TapModeAdapters.Refresh();
            TapModeServerInfo.Refresh(uri.Host);
        }

        var outbound = XrayOutbound.FromUri(uri);
        var sip003 = XrayOutbound.ExtractSIP003(outbound);

        XrayConfig.WriteFile(outbound);

        if(AppConfig.TapMode) {
            TapModeRouting.AddDefaultOverride();
            TapModeRouting.AddTunnel();
        } else {
            ProxyBackup.TrySave();
            NativeProxyManager.SetConfig(AppConfig.ProxyAddr + ':' + AppConfig.ProxyPort);
        }

        if(sip003 != null) {
            ProcMan.StartSIP003(sip003);
        }

        ProcMan.StartXray();

        if(AppConfig.TapMode) {
            ProcMan.StartTun2Socks();
        }

        WanInfo.RequestUpdate();

        Started = true;
    }

    public static void Stop() {
        EnsureStarted();

        if(AppConfig.TapMode) {
            ProcMan.StopTun2Socks();
        }

        ProcMan.StopXray();
        ProcMan.StopSIP003();

        if(AppConfig.TapMode) {
            TapModeRouting.UndoDefaultOverride();
            TapModeRouting.UndoTunnel();
        } else {
            ProxyBackup.TryRestore();
        }

        Started = false;
    }

    public static void EnsureStarted() {
        if(!Started) {
            throw new InvalidOperationException();
        }
    }

    public static void EnsureStopped() {
        if(Started) {
            throw new InvalidOperationException();
        }
    }

    static Mutex AcquireSingleInstanceLock() {
        var mutex = new Mutex(true, "Global\\3ae267c1-4cc4-4ead-aef7-4d8c12c5c46f", out var createdNew);
        if(!createdNew) {
            Windows.Win32.PInvoke.MessageBox(default, "Already running", default, default);
            Environment.Exit(default);
        }
        return mutex;
    }

    static void AppDomain_UnhandledException(object? s, UnhandledExceptionEventArgs e) {
        Windows.Win32.PInvoke.MessageBox(default, e.ExceptionObject.ToString(), default, default);
    }

    static void WanInfo_Ready(object? s, WanInfoEventArgs e) {
        if(!Started) {
            return;
        }
        if(e.IP.Length > 0) {
            UI.ShowBalloon($"{e.IP} ({e.CountryCode})");
        } else {
            UI.ShowBalloon("Failed to detect WAN IP", true);
        }
    }
}
