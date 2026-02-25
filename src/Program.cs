namespace Project;

static partial class Program {
    public static readonly Guid AppGuid = new(0x3ae267c1, 0x4cc4, 0x4ead, 0xae, 0xf7, 0x4d, 0x8c, 0x12, 0xc5, 0xc4, 0x6f);

    internal static readonly object SingleInstanceLock = AcquireSingleInstanceLock();

    static CancellationTokenSource? WanInfoUpdateCancellation;

    public static bool Started { get; private set; }

    static void Main() {
        AppDomain.CurrentDomain.UnhandledException += AppDomain_UnhandledException;
        WanInfo.Ready += WanInfo_Ready;

        UndoTrafficRedirect();

        AppConfig.Load();
        ServerList.Load();

        UI.Run();
    }

    public static void Start() {
        EnsureStopped();
        EnsureGeoIP();

        var uri = SelectedServer.GetUri();

        if(AppConfig.TunMode) {
            TunModeRouting.FindDefaults();
            TunModeServerInfo.Refresh(uri.Host);

            Wintun.EnsureCreated();
            TunModeAdapters.Refresh();
            TunModeAdapters.SetTunParams(false);
        }

        var outbound = XrayOutbound.FromUri(uri);
        var sip003 = XrayOutbound.ExtractSIP003(outbound);

        XrayConfig.WriteFile(outbound);

        try {
            SetupTrafficRedirect();

            if(sip003 != null) {
                ProcMan.StartSIP003(sip003);
            }

            ProcMan.StartXray();
        } catch {
            ProcMan.StopAll();
            UndoTrafficRedirect();
            Wintun.EnsureClosed();
            throw;
        }

        WanInfoUpdateCancellation = new(TimeSpan.FromMinutes(1));
        WanInfo.RequestUpdate(WanInfoUpdateCancellation.Token);

        Started = true;
    }

    public static void Stop() {
        EnsureStarted();

        if(WanInfoUpdateCancellation != null) {
            WanInfoUpdateCancellation.Cancel();
            WanInfoUpdateCancellation.Dispose();
            WanInfoUpdateCancellation = null;
        }

        ProcMan.StopAll();
        UndoTrafficRedirect();
        Wintun.EnsureClosed();

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

    static void SetupTrafficRedirect() {
        if(AppConfig.TunMode) {
            if(AppConfig.TunModeBypassProxy) {
                ProxyBackup.TrySave();
                NativeProxyManager.SetDirectOnly();
            }
            TunModeOutsideDnsBlock.Start();
            TunModeRouting.AddDefaultOverride();
            TunModeRouting.AddTunnel();
        } else {
            ProxyBackup.TrySave();
            NativeProxyManager.SetConfig(AppConfig.Proxy);
        }
    }

    static void UndoTrafficRedirect() {
        TunModeOutsideDnsBlock.Stop();
        TunModeRouting.UndoAll();
        ProxyBackup.TryRestore();
    }

    static Mutex AcquireSingleInstanceLock() {
        var mutex = new Mutex(true, "Global\\" + AppGuid, out var createdNew);
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

    static void EnsureGeoIP() {
        if(!AppConfig.TunModeBypassRU) {
            return;
        }
        var filePath = Path.Join(AppContext.BaseDirectory, "geoip.dat");
        if(!File.Exists(filePath)) {
            throw new UIException("Missing " + filePath);
        }
    }
}
