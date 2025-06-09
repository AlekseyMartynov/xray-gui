namespace Project;

class Program {

    static void Main() {
        WanInfo.Ready += (s, e) => Console.WriteLine(e.IP);

        ProxyBackup.TryRestore();
        TapModeRouting.UndoAll();

        AppConfig.Load();
        ServerList.Load();

        Start();
        Console.ReadLine();
        Stop();
    }

    static void Start() {
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
    }

    static void Stop() {
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
    }
}
